using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 협동 플레이 동기화: 예전에는 스폰 시점에 GetOrAdd&lt;EnemyHealth&gt;()로
    /// 런타임에 붙던 순수 로컬 컴포넌트였다. Fusion은 NetworkBehaviour를
    /// 런타임에 AddComponent로 붙이는 것을 지원하지 않으므로, 이제는 적
    /// 프리팹 자체에 미리 붙어 있어야 하고(팀원이 에디터에서 작업),
    /// 체력은 [Networked]로 모든 클라이언트에 동일하게 보인다.
    ///
    /// 데미지는 State Authority(이 적을 스폰한 클라이언트)만 실제로
    /// 적용할 수 있다. 다른 클라이언트가 때린 경우 RPC로 "이만큼
    /// 때렸다"고 요청만 보내고, State Authority가 실제 체력을 깎은 뒤
    /// 그 결과가 다시 모두에게 동기화된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool damageEnabled = true;
        [SerializeField, Min(8)] private int rememberedShotCount = 128;

        // 몬스터 사망 시 재생할 효과음. 비워두면 Resources/SFX/Enemy/death를 자동으로 불러온다.
        [SerializeField] private AudioClip deathSfx;
        [SerializeField, Range(0f, 1f)] private float deathSfxVolume = 0.45f;
        private static AudioClip cachedDeathSfx;
        private const string DeathSfxResourcePath = "SFX/Enemy/death";

        private readonly HashSet<string> processedShotKeys = new HashSet<string>();
        private readonly Queue<string> processedShotOrder = new Queue<string>();
        private RoleSynergyTracker synergyTracker;

        // 네트워크 오브젝트가 아직 아닌 경우(팀원이 프리팹에 NetworkObject를
        // 붙이기 전, 혹은 옛날 씬을 그대로 열었을 때)를 위한 로컬 전용
        // 체력 저장소. 네트워크 오브젝트라면 NetworkedHealth/NetworkedIsDead가
        // 진짜 값이고 이 필드들은 그 값을 그대로 미러링만 한다.
        private float localHealthFallback;
        private bool localIsDeadFallback;

        [Networked, OnChangedRender(nameof(HandleNetworkedHealthChanged))]
        private float NetworkedHealth { get; set; }

        [Networked, OnChangedRender(nameof(HandleNetworkedDeathChanged))]
        private NetworkBool NetworkedIsDead { get; set; }

        // Object != null만으로는 부족하다 - Fusion의 Spawned() 콜백이 아직
        // 호출되기 전에도 Object 참조 자체는 이미 채워져 있을 수 있어서,
        // 그 짧은 시점에 [Networked] 프로퍼티(NetworkedHealth/NetworkedIsDead)에
        // 접근하면 "Networked properties can only be accessed when Spawned()
        // has been called" 예외가 난다. IsValid는 실제로 스폰이 끝나 안전하게
        // 접근 가능한 상태인지까지 확인해준다.
        private bool IsNetworked => IsValid;

#if UNITY_EDITOR
        private static bool editorTestDamageBoostEnabled;
        private static float editorTestDamageMultiplier = 1f;
#endif

        public float MaxHealth => maxHealth;
        public float CurrentHealth => IsNetworked ? NetworkedHealth : localHealthFallback;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : CurrentHealth / maxHealth;
        public bool IsDead => IsNetworked ? NetworkedIsDead : localIsDeadFallback;
        public bool DamageEnabled => damageEnabled;

        public event Action<EnemyHealth, float, float> HealthChanged;
        public event Action<EnemyHealth, DamageInfo> HitRegistered;
        public event Action<EnemyHealth, DamageInfo> Died;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorTestDamageSettings()
        {
            editorTestDamageBoostEnabled = false;
            editorTestDamageMultiplier = 1f;
        }

        internal static void ConfigureEditorTestDamage(
            bool enabled,
            float multiplier)
        {
            editorTestDamageBoostEnabled = enabled;
            editorTestDamageMultiplier = Mathf.Max(1f, multiplier);
        }
