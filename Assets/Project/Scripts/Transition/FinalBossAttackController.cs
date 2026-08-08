using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 선물상자 최종 보스의 전투 연출을 담당합니다.
///
/// - 평소에는 제자리에서 불규칙하게 날뛰는 느낌의 바운스 모션
/// - HP 2/3, 1/3 구간에서 Director 요청을 받아 코어 쪽으로 돌진/회전 이동
/// - 코어 근처까지 도달하면 내려찍기/회전 직접 공격
/// - 보라/적색 오염 팔레트와 검은 기운 ParticleSystem
/// - 소환 시 오염 기운 펄스
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalBossAttackController : MonoBehaviour
{
    [Header("선물상자 모델 방향")]
    [Tooltip(
        "GiftBox FBX의 축 보정값입니다. 보스가 옆으로 누우면 -90, " +
        "이미 똑바로 서 있으면 0으로 변경하세요.")]
    [SerializeField]
    private float modelPitchOffset = -90f;

    [Tooltip("모델 정면이 코어 반대쪽을 보면 180으로 변경하세요.")]
    [SerializeField]
    private float modelYawOffset;

    [Header("페이즈 이동")]
    [Tooltip("코어 근처에서 직접 공격을 시작하는 거리")]
    [SerializeField, Min(0.5f)]
    private float attackRange = 5.5f;

    [Tooltip("HP 1/3이 깎일 때마다 현재 코어 거리의 이 비율만큼 전진합니다.")]
    [SerializeField, Range(0.1f, 0.8f)]
    private float phaseAdvanceFraction = 0.35f;

    [Tooltip("페이즈 이동 후에도 코어와 최소한 유지할 거리")]
    [SerializeField, Min(0.5f)]
    private float phaseMinimumCoreDistance = 7.5f;

    [SerializeField, Min(0.1f)]
    private float phaseAdvanceDuration = 2.15f;

    [SerializeField, Min(0f)]
    private float phaseHopHeight = 0.9f;

    [SerializeField, Min(0f)]
    private float turnSpeed = 260f;

    [Header("날뛰는 대기 모션")]
    [SerializeField, Min(0f)]
    private float rageHopHeight = 0.10f;

    [SerializeField, Min(0f)]
    private float rageScaleAmount = 0.035f;

    [SerializeField, Min(0.1f)]
    private float rageFrequency = 4.2f;

    [Header("내려찍기 공격")]
    [SerializeField, Min(0f)]
    private float slamWindupDuration = 0.55f;

    [SerializeField, Min(0.1f)]
    private float slamDuration = 0.7f;

    [SerializeField, Min(0f)]
    private float slamJumpHeight = 1.4f;

    [Header("회전 공격")]
    [SerializeField, Min(0f)]
    private float spinWindupDuration = 0.4f;

    [SerializeField, Min(0.1f)]
    private float spinDuration = 0.9f;

    [SerializeField, Min(0.25f)]
    private float spinTurns = 2f;

    [SerializeField, Min(0f)]
    private float spinLungeDistance = 0.8f;

    [Header("오염된 보스 색상")]
    [SerializeField]
    private Color corruptedBodyColor =
        new Color(0.20f, 0.035f, 0.28f, 1f);

    [SerializeField]
    private Color corruptedRibbonColor =
        new Color(0.62f, 0.035f, 0.11f, 1f);

    [SerializeField]
    private Color corruptedAccentColor =
        new Color(0.34f, 0.02f, 0.42f, 1f);

    [Header("검은 오염 기운")]
    [SerializeField, Min(0f)]
    private float auraHeightOffset = 0.9f;

    [SerializeField, Min(0f)]
    private float auraRadius = 1.05f;

    [SerializeField, Min(1f)]
    private float auraEmissionRate = 22f;

    [Header("피격 표시")]
    [SerializeField]
    private Color hitFlashColor =
        new Color(1f, 0.25f, 0.45f, 1f);

    [SerializeField, Min(0.02f)]
    private float hitFlashDuration = 0.12f;

    [Header("Director에서 전달되는 전투 수치")]
    [SerializeField]
    private CoreState targetCore;

    [SerializeField, Min(0f)]
    private float coreDamage = 20f;

    [SerializeField, Min(0.1f)]
    private float attackInterval = 2.5f;

    [SerializeField, Min(0f)]
    private float firstAttackDelay = 4f;

    private readonly List<MaterialColorState> materialColorStates =
        new List<MaterialColorState>();

    private EnemyHealth health;
    private Rigidbody body;
    private Vector3 baseScale;
    private float groundY;
    private float nextAttackTime;
    private float rageTime;
    private int nextAttackIndex;
    private bool configured;
    private bool attacking;
    private bool phaseMoving;
    private bool isDead;
    private bool orientationApplied;

    private Coroutine attackRoutine;
    private Coroutine phaseRoutine;
    private Coroutine flashRoutine;

    private GameObject auraObject;
    private ParticleSystem auraParticles;
    private Material auraMaterial;

    public bool IsPhaseMoving => phaseMoving;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private struct MaterialColorState
    {
        public Material Material;
        public int PropertyId;
        public Color OriginalColor;
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureRigidbody();
        ApplyInitialModelOrientation();

        baseScale = transform.localScale;
        groundY = transform.position.y;

        ApplyCorruptedPalette();
        CacheMaterialColors();
        CreateCorruptionAura();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToHealth();

        isDead = health != null && health.IsDead;

        if (auraObject != null)
        {
            auraObject.SetActive(true);
        }

        if (configured)
        {
            nextAttackTime =
                Time.time + Mathf.Max(0f, firstAttackDelay);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromHealth();
        StopOwnedRoutines();
        RestoreVisualState(false);

        if (auraObject != null)
        {
            auraObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (auraObject != null)
        {
            Destroy(auraObject);
        }

        if (auraMaterial != null)
        {
            Destroy(auraMaterial);
        }
    }

    private void Update()
    {
        UpdateAuraPosition();

        if (!configured ||
            isDead ||
            targetCore == null ||
            targetCore.IsDestroyed ||
            (health != null && health.IsDead))
        {
            return;
        }

        if (phaseMoving || attacking)
        {
            return;
        }

        Vector3 corePosition = targetCore.transform.position;
        Vector3 toCore = corePosition - transform.position;
        toCore.y = 0f;

        FaceDirection(toCore);
        UpdateRageMotion();

        // 기존처럼 전투 시작과 동시에 코어까지 자동 이동하지 않습니다.
        // HP가 1/3씩 감소할 때 Director가 AdvanceTowardCore를 호출합니다.
        if (toCore.magnitude > attackRange)
        {
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            StartNextAttack();
        }
    }

    /// <summary>
    /// FinalBossDirector가 보스 등장 연출을 마친 뒤 호출합니다.
    /// </summary>
    public void Configure(
        CoreState core,
        float damage,
        float interval,
        float initialDelay)
    {
        targetCore = core;
        coreDamage = Mathf.Max(0f, damage);
        attackInterval = Mathf.Max(0.1f, interval);
        firstAttackDelay = Mathf.Max(0f, initialDelay);

        CacheReferences();
        ConfigureRigidbody();
        SubscribeToHealth();

        groundY = transform.position.y;
        baseScale = transform.localScale;
        rageTime = 0f;
        nextAttackIndex = 0;
        isDead = false;
        configured = true;
        enabled = true;

        nextAttackTime = Time.time + firstAttackDelay;

        if (targetCore == null)
        {
            Debug.LogWarning(
                "[FinalBoss] Target Core가 연결되지 않아 " +
                "선물상자 보스가 코어를 바라볼 수 없습니다.",
                this);
        }
    }

    /// <summary>
    /// 보스 HP 페이즈가 바뀔 때 호출됩니다.
    /// 1페이즈는 뛰어가고, 2페이즈는 회전하며 더 가까이 접근합니다.
    /// </summary>
    public void AdvanceTowardCore(int phaseIndex)
    {
        if (!configured ||
            isDead ||
            targetCore == null ||
            targetCore.IsDestroyed ||
            phaseRoutine != null)
        {
            return;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            attacking = false;
            transform.localScale = baseScale;
        }

        phaseRoutine = StartCoroutine(
            PhaseAdvanceRoutine(Mathf.Max(1, phaseIndex)));
    }

    /// <summary>
    /// 하수인 소환 순간 검은 기운을 강하게 한 번 뿜습니다.
    /// </summary>
    public void PlaySummonPulse()
    {
        if (auraParticles == null)
        {
            return;
        }

        auraParticles.Emit(26);
    }

    private IEnumerator PhaseAdvanceRoutine(int phaseIndex)
    {
        phaseMoving = true;
        transform.localScale = baseScale;

        Vector3 startPosition = transform.position;
        startPosition.y = groundY;

        Vector3 corePosition = targetCore.transform.position;
        Vector3 toCore = corePosition - startPosition;
        toCore.y = 0f;

        float currentDistance = toCore.magnitude;
        Vector3 direction =
            currentDistance > 0.001f
                ? toCore / currentDistance
                : Vector3.forward;

        float desiredDistance = Mathf.Max(
            phaseMinimumCoreDistance,
            currentDistance * (1f - phaseAdvanceFraction));

        float moveDistance = Mathf.Max(
            0f,
            currentDistance - desiredDistance);

        Vector3 targetPosition = startPosition + direction * moveDistance;
        targetPosition.y = groundY;

        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, phaseAdvanceDuration);

        PlaySummonPulse();

        while (elapsed < duration && CanContinueAttack())
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            Vector3 next = Vector3.Lerp(startPosition, targetPosition, smoothT);

            float hopCycles = phaseIndex == 1 ? 3f : 5f;
            next.y = groundY +
                     Mathf.Abs(Mathf.Sin(t * Mathf.PI * hopCycles)) *
                     phaseHopHeight;

            SetPosition(next);

            if (phaseIndex >= 2)
            {
                float spinAngle = t * 720f;
                transform.rotation =
                    Quaternion.AngleAxis(spinAngle, Vector3.up) *
                    startRotation;
            }
            else
            {
                FaceDirection(direction);
            }

            yield return null;
        }

        SetPosition(targetPosition);
        groundY = targetPosition.y;
        transform.localScale = baseScale;

        Vector3 finalToCore = targetCore.transform.position - transform.position;
        finalToCore.y = 0f;
        FaceDirection(finalToCore);

        phaseMoving = false;
        phaseRoutine = null;
        nextAttackTime = Time.time + attackInterval;
    }

    private void UpdateRageMotion()
    {
        rageTime += Time.deltaTime * rageFrequency;

        float pulse = 1f + Mathf.Sin(rageTime * 1.7f) * rageScaleAmount;
        float squash = 1f - Mathf.Sin(rageTime * 2.3f) * rageScaleAmount * 0.65f;

        transform.localScale = Vector3.Scale(
            baseScale,
            new Vector3(pulse, squash, pulse));

        float hop =
            Mathf.Abs(Mathf.Sin(rageTime * 1.35f)) *
            rageHopHeight;

        SetPosition(new Vector3(
            transform.position.x,
            groundY + hop,
            transform.position.z));
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float targetYaw =
            Mathf.Atan2(direction.x, direction.z) *
            Mathf.Rad2Deg +
            modelYawOffset;

        Quaternion targetRotation =
            Quaternion.Euler(
                modelPitchOffset,
                targetYaw,
                0f);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
    }

    private void StartNextAttack()
    {
        if (attackRoutine != null)
        {
            return;
        }

        attacking = true;
        transform.localScale = baseScale;
        SetPosition(new Vector3(
            transform.position.x,
            groundY,
            transform.position.z));

        if (nextAttackIndex % 2 == 0)
        {
            attackRoutine = StartCoroutine(SlamAttackRoutine());
        }
        else
        {
            attackRoutine = StartCoroutine(SpinAttackRoutine());
        }

        nextAttackIndex++;
    }

    private IEnumerator SlamAttackRoutine()
    {
        Vector3 startPosition =
            new Vector3(
                transform.position.x,
                groundY,
                transform.position.z);

        Vector3 squashedScale =
            Vector3.Scale(
                baseScale,
                new Vector3(1.12f, 0.72f, 1.12f));

        yield return AnimateScale(
            baseScale,
            squashedScale,
            slamWindupDuration);

        float elapsed = 0f;

        while (elapsed < slamDuration && CanContinueAttack())
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slamDuration);
            float jumpOffset = Mathf.Sin(t * Mathf.PI) * slamJumpHeight;

            SetPosition(startPosition + Vector3.up * jumpOffset);

            transform.localScale =
                Vector3.Lerp(
                    squashedScale,
                    baseScale,
                    Mathf.Clamp01(t * 2f));

            yield return null;
        }

        SetPosition(startPosition);
        transform.localScale = baseScale;

        DamageCore(1f);
        FinishAttack();
    }

    private IEnumerator SpinAttackRoutine()
    {
        Vector3 startPosition =
            new Vector3(
                transform.position.x,
                groundY,
                transform.position.z);

        Vector3 pulseScale = baseScale * 1.1f;

        yield return AnimateScale(
            baseScale,
            pulseScale,
            spinWindupDuration);

        transform.localScale = baseScale;

        Quaternion startRotation = transform.rotation;

        Vector3 lungeDirection =
            targetCore != null
                ? targetCore.transform.position - startPosition
                : transform.forward;

        lungeDirection.y = 0f;

        if (lungeDirection.sqrMagnitude <= 0.0001f)
        {
            lungeDirection = Vector3.forward;
        }

        lungeDirection.Normalize();

        float elapsed = 0f;

        while (elapsed < spinDuration && CanContinueAttack())
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spinDuration);
            float spinAngle = t * spinTurns * 360f;

            transform.rotation =
                Quaternion.AngleAxis(spinAngle, Vector3.up) *
                startRotation;

            float lunge =
                Mathf.Sin(t * Mathf.PI) *
                spinLungeDistance;

            SetPosition(startPosition + lungeDirection * lunge);

            yield return null;
        }

        SetPosition(startPosition);
        transform.rotation = startRotation;
        transform.localScale = baseScale;

        DamageCore(0.8f);
        FinishAttack();
    }

    private IEnumerator AnimateScale(
        Vector3 from,
        Vector3 to,
        float duration)
    {
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration && CanContinueAttack())
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    private bool CanContinueAttack()
    {
        return !isDead &&
               configured &&
               targetCore != null &&
               !targetCore.IsDestroyed &&
               (health == null || !health.IsDead);
    }

    private void DamageCore(float damageMultiplier)
    {
        if (!CanContinueAttack())
        {
            return;
        }

        float appliedDamage =
            coreDamage * Mathf.Max(0f, damageMultiplier);

        targetCore.TakeDamage(appliedDamage);

        Debug.Log(
            "[FinalBoss] 선물상자 보스가 코어에 " +
            appliedDamage.ToString("0.#") +
            " 피해를 가했습니다.",
            this);
    }

    private void FinishAttack()
    {
        attackRoutine = null;
        attacking = false;
        nextAttackTime = Time.time + attackInterval;
    }

    private void CacheReferences()
    {
        health ??= GetComponent<EnemyHealth>();
        body ??= GetComponent<Rigidbody>();
    }

    private void ConfigureRigidbody()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void ApplyInitialModelOrientation()
    {
        if (orientationApplied)
        {
            return;
        }

        orientationApplied = true;

        transform.rotation =
            Quaternion.Euler(
                modelPitchOffset,
                transform.eulerAngles.y + modelYawOffset,
                0f);
    }

    private void SetPosition(Vector3 position)
    {
        if (body != null && body.isKinematic)
        {
            body.position = position;
        }
        else
        {
            transform.position = position;
        }
    }

    private void SubscribeToHealth()
    {
        if (health == null)
        {
            return;
        }

        health.HitRegistered -= HandleHit;
        health.HitRegistered += HandleHit;

        health.Died -= HandleDied;
        health.Died += HandleDied;
    }

    private void UnsubscribeFromHealth()
    {
        if (health == null)
        {
            return;
        }

        health.HitRegistered -= HandleHit;
        health.Died -= HandleDied;
    }

    private void HandleHit(EnemyHealth _, DamageInfo __)
    {
        if (isDead)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private void HandleDied(EnemyHealth _, DamageInfo __)
    {
        isDead = true;
        configured = false;
        StopOwnedRoutines();
        RestoreVisualState(true);

        if (auraParticles != null)
        {
            ParticleSystem.EmissionModule emission = auraParticles.emission;
            emission.enabled = false;
            auraParticles.Emit(40);
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        SetMaterialColor(hitFlashColor);

        if (hitFlashDuration > 0f)
        {
            yield return new WaitForSeconds(hitFlashDuration);
        }

        RestoreMaterialColors();
        flashRoutine = null;
    }

    private void ApplyCorruptedPalette()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer == null ||
                modelRenderer is ParticleSystemRenderer ||
                modelRenderer is LineRenderer)
            {
                continue;
            }

            string rendererName = modelRenderer.gameObject.name.ToLowerInvariant();
            Color targetColor;

            if (rendererName.Contains("bow") ||
                rendererName.Contains("ribbon"))
            {
                targetColor = corruptedRibbonColor;
            }
            else if (rendererName.Contains("gift") ||
                     rendererName.Contains("box") ||
                     rendererName.Contains("cube") ||
                     rendererName.Contains("body") ||
                     rendererName.Contains("lid"))
            {
                targetColor = corruptedBodyColor;
            }
            else
            {
                targetColor = corruptedAccentColor;
            }

            Material[] materials = modelRenderer.materials;

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                Color applied = targetColor;

                if (material.HasProperty(BaseColorId))
                {
                    Color old = material.GetColor(BaseColorId);
                    applied.a = old.a;
                    material.SetColor(BaseColorId, applied);
                }
                else if (material.HasProperty(ColorId))
                {
                    Color old = material.GetColor(ColorId);
                    applied.a = old.a;
                    material.SetColor(ColorId, applied);
                }

                if (material.HasProperty(EmissionColorId))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(
                        EmissionColorId,
                        targetColor * 0.35f);
                }
            }
        }
    }

    private void CacheMaterialColors()
    {
        materialColorStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer == null ||
                modelRenderer is ParticleSystemRenderer ||
                modelRenderer is LineRenderer)
            {
                continue;
            }

            Material[] materials = modelRenderer.materials;

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                int propertyId;

                if (material.HasProperty(BaseColorId))
                {
                    propertyId = BaseColorId;
                }
                else if (material.HasProperty(ColorId))
                {
                    propertyId = ColorId;
                }
                else
                {
                    continue;
                }

                materialColorStates.Add(
                    new MaterialColorState
                    {
                        Material = material,
                        PropertyId = propertyId,
                        OriginalColor = material.GetColor(propertyId)
                    });
            }
        }
    }

    private void SetMaterialColor(Color color)
    {
        foreach (MaterialColorState state in materialColorStates)
        {
            if (state.Material == null)
            {
                continue;
            }

            Color tintedColor = color;
            tintedColor.a = state.OriginalColor.a;

            state.Material.SetColor(
                state.PropertyId,
                tintedColor);
        }
    }

    private void RestoreMaterialColors()
    {
        foreach (MaterialColorState state in materialColorStates)
        {
            if (state.Material == null)
            {
                continue;
            }

            state.Material.SetColor(
                state.PropertyId,
                state.OriginalColor);
        }
    }

    private void CreateCorruptionAura()
    {
        if (auraObject != null)
        {
            return;
        }

        auraObject = new GameObject("FinalBoss_CorruptionAura");
        auraObject.transform.position =
            transform.position + Vector3.up * auraHeightOffset;

        auraParticles = auraObject.AddComponent<ParticleSystem>();
        auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = auraParticles.main;
        main.duration = 5f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.58f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.01f, 0.005f, 0.015f, 0.58f),
            new Color(0.18f, 0.01f, 0.24f, 0.42f));

        ParticleSystem.EmissionModule emission = auraParticles.emission;
        emission.rateOverTime = auraEmissionRate;

        ParticleSystem.ShapeModule shape = auraParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = auraRadius;
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            auraParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(0.65f, 1.45f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);

        ParticleSystem.NoiseModule noise = auraParticles.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.35f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            auraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.08f, 0.005f, 0.10f), 0f),
                new GradientColorKey(new Color(0.01f, 0.005f, 0.015f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.18f),
                new GradientAlphaKey(0.35f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer particleRenderer =
            auraObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 20;

        auraMaterial = CreateAuraMaterial();
        if (auraMaterial != null)
        {
            particleRenderer.material = auraMaterial;
        }

        auraParticles.Play();
    }

    private Material CreateAuraMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        shader ??= Shader.Find("Particles/Standard Unlit");
        shader ??= Shader.Find("Unlit/Color");

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = "FinalBoss_CorruptionAura_Runtime"
        };

        Color white = Color.white;

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, white);
        }
        else if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, white);
        }

        // URP particle shader가 투명 모드 속성을 지원할 경우 검은 연기가
        // 사각형으로 가려지지 않도록 알파 블렌딩으로 전환합니다.
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;

        return material;
    }

    private void UpdateAuraPosition()
    {
        if (auraObject == null)
        {
            return;
        }

        auraObject.transform.position =
            transform.position + Vector3.up * auraHeightOffset;
    }

    private void StopOwnedRoutines()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (phaseRoutine != null)
        {
            StopCoroutine(phaseRoutine);
            phaseRoutine = null;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        attacking = false;
        phaseMoving = false;
    }

    private void RestoreVisualState(bool restorePosition)
    {
        RestoreMaterialColors();

        if (baseScale != Vector3.zero)
        {
            transform.localScale = baseScale;
        }

        if (restorePosition)
        {
            SetPosition(new Vector3(
                transform.position.x,
                groundY,
                transform.position.z));
        }
    }

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.5f, attackRange);
        phaseAdvanceFraction = Mathf.Clamp(phaseAdvanceFraction, 0.1f, 0.8f);
        phaseMinimumCoreDistance = Mathf.Max(0.5f, phaseMinimumCoreDistance);
        phaseAdvanceDuration = Mathf.Max(0.1f, phaseAdvanceDuration);
        phaseHopHeight = Mathf.Max(0f, phaseHopHeight);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        rageHopHeight = Mathf.Max(0f, rageHopHeight);
        rageScaleAmount = Mathf.Max(0f, rageScaleAmount);
        rageFrequency = Mathf.Max(0.1f, rageFrequency);
        slamWindupDuration = Mathf.Max(0f, slamWindupDuration);
        slamDuration = Mathf.Max(0.1f, slamDuration);
        slamJumpHeight = Mathf.Max(0f, slamJumpHeight);
        spinWindupDuration = Mathf.Max(0f, spinWindupDuration);
        spinDuration = Mathf.Max(0.1f, spinDuration);
        spinTurns = Mathf.Max(0.25f, spinTurns);
        spinLungeDistance = Mathf.Max(0f, spinLungeDistance);
        auraHeightOffset = Mathf.Max(0f, auraHeightOffset);
        auraRadius = Mathf.Max(0f, auraRadius);
        auraEmissionRate = Mathf.Max(1f, auraEmissionRate);
        hitFlashDuration = Mathf.Max(0.02f, hitFlashDuration);
        coreDamage = Mathf.Max(0f, coreDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
    }
}
