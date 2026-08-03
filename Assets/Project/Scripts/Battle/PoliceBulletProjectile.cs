using UnityEngine;
using DreamGuardians;

[DisallowMultipleComponent]
public sealed class PoliceBulletProjectile : MonoBehaviour
{
    private float damage = 10f;
    private int shotId;
    private bool hasHit;

    public void Initialize(float bulletDamage, int uniqueShotId)
    {
        damage = Mathf.Max(0f, bulletDamage);
        shotId = uniqueShotId;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit || collision == null) return;

        Vector3 hitPoint = transform.position;
        if (collision.contactCount > 0) hitPoint = collision.GetContact(0).point;

        EnemyHealth enemy = collision.collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            HitEnemy(enemy, hitPoint);
            return;
        }

        hasHit = true;
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || other == null) return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        HitEnemy(enemy, hitPoint);
    }

    private void HitEnemy(EnemyHealth enemy, Vector3 hitPoint)
    {
        if (hasHit || enemy == null || enemy.IsDead) return;

        hasHit = true;

        DamageInfo damageInfo = new DamageInfo(
            damage,
            "POLICE_BULLET_PROJECTILE",
            PlayerRole.Police,
            shotId,
            hitPoint,
            true
        );

        bool damageApplied = enemy.TakeDamage(damageInfo);

        if (damageApplied)
        {
            Debug.Log($"경찰 총알 실제 충돌 피해: {enemy.gameObject.name} / {damage}");
        }

        // ⚡ [원소 시너지 연동] StatusReceiver 탐색 및 감전 속성 + 넉백 위치 전달
        StatusReceiver statusReceiver = enemy.GetComponentInParent<StatusReceiver>();
        if (statusReceiver == null) statusReceiver = enemy.GetComponentInChildren<StatusReceiver>();

        if (statusReceiver != null)
        {
            statusReceiver.ApplyElementalAttack(ElementalType.Electric, damage, transform.position);
        }

        Destroy(gameObject);
    }
}