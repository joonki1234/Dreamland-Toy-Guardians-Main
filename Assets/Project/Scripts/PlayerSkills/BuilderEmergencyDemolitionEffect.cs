using System;
using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>긴급 철거의 FBX Hammer Animation, 충돌 연출 및 범위 공격을 관리합니다.</summary>
public sealed class BuilderEmergencyDemolitionEffect : MonoBehaviour
{
    private enum VfxRole { MainSwing, SecondarySwing, Debris, Dust, Spawn }
    private const int HitBufferSize = 128;
    private static int nextSkillShotId = 1200000;
    private readonly Collider[] hitBuffer = new Collider[HitBufferSize];
    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();

    private Transform origin;
    private Transform directionSource;
    private GameObject animatedHammerPrefab;
    private AnimationClip hammerAnimationClip;
    private Material animatedHammerMaterial;
    private Vector3 spawnOffset;
    private float targetVisualSize;
    private float preparationDuration;
    private Vector3 swingVfxEndOffset;
    private Vector3 hammerAnchorOffset;
    private Quaternion hammerAnchorRotation;
    private float hammerImpactNormalizedTime;
    private float swingArcHeight;
    private GameObject trailPrefab;
    private Vector3 trailScale;
    private Vector3 trailOffset;
    private Vector3 trailRotationOffset;
    private BuilderVfxMaterialOverride[] vfxMaterialOverrides;
    private GameObject secondaryTrailPrefab;
    private Vector3 secondaryTrailScale;
    private Vector3 secondaryTrailRotation;
    private float secondaryTrailDelay;
    private GameObject swingParticlePrefab;
    private Vector3 swingParticleOffset;
    private GameObject spawnVfx;
    private Vector3 spawnVfxScale;
    private float spawnVfxLifetime;
    private GameObject demolitionImpactPrefab;
    private Mesh[] debrisMeshes;
    private Material debrisMaterial;
    private GameObject dustVfx;
    private Vector3 dustScale;
    private LayerMask groundMask;
    private float rayStartHeight;
    private float rayDistance;
    private LayerMask monsterMask;
    private float impactRadius;
    private float directRadius;
    private float directDamage;
    private float shockwaveDamage;
    private float stunDuration;
    private float knockbackDistance;
    private float knockbackDuration;
    private AudioClip hammerImpactSound;
    private float hammerImpactVolume;
    private AudioClip boomImpactSound;
    private float boomImpactVolume;
    private float boomImpactDelay;
    private float audioMinDistance;
    private float audioMaxDistance;
    private Action<BuilderEmergencyDemolitionEffect> finished;
    private GameObject hammerObject;
    private Transform hammerAnimationAnchor;
    private Transform hammerImpactPoint;
    private GameObject trailObject;
    private GameObject secondaryTrailObject;
    private GameObject swingParticleObject;
    private Coroutine swingRoutine;
    private bool isFinishing;

