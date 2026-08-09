using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// Waspy 드론의 공중 이동, 호버링, 코어 원거리 공격과
    /// Hit/Die Animator Trigger를 관리합니다.
    ///
    /// 체력, 체력바, 정화와 웨이브 생존 수 추적은
    /// 기존 DreamEnemySpawner 시스템을 그대로 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DroneEnemyWaspy : MonoBehaviour
    {
        [Header("공중 이동")]

        [Tooltip("코어 중심에서 이 거리만큼 떨어진 공중에서 멈춥니다.")]
        [SerializeField, Min(0.5f)]
        private float attackRange = 8f;

        [Tooltip("코어 기준 비행 높이입니다.")]
        [SerializeField, Min(0.5f)]
        private float flightHeight = 3f;

        [SerializeField, Min(0f)]
        private float moveSpeed = 1.8f;

        [SerializeField, Min(0f)]
        private float turnSpeed = 6f;

        [Tooltip("모델의 실제 정면이 Unity +Z와 다를 때 조정합니다.")]
        [SerializeField]
        private float modelYawOffset;

        [SerializeField, Min(0.05f)]
        private float arrivalTolerance = 0.35f;

        [Header("호버링")]

        [SerializeField, Min(0f)]
        private float hoverAmplitude = 0.12f;

        [SerializeField, Min(0f)]
        private float hoverFrequency = 2f;

        [Header("코어 공격")]

        [SerializeField, Min(0f)]
        private float coreDamage = 1f;

        [SerializeField, Min(0.1f)]
        private float attackInterval = 1f;

        [Tooltip("비워두면 드론 최상위 위치에서 레이저가 시작됩니다.")]
        [SerializeField]
        private Transform muzzle;

        [SerializeField]
        private Color beamColor =
            new Color(0.2f, 0.95f, 1f, 1f);

        [SerializeField, Min(0.005f)]
        private float beamWidth = 0.075f;

        [SerializeField, Min(0.02f)]
        private float beamDuration = 0.26f;

        [Header("애니메이션")]

        [Tooltip("비워두면 자식에서 Animator를 자동으로 찾습니다.")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private string hitTriggerName = "Hit";

        [SerializeField]
        private string dieTriggerName = "Die";


        private CoreState targetCore;
        private EnemyHealth health;
        private Vector3 attackDestination;
        private float nextAttackTime;
        private float hoverTimeOffset;
        private bool isDead;
        private bool hasHitTrigger;
        private bool hasDieTrigger;

        private LineRenderer attackBeam;
        private Material beamMaterial;
        private Coroutine beamRoutine;


        public float AttackRange => attackRange;
        public float FlightHeight => flightHeight;
        public float MoveSpeed => moveSpeed;
        public float CoreDamage => coreDamage;
        public float AttackInterval => attackInterval;


        private void Awake()
        {
            CacheReferences();
            CacheAnimatorParameters();

            hoverTimeOffset =
                Random.Range(0f, Mathf.PI * 2f);
        }


        private void OnEnable()
        {
            isDead = false;
            CacheReferences();
            CacheAnimatorParameters();
            SubscribeToHealth();
        }


        private void OnDisable()
        {
            UnsubscribeFromHealth();
            HideBeam();
        }


        private void OnDestroy()
        {
            if (beamMaterial != null)
            {
                Destroy(beamMaterial);
            }
        }


        private void Update()
        {
            if (isDead ||
                (health != null && health.IsDead) ||
                targetCore == null ||
                targetCore.IsDestroyed)
            {
                return;
            }

            Vector3 toDestination =
                attackDestination - transform.position;

            if (toDestination.magnitude > arrivalTolerance)
            {
                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        attackDestination,
                        moveSpeed * Time.deltaTime);

                RotateTowards(toDestination);
                return;
            }

            Vector3 hoverDestination = attackDestination;

            hoverDestination.y +=
                Mathf.Sin(
                    Time.time * hoverFrequency +
                    hoverTimeOffset) *
                hoverAmplitude;

            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    hoverDestination,
                    moveSpeed * Time.deltaTime);

            RotateTowards(
                targetCore.AttackTargetPosition -
                transform.position);

            TryAttackCore();
        }


        /// <summary>
        /// DreamEnemySpawner가 드론을 만든 직후 호출합니다.
        /// </summary>
        public void Configure(CoreState core)
        {
            targetCore = core;
            CacheReferences();
            CacheAnimatorParameters();
            SubscribeToHealth();

            if (targetCore == null)
            {
                Debug.LogWarning(
                    "[DroneEnemyWaspy] Target Core가 없어 " +
                    "드론을 움직일 수 없습니다.",
                    this);

                return;
            }

            Vector3 corePosition =
                targetCore.transform.position;

            Vector3 outwardDirection =
                transform.position - corePosition;

            outwardDirection.y = 0f;

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = transform.forward;
                outwardDirection.y = 0f;
            }

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = Vector3.forward;
            }

            outwardDirection.Normalize();

            attackDestination =
                corePosition +
                outwardDirection * attackRange;

            attackDestination.y =
                corePosition.y + flightHeight;

            nextAttackTime =
                Time.time + Random.Range(0.15f, 0.45f);
        }


        /// <summary>
        /// 프리팹에 Collider가 없을 때 모델 렌더러 크기에 맞는
        /// BoxCollider를 자동으로 추가합니다.
        /// </summary>
        public void EnsureHitCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            Renderer[] modelRenderers =
                GetComponentsInChildren<Renderer>(true);

            bool hasBounds = false;
            Bounds localBounds = new Bounds();

            foreach (Renderer modelRenderer in modelRenderers)
            {
                if (modelRenderer == null ||
                    modelRenderer is ParticleSystemRenderer ||
                    modelRenderer is LineRenderer)
                {
                    continue;
                }

                Bounds worldBounds = modelRenderer.bounds;

                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 worldCorner =
                                worldBounds.center +
                                Vector3.Scale(
                                    worldBounds.extents,
                                    new Vector3(x, y, z));

                            Vector3 localCorner =
                                transform.InverseTransformPoint(
                                    worldCorner);

                            if (!hasBounds)
                            {
                                localBounds =
                                    new Bounds(
                                        localCorner,
                                        Vector3.zero);

                                hasBounds = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning(
                    "[DroneEnemyWaspy] Collider를 자동 생성할 " +
                    "Renderer를 찾지 못했습니다.",
                    this);

                return;
            }

            BoxCollider hitCollider =
                gameObject.AddComponent<BoxCollider>();

            hitCollider.center = localBounds.center;
            hitCollider.size = localBounds.size;
        }


        private void TryAttackCore()
        {
            if (Time.time < nextAttackTime)
            {
                return;
            }

            nextAttackTime =
                Time.time + attackInterval;

            // 레이저는 코어를 향해 표시되고, 발사 1회당 1회의 피해만 줍니다.
            ShowAttackBeam();

            Vector3 muzzlePosition =
                muzzle != null
                    ? muzzle.position
                    : transform.position;
            Vector3 targetPosition = targetCore.AttackTargetPosition;
            Vector3 beamDirection = targetPosition - muzzlePosition;

            DreamlandCombatFx.SpawnMuzzleFlash(
                muzzlePosition,
                beamDirection,
                beamColor);
            DreamlandCombatFx.SpawnDroneLaserImpact(
                targetPosition,
                beamColor);

            targetCore.TakeDamage(coreDamage);
        }


        private void ShowAttackBeam()
        {
            EnsureBeamRenderer();

            if (attackBeam == null)
            {
                return;
            }

            if (beamRoutine != null)
            {
                StopCoroutine(beamRoutine);
            }

            beamRoutine =
                StartCoroutine(BeamRoutine());
        }


        private IEnumerator BeamRoutine()
        {
            attackBeam.enabled = true;

            float elapsed = 0f;

            while (elapsed < beamDuration &&
                   !isDead &&
                   targetCore != null)
            {
                elapsed += Time.deltaTime;

                Vector3 start =
                    muzzle != null
                        ? muzzle.position
                        : transform.position;

                // LineRenderer를 월드 좌표로 갱신해 드론 회전과 관계없이
                // 레이저 끝점이 항상 코어를 정확히 향하도록 합니다.
                Vector3 end =
                    targetCore.AttackTargetPosition;

                attackBeam.SetPosition(0, start);
                attackBeam.SetPosition(1, end);

                float alpha =
                    1f - Mathf.Clamp01(
                        elapsed / beamDuration);

                Color fadedColor = beamColor;
                fadedColor.a *= alpha;

                attackBeam.startColor = fadedColor;
                attackBeam.endColor = fadedColor;

                float pulse = 0.82f + Mathf.Sin(elapsed * 65f) * 0.18f;
                attackBeam.startWidth = beamWidth * pulse;
                attackBeam.endWidth = beamWidth * 0.48f * pulse;

                yield return null;
            }

            HideBeam();
        }


        private void EnsureBeamRenderer()
        {
            if (attackBeam != null)
            {
                return;
            }

            GameObject beamObject =
                new GameObject("DroneAttackBeam");

            beamObject.transform.SetParent(
                transform,
                false);

            attackBeam =
                beamObject.AddComponent<LineRenderer>();

            attackBeam.useWorldSpace = true;
            attackBeam.positionCount = 2;
            attackBeam.startWidth = beamWidth;
            attackBeam.endWidth = beamWidth * 0.45f;
            attackBeam.numCapVertices = 3;
            attackBeam.enabled = false;
            attackBeam.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");

            shader ??= Shader.Find("Sprites/Default");
            shader ??= Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[DroneEnemyWaspy] 레이저에 사용할 " +
                    "Unlit Shader를 찾지 못했습니다.",
                    this);

                return;
            }

            beamMaterial = new Material(shader)
            {
                name = "DroneBeam_Runtime",
                color = beamColor
            };

            if (beamMaterial.HasProperty("_BaseColor"))
            {
                beamMaterial.SetColor(
                    "_BaseColor",
                    beamColor);
            }

            attackBeam.material = beamMaterial;
        }


        private void HideBeam()
        {
            if (attackBeam != null)
            {
                attackBeam.enabled = false;
            }

            beamRoutine = null;
        }


        private void RotateTowards(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up) *
                Quaternion.Euler(
                    0f,
                    modelYawOffset,
                    0f);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);
        }


        private void CacheReferences()
        {
            if (animator == null)
            {
                animator =
                    GetComponentInChildren<Animator>(true);
            }

            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            ResolveMuzzle();
        }


        private void ResolveMuzzle()
        {
            if (muzzle != null)
            {
                return;
            }

            Transform fallback = null;
            Transform[] children =
                GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child == null || child == transform)
                {
                    continue;
                }

                string lowerName =
                    child.name.ToLowerInvariant();

                // Waspy 모델의 총구 본 이름은 gun.r_end / gun.l_end 계열입니다.
                if (lowerName.Contains("muzzle") ||
                    (lowerName.Contains("gun") &&
                     lowerName.Contains("end")))
                {
                    muzzle = child;
                    return;
                }

                if (fallback == null &&
                    (lowerName.Contains("gun") ||
                     lowerName.Contains("barrel") ||
                     lowerName.Contains("laser")))
                {
                    fallback = child;
                }
            }

            muzzle = fallback;
        }


        private void CacheAnimatorParameters()
        {
            hasHitTrigger = false;
            hasDieTrigger = false;

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter
                     in animator.parameters)
            {
                if (parameter.type !=
                    AnimatorControllerParameterType.Trigger)
                {
                    continue;
                }

                if (parameter.name == hitTriggerName)
                {
                    hasHitTrigger = true;
                }
                else if (parameter.name == dieTriggerName)
                {
                    hasDieTrigger = true;
                }
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
            if (isDead || animator == null)
            {
                return;
            }

            if (hasHitTrigger)
            {
                if (hasDieTrigger)
                {
                    animator.ResetTrigger(dieTriggerName);
                }

                animator.SetTrigger(hitTriggerName);
            }
        }


        private void HandleDied(
            EnemyHealth _,
            DamageInfo __)
        {
            isDead = true;
            HideBeam();

            if (animator == null || !hasDieTrigger)
            {
                return;
            }

            if (hasHitTrigger)
            {
                animator.ResetTrigger(hitTriggerName);
            }

            animator.SetTrigger(dieTriggerName);
        }


        private void OnValidate()
        {
            attackRange = Mathf.Max(0.5f, attackRange);
            flightHeight = Mathf.Max(0.5f, flightHeight);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            arrivalTolerance =
                Mathf.Max(0.05f, arrivalTolerance);
            hoverAmplitude = Mathf.Max(0f, hoverAmplitude);
            hoverFrequency = Mathf.Max(0f, hoverFrequency);
            coreDamage = Mathf.Max(0f, coreDamage);
            attackInterval = Mathf.Max(0.1f, attackInterval);
            beamWidth = Mathf.Max(0.005f, beamWidth);
            beamDuration = Mathf.Max(0.02f, beamDuration);
        }
    }
}
