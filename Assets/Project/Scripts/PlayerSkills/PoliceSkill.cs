using System;
using UnityEngine;

[Serializable]
public sealed class PoliceSkill : IJobSkill
{
    [Header("목표 지정")]
    [Tooltip("집중포화 목표를 찾는 최대 거리")]
    [Min(0.1f)] [SerializeField] private float maxTargetDistance = 30f;
    [Tooltip("목표 지정 Raycast가 확인할 레이어")]
    [SerializeField] private LayerMask targetMask = ~0;

    [Header("피해")]
    [Tooltip("목표 위치를 중심으로 피해를 주는 반경")]
    [Min(0.1f)] [SerializeField] private float attackRadius = 4f;
    [Tooltip("사격 중 피해가 적용되는 횟수")]
    [Min(1)] [SerializeField] private int damageTickCount = 6;
    [Tooltip("각 Tick마다 적에게 주는 피해")]
    [Min(0f)] [SerializeField] private float damagePerTick = 5f;
    [Tooltip("범위 판정에서 확인할 레이어")]
    [SerializeField] private LayerMask enemyMask = ~0;

    [Header("연출 시간")]
    [Min(0f)] [SerializeField] private float preparationTime = 0.5f;
    [Min(0.1f)] [SerializeField] private float firingDuration = 1.5f;

    [Header("꿈빛 총 연출")]
    [Tooltip("연출용으로 복제할 Police 총 오브젝트. 실제 원본은 변경하지 않습니다.")]
    [SerializeField] private GameObject gunVisualSource;
    [Range(1, 12)] [SerializeField] private int gunCount = 6;
    [Min(0.1f)] [SerializeField] private float gunFormationRadius = 4f;
    [Min(0f)] [SerializeField] private float gunHeight = 2.7f;
    [SerializeField] private Vector3 gunRotationOffset;
    [Header("Vefects Lightning Beam")]
    [Tooltip("Vefects 원본 Lightning Texture/LUT를 사용하는 Police 전용 Beam Material")]
    [SerializeField] private Material lightningBeamMaterial;
    [Min(2)] [SerializeField] private int lightningSegmentCount = 14;
    [Min(0f)] [SerializeField] private float lightningJitter = 0.3f;
    [Min(0.01f)] [SerializeField] private float lightningRefreshInterval = 0.08f;
    [Min(0.01f)] [SerializeField] private float lightningBeamWidth = 0.18f;

    [Header("전기 효과음")]
    [Tooltip("집중포화 사격 시작 순간 한 번 재생할 짧은 전기 파열음")]
    [SerializeField] private AudioClip electricCrackSound;
    [Range(0f, 1f)] [SerializeField] private float electricCrackVolume = 0.45f;
    [Tooltip("집중포화 사격 시작 순간 한 번 재생할 전기 폭발/충격음")]
    [SerializeField] private AudioClip electricImpactSound;
    [Range(0f, 1f)] [SerializeField] private float electricImpactVolume = 0.48f;
    [Tooltip("집중포화 사격 중 반복 재생할 전기/방전 효과음")]
    [SerializeField] private AudioClip electricFireSound;
    [Range(0f, 1f)] [SerializeField] private float electricFireVolume = 0.22f;
    [Min(0f)] [SerializeField] private float electricSoundMinDistance = 7f;
    [Min(0.01f)] [SerializeField] private float electricSoundMaxDistance = 25f;

    public void Execute(JobSkillContext context)
    {
        Vector3 targetPosition = FindTargetPosition(context);

        GameObject effectObject = new GameObject("Police_FocusedFire_Effect");
        PoliceFocusedFireEffect effect = effectObject.AddComponent<PoliceFocusedFireEffect>();

        effect.Initialize(
            targetPosition,
            context.Direction.forward,
            gunVisualSource,
            Mathf.Clamp(gunCount, 1, 12),
            Mathf.Max(0.1f, gunFormationRadius),
            Mathf.Max(0f, gunHeight),
            gunRotationOffset,
            Mathf.Max(0f, preparationTime),
            Mathf.Max(0.1f, firingDuration),
            Mathf.Max(0.1f, attackRadius),
            Mathf.Max(1, damageTickCount),
            Mathf.Max(0f, damagePerTick),
            enemyMask,
            lightningBeamMaterial,
            Mathf.Max(2, lightningSegmentCount),
            Mathf.Max(0f, lightningJitter),
            Mathf.Max(0.01f, lightningRefreshInterval),
            Mathf.Max(0.01f, lightningBeamWidth),
            electricCrackSound,
            Mathf.Clamp01(electricCrackVolume),
            electricImpactSound,
            Mathf.Clamp01(electricImpactVolume),
            electricFireSound,
            Mathf.Clamp01(electricFireVolume),
            Mathf.Max(0f, electricSoundMinDistance),
            Mathf.Max(electricSoundMinDistance + 0.01f, electricSoundMaxDistance)
        );
    }

    private Vector3 FindTargetPosition(JobSkillContext context)
    {
        Ray ray = new Ray(context.Direction.position, context.Direction.forward);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            Mathf.Max(0.1f, maxTargetDistance),
            targetMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    !hit.collider.transform.IsChildOf(context.Origin.root))
                {
                    return hit.point;
                }
            }
        }

        return context.Direction.position +
               context.Direction.forward * Mathf.Max(0.1f, maxTargetDistance);
    }
}
