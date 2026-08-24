using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class BuilderVfxMaterialOverride
{
    [SerializeField] private string sourceMaterialName;
    [SerializeField] private Material replacementMaterial;

    public string SourceMaterialName => sourceMaterialName;
    public Material ReplacementMaterial => replacementMaterial;
}

[Serializable]
public sealed class BuilderSkill : IJobSkill
{
    [Header("긴급 철거 - 망치")]
    [Tooltip("Blender Animation Curve가 포함된 Builder Magic Hammer FBX")]
    [SerializeField] private GameObject animatedHammerPrefab;
    [SerializeField] private AnimationClip hammerAnimationClip;
    [SerializeField] private Material animatedHammerMaterial;
    [Tooltip("플레이어 로컬 기준 망치 시작 위치 (좌/위/전방)")]
    [SerializeField] private Vector3 hammerSpawnOffset = new Vector3(-1.4f, 2.6f, 0.2f);
    [Tooltip("Renderer bounds의 가장 긴 축을 이 월드 크기에 맞춥니다.")]
    [Min(0.1f)] [SerializeField] private float hammerTargetVisualSize = 5.5f;
    [Min(0f)] [SerializeField] private float preparationDuration = 0.2f;
    [Header("긴급 철거 - FBX Animation")]
    [FormerlySerializedAs("hammerImpactOffset")]
    [Tooltip("Hammer 충돌점이 아니라 Swing Trail 경로의 시각적 종점입니다.")]
    [SerializeField] private Vector3 swingVfxEndOffset = new Vector3(0f, 0.1f, 2.8f);
    [SerializeField] private Vector3 hammerAnchorOffset = new Vector3(-2.8f, 1.1f, 3.2f);
    [SerializeField] private Vector3 hammerAnchorRotation = new Vector3(0f, -20f, -10f);
    [Range(0f, 1f)] [SerializeField] private float hammerImpactNormalizedTime = 0.79f;
    [Tooltip("베지어 곡선을 위로 부풀리는 높이. 낮을수록 더 가파르게 내려찍습니다.")]
    [Min(0f)] [SerializeField] private float swingArcHeight = 0.75f;

    [Header("긴급 철거 - Swing Trail")]
    [SerializeField] private GameObject swingTrailVfx;
    [SerializeField] private Vector3 swingTrailScale = Vector3.one;
    [SerializeField] private Vector3 swingTrailOffset = new Vector3(0f, 1.2f, 1.5f);
    [SerializeField] private Vector3 swingTrailRotationOffset = new Vector3(0f, 0f, 90f);
    [Tooltip("Builder가 생성한 VFX 인스턴스에만 적용되는 Material 이름별 URP 교체표")]
    [SerializeField] private BuilderVfxMaterialOverride[] vfxMaterialOverrides;
    [SerializeField] private GameObject secondarySwingVfx;
    [SerializeField] private Vector3 secondarySwingScale = new Vector3(0.82f, 0.82f, 0.82f);
    [SerializeField] private Vector3 secondarySwingRotationOffset = new Vector3(0f, 8f, -8f);
    [Min(0f)] [SerializeField] private float secondarySwingDelay = 0.08f;
    [Tooltip("망치 머리를 따라가는 약한 입자 효과")]
    [SerializeField] private GameObject swingParticleVfx;
    [SerializeField] private Vector3 swingParticleOffset = Vector3.zero;

    [Header("긴급 철거 - 등장 VFX")]
    [SerializeField] private GameObject hammerSpawnVfx;
    [SerializeField] private Vector3 hammerSpawnVfxScale = Vector3.one;
    [Min(0.05f)] [SerializeField] private float hammerSpawnVfxLifetime = 0.25f;

    [Header("긴급 철거 - Impact VFX")]
    [SerializeField] private GameObject demolitionImpactPrefab;
    [Tooltip("Builder 파편에 무작위로 사용하는 실제 Rock Mesh들")]
    [SerializeField] private Mesh[] debrisMeshes;
    [SerializeField] private Material debrisMaterial;
    [SerializeField] private GameObject dustVfx;
    [SerializeField] private Vector3 dustVfxScale = new Vector3(2f, 2f, 2f);

