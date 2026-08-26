using System.Collections.Generic;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// 요리사가 던진 음식 투사체.
/// 적과 실제로 충돌했을 때 피해를 준다.
/// MudSplat과 충돌하면 MudSplatSynergy가 시너지를 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class ChefFoodProjectile : MonoBehaviour
{
    [Header("공격 설정")]
    [SerializeField] private float damage = 14f;
    [SerializeField, Min(0f)] private float splashDamage = 7f;
    [SerializeField, Min(0.01f)] private float splashRadius = 2.5f;

    private bool hasHit;
    private static int nextShotId = 200000;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit)
        {
            return;
        }

        EnemyHealth enemy =
            collision.collider.GetComponentInParent<EnemyHealth>();

        if (enemy == null)
        {
            return;
        }

        hasHit = true;

        int shotId = nextShotId++;

        DamageInfo damageInfo = new DamageInfo(
            damage,
            "CHEF_FOOD_PROJECTILE",
            PlayerRole.Chef,
            shotId,
            collision.GetContact(0).point,
            false
        );

        bool damageApplied = enemy.TakeDamage(damageInfo);

        ApplySplashDamage(enemy, collision.GetContact(0).point);

        if (damageApplied)
        {
            Debug.Log(
                $"요리사 음식 실제 충돌 피해: " +
                $"{enemy.gameObject.name} / {damage}"
            );
        }

        Destroy(gameObject);
    }

    private void ApplySplashDamage(EnemyHealth directTarget, Vector3 hitPoint)
    {
        Collider[] hits = Physics.OverlapSphere(
            hitPoint, splashRadius, ~0, QueryTriggerInteraction.Collide);
        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>
        {
            directTarget
        };

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth splashTarget = hits[i] != null
                ? hits[i].GetComponentInParent<EnemyHealth>()
                : null;
            if (splashTarget == null || splashTarget.IsDead ||
                !damagedEnemies.Add(splashTarget))
            {
                continue;
            }

            splashTarget.TakeDamage(new DamageInfo(
                splashDamage,
                "CHEF_FOOD_SPLASH",
                PlayerRole.Chef,
                nextShotId++,
                hitPoint,
                true));
        }
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
        splashDamage = Mathf.Max(0f, splashDamage);
        splashRadius = Mathf.Max(0.01f, splashRadius);
    }
}
