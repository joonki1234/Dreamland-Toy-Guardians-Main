using System.Collections;
using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// Waspy 드론의 공중 이동, 호버링, 코어 원거리 공격과
    /// Hit/Die Animator Trigger를 관리합니다.
    ///
    /// 체력, 체력바, 정화와 웨이브 생존 수 추적은
    /// 기존 DreamEnemySpawner 시스템을 그대로 사용합니다.
    ///
    /// NetworkBehaviour다: 비행 이동은 반드시 FixedUpdateNetwork()(시뮬레이션
    /// 틱) 안에서 transform.position을 바꿔야 한다. 평범한 MonoBehaviour로
    /// Update()에서 위치를 바꾸면 같은 오브젝트의 Fusion.NetworkTransform이
    /// 매 프레임 "마지막으로 확인된 시뮬레이션 틱 위치"로 되돌려버려서
    /// 실제로는 전혀 움직이지 않는 문제가 있었다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DroneEnemyWaspy : NetworkBehaviour
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

        [Tooltip("궤도를 도는 동안의 속도입니다(초당 각도).")]
        [SerializeField]
        private float orbitAngularSpeed = 14f;

        [Tooltip("공격 사거리에 도달한 뒤, 한 번에 얼마나 오래 궤도를 도는지(초)입니다. 이동하는 동안은 공격하지 않습니다.")]
        [SerializeField, Min(0.1f)]
        private float orbitMoveDuration = 1.8f;

        [Tooltip("궤도 이동을 멈추고 제자리에서 레이저를 쏘는 시간(초)입니다.")]
        [SerializeField, Min(0.1f)]
        private float orbitPauseDuration = 1.6f;

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

        [Tooltip("발사 시 재생할 효과음입니다. 비워두면 Resources/SFX/Enemy/attack을 자동으로 불러옵니다.")]
        [SerializeField]
        private AudioClip attackSfx;

        [SerializeField, Range(0f, 1f)]
        private float attackSfxVolume = 0.35f;

        private static AudioClip cachedAttackSfx;
        private const string AttackSfxResourcePath = "SFX/Enemy/attack";

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
        private float spawnGroundHeight;
        private Vector3 attackDestination;
        private float orbitAngleDegrees;
        private bool hasReachedOrbit;
        private bool isOrbitMoving = true;
        private float orbitPhaseTimer;
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


        public override void FixedUpdateNetwork()
        {
            // 협동 플레이 동기화: 비행/공격 이동은 State Authority(방장)
            // 클라이언트에서만 계산하고, 나머지는 NetworkTransform으로
            // 결과만 따라온다.
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (isDead ||
                (health != null && health.IsDead) ||
                targetCore == null ||
                targetCore.IsDestroyed)
            {
                return;
            }

            if (!hasReachedOrbit)
            {
                // 처음 도달하기 전에는 목표 지점을 고정해서(각도를 돌리지 않음)
                // moveSpeed와 상관없이 확실히 도착할 수 있게 한다. 예전에는 여기서도
                // 매 프레임 각도를 돌려버려서, moveSpeed가 궤도 속도를 못 따라가면
                // 영원히 도착하지 못해 레이저를 한 번도 못 쏘는 문제가 있었다.
                Vector3 toInitial =
                    attackDestination - transform.position;

                if (toInitial.magnitude > arrivalTolerance)
                {
                    transform.position =
                        Vector3.MoveTowards(
                            transform.position,
                            attackDestination,
                            moveSpeed * Runner.DeltaTime);

                    RotateTowards(toInitial);
                    return;
                }

                hasReachedOrbit = true;
                isOrbitMoving = true;
                orbitPhaseTimer = orbitMoveDuration;
            }

            orbitPhaseTimer -= Runner.DeltaTime;

            if (isOrbitMoving)
            {
                // 궤도를 도는 구간: 각도를 계속 돌려서 코어 주위를 이동한다.
                // 이 구간에서는 공격하지 않는다.
                UpdateOrbitDestination();
                HoverTowardsAttackDestination();

                if (orbitPhaseTimer <= 0f)
                {
                    isOrbitMoving = false;
                    orbitPhaseTimer = orbitPauseDuration;

                    // 멈추자마자 바로 쏘지 않고 아주 잠깐 조준하는 느낌을 준다.
                    nextAttackTime =
                        Mathf.Max(nextAttackTime, Time.time + 0.15f);
                }

                return;
            }

            // 멈춰서 쏘는 구간: 각도를 더 이상 돌리지 않아 attackDestination이
            // 고정되고, 그 자리에서 호버링하며 계속 공격한다.
            HoverTowardsAttackDestination();
            TryAttackCore();

            if (orbitPhaseTimer <= 0f)
            {
                isOrbitMoving = true;
                orbitPhaseTimer = orbitMoveDuration;
            }
        }


        /// <summary>
        /// 현재 attackDestination(고정이든 궤도 회전 중이든)에 호버링 흔들림을
        /// 더한 지점으로 이동하고, 코어 쪽을 바라보게 회전한다.
        /// </summary>
        private void HoverTowardsAttackDestination()
        {
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
                    moveSpeed * Runner.DeltaTime);

            RotateTowards(
                targetCore.AttackTargetPosition -
                transform.position);
        }


        /// <summary>
        /// orbitAngleDegrees를 계속 증가시켜 attackDestination을 코어 중심,
        /// attackRange 반지름, flightHeight 높이의 원 위를 도는 지점으로 갱신한다.
        /// </summary>
        private void UpdateOrbitDestination()
        {
            orbitAngleDegrees +=
                orbitAngularSpeed * Runner.DeltaTime;

            Vector3 corePosition =
                targetCore.transform.position;

            float radians =
                orbitAngleDegrees * Mathf.Deg2Rad;

            Vector3 orbitDirection =
                new Vector3(
                    Mathf.Cos(radians),
                    0f,
                    Mathf.Sin(radians));

            attackDestination =
                corePosition +
                orbitDirection * attackRange;

            attackDestination.y =
                Mathf.Max(corePosition.y, spawnGroundHeight) +
                flightHeight;
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

            // 스폰된 지면 높이를 함께 기억해둔다. 코어 높이만 기준으로
            // 삼으면 방향(A~D)별 포탈이 코어보다 지형이 낮거나 높은
            // 경우 목표 고도가 스폰 지점 지면과 비슷하거나 그보다
            // 낮아져 드론이 날아오르지 못하고 지면을 기어다니는
            // 것처럼 보일 수 있다.
            spawnGroundHeight = transform.position.y;

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

            // 이후 Update()의 UpdateOrbitDestination()이 이 각도부터 계속 회전시킨다.
            // 여기서 계산하는 초기 attackDestination은 궤도 진입 전 첫 접근 목표일
            // 뿐이고, 매 프레임 각도 기준으로 다시 계산되므로 값 자체는 유지할
            // 필요가 없다.
            orbitAngleDegrees =
                Mathf.Atan2(
                    outwardDirection.z,
                    outwardDirection.x) *
                Mathf.Rad2Deg;

            hasReachedOrbit = false;

            attackDestination =
                corePosition +
                outwardDirection * attackRange;

            attackDestination.y =
                Mathf.Max(corePosition.y, spawnGroundHeight) +
                flightHeight;

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

            PlayAttackSfx(muzzlePosition);
        }


        private void PlayAttackSfx(Vector3 position)
        {
            AudioClip clip = attackSfx;

            if (clip == null)
            {
                if (cachedAttackSfx == null)
                {
                    cachedAttackSfx = Resources.Load<AudioClip>(AttackSfxResourcePath);
                }

                clip = cachedAttackSfx;
            }

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, attackSfxVolume);
            }
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
                    turnSpeed * Runner.DeltaTime);
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
