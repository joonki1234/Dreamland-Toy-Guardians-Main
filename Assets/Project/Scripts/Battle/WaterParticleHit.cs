using System.Collections.Generic;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// 소방관의 물 파티클이 몬스터와 실제로 충돌했을 때
/// 일정 간격으로 피해를 적용한다.
///
/// 반드시 물 Particle System이 붙은 동일한 오브젝트에 추가한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class WaterParticleHit : MonoBehaviour
{
    [Header("물 공격 설정")]

    [Tooltip("한 번의 피해 판정마다 적용할 피해량")]
    [SerializeField, Min(0f)]
    private float damagePerTick = 1f;

    [Tooltip("같은 몬스터에게 피해를 다시 줄 때까지의 시간")]
    [SerializeField, Min(0.02f)]
    private float hitInterval = 0.1f;


    private ParticleSystem waterParticle;

    private readonly List<ParticleCollisionEvent> collisionEvents =
        new List<ParticleCollisionEvent>();

    private readonly Dictionary<EnemyHealth, float> nextHitTimes =
        new Dictionary<EnemyHealth, float>();

    private static int nextShotId = 400000;


    private void Awake()
    {
        waterParticle = GetComponent<ParticleSystem>();
    }


    /// <summary>
    /// Particle System의 Collision 모듈에서
    /// Send Collision Messages가 활성화되어야 호출된다.
    /// </summary>
    private void OnParticleCollision(GameObject other)
    {
        if (waterParticle == null || other == null)
        {
            return;
        }

        int collisionCount =
            ParticlePhysicsExtensions.GetCollisionEvents(
                waterParticle,
                other,
                collisionEvents
            );

        if (collisionCount <= 0)
        {
            return;
        }

        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        float currentTime = Time.time;

        if (nextHitTimes.TryGetValue(
                enemy,
                out float nextAllowedHitTime) &&
            currentTime < nextAllowedHitTime)
        {
            return;
        }

        nextHitTimes[enemy] =
            currentTime + hitInterval;

        Vector3 hitPoint =
            collisionEvents[0].intersection;

        DamageInfo damageInfo = new DamageInfo(
            damagePerTick,
            "FIREFIGHTER_WATER_PARTICLE",
            PlayerRole.Firefighter,
            nextShotId++,
            hitPoint,
            true
        );

        bool damageApplied =
            enemy.TakeDamage(damageInfo);

        if (damageApplied)
        {
            Debug.Log(
                $"소방관 물 실제 충돌 피해: " +
                $"{enemy.gameObject.name} / {damagePerTick}"
            );
        }
    }


    private void OnValidate()
    {
        damagePerTick =
            Mathf.Max(0f, damagePerTick);

        hitInterval =
            Mathf.Max(0.02f, hitInterval);
    }
}