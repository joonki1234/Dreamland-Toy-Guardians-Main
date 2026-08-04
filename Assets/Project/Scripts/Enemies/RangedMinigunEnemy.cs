using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 미니건 원거리 적의 전투 설정과 Animator 상태를 관리합니다.
    /// 이동, 회전, 코어 피해, 넉백은 기존 EnemyCoreMover가 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RangedMinigunEnemy : MonoBehaviour
    {
        [Header("원거리 전투 설정")]

        [Tooltip("코어 중심에서 이 거리만큼 떨어진 위치에서 멈춥니다.")]
        [SerializeField, Min(0.5f)]
        private float attackRange = 7f;

        [SerializeField, Min(0f)]
        private float moveSpeed = 0.32f;

        [SerializeField, Min(0f)]
        private float coreDamage = 4f;

        [SerializeField, Min(0.1f)]
        private float attackInterval = 0.75f;

        [Tooltip(
            "모델이 이동 방향과 반대로 보일 때 사용하는 Y축 회전 보정값입니다. " +
            "현재 미니건 로봇은 -Z 방향이 정면이므로 180도를 사용합니다.")]
        [SerializeField]
        private float modelYawOffset = 180f;


        [Header("애니메이션")]

        [Tooltip("비워두면 자식 오브젝트에서 Animator를 자동으로 찾습니다.")]
        [SerializeField]
        private Animator animator;

        [Tooltip("공격 지점에 도달했다고 판단할 여유 거리입니다.")]
        [SerializeField, Min(0.05f)]
        private float arrivalTolerance = 0.25f;

        [Tooltip("Animator의 대기 상태 이름")]
        [SerializeField]
        private string idleStateName = "Idle";

        [Tooltip("Animator의 걷기 상태 이름")]
        [SerializeField]
        private string walkingStateName = "Walking";

        [Tooltip("Animator의 미니건 공격 상태 이름")]
        [SerializeField]
        private string attackStateName = "AttackMinigun";

        [SerializeField, Min(0f)]
        private float animationCrossFadeDuration = 0.08f;


        private static readonly int IsMovingHash =
            Animator.StringToHash("IsMoving");

        private static readonly int IsAttackingHash =
            Animator.StringToHash("IsAttacking");

        private EnemyCoreMover mover;
        private EnemyHealth health;
        private bool hasIsMovingParameter;
        private bool hasIsAttackingParameter;
        private AnimationMode currentAnimationMode =
            AnimationMode.Unknown;


        private enum AnimationMode
        {
            Unknown,
            Idle,
            Moving,
            Attacking
        }


        public float AttackRange => attackRange;
        public float MoveSpeed => moveSpeed;
        public float CoreDamage => coreDamage;
        public float AttackInterval => attackInterval;
        public float ModelYawOffset => modelYawOffset;


        private void Awake()
        {
            CacheReferences();
            CacheAnimatorParameters();
        }


        private void Start()
        {
            CacheReferences();
            CacheAnimatorParameters();
            SubscribeToHealth();

            // 이 모델은 Animator 클립을 사용하므로 근접 적의
            // 파츠 회전 스크립트가 함께 실행되지 않게 합니다.
            ToyRobotMotion oldRobotMotion =
                GetComponent<ToyRobotMotion>();

            if (oldRobotMotion != null)
            {
                oldRobotMotion.enabled = false;
            }
        }


        private void OnEnable()
        {
            currentAnimationMode = AnimationMode.Unknown;
        }


        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }

            SetAnimatorState(false, false);
        }


        private void Update()
        {
            CacheReferences();

            if (health != null && health.IsDead)
            {
                SetAnimatorState(false, false);
                return;
            }

            if (mover == null ||
                !mover.enabled ||
                mover.TargetCore == null ||
                mover.TargetCore.IsDestroyed)
            {
                // 포탈 등장 연출 중에는 EnemyCoreMover가 꺼져 있으므로
                // Idle 상태를 유지합니다.
                SetAnimatorState(false, false);
                return;
            }

            Vector3 destination = mover.AttackDestination;
            destination.y = transform.position.y;

            Vector3 toDestination =
                destination - transform.position;

            toDestination.y = 0f;

            bool isAttacking = mover.IsAttackingCore;

            bool isMoving =
                !isAttacking &&
                toDestination.sqrMagnitude >
                arrivalTolerance * arrivalTolerance;

            SetAnimatorState(
                isMoving,
                isAttacking);
        }


        /// <summary>
        /// DreamEnemySpawner가 EnemyCoreMover 설정을 마친 뒤 호출합니다.
        /// </summary>
        public void Configure(EnemyCoreMover configuredMover)
        {
            mover = configuredMover;
            CacheReferences();
            CacheAnimatorParameters();
        }


        private void CacheReferences()
        {
            if (animator == null)
            {
                animator =
                    GetComponentInChildren<Animator>(true);
            }

            if (mover == null)
            {
                mover = GetComponent<EnemyCoreMover>();
            }

            if (health == null)
            {
                health = GetComponent<EnemyHealth>();

                if (health != null)
                {
                    SubscribeToHealth();
                }
            }
        }


        private void SubscribeToHealth()
        {
            if (health == null)
            {
                return;
            }

            health.Died -= HandleDied;
            health.Died += HandleDied;
        }


        private void CacheAnimatorParameters()
        {
            hasIsMovingParameter = false;
            hasIsAttackingParameter = false;

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter
                     in animator.parameters)
            {
                if (parameter.nameHash == IsMovingHash)
                {
                    hasIsMovingParameter = true;
                }
                else if (parameter.nameHash == IsAttackingHash)
                {
                    hasIsAttackingParameter = true;
                }
            }
        }


        private void SetAnimatorState(
            bool isMoving,
            bool isAttacking)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (hasIsMovingParameter)
            {
                animator.SetBool(
                    IsMovingHash,
                    isMoving);
            }

            if (hasIsAttackingParameter)
            {
                animator.SetBool(
                    IsAttackingHash,
                    isAttacking);
            }

            AnimationMode nextMode = isAttacking
                ? AnimationMode.Attacking
                : isMoving
                    ? AnimationMode.Moving
                    : AnimationMode.Idle;

            if (nextMode == currentAnimationMode)
            {
                return;
            }

            currentAnimationMode = nextMode;

            string stateName = idleStateName;

            if (nextMode == AnimationMode.Attacking)
            {
                stateName = attackStateName;
            }
            else if (nextMode == AnimationMode.Moving)
            {
                stateName = walkingStateName;
            }

            CrossFadeToStateIfPresent(stateName);
        }


        /// <summary>
        /// Bool 조건 전환이 누락되어도 공격/걷기 상태가 보이도록
        /// 실제 Animator 상태를 한 번 직접 전환합니다.
        /// </summary>
        private void CrossFadeToStateIfPresent(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName) ||
                animator == null ||
                animator.runtimeAnimatorController == null)
            {
                return;
            }

            int fullPathHash = Animator.StringToHash(
                "Base Layer." + stateName);

            if (!animator.HasState(0, fullPathHash))
            {
                Debug.LogWarning(
                    "[RangedMinigunEnemy] Animator에서 상태를 찾지 못했습니다: " +
                    stateName,
                    this);

                return;
            }

            animator.CrossFadeInFixedTime(
                fullPathHash,
                animationCrossFadeDuration,
                0);
        }


        private void HandleDied(
            EnemyHealth _,
            DamageInfo __)
        {
            SetAnimatorState(false, false);
            enabled = false;
        }


        private void OnValidate()
        {
            attackRange = Mathf.Max(0.5f, attackRange);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            coreDamage = Mathf.Max(0f, coreDamage);
            attackInterval = Mathf.Max(0.1f, attackInterval);
            arrivalTolerance =
                Mathf.Max(0.05f, arrivalTolerance);
            animationCrossFadeDuration =
                Mathf.Max(0f, animationCrossFadeDuration);
        }
    }
}
