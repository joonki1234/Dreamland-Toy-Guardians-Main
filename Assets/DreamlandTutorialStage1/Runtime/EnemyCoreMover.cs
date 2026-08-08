using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 적이 바닥 높이를 유지하면서 코어 방향으로 이동하고,
    /// 도착하면 일정 간격으로 코어를 공격한다.
    ///
    /// 미끼 시너지가 적용되면 일정 시간 동안
    /// 코어 대신 미끼 위치로 이동한 뒤,
    /// 시간이 끝나면 원래 코어 이동으로 복귀한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyCoreMover : MonoBehaviour
    {
        [Header("목표 설정")]

        [SerializeField]
        private CoreState targetCore;

        [SerializeField]
        private Vector3 attackDestination;

        [SerializeField]
        private bool useAttackDestination;


        [Header("이동 설정")]

        [SerializeField, Min(0f)]
        private float moveSpeed = 0.35f;

        [SerializeField, Min(0.05f)]
        private float arrivalDistance = 0.2f;

        [Tooltip("적이 생성된 높이를 계속 유지합니다.")]
        [SerializeField]
        private bool keepSpawnHeight = true;


        [Header("공격 설정")]

        [SerializeField, Min(0f)]
        private float coreDamage = 1f;

        [SerializeField, Min(0.1f)]
        private float attackInterval = 1.5f;


        [Header("회전 설정")]

        [SerializeField, Min(0f)]
        private float turnSpeed = 8f;

        [Tooltip(
            "모델의 정면 축이 Unity +Z와 다를 때 적용할 Y축 회전 보정값")]
        [SerializeField]
        private float modelYawOffset;


        private static Material meleeImpactMaterial;

        private EnemyHealth health;
        private float nextAttackTime;
        private float stunnedUntil;
        private float fixedHeight;

        private bool isBeingKnockedBack;
        private Coroutine knockbackRoutine;


        // 현재 미끼에 유인되고 있는지 여부
        private bool isLured;

        // 적이 이동할 미끼의 월드 위치
        private Vector3 lurePosition;

        // 미끼 유인이 끝나는 시각
        private float lureEndTime;


        public CoreState TargetCore => targetCore;
        public Vector3 AttackDestination => attackDestination;
        public bool IsAttackingCore { get; private set; }
        public bool IsLured => isLured;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();

            // 적이 생성된 순간의 바닥 높이를 저장한다.
            fixedHeight = transform.position.y;
        }


        private void OnEnable()
        {
            health ??= GetComponent<EnemyHealth>();

            // 오브젝트 풀링 등으로 다시 활성화될 경우
            // 현재 높이를 새 기준 높이로 저장한다.
            fixedHeight = transform.position.y;

            // 이전 활성화 상태의 유인 정보를 초기화한다.
            ClearLure();

            if (health != null)
            {
                health.Died -= HandleDied;
                health.Died += HandleDied;
            }
        }


        private void OnDisable()
        {
            IsAttackingCore = false;

            if (health != null)
            {
                health.Died -= HandleDied;
            }

            ClearLure();
        }


        private void Update()
        {
            if (health != null &&
                health.IsDead)
            {
                IsAttackingCore = false;
                return;
            }

            // 넉백 중에는 다른 이동을 하지 않는다.
            if (isBeingKnockedBack)
            {
                IsAttackingCore = false;
                return;
            }

            // 스턴 중에는 이동과 공격을 멈춘다.
            if (Time.time < stunnedUntil)
            {
                return;
            }

            // 미끼 시간이 끝났다면 원래 목표로 복귀한다.
            if (isLured &&
                Time.time >= lureEndTime)
            {
                ClearLure();
            }

            // 미끼에 유인 중이면 코어 대신 미끼로 이동한다.
            if (isLured)
            {
                MoveTowardsLure();
                return;
            }

            if (targetCore == null)
            {
                IsAttackingCore = false;
                return;
            }

            if (Time.time < stunnedUntil)
            {
                IsAttackingCore = false;
                return;
            }
            Vector3 destination =
                useAttackDestination
                    ? attackDestination
                    : targetCore.transform.position;

            MoveTowardsCoreDestination(
                destination
            );
        }


        /// <summary>
        /// 코어 또는 코어 공격 지점을 향해 이동한다.
        /// 목적지에 도착하면 코어를 공격한다.
        /// </summary>
        private void MoveTowardsCoreDestination(
            Vector3 destination)
        {
            bool arrived = MoveTowardsPosition(
                destination
            );

            if (arrived)
            {
                AttackCore();
            }
        }


        /// <summary>
        /// 미끼 위치를 향해 이동한다.
        /// 도착하더라도 코어는 공격하지 않고
        /// 미끼 위치 근처에서 대기한다.
        /// </summary>
        private void MoveTowardsLure()
        {
            bool arrived = MoveTowardsPosition(
                lurePosition
            );

            if (!arrived)
            {
                return;
            }

            Vector3 toLure =
                lurePosition -
                transform.position;

            toLure.y = 0f;

            RotateTowards(
                toLure.normalized
            );
        }


        /// <summary>
        /// 지정한 위치를 향해 XZ 평면으로 이동한다.
        /// 목적지에 도착했으면 true를 반환한다.
        /// </summary>
        private bool MoveTowardsPosition(
            Vector3 destination)
        {
            Vector3 currentPosition =
                transform.position;

            // 목표 높이를 현재 적 높이와 같게 만들어
            // 위아래 이동을 방지한다.
            destination.y =
                currentPosition.y;

            Vector3 toDestination =
                destination -
                currentPosition;

            toDestination.y = 0f;

            float distance =
                toDestination.magnitude;

            if (distance <= arrivalDistance)
            {
                return true;
            }

            Vector3 direction =
                toDestination /
                Mathf.Max(
                    distance,
                    0.0001f
                );

            Vector3 nextPosition =
                currentPosition +
                direction *
                moveSpeed *
                Time.deltaTime;

            if (keepSpawnHeight)
            {
                nextPosition.y =
                    fixedHeight;
            }

            transform.position =
                nextPosition;

            RotateTowards(direction);

            return false;
        }


        /// <summary>
        /// 코어를 수평 방향으로 바라보고
        /// 일정 간격으로 공격한다.
        /// </summary>
        private void AttackCore()
        {
            if (targetCore == null)
            {
                IsAttackingCore = false;
                return;
            }

            IsAttackingCore = true;

            Vector3 toCore =
                targetCore.transform.position -
                transform.position;

            toCore.y = 0f;

            RotateTowards(
                toCore.normalized
            );

            if (Time.time <
                nextAttackTime)
            {
                return;
            }

            nextAttackTime =
                Time.time +
                attackInterval;

            targetCore.TakeDamage(
                coreDamage
            );

            if (coreDamage > 0f)
            {
                PlayCoreMeleeImpact();
            }
        }


        /// <summary>
        /// 근접 적이 코어를 때렸다는 느낌이 바로 들도록
        /// 코어 표면에서 짧은 충격 스파크를 생성합니다.
        /// 외부 이펙트 프리팹 없이 런타임 ParticleSystem으로 동작합니다.
        /// </summary>
        private void PlayCoreMeleeImpact()
        {
            if (targetCore == null)
            {
                return;
            }

            Transform target = targetCore.EnergyTarget;
            Vector3 impactPosition =
                target != null
                    ? target.position
                    : targetCore.transform.position;

            Vector3 awayFromEnemy = impactPosition - transform.position;
            if (awayFromEnemy.sqrMagnitude <= 0.0001f)
            {
                awayFromEnemy = Vector3.up;
            }

            GameObject effectObject = new GameObject("Core_MeleeHit_Impact");
            effectObject.transform.position = impactPosition;
            effectObject.transform.rotation = Quaternion.LookRotation(
                awayFromEnemy.normalized,
                Vector3.up);

            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.22f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 28;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.22f, 0.08f, 1f),
                new Color(1f, 0.78f, 0.20f, 1f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 20)
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.18f;

            ParticleSystemRenderer particleRenderer =
                effectObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.material = GetMeleeImpactMaterial();

            particles.Play();
            Destroy(effectObject, 1f);
        }


        private static Material GetMeleeImpactMaterial()
        {
            if (meleeImpactMaterial != null)
            {
                return meleeImpactMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            shader ??= Shader.Find("Particles/Standard Unlit");
            shader ??= Shader.Find("Unlit/Color");

            if (shader == null)
            {
                return null;
            }

            meleeImpactMaterial = new Material(shader)
            {
                name = "CoreMeleeImpact_Runtime",
                hideFlags = HideFlags.DontSave
            };

            if (meleeImpactMaterial.HasProperty("_BaseColor"))
            {
                meleeImpactMaterial.SetColor(
                    "_BaseColor",
                    new Color(1f, 0.35f, 0.08f, 1f));
            }
            else if (meleeImpactMaterial.HasProperty("_Color"))
            {
                meleeImpactMaterial.SetColor(
                    "_Color",
                    new Color(1f, 0.35f, 0.08f, 1f));
            }

            return meleeImpactMaterial;
        }


        /// <summary>
        /// 지정한 위치로 적을 일정 시간 유인한다.
        /// 시간이 끝나면 기존 코어 목표로 자동 복귀한다.
        /// </summary>
        public void ApplyLure(
            Vector3 position,
            float duration)
        {
            if (health != null &&
                health.IsDead)
            {
                return;
            }

            float safeDuration =
                Mathf.Max(
                    0.01f,
                    duration
                );

            lurePosition = position;

            // 적이 위아래로 움직이지 않도록
            // 미끼 위치의 높이는 적의 높이에 맞춘다.
            lurePosition.y =
                keepSpawnHeight
                    ? fixedHeight
                    : transform.position.y;

            isLured = true;

            lureEndTime =
                Time.time +
                safeDuration;
        }


        /// <summary>
        /// 현재 적용된 미끼 유인을 즉시 해제한다.
        /// </summary>
        public void ClearLure()
        {
            isLured = false;
            lurePosition = Vector3.zero;
            lureEndTime = 0f;
        }


        /// <summary>
        /// 공격 지점을 사용하는 방식으로
        /// 적을 설정한다.
        /// </summary>
        public void Configure(
            CoreState core,
            Vector3 destination,
            float speed = 0.35f,
            float damage = 1f,
            float interval = 1.5f,
            float yawOffset = 0f)
        {
            targetCore = core;
            attackDestination = destination;

            useAttackDestination = true;
            moveSpeed = Mathf.Max(0f, speed);
            coreDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.1f, interval);
            modelYawOffset = yawOffset;
            IsAttackingCore = false;
        }


        /// <summary>
        /// 코어 위치를 직접 목표로 사용하는 방식으로
        /// 적을 설정한다.
        /// </summary>
        public void Configure(
            CoreState core,
            float speed = 0.35f,
            float damage = 1f,
            float interval = 1.5f,
            float yawOffset = 0f)
        {
            targetCore = core;
            useAttackDestination = false;
            moveSpeed = Mathf.Max(0f, speed);
            coreDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.1f, interval);
            modelYawOffset = yawOffset;
            IsAttackingCore = false;
        }


        /// <summary>
        /// 지정한 시간 동안 적의 이동과 공격을 멈춘다.
        /// </summary>
        public void ApplyStun(
            float duration)
        {
            stunnedUntil =
                Mathf.Max(
                    stunnedUntil,
                    Time.time +
                    Mathf.Max(
                        0f,
                        duration
                    )
                );
        }


        /// <summary>
        /// 적을 지정한 방향으로 짧게 밀어낸다.
        /// 기존 적 이동이 Transform 방식이므로
        /// Rigidbody 힘 대신 코루틴으로 이동한다.
        /// </summary>
        public void ApplyKnockback(
            Vector3 direction,
            float distance,
            float duration)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <=
                0.0001f)
            {
                direction =
                    -transform.forward;
            }

            if (knockbackRoutine != null)
            {
                StopCoroutine(
                    knockbackRoutine
                );
            }

            knockbackRoutine =
                StartCoroutine(
                    KnockbackRoutine(
                        direction.normalized,
                        Mathf.Max(
                            0f,
                            distance
                        ),
                        Mathf.Max(
                            0.01f,
                            duration
                        )
                    )
                );
        }


        private IEnumerator KnockbackRoutine(
            Vector3 direction,
            float distance,
            float duration)
        {
            isBeingKnockedBack = true;

            Vector3 startPosition =
                transform.position;

            Vector3 targetPosition =
                startPosition +
                direction *
                distance;

            targetPosition.y =
                keepSpawnHeight
                    ? fixedHeight
                    : startPosition.y;

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (health != null &&
                    health.IsDead)
                {
                    break;
                }

                elapsedTime +=
                    Time.deltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsedTime /
                        duration
                    );

                transform.position =
                    Vector3.Lerp(
                        startPosition,
                        targetPosition,
                        progress
                    );

                yield return null;
            }

            if (health == null ||
                !health.IsDead)
            {
                transform.position =
                    targetPosition;
            }

            isBeingKnockedBack = false;
            knockbackRoutine = null;
        }


        /// <summary>
        /// 적을 XZ 평면에서 목표 방향으로 회전시킨다.
        /// </summary>
        private void RotateTowards(
            Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <=
                0.0001f)
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
                    turnSpeed *
                    Time.deltaTime
                );
        }


        private void HandleDied(
            EnemyHealth _,
            DamageInfo __)
        {
            IsAttackingCore = false;
            ClearLure();

            enabled = false;
        }


        private void OnValidate()
        {
            moveSpeed =
                Mathf.Max(
                    0f,
                    moveSpeed
                );

            arrivalDistance =
                Mathf.Max(
                    0.05f,
                    arrivalDistance
                );

            coreDamage =
                Mathf.Max(
                    0f,
                    coreDamage
                );

            attackInterval =
                Mathf.Max(
                    0.1f,
                    attackInterval
                );

            turnSpeed =
                Mathf.Max(
                    0f,
                    turnSpeed
                );
        }
    }
}
