using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DreamGuardians;
using Fusion;

/// <summary>
/// 건축가의 흙 장판에 요리사 음식이 닿으면
/// 주변 적을 장판으로 유인한 뒤 범위 폭발을 일으킨다.
/// </summary>
public class MudSplatSynergy : NetworkBehaviour
{
    [Header("유인 설정")]

    [Tooltip("음식이 닿은 뒤 유인이 시작되기까지의 시간")]
    [SerializeField]
    private float lureStartDelay = 0.1f;

    [Tooltip("주변 적을 검색하는 유인 범위")]
    [SerializeField]
    private float lureRadius = 5f;

    [Tooltip("적을 장판 쪽으로 유인하는 시간")]
    [SerializeField]
    private float lureDuration = 2f;


    [Header("폭발 설정")]

    [Tooltip("폭발 피해가 적용되는 범위")]
    [SerializeField]
    private float explosionRadius = 2f;

    [Tooltip("폭발 피해량")]
    [SerializeField]
    private float explosionDamage = 30f;


    [Header("폭발 이펙트")]

    [Tooltip("시너지 폭발 순간 생성할 이펙트 프리팹")]
    [SerializeField]
    private GameObject explosionEffectPrefab;

    [Tooltip("생성된 폭발 이펙트의 크기 배율")]
    [SerializeField]
    private float explosionEffectScale = 0.7f;

    [Tooltip("생성된 폭발 이펙트를 삭제하기까지의 시간")]
    [SerializeField]
    private float explosionEffectLifetime = 3f;

    [Tooltip("폭발 이펙트가 바닥에 묻히지 않도록 올리는 높이")]
    [SerializeField]
    private float explosionEffectHeight = 0.05f;


    [Header("시너지 효과음")]

    [Tooltip("음식이 장판에 닿아 시너지가 활성화될 때 재생할 소리")]
    [SerializeField]
    private AudioClip activationSound;

    [SerializeField, Range(0f, 1f)]
    private float activationSoundVolume = 0.5f;

    [Tooltip("유인이 끝나고 실제 폭발할 때 재생할 소리")]
    [SerializeField]
    private AudioClip explosionSound;

    [SerializeField, Range(0f, 1f)]
    private float explosionSoundVolume = 0.65f;

    [SerializeField, Min(0.01f)]
    private float audioMinDistance = 3f;

    [SerializeField, Min(0.01f)]
    private float audioMaxDistance = 30f;

    [SerializeField, Range(0f, 1f)]
    private float audioDopplerLevel;


    [Header("넉백 설정")]

    [SerializeField]
    private float knockbackDistance = 0.7f;

    [SerializeField]
    private float knockbackDuration = 0.15f;

    [SerializeField]
    private float stunDuration = 0.15f;


    [Header("적 레이어")]

    [SerializeField]
    private LayerMask enemyLayer;


    private bool synergyActivated;

    private static int nextShotId = 100000;
    private bool spawnCompleted;
    private bool activationPresented;
    private bool explosionPresented;
    private float lifetime = 30f;
    private bool IsNetworked => spawnCompleted && Object != null && Object.IsValid;
    [Networked] public int Phase { get; private set; }
    [Networked] private TickTimer PhaseTimer { get; set; }
    [Networked] private TickTimer ExpiryTimer { get; set; }
    [Networked] private Vector3 PlacementPosition { get; set; }
    [Networked] private Quaternion PlacementRotation { get; set; }

    public void ConfigureLifetime(float seconds) => lifetime = Mathf.Max(0.1f, seconds);

    public override void Spawned()
    {
        spawnCompleted = true;
        if (Object.HasStateAuthority)
        {
            Phase = 0;
            PlacementPosition = transform.position;
            PlacementRotation = transform.rotation;
            ExpiryTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
            SynergyNetLog.Write($"MudSplat Spawn Builder={Object.StateAuthority} Object={Object.Id}", this);
        }
        Render();
    }

    public override void Render()
    {
        transform.SetPositionAndRotation(PlacementPosition, PlacementRotation);
        if (Phase == 3) HideTrap();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (ExpiryTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }
        if (!PhaseTimer.Expired(Runner)) return;
        if (Phase == 1)
        {
            Phase = 2;
            LureNearbyEnemies();
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, lureDuration);
        }
        else if (Phase == 2)
        {
            Phase = 3; // commit before any damage/event callback can re-enter
            PhaseTimer = TickTimer.None;
            RPC_PresentExplosion();
            Explode();
            // Keep the network object alive while the reliable result is delivered.
            ExpiryTimer = TickTimer.CreateFromSeconds(Runner, explosionEffectLifetime);
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || synergyActivated || (IsNetworked && Phase != 0) ||
            !RoleSynergyProgression.IsUnlocked)
        {
            return;
        }