    public void Initialize(
        Transform skillOrigin, Transform skillDirection, GameObject animatedHammer,
        AnimationClip animationClip, Material hammerMaterial,
        Vector3 hammerSpawnOffset, Vector3 swingEndOffset,
        Vector3 anchorOffset, Vector3 anchorRotation,
        float visualSize, float impactNormalizedTime,
        float preparation, float arcHeight,
        GameObject swingTrail, Vector3 swingScale, Vector3 swingOffset,
        Vector3 swingTrailRotation, BuilderVfxMaterialOverride[] materialOverrides,
        GameObject secondaryTrail, Vector3 secondaryScale, Vector3 secondaryRotation,
        float secondaryDelay, GameObject swingParticles, Vector3 particleOffset,
        GameObject hammerSpawn, Vector3 hammerSpawnScale, float hammerSpawnLifetime,
        GameObject demolitionImpact, Mesh[] debrisParticleMeshes,
        Material debrisRockMaterial, GameObject dust, Vector3 dustVfxScale,
        LayerMask groundLayer, float groundStartHeight, float groundDistance,
        LayerMask enemyLayer, float radius, float centerRadius, float centerDamage,
        float waveDamage, float stun, float knockback, float knockbackTime,
        AudioClip hammerSound, float hammerVolume, AudioClip boomSound,
        float boomVolume, float boomDelay, float minDistance, float maxDistance,
        Action<BuilderEmergencyDemolitionEffect> onFinished)
    {
        origin = skillOrigin; directionSource = skillDirection;
        animatedHammerPrefab = animatedHammer; hammerAnimationClip = animationClip;
        animatedHammerMaterial = hammerMaterial;
        spawnOffset = hammerSpawnOffset; swingVfxEndOffset = swingEndOffset;
        targetVisualSize = Mathf.Max(0.1f, visualSize);
        preparationDuration = Mathf.Max(0f, preparation);
        hammerAnchorOffset = anchorOffset;
        hammerAnchorRotation = Quaternion.Euler(anchorRotation);
        hammerImpactNormalizedTime = Mathf.Clamp01(impactNormalizedTime);
        swingArcHeight = Mathf.Max(0f, arcHeight);
        trailPrefab = swingTrail; trailScale = swingScale; trailOffset = swingOffset;
        trailRotationOffset = swingTrailRotation;
        vfxMaterialOverrides = materialOverrides;
        secondaryTrailPrefab = secondaryTrail; secondaryTrailScale = secondaryScale;
        secondaryTrailRotation = secondaryRotation; secondaryTrailDelay = Mathf.Max(0f, secondaryDelay);
        swingParticlePrefab = swingParticles; swingParticleOffset = particleOffset;
        spawnVfx = hammerSpawn; spawnVfxScale = hammerSpawnScale;
        spawnVfxLifetime = Mathf.Max(0.05f, hammerSpawnLifetime);
        demolitionImpactPrefab = demolitionImpact; debrisMeshes = debrisParticleMeshes;
        debrisMaterial = debrisRockMaterial;
        dustVfx = dust; dustScale = dustVfxScale;
        groundMask = groundLayer;
        rayStartHeight = Mathf.Max(0f, groundStartHeight);
        rayDistance = Mathf.Max(0.01f, groundDistance); monsterMask = enemyLayer;
        impactRadius = Mathf.Max(0.01f, radius);
        directRadius = Mathf.Clamp(centerRadius, 0.01f, impactRadius);
        directDamage = Mathf.Max(0f, centerDamage);
        shockwaveDamage = Mathf.Max(0f, waveDamage);
        stunDuration = Mathf.Max(0f, stun); knockbackDistance = Mathf.Max(0f, knockback);
        knockbackDuration = Mathf.Max(0.01f, knockbackTime);
        hammerImpactSound = hammerSound; hammerImpactVolume = Mathf.Clamp01(hammerVolume);
        boomImpactSound = boomSound; boomImpactVolume = Mathf.Clamp01(boomVolume);
        boomImpactDelay = Mathf.Clamp(boomDelay, 0f, 0.1f);
        audioMinDistance = Mathf.Max(0f, minDistance);
        audioMaxDistance = Mathf.Max(audioMinDistance + 0.01f, maxDistance);
        finished = onFinished;
        swingRoutine = StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        Vector3 forward = GetFlatForward();
        Quaternion frame = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 start = origin.root.position + frame * spawnOffset;
        Vector3 swingVfxEnd = FindGroundPoint(origin.root.position + frame * swingVfxEndOffset);

        SpawnTemporaryVfx(spawnVfx, start, spawnVfxScale, spawnVfxLifetime);

        if (animatedHammerPrefab != null)
        {
            GameObject anchorObject = new GameObject("BuilderHammerAnimationAnchor");
            anchorObject.transform.SetParent(transform, false);
            hammerAnimationAnchor = anchorObject.transform;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 anchorPosition = origin.root.position
                + right * hammerAnchorOffset.x
                + Vector3.up * hammerAnchorOffset.y
                + forward * hammerAnchorOffset.z;
            hammerAnimationAnchor.SetPositionAndRotation(
                anchorPosition,
                frame * hammerAnchorRotation);
            hammerObject = UnityEngine.Object.Instantiate(
                animatedHammerPrefab,
                hammerAnimationAnchor);
            hammerObject.name = "BuilderMagicHammer";
            BuilderMagicHammerVisual visual = hammerObject.GetComponent<BuilderMagicHammerVisual>();
            if (visual == null || visual.AnimationTarget == null)
            {
                Debug.LogError("[BuilderSkill] Runtime Hammer prefab is missing its animation target.");
                Cancel();
                yield break;
            }
            if (visual.ImpactPoint == null)
            {
                Debug.LogError("[BuilderSkill] Runtime Hammer prefab is missing HammerImpactPoint.");
                Cancel();
                yield break;
            }
            hammerImpactPoint = visual.ImpactPoint;
            GameObject animationTarget = visual.AnimationTarget;
            if (hammerAnimationClip != null) hammerAnimationClip.SampleAnimation(animationTarget, 0f);
            ApplyAnimatedHammerMaterial(visual.HammerRenderers);
            FitAnimatedHammerToVisualSize(hammerAnimationAnchor, visual.HammerRenderers);
        }
        if (trailPrefab != null)
        {
            trailObject = Instantiate(trailPrefab, origin.root.position + frame * trailOffset, frame);
            trailObject.name = "Builder_EmergencyDemolition_SwingTrail";
            trailObject.transform.localScale = Vector3.Scale(trailObject.transform.localScale, trailScale);
            ApplyBuilderMaterialOverrides(trailObject);
            PruneAndTintVfx(trailObject, VfxRole.MainSwing);
            trailObject.SetActive(false);
        }

        if (secondaryTrailPrefab != null)
        {
            secondaryTrailObject = Instantiate(secondaryTrailPrefab, start,
                frame * Quaternion.Euler(secondaryTrailRotation));
            secondaryTrailObject.name = "Builder_EmergencyDemolition_SecondarySlash";
            secondaryTrailObject.transform.localScale = Vector3.Scale(
                secondaryTrailObject.transform.localScale, secondaryTrailScale);
            ApplyBuilderMaterialOverrides(secondaryTrailObject);
            PruneAndTintVfx(secondaryTrailObject, VfxRole.SecondarySwing);
            secondaryTrailObject.SetActive(false);
        }
        if (swingParticlePrefab != null)
        {
            swingParticleObject = Instantiate(swingParticlePrefab, start, frame);
            swingParticleObject.name = "Builder_EmergencyDemolition_SwingParticles";
            ApplyBuilderMaterialOverrides(swingParticleObject);
            PruneAndTintVfx(swingParticleObject, VfxRole.Debris);
        }

        if (preparationDuration > 0f) yield return new WaitForSeconds(preparationDuration);
        if (trailObject != null) trailObject.SetActive(true);

        float animationDuration = hammerAnimationClip != null ? hammerAnimationClip.length : 0.8f;
        float impactTime = animationDuration * hammerImpactNormalizedTime;
        float elapsed = 0f;
        bool impactTriggered = false;
        while (elapsed < animationDuration)
        {
            if (origin == null || directionSource == null)
            {
                Cancel();
                yield break;
            }
            elapsed += Time.deltaTime;
            float sampledTime = Mathf.Min(elapsed, animationDuration);
            if (hammerObject != null && hammerAnimationClip != null)
            {
                BuilderMagicHammerVisual visual = hammerObject.GetComponent<BuilderMagicHammerVisual>();
                if (visual != null && visual.AnimationTarget != null)
                    hammerAnimationClip.SampleAnimation(visual.AnimationTarget, sampledTime);
            }
            if (!impactTriggered && elapsed >= impactTime)
            {
                impactTriggered = true;
                TriggerImpact(ResolveHammerImpactPosition());
            }
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float eased = t * t * (3f - 2f * t);
            Vector3 control = (start + swingVfxEnd) * 0.5f +
                              Vector3.up * swingArcHeight -
                              frame * Vector3.right * 0.2f;
            Vector3 a = Vector3.Lerp(start, control, eased);
            Vector3 b = Vector3.Lerp(control, swingVfxEnd, eased);
            Vector3 current = Vector3.Lerp(a, b, eased);
            Vector3 vfxTangent = 2f * (1f - eased) * (control - start) +
                                 2f * eased * (swingVfxEnd - control);
            Quaternion visualRotation = GetVfxPathRotation(vfxTangent, frame * Vector3.right);
            if (trailObject != null)
                trailObject.transform.SetPositionAndRotation(
                    current + visualRotation * trailOffset,
                    visualRotation * Quaternion.Euler(trailRotationOffset));
            if (secondaryTrailObject != null && elapsed >= secondaryTrailDelay)
            {
                if (!secondaryTrailObject.activeSelf) secondaryTrailObject.SetActive(true);
                secondaryTrailObject.transform.SetPositionAndRotation(
                    current + visualRotation * trailOffset,
                    visualRotation * Quaternion.Euler(trailRotationOffset + secondaryTrailRotation));
            }
            if (swingParticleObject != null)
                swingParticleObject.transform.SetPositionAndRotation(
                    current + visualRotation * swingParticleOffset, visualRotation);
            yield return null;
        }
        if (hammerObject != null && hammerAnimationClip != null)
        {
            BuilderMagicHammerVisual visual = hammerObject.GetComponent<BuilderMagicHammerVisual>();
            if (visual != null && visual.AnimationTarget != null)
                hammerAnimationClip.SampleAnimation(visual.AnimationTarget, animationDuration);
        }
        if (!impactTriggered) TriggerImpact(ResolveHammerImpactPosition());
        if (hammerAnimationAnchor != null) Destroy(hammerAnimationAnchor.gameObject);
        hammerAnimationAnchor = null;
        hammerObject = null;
        Finish();
    }

