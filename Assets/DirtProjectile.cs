using UnityEngine;

public class DirtProjectile : MonoBehaviour
{
    public int damagePerShard = 8; // 잔해 개당 데미지 (여러 개가 맞으면 누적 데미지!)

    private void OnCollisionEnter(Collision collision)
    {
        // 부딪힌 대상이 적/타겟인지 확인
        Debug.Log($"잔해 타격! 부딪힌 대상: {collision.gameObject.name}");

        // 예시: 적 스크립트에 데미지 전달
        // EnemyHealth enemy = collision.gameObject.GetComponent<EnemyHealth>();
        // if (enemy != null) { enemy.TakeDamage(damagePerShard); }

        // 부딪히면 잔해 소멸
        Destroy(gameObject);
    }
}