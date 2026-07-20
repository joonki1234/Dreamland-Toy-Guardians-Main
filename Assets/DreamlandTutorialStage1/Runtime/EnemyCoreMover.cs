using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemyCoreMover : MonoBehaviour
    {
        [SerializeField] private CoreState targetCore;
        [SerializeField] private Vector3 attackDestination;
        [SerializeField] private bool useAttackDestination;
        [SerializeField, Min(0f)] private float moveSpeed = 0.35f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.2f;
        [SerializeField, Min(0f)] private float coreDamage = 10f;
        [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;
        [SerializeField, Min(0f)] private float turnSpeed = 8f;

        private EnemyHealth health;
        private float nextAttackTime;
        private float stunnedUntil;

        public CoreState TargetCore => targetCore;
        public Vector3 AttackDestination => attackDestination;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            health ??= GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (health != null && health.IsDead)
            {
                return;
            }

            if (targetCore == null)
            {
                return;
            }

            if (Time.time < stunnedUntil)
            {
                return;
            }

            Vector3 destination = useAttackDestination
                ? attackDestination
                : targetCore.transform.position;
            destination.y = transform.position.y;

            Vector3 toDestination = destination - transform.position;
            float distance = toDestination.magnitude;

            if (distance > arrivalDistance)
            {
                Vector3 direction = toDestination / Mathf.Max(distance, 0.0001f);
                transform.position += direction * moveSpeed * Time.deltaTime;
                RotateTowards(direction);
                return;
            }

            Vector3 toCore = targetCore.transform.position - transform.position;
            toCore.y = 0f;
            RotateTowards(toCore.normalized);

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackInterval;
                targetCore.TakeDamage(coreDamage);
            }
        }

        public void Configure(
            CoreState core,
            Vector3 destination,
            float speed = 0.35f,
            float damage = 10f,
            float interval = 1.5f)
        {
            targetCore = core;
            attackDestination = destination;
            useAttackDestination = true;
            moveSpeed = Mathf.Max(0f, speed);
            coreDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.1f, interval);
        }

        public void Configure(
            CoreState core,
            float speed = 0.35f,
            float damage = 10f,
            float interval = 1.5f)
        {
            targetCore = core;
            useAttackDestination = false;
            moveSpeed = Mathf.Max(0f, speed);
            coreDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.1f, interval);
        }

        public void ApplyStun(float duration)
        {
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + Mathf.Max(0f, duration));
        }

        private void RotateTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        private void HandleDied(EnemyHealth _, DamageInfo __)
        {
            enabled = false;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            arrivalDistance = Mathf.Max(0.05f, arrivalDistance);
            coreDamage = Mathf.Max(0f, coreDamage);
            attackInterval = Mathf.Max(0.1f, attackInterval);
        }
    }
}