#endif

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            localHealthFallback = maxHealth;
            localIsDeadFallback = false;
            synergyTracker = GetComponent<RoleSynergyTracker>();
        }

        /// <summary>
        /// Runner.Spawn()의 onBeforeSpawned 콜백 안에서 호출될 것을
        /// 전제로 한다(PlayerJobController.SetJob과 동일한 패턴). 그
        /// 시점에는 State Authority인 스폰 주체 클라이언트만
        /// [Networked] 값을 쓸 수 있으므로 HasStateAuthority를 확인한다.
        /// </summary>
        public void Configure(float newMaxHealth, bool canTakeDamage)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            damageEnabled = canTakeDamage;
            processedShotKeys.Clear();
            processedShotOrder.Clear();

            if (IsNetworked)
            {
                if (Object.HasStateAuthority)
                {
                    NetworkedHealth = maxHealth;
                    NetworkedIsDead = false;
                }
            }
            else
            {
                localHealthFallback = maxHealth;
                localIsDeadFallback = false;
            }

            HealthChanged?.Invoke(this, CurrentHealth, maxHealth);
        }

        public void SetDamageEnabled(bool enabled)
        {
            damageEnabled = enabled;
        }

        public void RestoreFullHealth()
        {
            if (IsNetworked)
            {
                if (Object.HasStateAuthority)
                {
                    NetworkedHealth = maxHealth;
                    NetworkedIsDead = false;
                }
            }
            else
            {
                localHealthFallback = maxHealth;
                localIsDeadFallback = false;
            }

            // Configure()와 동일하게 항상 명시적으로 호출한다.
            // NetworkedHealth가 이미 maxHealth였다면(예: 튜토리얼 무적 상태) Fusion의
            // OnChangedRender는 값이 변하지 않아 발동하지 않으므로, 여기서 직접
            // 호출하지 않으면 체력바 UI가 갱신되지 않는다.
            HealthChanged?.Invoke(this, maxHealth, maxHealth);
        }

        /// <summary>
        /// 어느 클라이언트에서 맞았든 호출할 수 있는 진입점이다.
        /// - 이 적의 State Authority(스폰한 클라이언트)에서 호출됐다면
        ///   즉시 실제 체력을 깎는다.
        /// - 다른 클라이언트에서 호출됐다면(=다른 플레이어의 무기가
        ///   맞혔다면) State Authority에게 RPC로 데미지 적용을 요청만
        ///   하고, 실제 결과는 NetworkedHealth 동기화를 통해 곧 이
        ///   클라이언트에도 반영된다.
        /// </summary>
        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead || IsDuplicateShot(info))
            {
                return false;
            }

            RememberShot(info);

            // 맞은 순간의 즉각적인 피드백(히트마커/사운드 등)은 누가
            // 때렸는지와 무관하게 바로 쏴 준다.
            HitRegistered?.Invoke(this, info);
            DreamGameEvents.RaiseEnemyHit(this, info);

            if (IsNetworked && !Object.HasStateAuthority)
            {
                RPC_RequestDamage(
                    info.amount,
                    info.playerId,
                    (int)info.role,
                    info.shotId,
                    info.hitPoint,
                    info.allowSynergy);

                return true;
            }

            ApplyDamageAuthoritative(info);
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDamage(
            float amount,
            string playerId,
            int role,
            int shotId,
            Vector3 hitPoint,
            bool allowSynergy)
        {
            DamageInfo info = new DamageInfo(
                amount,
                playerId,
                (PlayerRole)role,
                shotId,
                hitPoint,
                allowSynergy);

            if (IsDead || IsDuplicateShot(info))
            {
                return;
            }

            RememberShot(info);

            ApplyDamageAuthoritative(info);
        }

        /// <summary>
        /// 실제 체력 계산은 이 메서드 하나로만 이뤄진다(State
        /// Authority에서 로컬 호출 또는 RPC_RequestDamage를 통해서만
        /// 도달). 네트워크 오브젝트라면 결과를 [Networked] 값에 써서
        /// 모두에게 전파하고, 그 반영(HealthChanged/Died 이벤트)은
        /// HandleNetworkedHealthChanged/HandleNetworkedDeathChanged가
        /// 담당한다 - 여기서 이중으로 이벤트를 쏘지 않는다.
        /// </summary>
        private void ApplyDamageAuthoritative(DamageInfo info)
        {
            bool wasDamageEnabled = damageEnabled;

            SynergyResult synergyResult = SynergyResult.None;
            if (info.allowSynergy)
            {
                synergyTracker ??= GetComponent<RoleSynergyTracker>();
                if (synergyTracker != null)
                {
                    synergyResult = synergyTracker.RegisterHit(info.role);
                }
            }

            if (!wasDamageEnabled)
            {
                return;
            }

            float multiplier = synergyTracker != null ? synergyTracker.CurrentDamageMultiplier : 1f;
            float totalDamage = Mathf.Max(0f, info.amount * multiplier + synergyResult.BonusDamage);

#if UNITY_EDITOR
            if (editorTestDamageBoostEnabled &&
                info.role != PlayerRole.None)
            {
                totalDamage *= editorTestDamageMultiplier;
            }
#endif

            if (totalDamage <= 0f)
            {
                return;
            }

            float newHealth = Mathf.Max(0f, CurrentHealth - totalDamage);
            bool willDie = newHealth <= 0f;

            if (IsNetworked)
            {
                NetworkedHealth = newHealth;

                if (willDie)
                {
                    NetworkedIsDead = true;
                }
            }
            else
            {
                localHealthFallback = newHealth;
                HealthChanged?.Invoke(this, localHealthFallback, maxHealth);

                if (willDie)
                {
                    localIsDeadFallback = true;
                    damageEnabled = false;
                    PlayDeathSfx();
                    Died?.Invoke(this, info);
                    DreamGameEvents.RaiseEnemyDied(this, info);
                }
            }
        }

        /// <summary>
        /// [Networked] NetworkedHealth가 바뀔 때마다(State Authority
        /// 본인 클라이언트를 포함해) 모든 클라이언트에서 호출된다.
        /// </summary>
        private void HandleNetworkedHealthChanged()
        {
            HealthChanged?.Invoke(this, NetworkedHealth, maxHealth);
        }

        /// <summary>
        /// [Networked] NetworkedIsDead가 true로 바뀔 때 모든
        /// 클라이언트에서 호출된다. RPC 경로에서는 이 클라이언트가 실제
        /// 킬샷의 DamageInfo를 갖고 있지 않을 수 있어 최소 정보만 담아
        /// Died 이벤트를 발생시킨다.
        /// </summary>
        private void HandleNetworkedDeathChanged()
        {
            if (!NetworkedIsDead)
            {
                return;
            }

            damageEnabled = false;
            PlayDeathSfx();

            DamageInfo fallbackInfo = new DamageInfo(
                0f,
                "NETWORK",
                PlayerRole.None,
                -1,
                transform.position,
                false);

            Died?.Invoke(this, fallbackInfo);
            DreamGameEvents.RaiseEnemyDied(this, fallbackInfo);
        }

        private void PlayDeathSfx()
        {
            AudioClip clip = deathSfx;

            if (clip == null)
            {
                if (cachedDeathSfx == null)
                {
                    cachedDeathSfx = Resources.Load<AudioClip>(DeathSfxResourcePath);
                }

                clip = cachedDeathSfx;
            }

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, deathSfxVolume);
            }
        }

        private bool IsDuplicateShot(DamageInfo info)
        {
            if (info.shotId < 0)
            {
                return false;
            }

            return processedShotKeys.Contains(BuildShotKey(info));
        }

        private void RememberShot(DamageInfo info)
        {
            if (info.shotId < 0)
            {
                return;
            }

            string key = BuildShotKey(info);
            if (!processedShotKeys.Add(key))
            {
                return;
            }

            processedShotOrder.Enqueue(key);
            while (processedShotOrder.Count > Mathf.Max(8, rememberedShotCount))
            {
                processedShotKeys.Remove(processedShotOrder.Dequeue());
            }
        }

        private static string BuildShotKey(DamageInfo info)
        {
            string playerId = string.IsNullOrWhiteSpace(info.playerId) ? "LOCAL" : info.playerId;
            return playerId + ":" + info.shotId;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            rememberedShotCount = Mathf.Max(8, rememberedShotCount);
        }
    }
}
