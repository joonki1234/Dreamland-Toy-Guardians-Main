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


        [Header("애니메이션")]

        [Tooltip("비워두면 자식 오브젝트에서 Animator를 자동으로 찾습니다.")]
        [SerializeField]
        private Animator animator;

        [Tooltip("공격 지점에 도달했다고 판단할 여유 거리입니다.")]
        [SerializeField, Min(0.05f)]
        private float arrivalTolerance = 0.25f;


        private static readonly int IsMovingHash =
            Animator.StringToHash("IsMoving");

        private static readonly int IsAttackingHash =
            Animator.StringToHash("IsAttacking");

        private EnemyCoreMover mover;
        private EnemyHealth health;
        private bool hasIsMovingParameter;
        private bool hasIsAttackingParameter;


        public float AttackRange => attackRange;
        public float MoveSpeed => moveSpeed;
        public float CoreDamage => coreDamage;
        public float AttackInterval => attackInterval;


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

            bool isMoving =
                toDestination.sqrMagnitude >
                arrivalTolerance * arrivalTolerance;

            SetAnimatorState(
                isMoving,
                !isMoving);
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
        }
    }
}