        ChefFoodProjectile food =
            other.GetComponentInParent<ChefFoodProjectile>();

        if (food == null || !food.CanActivateMudSplat)
        {
            return;
        }

        if (!food.TryConsumeForMudSplat()) return;
        if (IsNetworked)
        {
            SynergyNetLog.Write($"Chef Enter MudSplat Chef={Runner.LocalPlayer} Object={Object.Id}", this);
            RPC_RequestActivation();
            return;
        }

        synergyActivated = true;
        PresentActivation();
        StartCoroutine(ActivateSynergyRoutine());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestActivation(RpcInfo info = default)
    {
        if (Phase != 0 || !RoleSynergyProgression.IsUnlocked || ExpiryTimer.Expired(Runner)) return;
        Phase = 1;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, lureStartDelay);
        // Activation wins against the unused trap lifetime.
        ExpiryTimer = TickTimer.None;
        SynergyNetLog.Write($"MudSplat TRIGGERED Builder={Object.StateAuthority} Chef={info.Source} Object={Object.Id}", this);
        RPC_PresentActivation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PresentActivation() => PresentActivation();

    private void PresentActivation()
    {
        if (activationPresented) return;
        activationPresented = true;
        SpatialAudioOneShot.Play(
            activationSound,
            transform.position,
            activationSoundVolume,
            audioMinDistance,
            audioMaxDistance,
            audioDopplerLevel,
            "ChefBuilderSynergy_ActivationAudio"
        );

        RaiseOfficialSynergyEvent();

        SynergyNetLog.Write("MudSplat activation presentation", this);
    }


    private void RaiseOfficialSynergyEvent()
    {
        SynergyResult result = new SynergyResult(
            SynergyKind.ChefArchitectCombo,
            0f,
            PlayerRole.Chef,
            PlayerRole.Architect
        );

        DreamGameEvents.RaiseSynergyTriggered(
            new SynergyEventData(null, result)
        );
    }


    /// <summary>
    /// 잠시 기다린 뒤 적을 유인하고,
    /// 유인 시간이 끝나면 폭발한다.
    /// </summary>
    private IEnumerator ActivateSynergyRoutine()
    {
        yield return new WaitForSeconds(
            lureStartDelay
        );

        int luredEnemyCount =
            LureNearbyEnemies();

        SynergyNetLog.Write($"MudSplat Lured={luredEnemyCount}", this);

        yield return new WaitForSeconds(
            lureDuration
        );

        PresentExplosion();
        Explode();
        Destroy(gameObject);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PresentExplosion() => PresentExplosion();

    private void PresentExplosion()
    {
        if (explosionPresented) return;
        explosionPresented = true;
        SynergyNetLog.Write("MudSplat explosion presentation", this);
        CreateExplosionEffect();
        SpatialAudioOneShot.Play(
            explosionSound,
            transform.position,
            explosionSoundVolume,
            audioMinDistance,
            audioMaxDistance,
            audioDopplerLevel,
            "ChefBuilderSynergy_ExplosionAudio"
        );
        HideTrap();
    }

    private void HideTrap()
    {
        foreach (Renderer visual in GetComponentsInChildren<Renderer>()) visual.enabled = false;
        foreach (Collider hitbox in GetComponentsInChildren<Collider>()) hitbox.enabled = false;
    }


    /// <summary>
    /// 유인 범위 안의 적을 찾아
    /// 일정 시간 동안 장판 위치로 이동시킨다.
    /// </summary>
    private int LureNearbyEnemies()
    {
        if (IsNetworked && !Object.HasStateAuthority) return 0;
        Collider[] hitColliders =
            Physics.OverlapSphere(
                transform.position,
                lureRadius,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );

        // Collider가 여러 개인 적을 중복 처리하지 않는다.
        HashSet<EnemyCoreMover> luredMovers =
            new HashSet<EnemyCoreMover>();

        foreach (Collider hitCollider in hitColliders)
        {
            EnemyHealth enemy =
                hitCollider.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy.IsDead || enemy.GetComponent<FinalBossAttackController>() != null)
            {
                continue;
            }

            EnemyCoreMover mover =
                enemy.GetComponent<EnemyCoreMover>();

            if (mover == null)
            {
                continue;
            }

            if (!luredMovers.Add(mover))
            {
                continue;
            }

            enemy.RequestMudSplatLure(
                transform.position,
                lureDuration
            );
        }

        return luredMovers.Count;
    }


