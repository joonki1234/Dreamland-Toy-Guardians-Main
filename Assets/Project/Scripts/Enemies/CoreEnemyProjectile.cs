using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 원거리 적이 코어로 발사하는 가벼운 런타임 탄환입니다.
    /// 물리 충돌 대신 코어의 현재 목표 지점까지 실제로 이동한 뒤
    /// 도착 시점에만 코어 데미지를 적용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoreEnemyProjectile : MonoBehaviour
    {
        private CoreState targetCore;
        private Transform targetTransform;
        private float damage;
        private float speed;
        private float spawnedAt;
        private float maxLifetime = 3f;
        private float hitDistance = 0.12f;
        private float visualLength = 0.38f;

        private LineRenderer lineRenderer;
        private Material runtimeMaterial;
        private Color projectileColor;


        public static void Spawn(
            Vector3 startPosition,
            CoreState core,
            float projectileDamage,
            float projectileSpeed,
            Color color)
        {
            if (core == null ||
                core.IsDestroyed ||
                projectileDamage <= 0f)
            {
                return;
            }

            GameObject projectileObject =
                new GameObject("RangedEnemyBullet");

            projectileObject.transform.position = startPosition;

            CoreEnemyProjectile projectile =
                projectileObject.AddComponent<CoreEnemyProjectile>();

            projectile.Initialize(
                core,
                projectileDamage,
                projectileSpeed,
                color);
        }


        private void Initialize(
            CoreState core,
            float projectileDamage,
            float projectileSpeed,
            Color color)
        {
            targetCore = core;
            targetTransform = core.EnergyTarget;
            damage = Mathf.Max(0f, projectileDamage);
            speed = Mathf.Max(0.1f, projectileSpeed);
            projectileColor = color;
            spawnedAt = Time.time;

            EnsureRenderer();
            UpdateVisual(GetDirectionToTarget());
        }


        private void Update()
        {
            if (targetCore == null ||
                targetCore.IsDestroyed ||
                Time.time - spawnedAt >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            targetTransform = targetCore.EnergyTarget;

            Vector3 targetPosition =
                targetTransform != null
                    ? targetTransform.position
                    : targetCore.transform.position;

            Vector3 toTarget =
                targetPosition - transform.position;

            float distance = toTarget.magnitude;

            if (distance <= hitDistance ||
                distance <= speed * Time.deltaTime)
            {
                transform.position = targetPosition;
                UpdateVisual(toTarget);
                HitCore();
                return;
            }

            Vector3 direction =
                toTarget / Mathf.Max(distance, 0.0001f);

            transform.position +=
                direction * speed * Time.deltaTime;

            UpdateVisual(direction);
        }


        private Vector3 GetDirectionToTarget()
        {
            if (targetCore == null)
            {
                return transform.forward;
            }

            Transform target = targetCore.EnergyTarget;
            Vector3 targetPosition =
                target != null
                    ? target.position
                    : targetCore.transform.position;

            Vector3 direction =
                targetPosition - transform.position;

            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
        }


        private void HitCore()
        {
            if (targetCore != null &&
                !targetCore.IsDestroyed)
            {
                Vector3 impactPosition =
                    targetCore.EnergyTarget != null
                        ? targetCore.EnergyTarget.position
                        : targetCore.transform.position;

                targetCore.TakeDamage(damage);
                DreamlandCombatFx.SpawnCoreProjectileImpact(
                    impactPosition,
                    projectileColor);
            }

            Destroy(gameObject);
        }


        private void EnsureRenderer()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = 0.075f;
            lineRenderer.endWidth = 0.025f;
            lineRenderer.numCapVertices = 3;
            lineRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.startColor = projectileColor;
            lineRenderer.endColor = projectileColor;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");

            shader ??= Shader.Find("Sprites/Default");
            shader ??= Shader.Find("Unlit/Color");

            if (shader == null)
            {
                return;
            }

            runtimeMaterial = new Material(shader)
            {
                name = "EnemyBullet_Runtime",
                color = projectileColor
            };

            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                runtimeMaterial.SetColor(
                    "_BaseColor",
                    projectileColor);
            }

            lineRenderer.material = runtimeMaterial;
        }


        private void UpdateVisual(Vector3 direction)
        {
            if (lineRenderer == null)
            {
                return;
            }

            Vector3 safeDirection = direction;

            if (safeDirection.sqrMagnitude <= 0.0001f)
            {
                safeDirection = transform.forward;
            }

            safeDirection.Normalize();

            Vector3 head = transform.position;
            Vector3 tail = head - safeDirection * visualLength;

            lineRenderer.SetPosition(0, tail);
            lineRenderer.SetPosition(1, head);
        }


        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