    // Keeps the existing slash/particle path alignment independent from Hammer rotation.
    private static Quaternion GetVfxPathRotation(Vector3 tangent, Vector3 stableSide)
    {
        if (tangent.sqrMagnitude < 0.000001f)
            return Quaternion.identity;

        Vector3 travel = tangent.normalized;
        Vector3 upReference = Vector3.Cross(stableSide, travel).normalized;
        if (upReference.sqrMagnitude < 0.000001f)
            upReference = Vector3.up;

        Quaternion pathRotation = Quaternion.LookRotation(travel, upReference);
        Quaternion formerMeshOffset = Quaternion.Euler(-90f, 0f, 0f);
        Quaternion formerHeadAxisCorrection = Quaternion.FromToRotation(
            formerMeshOffset * Vector3.back,
            Vector3.forward);
        return pathRotation * formerHeadAxisCorrection * formerMeshOffset;
    }

    private void ApplyBuilderMaterialOverrides(GameObject effect, bool replaceMissing = false)
    {
        if (effect == null || vfxMaterialOverrides == null || vfxMaterialOverrides.Length == 0)
            return;

        Renderer[] renderers = effect.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                string sourceName = materials[i] != null ? materials[i].name : "<Missing>";
                if (materials[i] == null && !replaceMissing) continue;
                for (int mappingIndex = 0; mappingIndex < vfxMaterialOverrides.Length; mappingIndex++)
                {
                    BuilderVfxMaterialOverride mapping = vfxMaterialOverrides[mappingIndex];
                    if (mapping != null && mapping.ReplacementMaterial != null &&
                        string.Equals(sourceName, mapping.SourceMaterialName,
                            StringComparison.Ordinal))
                    {
                        materials[i] = mapping.ReplacementMaterial;
                        changed = true;
                        break;
                    }
                }
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private void PruneAndTintVfx(GameObject effect, VfxRole role)
    {
        if (effect == null) return;

        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            string objectName = particle.gameObject.name.Trim();
            bool keep;
            switch (role)
            {
                case VfxRole.MainSwing:
                    keep = particle.transform == effect.transform;
                    break;
                case VfxRole.SecondarySwing:
                    keep = particle.transform == effect.transform;
                    break;
                case VfxRole.Dust:
                    keep = particle.transform == effect.transform;
                    break;
                case VfxRole.Spawn:
                    keep = particle.transform == effect.transform;
                    break;
                default:
                    keep = particle.transform == effect.transform;
                    break;
            }

            if (!keep)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null) particleRenderer.enabled = false;
                continue;
            }