    /// <summary>
    /// 장판 위치에 폭발 이펙트를 생성한다.
    /// </summary>
    private void CreateExplosionEffect()
    {
        if (explosionEffectPrefab == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: " +
                "폭발 이펙트 프리팹이 연결되지 않았습니다."
            );

            return;
        }

        Vector3 effectPosition =
            transform.position +
            Vector3.up * explosionEffectHeight;

        GameObject effect = Instantiate(
            explosionEffectPrefab,
            effectPosition,
            Quaternion.identity
        );

        effect.transform.localScale *=
            explosionEffectScale;

        Destroy(
            effect,
            explosionEffectLifetime
        );
    }


    /// <summary>
    /// 폭발 범위 안의 적에게
    /// 피해와 넉백을 적용한다.
    /// </summary>
    private void Explode()
    {
        if (IsNetworked && !Object.HasStateAuthority) return;
        Collider[] hitColliders =
            Physics.OverlapSphere(
                transform.position,
                explosionRadius,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );

        HashSet<EnemyHealth> damagedEnemies =
            new HashSet<EnemyHealth>();

        int shotId = nextShotId++;

        foreach (Collider hitCollider in hitColliders)
        {
            EnemyHealth enemy =
                hitCollider.GetComponentInParent<EnemyHealth>();

            // 보스는 유인(LureNearbyEnemies)만 제외하고, 폭발 피해는 일반 적과
            // 동일하게 받는다. enemy.TakeDamage()가 내부적으로 보스일 때 자동으로
            // DreamEnemySpawner.BossCombat(State Authority)에게 데미지를 위임하므로
            // (EnemyHealth.TakeBossCombatDamage 참고) 여기서는 별도 분기가 필요 없다.
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            if (!damagedEnemies.Add(enemy))
            {
                continue;
            }

            DamageInfo damageInfo =
                new DamageInfo(
                    explosionDamage,
                    IsNetworked ? "MUDSPLAT_" + Object.Id : "CHEF_BUILDER_SYNERGY",
                    PlayerRole.Architect,
                    shotId,
                    enemy.transform.position,
                    false
                );
            damageInfo.synergyOrigin = transform.position;
            damageInfo.synergyImpulse = new Vector3(stunDuration, knockbackDistance, knockbackDuration);

            bool damageApplied =
                enemy.TakeDamage(damageInfo);

            if (!damageApplied)
            {
                continue;
            }

            SynergyNetLog.Write($"MudSplat Explosion Enemy={enemy.name} ShotId={shotId} Damage={explosionDamage}", this);
        }

        SynergyNetLog.Write($"MudSplat Explosion Targets={damagedEnemies.Count}", this);
    }


    /// <summary>
    /// Scene 창에서 유인 범위와 폭발 범위를 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 큰 원: 유인 범위
        Gizmos.DrawWireSphere(
            transform.position,
            lureRadius
        );

        // 작은 원: 폭발 범위
        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius
        );
    }


    private void OnValidate()
    {
        lureStartDelay =
            Mathf.Max(0f, lureStartDelay);

        lureRadius =
            Mathf.Max(0.1f, lureRadius);

        lureDuration =
            Mathf.Max(0.1f, lureDuration);

        explosionRadius =
            Mathf.Max(0.1f, explosionRadius);

        explosionDamage =
            Mathf.Max(0f, explosionDamage);

        explosionEffectScale =
            Mathf.Max(0.01f, explosionEffectScale);

        explosionEffectLifetime =
            Mathf.Max(0.1f, explosionEffectLifetime);

        audioMinDistance =
            Mathf.Max(0.01f, audioMinDistance);

        audioMaxDistance =
            Mathf.Max(audioMinDistance, audioMaxDistance);

        knockbackDistance =
            Mathf.Max(0f, knockbackDistance);

        knockbackDuration =
            Mathf.Max(0.01f, knockbackDuration);

        stunDuration =
            Mathf.Max(0f, stunDuration);
    }
}
