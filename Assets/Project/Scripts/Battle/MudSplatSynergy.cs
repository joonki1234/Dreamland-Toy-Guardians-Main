using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// 건축가의 MudSplat에 요리사 음식이 닿으면
/// 잠시 후 주변 적에게 범위 피해를 주고 함정을 제거한다.
/// </summary>
public class MudSplatSynergy : MonoBehaviour
{
    [Header("시너지 준비 시간")]
    [SerializeField] private float activationDelay = 0.4f;

    [Header("폭발 설정")]
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float explosionDamage = 30f;

    [Header("넉백 설정")]
    [SerializeField] private float knockbackDistance = 0.7f;
    [SerializeField] private float knockbackDuration = 0.15f;
    [SerializeField] private float stunDuration = 0.15f;

    [Header("적 레이어")]
    [SerializeField] private LayerMask enemyLayer;

    private bool synergyActivated;

    private static int nextShotId = 100000;

    private void OnTriggerEnter(Collider other)
    {
        if (synergyActivated ||
            !RoleSynergyProgression.IsUnlocked)
        {
            return;
        }

        ChefFoodProjectile food =
            other.GetComponentInParent<ChefFoodProjectile>();

        if (food == null)
        {
            return;
        }

        synergyActivated = true;

        Debug.Log("요리사 + 건축가 시너지: 미끼 함정 준비!");

        Destroy(food.gameObject);

        StartCoroutine(ActivateSynergyRoutine());
    }

    private IEnumerator ActivateSynergyRoutine()
    {
        yield return new WaitForSeconds(activationDelay);

        Explode();

        Debug.Log("요리사 + 건축가 시너지: 미끼 폭발!");

        Destroy(gameObject);
    }

    private void Explode()
    {
        Collider[] hitColliders = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            enemyLayer,
            QueryTriggerInteraction.Collide
        );

        // 한 적에게 Collider가 여러 개 있어도 피해는 한 번만 주기 위한 목록
        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        int shotId = nextShotId++;

        foreach (Collider hitCollider in hitColliders)
        {
            EnemyHealth enemy =
                hitCollider.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
            {
                continue;
            }

            if (!damagedEnemies.Add(enemy))
            {
                continue;
            }

            DamageInfo damageInfo = new DamageInfo(
                explosionDamage,
                "CHEF_BUILDER_SYNERGY",
                PlayerRole.Architect,
                shotId,
                enemy.transform.position,
                false
            );

            bool damageApplied = enemy.TakeDamage(damageInfo);

            if (damageApplied)
            {
                EnemyCoreMover mover =
                    enemy.GetComponent<EnemyCoreMover>();

                if (mover != null && !enemy.IsDead)
                {
                    // 폭발 중심에서 적 바깥쪽으로 향하는 방향
                    Vector3 knockbackDirection =
                        enemy.transform.position - transform.position;

                    knockbackDirection.y = 0f;

                    mover.ApplyStun(stunDuration);

                    mover.ApplyKnockback(
                        knockbackDirection,
                        knockbackDistance,
                        knockbackDuration
                    );
                }

                Debug.Log(
                    $"미끼 폭발 피해 및 넉백: " +
                    $"{enemy.gameObject.name} / {explosionDamage}"
                );
            }
        }

        Debug.Log($"미끼 폭발에 맞은 적 수: {damagedEnemies.Count}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    private void OnValidate()
    {
        activationDelay = Mathf.Max(0f, activationDelay);
        explosionRadius = Mathf.Max(0.1f, explosionRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);

        knockbackDistance = Mathf.Max(0f, knockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
    }
}