    [Header("긴급 철거 - 지면 탐색")]
    [SerializeField] private LayerMask groundLayerMask = 1 << 8;
    [Min(0f)] [SerializeField] private float groundRayStartHeight = 4f;
    [Min(0.01f)] [SerializeField] private float groundRayDistance = 12f;

    [Header("긴급 철거 - 충격파")]
    [SerializeField] private LayerMask monsterLayerMask = 1 << 7;
    [Min(0.01f)] [SerializeField] private float impactRadius = 5.5f;
    [Min(0.01f)] [SerializeField] private float directHitRadius = 2f;
    [Min(0f)] [SerializeField] private float directDamage = 90f;
    [Min(0f)] [SerializeField] private float shockwaveDamage = 60f;
    [Min(0f)] [SerializeField] private float stunDuration = 0.8f;
    [Min(0f)] [SerializeField] private float knockbackForce = 4.5f;
    [Min(0.01f)] [SerializeField] private float knockbackDuration = 0.3f;

    [Header("긴급 철거 - 사운드")]
    [FormerlySerializedAs("impactSound")]
    [SerializeField] private AudioClip hammerImpactSound;
    [FormerlySerializedAs("impactVolume")]
    [Range(0f, 1f)] [SerializeField] private float hammerImpactVolume = 0.8f;
    [SerializeField] private AudioClip boomImpactSound;
    [Range(0f, 1f)] [SerializeField] private float boomImpactVolume = 1f;
    [Range(0f, 0.1f)] [SerializeField] private float boomImpactDelay = 0.02f;
    [Min(0f)] [SerializeField] private float audioMinDistance = 3f;
    [Min(0.01f)] [SerializeField] private float audioMaxDistance = 30f;

    [NonSerialized] private BuilderEmergencyDemolitionEffect activeEffect;

    public bool IsActive => activeEffect != null;

    public void Execute(JobSkillContext context)
    {
        if (context.Origin == null || context.Direction == null || IsActive)
        {
            return;
        }

        GameObject effectObject = new GameObject("Builder_EmergencyDemolition_Effect");
        activeEffect = effectObject.AddComponent<BuilderEmergencyDemolitionEffect>();
        activeEffect.Initialize(
            context.Origin, context.Direction, animatedHammerPrefab,
            hammerAnimationClip, animatedHammerMaterial, hammerSpawnOffset,
            swingVfxEndOffset, hammerAnchorOffset, hammerAnchorRotation,
            hammerTargetVisualSize, hammerImpactNormalizedTime,
            preparationDuration, swingArcHeight,
            swingTrailVfx, swingTrailScale, swingTrailOffset,
            swingTrailRotationOffset, vfxMaterialOverrides,
            secondarySwingVfx, secondarySwingScale, secondarySwingRotationOffset,
            secondarySwingDelay, swingParticleVfx, swingParticleOffset,
            hammerSpawnVfx, hammerSpawnVfxScale, hammerSpawnVfxLifetime,
            demolitionImpactPrefab, debrisMeshes, debrisMaterial,
            dustVfx, dustVfxScale,
            groundLayerMask, groundRayStartHeight, groundRayDistance,
            monsterLayerMask, impactRadius, directHitRadius, directDamage,
            shockwaveDamage, stunDuration, knockbackForce, knockbackDuration,
            hammerImpactSound, hammerImpactVolume, boomImpactSound,
            boomImpactVolume, boomImpactDelay, audioMinDistance, audioMaxDistance,
            HandleEffectFinished);
    }

    public void Cancel()
    {
        if (activeEffect != null)
        {
            activeEffect.Cancel();
        }
        activeEffect = null;
    }

    private void HandleEffectFinished(BuilderEmergencyDemolitionEffect effect)
    {
        if (activeEffect == effect)
        {
            activeEffect = null;
        }
    }
}
