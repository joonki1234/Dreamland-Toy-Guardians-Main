using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 선물상자 최종 보스의 이동과 공격을 담당합니다.
///
/// FinalBossDirector가 보스를 생성한 뒤 Configure를 호출하면
/// 코어 쪽으로 통통 뛰어 접근하고, 내려찍기와 회전 공격을
/// 번갈아 사용합니다. 피격 시에는 머티리얼을 잠시 밝게 점멸합니다.
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

    [Header("통통 뛰기 이동")]

    [Tooltip("코어에서 이 거리만큼 떨어진 곳까지 접근합니다.")]
    [SerializeField, Min(0.5f)]
    private float attackRange = 5.5f;

    [SerializeField, Min(0f)]
    private float moveSpeed = 1.7f;

    [SerializeField, Min(0f)]
    private float turnSpeed = 240f;

    [SerializeField, Min(0f)]
    private float moveHopHeight = 0.45f;

    [SerializeField, Min(0.1f)]
    private float moveHopFrequency = 1.6f;

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
    private float moveHopTime;
    private float nextAttackTime;
    private int nextAttackIndex;
    private bool configured;
    private bool attacking;
    private bool isDead;
    private bool orientationApplied;

    private Coroutine attackRoutine;
    private Coroutine flashRoutine;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");


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

        CacheMaterialColors();
    }


    private void OnEnable()
    {
        CacheReferences();
        SubscribeToHealth();

        isDead = health != null && health.IsDead;

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
    }


    private void Update()
    {
        if (!configured ||
            attacking ||
            isDead ||
            targetCore == null ||
            targetCore.IsDestroyed ||
            (health != null && health.IsDead))
        {
            return;
        }

        Vector3 corePosition =
            targetCore.transform.position;

        Vector3 toCore =
            corePosition - transform.position;

        toCore.y = 0f;

        FaceDirection(toCore);

        if (toCore.magnitude > attackRange)
        {
            MoveTowardCore(corePosition, toCore);
            return;
        }

        SetPosition(new Vector3(
            transform.position.x,
            Mathf.MoveTowards(
                transform.position.y,
                groundY,
                moveSpeed * Time.deltaTime),
            transform.position.z));

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
        moveHopTime = 0f;
        nextAttackIndex = 0;
        isDead = false;
        configured = true;
        enabled = true;

        nextAttackTime =
            Time.time + firstAttackDelay;

        if (targetCore == null)
        {
            Debug.LogWarning(
                "[FinalBoss] Target Core가 연결되지 않아 " +
                "선물상자 보스가 움직일 수 없습니다.",
                this);
        }
    }


    private void MoveTowardCore(
        Vector3 corePosition,
        Vector3 toCore)
    {
        Vector3 planarDirection =
            toCore.sqrMagnitude > 0.0001f
                ? toCore.normalized
                : Vector3.forward;

        Vector3 destination =
            corePosition - planarDirection * attackRange;

        Vector3 currentPlanarPosition =
            new Vector3(
                transform.position.x,
                0f,
                transform.position.z);

        Vector3 destinationPlanarPosition =
            new Vector3(
                destination.x,
                0f,
                destination.z);

        Vector3 nextPlanarPosition =
            Vector3.MoveTowards(
                currentPlanarPosition,
                destinationPlanarPosition,
                moveSpeed * Time.deltaTime);

        moveHopTime +=
            Time.deltaTime * moveHopFrequency * Mathf.PI;

        float hopOffset =
            Mathf.Abs(Mathf.Sin(moveHopTime)) *
            moveHopHeight;

        SetPosition(new Vector3(
            nextPlanarPosition.x,
            groundY + hopOffset,
            nextPlanarPosition.z));
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

        if (nextAttackIndex % 2 == 0)
        {
            attackRoutine =
                StartCoroutine(SlamAttackRoutine());
        }
        else
        {
            attackRoutine =
                StartCoroutine(SpinAttackRoutine());
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

            float t =
                Mathf.Clamp01(elapsed / slamDuration);

            float jumpOffset =
                Mathf.Sin(t * Mathf.PI) * slamJumpHeight;

            SetPosition(
                startPosition +
                Vector3.up * jumpOffset);

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

        Vector3 pulseScale =
            baseScale * 1.1f;

        yield return AnimateScale(
            baseScale,
            pulseScale,
            spinWindupDuration);

        transform.localScale = baseScale;

        Quaternion startRotation =
            transform.rotation;

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

            float t =
                Mathf.Clamp01(elapsed / spinDuration);

            float spinAngle =
                t * spinTurns * 360f;

            transform.rotation =
                Quaternion.AngleAxis(
                    spinAngle,
                    Vector3.up) *
                startRotation;

            float lunge =
                Mathf.Sin(t * Mathf.PI) *
                spinLungeDistance;

            SetPosition(
                startPosition +
                lungeDirection * lunge);

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

            float t =
                Mathf.Clamp01(elapsed / duration);

            t = t * t * (3f - 2f * t);

            transform.localScale =
                Vector3.Lerp(from, to, t);

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
        nextAttackTime =
            Time.time + attackInterval;
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
        body.constraints =
            RigidbodyConstraints.FreezeRotation;
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


    private void HandleHit(
        EnemyHealth _,
        DamageInfo __)
    {
        if (isDead)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine =
            StartCoroutine(HitFlashRoutine());
    }


    private void HandleDied(
        EnemyHealth _,
        DamageInfo __)
    {
        isDead = true;
        configured = false;
        StopOwnedRoutines();
        RestoreVisualState(true);
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


    private void CacheMaterialColors()
    {
        materialColorStates.Clear();

        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer == null ||
                modelRenderer is ParticleSystemRenderer ||
                modelRenderer is LineRenderer)
            {
                continue;
            }

            Material[] materials =
                modelRenderer.materials;

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
                        OriginalColor =
                            material.GetColor(propertyId)
                    });
            }
        }
    }


    private void SetMaterialColor(Color color)
    {
        foreach (MaterialColorState state
                 in materialColorStates)
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
        foreach (MaterialColorState state
                 in materialColorStates)
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


    private void StopOwnedRoutines()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        attacking = false;
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
        moveSpeed = Mathf.Max(0f, moveSpeed);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        moveHopHeight = Mathf.Max(0f, moveHopHeight);
        moveHopFrequency = Mathf.Max(0.1f, moveHopFrequency);
        slamWindupDuration = Mathf.Max(0f, slamWindupDuration);
        slamDuration = Mathf.Max(0.1f, slamDuration);
        slamJumpHeight = Mathf.Max(0f, slamJumpHeight);
        spinWindupDuration = Mathf.Max(0f, spinWindupDuration);
        spinDuration = Mathf.Max(0.1f, spinDuration);
        spinTurns = Mathf.Max(0.25f, spinTurns);
        spinLungeDistance = Mathf.Max(0f, spinLungeDistance);
        hitFlashDuration = Mathf.Max(0.02f, hitFlashDuration);
        coreDamage = Mathf.Max(0f, coreDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
    }
}