            ParticleSystem.MainModule main = particle.main;
            ParticleSystemRenderer keptRenderer = particle.GetComponent<ParticleSystemRenderer>();
            if (keptRenderer != null) keptRenderer.enabled = true;
            switch (role)
            {
                case VfxRole.MainSwing:
                    main.startColor = new Color(1f, 0.55f, 0.08f, 0.9f);
                    break;
                case VfxRole.SecondarySwing:
                    main.startColor = new Color(1f, 0.72f, 0.25f, 0.55f);
                    break;
                case VfxRole.Debris:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6f);
                    main.startSize3D = true;
                    main.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                    main.startSizeY = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                    main.startSizeZ = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
                    main.startRotation3D = true;
                    main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startColor = Color.white;
                    if (keptRenderer != null)
                    {
                        keptRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                        if (debrisMeshes != null && debrisMeshes.Length > 0)
                            keptRenderer.SetMeshes(debrisMeshes);
                        keptRenderer.meshDistribution = ParticleSystemMeshDistribution.NonUniformRandom;
                        keptRenderer.velocityScale = 0f;
                        keptRenderer.lengthScale = 0f;
                    }
                    ParticleSystem.TrailModule trails = particle.trails;
                    trails.enabled = false;
                    ParticleSystem.EmissionModule emission = particle.emission;
                    emission.SetBursts(new[]
                    {
                        new ParticleSystem.Burst(0f, (short)10, (short)14)
                    });
                    break;
                case VfxRole.Dust:
                    main.startLifetime = 0.7f;
                    main.startColor = new Color(0.5f, 0.42f, 0.34f, 0.3f);
                    break;
            }
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(false);
        }
    }

    private void ApplyAnimatedHammerMaterial(Renderer[] renderers)
    {
        if (renderers == null || animatedHammerMaterial == null) return;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = animatedHammerMaterial;
            renderer.sharedMaterials = materials;
        }
    }

    private void FitAnimatedHammerToVisualSize(Transform anchor, Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float factor = longest > 0.0001f ? targetVisualSize / longest : 1f;
        anchor.localScale *= factor;
#if UNITY_EDITOR
        Debug.Log($"[BuilderSkill] Animated Hammer bounds {bounds.size}, anchor scale factor {factor:0.###}, target {targetVisualSize:0.###}m");
#endif
    }

    private Vector3 GetFlatForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(directionSource.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(origin.root.forward, Vector3.up);
        return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
    }

    private Vector3 FindGroundPoint(Vector3 desired)
    {
        Vector3 rayOrigin = desired + Vector3.up * rayStartHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        desired.y = origin.root.position.y;
        return desired;
    }

    private Vector3 ResolveHammerImpactPosition()
    {
        Vector3 contactPosition = hammerImpactPoint != null
            ? hammerImpactPoint.position
            : hammerAnimationAnchor.position;
        Vector3 rayOrigin = contactPosition + Vector3.up * 0.3f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                2f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return contactPosition;
    }

    private void TriggerImpact(Vector3 point)
    {
        Vector3 visualPoint = ResolveVisualImpactPosition(point);
        SpawnContactFlash(visualPoint);
        BuilderDemolitionImpactVisual impactVisual = null;
        if (demolitionImpactPrefab != null)
        {
            GameObject visualObject = Instantiate(demolitionImpactPrefab, visualPoint, Quaternion.identity);
            impactVisual = visualObject.GetComponent<BuilderDemolitionImpactVisual>();
            if (impactVisual != null) impactVisual.Play();
        }
        Vector3 debrisPoint = impactVisual != null ? impactVisual.DebrisOrigin.position : visualPoint;
        Vector3 dustPoint = impactVisual != null ? impactVisual.DustOrigin.position : visualPoint;
        SpawnRockDebris(debrisPoint);
        SpawnTemporaryVfx(dustVfx, dustPoint, dustScale, 0.75f, true, VfxRole.Dust);
        PlayImpactSounds(point);

        int count = Physics.OverlapSphereNonAlloc(point, impactRadius, hitBuffer,
            monsterMask, QueryTriggerInteraction.Collide);
        int shotId = nextSkillShotId++;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = hitBuffer[i];
            hitBuffer[i] = null;
            EnemyHealth enemy = candidate != null ? candidate.GetComponentInParent<EnemyHealth>() : null;
            if (enemy == null || enemy.IsDead || !hitEnemies.Add(enemy)) continue;

            Vector3 horizontal = enemy.transform.position - point;
            horizontal.y = 0f;
            bool direct = horizontal.magnitude <= directRadius;
            DamageInfo info = new DamageInfo(direct ? directDamage : shockwaveDamage,
                "BUILDER_EMERGENCY_DEMOLITION", PlayerRole.Architect, shotId,
                candidate.ClosestPoint(point), true);
            enemy.TakeDamage(info);

            EnemyCoreMover mover = enemy != null ? enemy.GetComponent<EnemyCoreMover>() : null;
            if (enemy != null && !enemy.IsDead && mover != null && mover.isActiveAndEnabled)
            {
                Vector3 pushDirection = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : GetFlatForward();
                mover.ApplyKnockback(pushDirection, knockbackDistance * (direct ? 1f : 0.65f), knockbackDuration);
                if (direct && stunDuration > 0f) mover.ApplyStun(stunDuration);
            }
        }
    }

    private Vector3 ResolveVisualImpactPosition(Vector3 impactPosition)
    {
        Vector3 visualPosition = impactPosition + Vector3.up * 0.02f;
        MeshRenderer[] roadRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Exclude);

        bool foundRoadSurface = false;
        float highestSurfaceY = float.NegativeInfinity;
        for (int i = 0; i < roadRenderers.Length; i++)
        {
            MeshRenderer roadRenderer = roadRenderers[i];
            if (!IsActiveRoadRenderer(roadRenderer)) continue;

            Bounds bounds = roadRenderer.bounds;
            bool insideXZ = impactPosition.x >= bounds.min.x && impactPosition.x <= bounds.max.x &&
                            impactPosition.z >= bounds.min.z && impactPosition.z <= bounds.max.z;
            if (!insideXZ)
                continue;

            float surfaceY = bounds.max.y;
            if (surfaceY >= impactPosition.y - 0.05f &&
                surfaceY <= impactPosition.y + 2f &&
                surfaceY > highestSurfaceY)
            {
                highestSurfaceY = surfaceY;
                foundRoadSurface = true;
            }
        }

        if (foundRoadSurface)
            visualPosition.y = highestSurfaceY + 0.02f;

        return visualPosition;
    }

    private static bool IsActiveRoadRenderer(MeshRenderer roadRenderer)
    {
        if (roadRenderer == null || !roadRenderer.enabled ||
            !roadRenderer.gameObject.activeInHierarchy)
            return false;

        MeshFilter meshFilter = roadRenderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return false;

        Transform current = roadRenderer.transform;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.StartsWith("CS_Road_", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("DreamRoad", StringComparison.OrdinalIgnoreCase) ||
                IsNumberedRoadName(objectName))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static bool IsNumberedRoadName(string objectName)
    {
        return objectName != null && objectName.Length == 6 &&
               objectName.StartsWith("Road_", StringComparison.OrdinalIgnoreCase) &&
               objectName[5] >= '0' && objectName[5] <= '4';
    }

    private void SpawnContactFlash(Vector3 point)
    {
        GameObject flashObject = new GameObject("Builder_EmergencyDemolition_ContactFlash");
        flashObject.transform.position = point + Vector3.up * 0.08f;
        Light flash = flashObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.9f, 0.72f);
        flash.range = 3f;
        flash.intensity = 7f;
        flash.shadows = LightShadows.None;
        StartCoroutine(FadeContactFlash(flash, 0.09f));
    }

    private void SpawnRockDebris(Vector3 point)
    {
        if (debrisMeshes == null || debrisMeshes.Length == 0 || debrisMaterial == null) return;
        int count = UnityEngine.Random.Range(12, 17);
        for (int i = 0; i < count; i++)
        {
            Mesh mesh = debrisMeshes[UnityEngine.Random.Range(0, debrisMeshes.Length)];
            if (mesh == null) continue;

            GameObject shard = new GameObject("Builder_Demolition_RockShard");
            shard.transform.position = point + Vector3.up * 0.04f;
            shard.transform.rotation = UnityEngine.Random.rotation;
            float longestBound = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
            float targetSize = UnityEngine.Random.Range(0.12f, 0.23f);
            float normalizedScale = longestBound > 0.0001f ? targetSize / longestBound : targetSize;
            shard.transform.localScale = normalizedScale * new Vector3(
                UnityEngine.Random.Range(0.75f, 1.1f),
                UnityEngine.Random.Range(0.75f, 1.1f),
                UnityEngine.Random.Range(0.75f, 1.1f));

            MeshFilter filter = shard.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = shard.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = debrisMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 direction = new Vector3(randomCircle.x, UnityEngine.Random.Range(0.75f, 1.15f), randomCircle.y).normalized;
            float speed = UnityEngine.Random.Range(3.5f, 5.5f);
            float lifetime = UnityEngine.Random.Range(0.7f, 1f);
            BuilderRockShardMotion motion = shard.AddComponent<BuilderRockShardMotion>();
            motion.Initialize(direction * speed, lifetime);
        }
    }

    private static IEnumerator FadeContactFlash(Light flash, float duration)
    {
        float initialIntensity = flash.intensity;
        float elapsed = 0f;
        while (flash != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            flash.intensity = initialIntensity * (1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        if (flash != null) Destroy(flash.gameObject);
    }


    private void SpawnTemporaryVfx(
        GameObject prefab, Vector3 position, Vector3 scale, float lifetime,
        bool replaceMissingMaterials = false, VfxRole role = VfxRole.Spawn)
    {
        if (prefab == null) return;
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        ApplyBuilderMaterialOverrides(instance, replaceMissingMaterials);
        PruneAndTintVfx(instance, role);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scale);
        Destroy(instance, lifetime);
    }

    private void PlayImpactSounds(Vector3 point)
    {
        PlaySpatialOneShot(hammerImpactSound, hammerImpactVolume, point, "Hammer");
        if (boomImpactSound != null)
            StartCoroutine(PlayDelayedSpatialOneShot(
                boomImpactSound, boomImpactVolume, point, "Boom", boomImpactDelay));
    }

    private IEnumerator PlayDelayedSpatialOneShot(
        AudioClip clip, float volume, Vector3 point, string layerName, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        PlaySpatialOneShot(clip, volume, point, layerName);
    }

    private void PlaySpatialOneShot(AudioClip clip, float volume, Vector3 point, string layerName)
    {
        if (clip == null) return;
        GameObject audioObject = new GameObject("Builder_EmergencyDemolition_ImpactAudio");
        audioObject.name += "_" + layerName;
        audioObject.transform.position = point;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip; source.volume = volume; source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = audioMinDistance; source.maxDistance = audioMaxDistance;
        source.Play();
        Destroy(audioObject, clip.length + 0.1f);
    }

    public void Cancel()
    {
        if (isFinishing) return;
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        Finish();
    }

    private void Finish()
    {
        if (isFinishing) return;
        isFinishing = true;
        if (hammerAnimationAnchor != null) Destroy(hammerAnimationAnchor.gameObject);
        else if (hammerObject != null) Destroy(hammerObject);
        if (trailObject != null) Destroy(trailObject);
        if (secondaryTrailObject != null) Destroy(secondaryTrailObject);
        if (swingParticleObject != null) Destroy(swingParticleObject);
        finished?.Invoke(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (hammerAnimationAnchor != null) Destroy(hammerAnimationAnchor.gameObject);
        else if (hammerObject != null) Destroy(hammerObject);
        if (trailObject != null) Destroy(trailObject);
        if (secondaryTrailObject != null) Destroy(secondaryTrailObject);
        if (swingParticleObject != null) Destroy(swingParticleObject);
        if (!isFinishing) finished?.Invoke(this);
    }
}

internal sealed class BuilderRockShardMotion : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 rotationAxis;
    private float angularSpeed;

    public void Initialize(Vector3 initialVelocity, float lifetime)
    {
        velocity = initialVelocity;
        rotationAxis = UnityEngine.Random.onUnitSphere;
        angularSpeed = UnityEngine.Random.Range(240f, 520f);
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        float delta = Time.deltaTime;
        velocity += Physics.gravity * (1.35f * delta);
        transform.position += velocity * delta;
        transform.rotation = Quaternion.AngleAxis(angularSpeed * delta, rotationAxis) * transform.rotation;
    }
}
