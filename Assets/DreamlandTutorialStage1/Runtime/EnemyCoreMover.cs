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

        [Header("박치기 공격 연출")]
        [SerializeField, Min(0.05f)]
        private float headbuttWindupDuration = 0.16f;

        [SerializeField, Min(0.05f)]
        private float headbuttLungeDuration = 0.18f;

        [SerializeField, Min(0.05f)]
        private float headbuttRecoverDuration = 0.24f;

        [SerializeField, Min(0.1f)]
        private float headbuttDistance = 1.65f;

        [SerializeField, Min(0f)]
        private float headbuttWindbackDistance = 0.22f;

        [Tooltip("박치기 명중 시 재생할 효과음입니다. 비워두면 Resources/SFX/Enemy/attack_melee를 자동으로 불러옵니다.")]
        [SerializeField]
        private AudioClip headbuttAttackSfx;

        [SerializeField, Range(0f, 1f)]
        private float headbuttAttackSfxVolume = 0.35f;

        private static AudioClip cachedHeadbuttAttackSfx;
        private const string HeadbuttAttackSfxResourcePath = "SFX/Enemy/attack_melee";


        [Header("회전 설정")]

        [SerializeField, Min(0f)]
        private float turnSpeed = 8f;

        [Tooltip(
            "모델의 정면 축이 Unity +Z와 다를 때 적용할 Y축 회전 보정값")]
        [SerializeField]
        private float modelYawOffset;


        [Header("회피 기동 (원거리 미니건 적 전용)")]

        [Tooltip(
            "켜면 접근 중에는 좌우로 흔들리며 다가오고, 도착해서 공격할 때는 " +
            "그 자리에서 좌우로 사이드스텝합니다. 근접 로봇에는 켜지 마세요.")]
        [SerializeField]
        private bool useZigzagMovement;

        [Tooltip("좌우 흔들림/사이드스텝 폭입니다(미터). 도착 후에는 도착 지점 기준 이 범위를 벗어나지 않습니다.")]
        [SerializeField, Min(0f)]
        private float zigzagAmplitude = 1.2f;

        [Tooltip("좌우 흔들림/사이드스텝이 초당 몇 번 왕복하는지입니다.")]
        [SerializeField, Min(0f)]
        private float zigzagFrequency = 1.2f;

        // useZigzagMovement일 때만 쓰는, 지그재그 오프셋이 섞이지 않은 순수 경로 위치.
        // transform.position에는 매 프레임 이 값 + 사인파 오프셋을 그대로 대입하므로,
        // 오프셋이 프레임마다 누적되어 원래 경로에서 계속 멀어지는 일이 없다.
        private Vector3 zigzagPathPosition;


        private EnemyHealth health;
        private float nextAttackTime;
        private float stunnedUntil;
        private float fixedHeight;

        private bool isBeingKnockedBack;
        private Coroutine knockbackRoutine;
        private Coroutine headbuttRoutine;
        private bool isHeadbutting;


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

            zigzagPathPosition = transform.position;
        }


        private void OnEnable()
        {
            health ??= GetComponent<EnemyHealth>();

            // 오브젝트 풀링 등으로 다시 활성화될 경우
            // 현재 높이를 새 기준 높이로 저장한다.
            fixedHeight = transform.position.y;
            zigzagPathPosition = transform.position;

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

            if (headbuttRoutine != null)
            {
                StopCoroutine(headbuttRoutine);
                headbuttRoutine = null;
            }
            isHeadbutting = false;

            if (health != null)
            {
                health.Died -= HandleDied;
            }

            ClearLure();
        }


        private void Update()
        {
            // 협동 플레이 동기화: 이동/코어 공격은 이 적을 스폰한
            // State Authority(방장) 클라이언트에서만 계산한다. 다른
            // 클라이언트는 NetworkTransform으로 그 결과를 그대로
            // 따라오기만 해야 하므로, 여기서 직접 위치를 바꾸거나
            // 코어에 중복으로 피해를 주면 안 된다.
            if (!EnemyNetworkAuthority.HasAuthority(this))
            {
                return;
            }

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

                // 넉백이 transform.position을 직접 바꾸는 동안에도 순수 경로 위치를
                // 계속 맞춰둬서, 넉백이 끝난 뒤 지그재그 오프셋이 예전 위치를
                // 기준으로 다시 계산되어 순간이동하듯 튀는 일이 없게 한다.
                if (useZigzagMovement)
                {
                    zigzagPathPosition = transform.position;
                }

                return;
            }

            if (isHeadbutting)
            {
                IsAttackingCore = true;
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
            // 지그재그를 쓰는 동안은 오프셋이 섞이지 않은 순수 경로 위치를 기준으로
            // 거리/방향을 계산한다. 그래야 좌우로 흔들리는 동안에도 도착 판정과
            // 다음 이동 방향이 실제 시각적 흔들림에 영향받지 않고 항상 정확하다.
            Vector3 currentPosition =
                useZigzagMovement
                    ? zigzagPathPosition
                    : transform.position;

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
                if (useZigzagMovement)
                {
                    zigzagPathPosition = currentPosition;
                    ApplySidestepAtDestination(destination);
                }

                return true;
            }

            Vector3 direction =
                toDestination /
                Mathf.Max(
                    distance,
                    0.0001f
                );

            Vector3 nextPathPosition =
                currentPosition +
                direction *
                moveSpeed *
                Time.deltaTime;

            if (keepSpawnHeight)
            {
                nextPathPosition.y =
                    fixedHeight;
            }

            Vector3 renderedPosition =
                nextPathPosition;

            if (useZigzagMovement)
            {
                zigzagPathPosition = nextPathPosition;

                // 진행 방향에 수직인 축으로 사인파를 얹어 "전진하면서 좌우로
                // 흔들리는" 웨이브를 만든다. 순수 전진(nextPathPosition)은
                // 그대로 보장되고, 여기에 흔들림만 더해지는 구조라 전진 속도가
                // 깎이거나 뒤로 가는 일이 없다.
                Vector3 perpendicular =
                    new Vector3(
                        -direction.z,
                        0f,
                        direction.x);

                float lateralOffset =
                    Mathf.Sin(
                        Time.time *
                        zigzagFrequency *
                        Mathf.PI *
                        2f) *
                    zigzagAmplitude;

                renderedPosition =
                    nextPathPosition +
                    perpendicular *
                    lateralOffset;
            }

            transform.position =
                renderedPosition;

            RotateTowards(direction);

            return false;
        }


        /// <summary>
        /// 지그재그 적이 공격 지점에 도착한 뒤, 그 자리에 멈추는 대신 도착
        /// 지점(destination)을 기준으로 좌우로만 사이드스텝하게 한다.
        /// 매 프레임 "고정된 도착 지점 + 그 순간의 사인 값"으로 절대 위치를
        /// 다시 계산하므로 오프셋이 누적되지 않고, 항상 zigzagAmplitude
        /// 범위 안에서만 움직인다(코어→적 방향 기준이 아니라 이동 경로 기준
        /// 진행 방향의 수직 축을 계속 씁니다).
        /// </summary>
        private void ApplySidestepAtDestination(
            Vector3 destination)
        {
            if (targetCore == null)
            {
                transform.position = destination;
                return;
            }

            Vector3 toDestination =
                destination -
                targetCore.transform.position;

            toDestination.y = 0f;

            if (toDestination.sqrMagnitude <= 0.0001f)
            {
                transform.position = destination;
                return;
            }

            Vector3 outward = toDestination.normalized;

            Vector3 perpendicular =
                new Vector3(
                    -outward.z,
                    0f,
                    outward.x);

            float lateralOffset =
                Mathf.Sin(
                    Time.time *
                    zigzagFrequency *
                    Mathf.PI *
                    2f) *
                zigzagAmplitude;

            Vector3 desiredPosition =
                destination +
                perpendicular *
                lateralOffset;

            if (keepSpawnHeight)
            {
                desiredPosition.y = fixedHeight;
            }

            transform.position = desiredPosition;
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

            // 원거리 적은 EnemyCoreMover의 데미지가 0이고,
            // IsAttackingCore 신호만 사용해 전용 사격 스크립트를 실행합니다.
            if (coreDamage <= 0f)
            {
                return;
            }

            if (Time.time < nextAttackTime ||
                headbuttRoutine != null)
            {
                return;
            }

            nextAttackTime = Time.time + attackInterval;
            headbuttRoutine = StartCoroutine(HeadbuttAttackRoutine());
        }

        private IEnumerator HeadbuttAttackRoutine()
        {
            if (targetCore == null || targetCore.IsDestroyed)
            {
                headbuttRoutine = null;
                yield break;
            }

            isHeadbutting = true;
            IsAttackingCore = true;

            Vector3 restPosition = transform.position;
            Vector3 corePosition = targetCore.AttackTargetPosition;

            Vector3 toCore = corePosition - restPosition;
            toCore.y = 0f;

            Vector3 direction = toCore.sqrMagnitude > 0.0001f
                ? toCore.normalized
                : transform.forward;
            direction.y = 0f;

            RotateTowards(direction);

            float safeDistance = Mathf.Max(0.25f, toCore.magnitude - 0.65f);
            float lungeDistance = Mathf.Min(headbuttDistance, safeDistance);

            Vector3 windbackPosition =
                restPosition - direction * headbuttWindbackDistance;
            Vector3 impactPosition =
                restPosition + direction * lungeDistance;

            DreamlandCombatFx.SpawnChargeDust(restPosition);

            float elapsed = 0f;
            float windup = Mathf.Max(0.05f, headbuttWindupDuration);
            while (elapsed < windup && !IsUnavailableForAttack())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / windup);
                transform.position = Vector3.Lerp(
                    restPosition,
                    windbackPosition,
                    t * t);
                yield return null;
            }

            elapsed = 0f;
            float lunge = Mathf.Max(0.05f, headbuttLungeDuration);
            while (elapsed < lunge && !IsUnavailableForAttack())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lunge);
                float fastT = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = Vector3.Lerp(
                    windbackPosition,
                    impactPosition,
                    fastT);
                yield return null;
            }

            if (!IsUnavailableForAttack())
            {
                targetCore.TakeDamage(coreDamage);

                // 드론의 파란 코어 충격과 같은 짧은 구형 효과를
                // 근접 공격에는 붉은색으로 표시합니다.
                DreamlandCombatFx.SpawnHeadbuttImpact(
                    targetCore.AttackTargetPosition,
                    direction);

                PlayHeadbuttAttackSfx(targetCore.AttackTargetPosition);
            }

            elapsed = 0f;
            float recover = Mathf.Max(0.05f, headbuttRecoverDuration);
            Vector3 recoverStart = transform.position;
            while (elapsed < recover &&
                   health != null &&
                   !health.IsDead)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / recover);
                float smooth = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(
                    recoverStart,
                    restPosition,
                    smooth);
                yield return null;
            }

            if (health == null || !health.IsDead)
            {
                transform.position = restPosition;
            }

            isHeadbutting = false;
            headbuttRoutine = null;
        }

        private bool IsUnavailableForAttack()
        {
            return targetCore == null ||
                   targetCore.IsDestroyed ||
                   (health != null && health.IsDead);
        }

        private void PlayHeadbuttAttackSfx(Vector3 position)
        {
            AudioClip clip = headbuttAttackSfx;

            if (clip == null)
            {
                if (cachedHeadbuttAttackSfx == null)
                {
                    cachedHeadbuttAttackSfx = Resources.Load<AudioClip>(HeadbuttAttackSfxResourcePath);
                }

                clip = cachedHeadbuttAttackSfx;
            }

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, headbuttAttackSfxVolume);
            }
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
        /// 원거리 미니건 적처럼 접근 중 좌우로 흔들리고, 도착해서 공격할 때는
        /// 도착 지점 근처에서 사이드스텝하도록 켠다. Configure() 이후에 호출하세요.
        /// 근접 로봇에는 호출하지 마세요.
        /// </summary>
        public void SetZigzagMovement(
            bool enabled,
            float amplitude,
            float frequency)
        {
            useZigzagMovement = enabled;
            zigzagAmplitude = Mathf.Max(0f, amplitude);
            zigzagFrequency = Mathf.Max(0f, frequency);
            zigzagPathPosition = transform.position;
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

            headbuttWindupDuration = Mathf.Max(0.05f, headbuttWindupDuration);
            headbuttLungeDuration = Mathf.Max(0.05f, headbuttLungeDuration);
            headbuttRecoverDuration = Mathf.Max(0.05f, headbuttRecoverDuration);
            headbuttDistance = Mathf.Max(0.1f, headbuttDistance);
            headbuttWindbackDistance = Mathf.Max(0f, headbuttWindbackDistance);

            turnSpeed =
                Mathf.Max(
                    0f,
                    turnSpeed
                );
        }
    }
}
