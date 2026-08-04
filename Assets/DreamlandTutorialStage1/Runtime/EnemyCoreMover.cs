using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 적이 바닥 높이를 유지하면서 코어 방향으로 이동하고,
    /// 도착하면 일정 간격으로 코어를 공격한다.
    ///
    /// 이동과 회전은 XZ 평면에서만 처리하므로
    /// 코어나 공격 지점의 Y 위치가 달라도 적이 공중으로 뜨지 않는다.
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
        private float coreDamage = 10f;

        [SerializeField, Min(0.1f)]
        private float attackInterval = 1.5f;


        [Header("회전 설정")]

        [SerializeField, Min(0f)]
        private float turnSpeed = 8f;

        [Tooltip(
            "모델의 정면 축이 Unity +Z와 다를 때 적용할 Y축 회전 보정값")]
        [SerializeField]
        private float modelYawOffset;


        private EnemyHealth health;
        private float nextAttackTime;
        private float stunnedUntil;
        private float fixedHeight;
        private bool isBeingKnockedBack;
        private Coroutine knockbackRoutine;


        public CoreState TargetCore => targetCore;
        public Vector3 AttackDestination => attackDestination;
        public bool IsAttackingCore { get; private set; }


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
        }


        private void Update()
        {
            if (health != null && health.IsDead)
            {
                IsAttackingCore = false;
                return;
            }

            // 넉백 중에는 코어 방향으로 이동하지 않는다.
            if (isBeingKnockedBack)
            {
                IsAttackingCore = false;
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

            Vector3 destination = useAttackDestination
                ? attackDestination
                : targetCore.transform.position;

            MoveTowardsDestination(destination);
        }


        /// <summary>
        /// 목표 지점을 향해 XZ 평면으로만 이동한다.
        /// </summary>
        private void MoveTowardsDestination(Vector3 destination)
        {
            Vector3 currentPosition = transform.position;

            // 목표 높이를 현재 적 높이와 동일하게 만들어
            // 위아래 이동을 완전히 제거한다.
            destination.y = currentPosition.y;

            Vector3 toDestination =
                destination - currentPosition;

            // 안전하게 한 번 더 Y축을 제거한다.
            toDestination.y = 0f;

            float distance = toDestination.magnitude;

            if (distance > arrivalDistance)
            {
                IsAttackingCore = false;

                Vector3 direction =
                    toDestination /
                    Mathf.Max(distance, 0.0001f);

                Vector3 nextPosition =
                    currentPosition +
                    direction * moveSpeed * Time.deltaTime;

                if (keepSpawnHeight)
                {
                    nextPosition.y = fixedHeight;
                }

                transform.position = nextPosition;

                RotateTowards(direction);
                return;
            }

            AttackCore();
        }


        /// <summary>
        /// 코어를 수평 방향으로 바라보고 일정 간격으로 공격한다.
        /// </summary>
        private void AttackCore()
        {
            IsAttackingCore = true;

            Vector3 toCore =
                targetCore.transform.position -
                transform.position;

            // 적이 위나 아래로 기울어지지 않게 수평 방향만 사용한다.
            toCore.y = 0f;

            RotateTowards(toCore.normalized);

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime =
                    Time.time + attackInterval;

                targetCore.TakeDamage(coreDamage);
            }
        }


        /// <summary>
        /// 공격 지점을 사용하는 방식으로 적을 설정한다.
        /// </summary>
        public void Configure(
            CoreState core,
            Vector3 destination,
            float speed = 0.35f,
            float damage = 10f,
            float interval = 1.5f,
            float yawOffset = 0f)
        {
            targetCore = core;

            // 공격 지점의 Y값은 이동에 사용하지 않지만
            // 원래 전달된 값은 그대로 저장한다.
            attackDestination = destination;

            useAttackDestination = true;
            moveSpeed = Mathf.Max(0f, speed);
            coreDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.1f, interval);
            modelYawOffset = yawOffset;
            IsAttackingCore = false;
        }


        /// <summary>
        /// 코어의 위치를 직접 목표로 사용하는 방식으로 적을 설정한다.
        /// </summary>
        public void Configure(
            CoreState core,
            float speed = 0.35f,
            float damage = 10f,
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
        public void ApplyStun(float duration)
        {
            stunnedUntil = Mathf.Max(
                stunnedUntil,
                Time.time + Mathf.Max(0f, duration));
        }

        /// <summary>
        /// 적을 지정한 방향으로 짧게 밀어낸다.
        /// 기존 적 이동이 Transform 방식이므로 Rigidbody 힘 대신
        /// 코루틴으로 위치를 이동한다.
        /// </summary>
        public void ApplyKnockback(
            Vector3 direction,
            float distance,
            float duration)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = -transform.forward;
            }

            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }

            knockbackRoutine = StartCoroutine(
                KnockbackRoutine(
                    direction.normalized,
                    Mathf.Max(0f, distance),
                    Mathf.Max(0.01f, duration)
                )
            );
        }

        private IEnumerator KnockbackRoutine(
            Vector3 direction,
            float distance,
            float duration)
        {
            isBeingKnockedBack = true;

            Vector3 startPosition = transform.position;
            Vector3 targetPosition =
                startPosition + direction * distance;

            // 적이 위아래로 뜨지 않도록 기존 높이를 유지한다.
            targetPosition.y = keepSpawnHeight
                ? fixedHeight
                : startPosition.y;

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (health != null && health.IsDead)
                {
                    break;
                }

                elapsedTime += Time.deltaTime;

                float progress = Mathf.Clamp01(
                    elapsedTime / duration
                );

                transform.position = Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    progress
                );

                yield return null;
            }

            if (health == null || !health.IsDead)
            {
                transform.position = targetPosition;
            }

            isBeingKnockedBack = false;
            knockbackRoutine = null;
        }


        /// <summary>
        /// 적을 XZ 평면에서 목표 방향으로 회전시킨다.
        /// </summary>
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


        private void HandleDied(
            EnemyHealth _,
            DamageInfo __)
        {
            IsAttackingCore = false;
            enabled = false;
        }


        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            arrivalDistance =
                Mathf.Max(0.05f, arrivalDistance);
            coreDamage = Mathf.Max(0f, coreDamage);
            attackInterval =
                Mathf.Max(0.1f, attackInterval);
            turnSpeed = Mathf.Max(0f, turnSpeed);
        }
    }
}
