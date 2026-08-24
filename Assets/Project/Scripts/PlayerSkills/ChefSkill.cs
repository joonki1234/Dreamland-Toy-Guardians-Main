using System;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class ChefSkill : IJobSkill
{
    [Header("Chef 스페셜 메뉴")]
    [Tooltip("스페셜 메뉴에서 사용할 고정 음식 Prefab입니다. 기본 공격의 랜덤 음식 목록과는 별개입니다.")]
    [SerializeField] private GameObject specialMenuFoodPrefab;

    [Tooltip("기존 Chef 음식 Prefab의 Scale을 몇 배로 크게 보여줄지 설정합니다.")]
    [Min(0.1f)] [SerializeField] private float giantFoodScaleMultiplier = 10f;

    [Tooltip("플레이어 발밑에서 떨어질 수 있는 최소 거리")]
    [Min(0.1f)] [SerializeField] private float minTargetDistance = 2.5f;

    [Tooltip("시선 기반 착탄 위치의 최대 거리")]
    [Min(0.1f)] [SerializeField] private float maxTargetDistance = 12f;

    [Tooltip("목표 지점 위쪽에서 생성될 높이")]
    [Min(0f)] [SerializeField] private float dropHeight = 60f;

    [Tooltip("낙하 시작 속도")]
    [Min(0.01f)] [SerializeField] private float initialFallSpeed = 2f;

    [Tooltip("낙하 가속도")]
    [Min(0f)] [SerializeField] private float fallAcceleration = 14f;

    [Tooltip("낙하 최대 속도")]
    [Min(0.01f)] [SerializeField] private float maxFallSpeed = 45f;

    [Tooltip("착탄 후 폭발까지의 대기 시간")]
    [Min(0f)] [SerializeField] private float explosionDelay = 0.5f;

    [Tooltip("착탄 범위 반경")]
    [Min(0.01f)] [SerializeField] private float explosionRadius = 5f;

    [Tooltip("범위 대미지")]
    [Min(0f)] [SerializeField] private float explosionDamage = 60f;

    [Header("폭발 VFX")]
    [Tooltip("실제 폭발 순간 착탄 지점에 생성할 VFX Prefab")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [Tooltip("생성된 폭발 VFX의 크기 배율")]
    [Min(0.01f)] [SerializeField] private float explosionVfxScale = 2f;
    [Tooltip("폭발 VFX를 바닥에서 띄울 높이")]
    [SerializeField] private float explosionVfxHeightOffset = 0.15f;

    [Tooltip("바닥을 찾는 레이어")]
    [SerializeField] private LayerMask groundLayerMask = 1 << 8;

    [Tooltip("지면 탐색을 시작할 높이")]
    [Min(0f)] [SerializeField] private float groundRayStartHeight = 3f;

    [Tooltip("지면 탐색 최대 거리")]
    [Min(0.01f)] [SerializeField] private float groundRayDistance = 20f;

    [Tooltip("광역 피해 대상 레이어")]
    [SerializeField] private LayerMask monsterLayerMask = 1 << 7;

    [Header("낙하 사운드")]
    [SerializeField] private AudioClip fallSound;
    [Range(0f, 1f)] [SerializeField] private float fallSoundVolume = 0.8f;
    [Range(0.01f, 3f)] [SerializeField] private float fallPitchMin = 0.9f;
    [Range(0.01f, 3f)] [SerializeField] private float fallPitchMax = 1.15f;
    [FormerlySerializedAs("fallAudioMinDistance")]
    [Min(0f)] [SerializeField] private float audioMinDistance = 3f;
    [FormerlySerializedAs("fallAudioMaxDistance")]
    [Min(0.01f)] [SerializeField] private float audioMaxDistance = 30f;
    [Range(0f, 0.2f)] [SerializeField] private float audioDopplerLevel = 0.1f;

    [Header("착탄 사운드")]
    [FormerlySerializedAs("impactSound")]
    [SerializeField] private AudioClip landingSound;
    [Range(0f, 1f)] [SerializeField] private float landingSoundVolume = 1f;

    [Header("폭발 사운드")]
    [SerializeField] private AudioClip explosionSound;
    [Range(0f, 1f)] [SerializeField] private float explosionSoundVolume = 1f;

    public void Execute(JobSkillContext context)
    {
        if (context.Origin == null)
        {
            Debug.LogWarning("[ChefSkill] 스페셜 메뉴: Origin Transform이 비어 있습니다.");
            return;
        }

        GameObject selectedFood = GetSpecialMenuFoodPrefab(context);
        if (selectedFood == null)
        {
            Debug.LogWarning("[ChefSkill] 스페셜 메뉴: 현재 프로젝트에 실제 Burger/Hamburger Prefab이 없습니다. Inspector에서 실제 햄버거 Prefab을 직접 연결해 주세요.");
            return;
        }

        Vector3 groundPoint = FindTargetGroundPoint(context);
        Vector3 spawnPosition = groundPoint + Vector3.up * Mathf.Max(0f, dropHeight);

        GameObject spawnedFood = UnityEngine.Object.Instantiate(selectedFood, spawnPosition, Quaternion.identity);
        spawnedFood.name = "Chef_GiantFood";

        Vector3 originalScale = spawnedFood.transform.localScale;
        float scaleFactor = Mathf.Max(0.1f, giantFoodScaleMultiplier);
        spawnedFood.transform.localScale = Vector3.Scale(originalScale, Vector3.one * scaleFactor);

        DisableFoodProjectileBehavior(spawnedFood);

        ChefSpecialMenuProjectile specialProjectile = spawnedFood.GetComponent<ChefSpecialMenuProjectile>();
        if (specialProjectile == null)
        {
            specialProjectile = spawnedFood.AddComponent<ChefSpecialMenuProjectile>();
        }

        specialProjectile.Initialize(
            groundPoint,
            Mathf.Max(0.01f, initialFallSpeed),
            Mathf.Max(0f, fallAcceleration),
            Mathf.Max(0.01f, maxFallSpeed),
            Mathf.Max(0f, explosionDelay),
            Mathf.Max(0.01f, explosionRadius),
            Mathf.Max(0f, explosionDamage),
            monsterLayerMask,
            fallSound,
            Mathf.Clamp01(fallSoundVolume),
            Mathf.Clamp(fallPitchMin, 0.01f, 3f),
            Mathf.Clamp(fallPitchMax, 0.01f, 3f),
            landingSound,
            Mathf.Clamp01(landingSoundVolume),
            explosionSound,
            Mathf.Clamp01(explosionSoundVolume),
            Mathf.Max(0f, audioMinDistance),
            Mathf.Max(audioMinDistance + 0.01f, audioMaxDistance),
            Mathf.Clamp(audioDopplerLevel, 0f, 0.2f),
            explosionVfxPrefab,
            Mathf.Max(0.01f, explosionVfxScale),
            explosionVfxHeightOffset
        );

        SetupFallAudio(spawnedFood);

        if (spawnedFood.TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        if (spawnedFood.TryGetComponent(out ChefFoodProjectile basicProjectile))
        {
            basicProjectile.enabled = false;
        }

        Debug.Log($"[ChefSkill] 스페셜 메뉴 발동: {selectedFood.name} -> {groundPoint} / Damage {explosionDamage}");
    }

    private GameObject GetSpecialMenuFoodPrefab(JobSkillContext context)
    {
        if (specialMenuFoodPrefab != null)
        {
            return specialMenuFoodPrefab;
        }

        return null;
    }

    private Vector3 FindTargetGroundPoint(JobSkillContext context)
    {
        Vector3 rootPosition = context.Origin.root.position;
        Vector3 viewForward = context.Forward;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(viewForward, Vector3.up);
        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            horizontalForward = Vector3.ProjectOnPlane(context.Origin.root.forward, Vector3.up);
        }

        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            horizontalForward = Vector3.forward;
        }

        horizontalForward.Normalize();

        float minDistance = Mathf.Max(0.1f, minTargetDistance);
        float maxDistance = Mathf.Max(minDistance, maxTargetDistance);
        float fallbackDistance = maxDistance;
        if (context.Direction != null && Physics.Raycast(
                context.Direction.position,
                viewForward.normalized,
                out RaycastHit viewHit,
                Mathf.Max(0.01f, groundRayDistance),
                groundLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 hitOffset = viewHit.point - rootPosition;
            hitOffset.y = 0f;
            float hitDistance = hitOffset.magnitude;
            if (hitDistance >= minDistance && hitDistance <= maxDistance)
            {
                return viewHit.point;
            }

            if (hitDistance < minDistance)
            {
                fallbackDistance = minDistance;
            }
            else
            {
                horizontalForward = hitOffset.normalized;
            }
        }

        Vector3 fallbackPosition = rootPosition + horizontalForward * Mathf.Clamp(fallbackDistance, minDistance, maxDistance);
        if (Physics.Raycast(
                fallbackPosition + Vector3.up * Mathf.Max(0f, groundRayStartHeight),
                Vector3.down,
                out RaycastHit fallbackHit,
                Mathf.Max(0.01f, groundRayDistance),
                groundLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            return fallbackHit.point;
        }

        return fallbackPosition;
    }

    private void DisableFoodProjectileBehavior(GameObject foodObject)
    {
        if (foodObject == null)
        {
            return;
        }

        if (foodObject.TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.useGravity = false;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        if (foodObject.TryGetComponent(out ChefFoodProjectile projectile))
        {
            projectile.enabled = false;
        }
    }

    private void SetupFallAudio(GameObject foodObject)
    {
        if (foodObject == null)
        {
            return;
        }

        AudioSource source = foodObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = foodObject.AddComponent<AudioSource>();
        }

        source.clip = fallSound;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = Mathf.Clamp(audioDopplerLevel, 0f, 0.2f);
        source.minDistance = Mathf.Max(0f, audioMinDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.01f, audioMaxDistance);
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.volume = Mathf.Clamp01(fallSoundVolume);
        source.pitch = Mathf.Clamp(fallPitchMin, 0.01f, 3f);
        source.Stop();
    }

    private void OnValidate()
    {
        giantFoodScaleMultiplier = Mathf.Max(0.1f, giantFoodScaleMultiplier);
        minTargetDistance = Mathf.Max(0.1f, minTargetDistance);
        maxTargetDistance = Mathf.Max(minTargetDistance, maxTargetDistance);
        dropHeight = Mathf.Max(0f, dropHeight);
        initialFallSpeed = Mathf.Max(0.01f, initialFallSpeed);
        fallAcceleration = Mathf.Max(0f, fallAcceleration);
        maxFallSpeed = Mathf.Max(initialFallSpeed, maxFallSpeed);
        explosionDelay = Mathf.Max(0f, explosionDelay);
        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);
        explosionVfxScale = Mathf.Max(0.01f, explosionVfxScale);
        groundRayDistance = Mathf.Max(0.01f, groundRayDistance);
        audioMinDistance = Mathf.Max(0f, audioMinDistance);
        audioMaxDistance = Mathf.Max(audioMinDistance + 0.01f, audioMaxDistance);
        fallPitchMin = Mathf.Clamp(fallPitchMin, 0.01f, 3f);
        fallPitchMax = Mathf.Clamp(fallPitchMax, fallPitchMin, 3f);
        audioDopplerLevel = Mathf.Clamp(audioDopplerLevel, 0f, 0.2f);
    }
}
