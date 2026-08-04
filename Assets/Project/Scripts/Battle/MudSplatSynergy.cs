using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// 건축가의 흙 장판에 요리사 음식이 닿으면
/// 주변 적을 장판으로 유인한 뒤 범위 폭발을 일으킨다.
/// </summary>
public class MudSplatSynergy : MonoBehaviour
{
    [Header("유인 설정")]

    [Tooltip("음식이 닿은 뒤 유인이 시작되기까지의 시간")]
    [SerializeField]
    private float lureStartDelay = 0.1f;

    [Tooltip("주변 적을 검색하는 유인 범위")]
    [SerializeField]
    private float lureRadius = 5f;

    [Tooltip("적을 장판 쪽으로 유인하는 시간")]
    [SerializeField]
    private float lureDuration = 2f;


    [Header("폭발 설정")]

    [Tooltip("폭발 피해가 적용되는 범위")]
    [SerializeField]
    private float explosionRadius = 2f;

    [Tooltip("폭발 피해량")]
    [SerializeField]
    private float explosionDamage = 30f;


    [Header("폭발 이펙트")]

    [Tooltip("시너지 폭발 순간 생성할 이펙트 프리팹")]
    [SerializeField]
    private GameObject explosionEffectPrefab;

    [Tooltip("생성된 폭발 이펙트의 크기 배율")]
    [SerializeField]
    private float explosionEffectScale = 0.7f;

    [Tooltip("생성된 폭발 이펙트를 삭제하기까지의 시간")]
    [SerializeField]
    private float explosionEffectLifetime = 3f;

    [Tooltip("폭발 이펙트가 바닥에 묻히지 않도록 올리는 높이")]
    [SerializeField]
    private float explosionEffectHeight = 0.05f;


    [Header("넉백 설정")]

    [SerializeField]
    private float knockbackDistance = 0.7f;

    [SerializeField]
    private float knockbackDuration = 0.15f;

    [SerializeField]
    private float stunDuration = 0.15f;


    [Header("적 레이어")]

    [SerializeField]
    private LayerMask enemyLayer;


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

        Debug.Log(
            "요리사 + 건축가 시너지: 미끼 함정 준비!"
        );

        // 장판에 닿은 음식은 제거한다.
        Destroy(food.gameObject);

        StartCoroutine(
            ActivateSynergyRoutine()
        );
    }


    /// <summary>
    /// 잠시 기다린 뒤 적을 유인하고,
    /// 유인 시간이 끝나면 폭발한다.
    /// </summary>
    private IEnumerator ActivateSynergyRoutine()
    {
        yield return new WaitForSeconds(
            lureStartDelay
        );

        int luredEnemyCount =
            LureNearbyEnemies();

        Debug.Log(
            $"미끼에 유인된 적 수: {luredEnemyCount}"
        );

        yield return new WaitForSeconds(
            lureDuration
        );

        CreateExplosionEffect();
        Explode();

        Debug.Log(
            "요리사 + 건축가 시너지: 미끼 폭발!"
        );

        Destroy(gameObject);
    }


    /// <summary>
    /// 유인 범위 안의 적을 찾아
    /// 일정 시간 동안 장판 위치로 이동시킨다.
    /// </summary>
    private int LureNearbyEnemies()
    {
        Collider[] hitColliders =
            Physics.OverlapSphere(
                transform.position,
                lureRadius,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );

        // Collider가 여러 개인 적을 중복 처리하지 않는다.
        HashSet<EnemyCoreMover> luredMovers =
            new HashSet<EnemyCoreMover>();

        foreach (Collider hitCollider in hitColliders)
        {
            EnemyHealth enemy =
                hitCollider.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy.IsDead)
            {
                continue;
            }

            EnemyCoreMover mover =
                enemy.GetComponent<EnemyCoreMover>();

            if (mover == null)
            {
                continue;
            }

            if (!luredMovers.Add(mover))
            {
                continue;
            }

            mover.ApplyLure(
                transform.position,
                lureDuration
            );
        }

        return luredMovers.Count;
    }


    /// <summary>
    /// 장판 위치에 폭발 이펙트를 생성한다.
    /// </summary>
    private void CreateExplosionEffect()
    {
        if (explosionEffectPrefab == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: " +
                "폭발 이펙트 프리팹이 연결되지 않았습니다."
            );

            return;
        }

        Vector3 effectPosition =
            transform.position +
            Vector3.up * explosionEffectHeight;

        GameObject effect = Instantiate(
            explosionEffectPrefab,
            effectPosition,
            Quaternion.identity
        );

        effect.transform.localScale *=
            explosionEffectScale;

        Destroy(
            effect,
            explosionEffectLifetime
        );
    }


    /// <summary>
    /// 폭발 범위 안의 적에게
    /// 피해와 넉백을 적용한다.
    /// </summary>
    private void Explode()
    {
        Collider[] hitColliders =
            Physics.OverlapSphere(
                transform.position,
                explosionRadius,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );

        HashSet<EnemyHealth> damagedEnemies =
            new HashSet<EnemyHealth>();

        int shotId = nextShotId++;

        foreach (Collider hitCollider in hitColliders)
        {
            EnemyHealth enemy =
                hitCollider.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy.IsDead)
            {
                continue;
            }

            if (!damagedEnemies.Add(enemy))
            {
                continue;
            }

            DamageInfo damageInfo =
                new DamageInfo(
                    explosionDamage,
                    "CHEF_BUILDER_SYNERGY",
                    PlayerRole.Architect,
                    shotId,
                    enemy.transform.position,
                    false
                );

            bool damageApplied =
                enemy.TakeDamage(damageInfo);

            if (!damageApplied)
            {
                continue;
            }

            EnemyCoreMover mover =
                enemy.GetComponent<EnemyCoreMover>();

            if (mover != null &&
                !enemy.IsDead)
            {
                Vector3 knockbackDirection =
                    enemy.transform.position -
                    transform.position;

                knockbackDirection.y = 0f;

                mover.ApplyStun(
                    stunDuration
                );

                mover.ApplyKnockback(
                    knockbackDirection,
                    knockbackDistance,
                    knockbackDuration
                );
            }

            Debug.Log(
                $"미끼 폭발 피해 및 넉백: " +
                $"{enemy.gameObject.name} / " +
                $"{explosionDamage}"
            );
        }

        Debug.Log(
            $"미끼 폭발에 맞은 적 수: " +
            $"{damagedEnemies.Count}"
        );
    }


    /// <summary>
    /// Scene 창에서 유인 범위와 폭발 범위를 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 큰 원: 유인 범위
        Gizmos.DrawWireSphere(
            transform.position,
            lureRadius
        );

        // 작은 원: 폭발 범위
        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius
        );
    }


    private void OnValidate()
    {
        lureStartDelay =
            Mathf.Max(0f, lureStartDelay);

        lureRadius =
            Mathf.Max(0.1f, lureRadius);

        lureDuration =
            Mathf.Max(0.1f, lureDuration);

        explosionRadius =
            Mathf.Max(0.1f, explosionRadius);

        explosionDamage =
            Mathf.Max(0f, explosionDamage);

        explosionEffectScale =
            Mathf.Max(0.01f, explosionEffectScale);

        explosionEffectLifetime =
            Mathf.Max(0.1f, explosionEffectLifetime);

        knockbackDistance =
            Mathf.Max(0f, knockbackDistance);

        knockbackDuration =
            Mathf.Max(0.01f, knockbackDuration);

        stunDuration =
            Mathf.Max(0f, stunDuration);
    }
}