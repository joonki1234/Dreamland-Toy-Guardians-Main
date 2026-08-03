using UnityEngine;
using DreamGuardians;

/// <summary>
/// 경찰이 발사한 실제 총알 투사체.
///
/// 몬스터와 직접 충돌했을 때만 피해를 적용한다.
/// 일반 Collider와 Trigger Collider를 모두 지원한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PoliceBulletProjectile : MonoBehaviour
{
    private float damage = 10f;
    private int shotId;
    private bool hasHit;

    /// <summary>
    /// GunController가 총알을 생성한 직후 호출한다.
    /// </summary>
    public void Initialize(
        float bulletDamage,
        int uniqueShotId)
    {
        damage = Mathf.Max(0f, bulletDamage);
        shotId = uniqueShotId;
    }

    /// <summary>
    /// Is Trigger가 꺼진 몬스터 Collider와 충돌했을 때 실행된다.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit || collision == null)
        {
            return;
        }

        Vector3 hitPoint = transform.position;

        if (collision.contactCount > 0)
        {
            hitPoint = collision.GetContact(0).point;
        }

        EnemyHealth enemy =
            collision.collider.GetComponentInParent<EnemyHealth>();

        if (enemy != null)
        {
            HitEnemy(enemy, hitPoint);
            return;
        }

        // 벽이나 바닥 등 환경에 부딪히면 총알을 제거한다.
        hasHit = true;
        Destroy(gameObject);
    }

    /// <summary>
    /// 몬스터 Collider가 Trigger인 경우에도 피격되도록 처리한다.
    /// 적이 아닌 Trigger는 무시한다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || other == null)
        {
            return;
        }

        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy == null)
        {
            return;
        }

        Vector3 hitPoint =
            other.ClosestPoint(transform.position);

        HitEnemy(enemy, hitPoint);
    }

    private void HitEnemy(
        EnemyHealth enemy,
        Vector3 hitPoint)
    {
        if (hasHit || enemy == null || enemy.IsDead)
        {
            return;
        }

        hasHit = true;

        DamageInfo damageInfo = new DamageInfo(
            damage,
            "POLICE_BULLET_PROJECTILE",
            PlayerRole.Police,
            shotId,
            hitPoint,
            true
        );

        bool damageApplied =
            enemy.TakeDamage(damageInfo);

        if (damageApplied)
        {
            Debug.Log(
                $"경찰 총알 실제 충돌 피해: " +
                $"{enemy.gameObject.name} / {damage}"
            );
        }

        Destroy(gameObject);
    }
}