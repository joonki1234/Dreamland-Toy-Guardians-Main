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
    [SerializeField] private float damage = 10f;

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

        if (damageApplied)
        {
            Debug.Log(
                $"요리사 음식 실제 충돌 피해: " +
                $"{enemy.gameObject.name} / {damage}"
            );
        }

        Destroy(gameObject);
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
    }
}