using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 최종 보스의 "게임플레이 상태"만 이 NetworkObject(DreamEnemySpawner) 하나의
    /// State Authority에서 관리합니다. 보스 모델/애니메이션/이펙트/카메라 연출은
    /// 지금처럼 각 Peer가 로컬로 Instantiate한 오브젝트(FinalBossDirector.SpawnBossObject)가
    /// 계속 담당하고, 이 파일은 그 로컬 보스의 EnemyHealth/FinalBossAttackController를
    /// "Presentation Proxy"로 바인딩해서 공유 HP/Damage/Synergy/Phase/Pattern/Death를
    /// 밀어넣어 주는 다리 역할만 합니다.
    ///
    /// 보스 자체를 NetworkObject로 만들거나 Runner.Spawn()하지 않습니다 - 기존
    /// DreamEnemySpawner.BossFace.cs(BindBossFace 패턴)와 완전히 같은 방식입니다.
    /// </summary>
    public sealed partial class DreamEnemySpawner
    {
        // ---------------------------------------------------------------
        // Networked 상태
        // ---------------------------------------------------------------

        // 새 보스전이 시작될 때마다 증가한다. 이전 전투에서 날아가던 투사체/MudSplat이
        // 새 전투에 뒤늦게 도착해 데미지를 주는 것을 막기 위한 기준값이다.
        [Networked] public int BossBattleRevision { get; private set; }

        [Networked] public float BossMaxHealth { get; private set; }

        [Networked, OnChangedRender(nameof(HandleBossHealthNetworkChanged))]
        public float BossCurrentHealth { get; private set; }

        [Networked] public NetworkBool BossDamageEnabledNet { get; private set; }

        [Networked, OnChangedRender(nameof(HandleBossDeathNetworkChanged))]
        public NetworkBool BossIsDead { get; private set; }

        // 0 = HP 2/3 초과, 1 = HP 2/3 이하, 2 = HP 1/3 이하. 참고용으로 공유하지만
        // 실제 Phase 진행(AdvanceTowardCore)은 이미 동기화된 BossCurrentHealth 비율로
        // 각 Peer가 동일하게 계산하므로 별도 브로드캐스트 이벤트가 필요 없다.
        [Networked] public int BossPhase { get; private set; }

        [Networked, OnChangedRender(nameof(HandleBossPatternNetworkChanged))]
        public int BossPatternRevision { get; private set; }

        [Networked] public int BossPatternType { get; private set; }

        // Fusion의 Runner.SimulationTime(초 단위 네트워크 시간)을 그대로 기록한다.
        // BossFaceSnapshot.StartedAt과 동일하게 이미 검증된 방식이다.
        [Networked] public double BossPatternStartTime { get; private set; }

        [Networked, OnChangedRender(nameof(HandleBossSynergyNetworkChanged))]
        public int BossSynergyRevision { get; private set; }

        [Networked] private int BossSynergyKindValue { get; set; }
        [Networked] private float BossSynergyBonusDamage { get; set; }
        [Networked] private int BossSynergyFirstRole { get; set; }
        [Networked] private int BossSynergySecondRole { get; set; }

        // ---------------------------------------------------------------
        // 이 Peer의 로컬 보스(Presentation Proxy) 바인딩
        // ---------------------------------------------------------------

        private EnemyHealth localBossHealth;
        private RoleSynergyTracker localBossSynergyTracker;
        private FinalBossAttackController localBossAttack;

        private readonly HashSet<string> processedBossDamageKeys = new HashSet<string>();
        private readonly Queue<string> processedBossDamageOrder = new Queue<string>();

        /// <summary>
        /// 이 Peer가 State Authority이면서 동시에 Shared Mode 방장일 때만
        /// "보스 다음 행동(패턴)을 스스로 결정"할 권한이 있다 - BossFace의
        /// IsBossFaceAuthority와 동일한 기준이다.
        /// </summary>
        public bool IsBossCombatAuthority =>
            IsTutorialSessionReady && Object.HasStateAuthority && Runner.IsSharedModeMasterClient;

        /// <summary>
        /// FinalBossDirector.ConfigureBossComponents()가 보스 등장 직후 호출한다.
        /// bossHealth/bossAttack은 "이 Peer 자신의" 로컬 보스 인스턴스에 붙은
        /// 컴포넌트다 - 다른 Peer의 보스와는 무관하다.
        /// </summary>
        public void BindBossCombat(EnemyHealth bossHealth, FinalBossAttackController bossAttack)
        {
            if (localBossHealth != null && localBossHealth != bossHealth)
            {
                localBossHealth.BindBossCombatSpawner(null);
            }

            localBossHealth = bossHealth;
            localBossSynergyTracker = bossHealth != null ? bossHealth.GetComponent<RoleSynergyTracker>() : null;
            localBossAttack = bossAttack;

            if (bossHealth != null)
            {
                bossHealth.BindBossCombatSpawner(this);
            }
        }

        public void UnbindBossCombat(EnemyHealth bossHealth)
        {
            if (localBossHealth != bossHealth)
            {
                return;
            }

            if (bossHealth != null)
            {
                bossHealth.BindBossCombatSpawner(null);
            }

            localBossHealth = null;
            localBossSynergyTracker = null;
            localBossAttack = null;
        }

        // ---------------------------------------------------------------
        // 보스전 시작 / 피격 가능 여부
        // ---------------------------------------------------------------

        /// <summary>
        /// EnemyHealth.Configure(bossMaxHealth, ...)가 보스에서 호출될 때 대신
        /// 실행된다. 모든 Peer가 자기 로컬 FinalBossDirector에서 동시에 이 메서드를
        /// 호출하지만, 실제로 값을 쓰는 것은 State Authority뿐이다(나머지는
        /// 조용히 무시되고 네트워크 복제 결과를 그대로 받는다) - DreamEnemySpawner의
        /// 다른 Networked 값들과 동일한 규칙이다.
        /// </summary>
        public void RequestBossBattleStart(float maxHealth)
        {
            if (!IsTutorialSessionReady || !Object.HasStateAuthority)
            {
                return;
            }

            BossBattleRevision++;
            BossMaxHealth = Mathf.Max(1f, maxHealth);
            BossCurrentHealth = BossMaxHealth;
            BossDamageEnabledNet = false;
            BossIsDead = false;
            BossPhase = 0;
            BossPatternRevision = 0;
            BossPatternType = 0;
            BossPatternStartTime = 0d;
            BossSynergyRevision = 0;

            processedBossDamageKeys.Clear();
            processedBossDamageOrder.Clear();
        }

        public void RequestSetBossDamageEnabled(bool enabledValue)
        {
            if (!IsTutorialSessionReady || !Object.HasStateAuthority)
            {
                return;
            }

            BossDamageEnabledNet = enabledValue;
        }

        // ---------------------------------------------------------------
        // Damage Authority
        // ---------------------------------------------------------------

        /// <summary>
        /// 이 Peer의 로컬 보스 EnemyHealth가 자기 Collider에서 피격을 감지했을 때
        /// 호출한다(EnemyHealth.TakeBossCombatDamage). 실제 데미지 계산/적용은
        /// 여기서 State Authority에게 전달되어 딱 한 번만 이뤄진다.
        /// </summary>
        public void RequestBossDamage(DamageInfo info)
        {
            if (!IsTutorialSessionReady)
            {
                return;
            }

            if (Object.HasStateAuthority)
            {
                PlayerRef localAttacker = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
                ApplyBossDamageAuthoritative(
                    localAttacker, info.amount, info.role, info.shotId, info.allowSynergy, BossBattleRevision);
            }
            else
            {
                RPC_RequestBossDamage(
                    info.amount, (int)info.role, info.shotId, info.allowSynergy, BossBattleRevision);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestBossDamage(
            float amount,
            int role,
            int shotId,
            bool allowSynergy,
            int requestRevision,
            RpcInfo rpcInfo = default)
        {
            ApplyBossDamageAuthoritative(
                rpcInfo.Source, amount, (PlayerRole)role, shotId, allowSynergy, requestRevision);
        }

        /// <summary>
        /// 실제 보스 체력 계산은 이 메서드 하나로만 이뤄진다(State Authority에서만
        /// 실행됨이 보장됨). 중복 검사 키는 "누가(PlayerRef) + ShotId"라서, 서로
        /// 다른 플레이어가 우연히 같은 ShotId를 써도 서로의 공격을 지우지 않는다.
        /// requestRevision이 현재 BossBattleRevision과 다르면(=이전 보스전에서
        /// 날아오던 늦은 요청) 조용히 무시한다.
        /// </summary>
        private void ApplyBossDamageAuthoritative(
            PlayerRef attacker,
            float amount,
            PlayerRole role,
            int shotId,
            bool allowSynergy,
            int requestRevision)
        {
            if (!Object.HasStateAuthority) return;
            if (requestRevision != BossBattleRevision) return;
            if (BossIsDead || !BossDamageEnabledNet) return;

            if (shotId >= 0)
            {
                string key = attacker + ":" + shotId;

                if (!processedBossDamageKeys.Add(key))
                {
                    return;
                }

                processedBossDamageOrder.Enqueue(key);

                while (processedBossDamageOrder.Count > 256)
                {
                    processedBossDamageKeys.Remove(processedBossDamageOrder.Dequeue());
                }
            }

            SynergyResult synergyResult = SynergyResult.None;

            if (allowSynergy && localBossSynergyTracker != null)
            {
                // RegisterHit 내부에서 트리거되면 ApplyEffect + owner.PublishSynergy까지
                // 그대로 실행된다(RoleSynergyTracker의 기존 일반 적 로직 재사용) -
                // owner(=localBossHealth)의 PublishSynergy가 bossCombatSpawner가
                // 바인딩된 것을 보고 BroadcastBossSynergy로 모든 Peer에 전파한다.
                synergyResult = localBossSynergyTracker.RegisterHit(role, attacker.ToString());
            }

            float totalDamage = Mathf.Max(0f, amount + synergyResult.BonusDamage);

#if UNITY_EDITOR
            if (role != PlayerRole.None)
            {
                totalDamage *= EnemyHealth.GetEditorTestDamageMultiplierForBoss();
            }
#endif

            if (totalDamage <= 0f)
            {
                return;
            }

            float newHealth = Mathf.Max(0f, BossCurrentHealth - totalDamage);
            BossCurrentHealth = newHealth;
            BossPhase = ComputeBossPhase(newHealth, BossMaxHealth);

            if (newHealth <= 0f && !BossIsDead)
            {
                BossIsDead = true;
            }
        }

        private static int ComputeBossPhase(float current, float max)
        {
            if (max <= 0f) return 0;

            float normalized = current / max;

            if (normalized <= 1f / 3f) return 2;
            if (normalized <= 2f / 3f) return 1;
            return 0;
        }

        /// <summary>
        /// EnemyHealth.PublishSynergy가 보스일 때 이 메서드로 위임한다.
        /// State Authority만 실제로 값을 쓰고, 그 결과가 모든 Peer에 복제되면
        /// HandleBossSynergyNetworkChanged가 각자 Presentation(SFX/UI)만 1회 재생한다.
        /// </summary>
        public void BroadcastBossSynergy(SynergyResult result)
        {
            if (!IsTutorialSessionReady || !Object.HasStateAuthority || !result.Triggered)
            {
                return;
            }

            BossSynergyKindValue = (int)result.Kind;
            BossSynergyBonusDamage = result.BonusDamage;
            BossSynergyFirstRole = (int)result.FirstRole;
            BossSynergySecondRole = (int)result.SecondRole;
            BossSynergyRevision++;
        }

        // ---------------------------------------------------------------
        // Pattern Authority
        // ---------------------------------------------------------------

        /// <summary>
        /// FinalBossAttackController가 "다음 공격을 시작할 시점"이라고 로컬에서
        /// 판단했을 때(IsBossCombatAuthority인 Peer만) 호출한다. 실제 공격 코루틴은
        /// 여기서 시작하지 않고, BossPatternRevision 변화를 관찰한 모든 Peer(권한
        /// 있는 이 Peer 자신 포함)가 동시에 PlayNetworkAttackPattern으로 재생한다.
        /// </summary>
        public void RequestBossPatternStart(int patternType)
        {
            if (!IsTutorialSessionReady || !Object.HasStateAuthority)
            {
                return;
            }

            BossPatternType = patternType;
            BossPatternRevision++;
            BossPatternStartTime = Runner != null ? Runner.SimulationTime : 0d;
        }

        private void HandleBossHealthNetworkChanged()
        {
            localBossHealth?.HandleBossCombatStateChanged();
        }

        private void HandleBossDeathNetworkChanged()
        {
            if (!BossIsDead) return;
            localBossHealth?.HandleBossCombatDeath();
        }

        private void HandleBossPatternNetworkChanged()
        {
            localBossAttack?.PlayNetworkAttackPattern(BossPatternType);
        }

        private void HandleBossSynergyNetworkChanged()
        {
            if (BossSynergyRevision <= 0 || localBossSynergyTracker == null) return;

            SynergyResult result = new SynergyResult(
                (SynergyKind)BossSynergyKindValue,
                BossSynergyBonusDamage,
                (PlayerRole)BossSynergyFirstRole,
                (PlayerRole)BossSynergySecondRole);

            localBossSynergyTracker.Present(result);
        }
    }
}
