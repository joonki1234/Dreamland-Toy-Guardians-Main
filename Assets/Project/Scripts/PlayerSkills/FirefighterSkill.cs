using System;
using UnityEngine;

[Serializable]
public sealed class FirefighterSkill : IJobSkill
{
    [Header("소방차 출동!")]
    [Tooltip("돌진 연출에 사용할 소방차 Prefab")]
    [SerializeField] private GameObject fireTruckPrefab;

    [Tooltip("플레이어 앞쪽 소환 거리")]
    [Min(0f)] [SerializeField] private float spawnDistance = 2.5f;

    [Tooltip("플레이어 기준 소환 높이 보정")]
    [SerializeField] private float spawnHeightOffset;

    [Header("소환 바닥 탐색")]
    [Tooltip("소방차 소환 높이를 찾을 바닥 레이어")]
    [SerializeField] private LayerMask groundLayerMask = 1 << 8;

    [Tooltip("기존 플레이어 Root 기준 소환 위치에서 Ray를 시작할 위쪽 높이")]
    [Min(0f)] [SerializeField] private float groundRayStartHeight = 3f;

    [Tooltip("소환 순간 아래 방향으로 바닥을 찾을 최대 거리")]
    [Min(0.01f)] [SerializeField] private float groundRayDistance = 10f;

    [Tooltip("소방차의 초당 이동 거리")]
    [Min(0.01f)] [SerializeField] private float moveSpeed = 8f;

    [Tooltip("화면 밖으로 충분히 이동한 소방차를 정리하는 거리")]
    [Min(0.01f)] [SerializeField] private float cleanupDistance = 100f;

    [Header("적 넉백 감지")]
    [Min(0.01f)] [SerializeField] private float hitboxWidth = 2.5f;
    [Min(0.01f)] [SerializeField] private float hitboxHeight = 2f;
    [Min(0.01f)] [SerializeField] private float hitboxLength = 3f;
    [Min(0f)] [SerializeField] private float knockbackDistance = 4f;
    [Min(0.01f)] [SerializeField] private float knockbackDuration = 0.3f;
    [Tooltip("소방차 넉백이 감지할 Monster 레이어")]
    [SerializeField] private LayerMask monsterLayerMask = 1 << 7;

    [Header("소방차 피해")]
    [Min(0f)] [SerializeField] private float truckDamage = 50f;

    [Header("소방차 사운드")]
    [Tooltip("차량이 이동하는 동안 반복 재생할 사이렌")]
    [SerializeField] private AudioClip sirenSound;
    [Range(0f, 1f)] [SerializeField] private float sirenVolume = 0.8f;
    [Tooltip("차량이 등장할 때 한 번 재생할 효과음")]
    [SerializeField] private AudioClip spawnSound;
    [Range(0f, 1f)] [SerializeField] private float spawnSoundVolume = 1f;
    [Tooltip("차량이 적과 처음 충돌할 때 재생할 효과음")]
    [SerializeField] private AudioClip impactSound;
    [Range(0f, 1f)] [SerializeField] private float impactVolume = 1f;
    [Tooltip("여러 적을 연속으로 타격할 때 충돌음이 겹치지 않도록 하는 최소 재생 간격")]
    [Min(0f)] [SerializeField] private float impactSoundMinInterval = 0.1f;
    [Min(0f)] [SerializeField] private float audioMinDistance = 3f;
    [Min(0.01f)] [SerializeField] private float audioMaxDistance = 30f;
    [Range(0f, 0.2f)] [SerializeField] private float audioDopplerLevel = 0.1f;

    public void Execute(JobSkillContext context)
    {
        if (fireTruckPrefab == null)
        {
            Debug.LogWarning("소방차 출동!: Fire Truck Prefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(context.Forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(context.Origin.root.forward, Vector3.up);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        // HMD 높이가 아닌 네트워크 플레이어 루트를 몸 중심 기준으로 사용합니다.
        Vector3 spawnPosition = context.Origin.root.position +
                                forward * Mathf.Max(0f, spawnDistance);
        Vector3 groundRayOrigin = spawnPosition +
                                  Vector3.up * Mathf.Max(0f, groundRayStartHeight);

        if (Physics.Raycast(
                groundRayOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                Mathf.Max(0.01f, groundRayDistance),
                groundLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            spawnPosition.y = groundHit.point.y + spawnHeightOffset;
        }
        else
        {
            // floor 레이어가 없는 테스트 씬에서도 기존 소환 방식으로 안전하게 동작합니다.
            spawnPosition.y = context.Origin.root.position.y + spawnHeightOffset;

#if UNITY_EDITOR
            Debug.LogWarning(
                $"소방차 출동!: 소환 위치 아래에서 바닥을 찾지 못해 " +
                $"플레이어 Root Y를 사용합니다. (Origin: {groundRayOrigin}, " +
                $"Distance: {groundRayDistance})"
            );
#endif
        }

        Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
        GameObject truckObject = UnityEngine.Object.Instantiate(
            fireTruckPrefab,
            spawnPosition,
            spawnRotation
        );

        FireTruckSkillMover mover = truckObject.GetComponent<FireTruckSkillMover>();
        if (mover == null)
        {
            mover = truckObject.AddComponent<FireTruckSkillMover>();
        }

        mover.Initialize(
            forward,
            Mathf.Max(0.01f, moveSpeed),
            Mathf.Max(0.01f, cleanupDistance),
            Mathf.Max(0.01f, hitboxWidth),
            Mathf.Max(0.01f, hitboxHeight),
            Mathf.Max(0.01f, hitboxLength),
            Mathf.Max(0f, knockbackDistance),
            Mathf.Max(0.01f, knockbackDuration),
            monsterLayerMask,
            Mathf.Max(0f, truckDamage),
            sirenSound,
            Mathf.Clamp01(sirenVolume),
            spawnSound,
            Mathf.Clamp01(spawnSoundVolume),
            impactSound,
            Mathf.Clamp01(impactVolume),
            Mathf.Max(0f, impactSoundMinInterval),
            Mathf.Max(0f, audioMinDistance),
            Mathf.Max(audioMinDistance + 0.01f, audioMaxDistance),
            Mathf.Clamp(audioDopplerLevel, 0f, 0.2f)
        );
    }
}
