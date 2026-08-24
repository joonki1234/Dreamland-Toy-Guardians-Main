using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 소환 시 저장한 수평 방향으로 소방차를 계속 이동시키고 맵 바깥 정리 거리에서 제거합니다.
/// 일반 물리 충돌 없이 적 감지, 피해, 넉백과 차량 연출을 함께 관리합니다.
/// </summary>
public sealed class FireTruckSkillMover : MonoBehaviour
{
    private const int HitBufferSize = 64;
    private static int nextTruckShotId = 900000;

    private readonly Collider[] hitBuffer = new Collider[HitBufferSize];
    private readonly HashSet<EnemyCoreMover> knockedBackEnemies =
        new HashSet<EnemyCoreMover>();

    private Vector3 moveDirection;
    private float moveSpeed;
    private float cleanupDistance;
    private Vector3 hitboxSize;
    private float knockbackDistance;
    private float knockbackDuration;
    private LayerMask monsterLayerMask;
    private float truckDamage;
    private int truckShotId;
    private AudioSource sirenAudioSource;
    private AudioSource spawnAudioSource;
    private AudioSource impactAudioSource;
    private AudioClip impactSound;
    private float impactSoundMinInterval;
    private float nextImpactSoundTime;
    private float travelledDistance;
    private bool initialized;

    public void Initialize(
        Vector3 direction,
        float speed,
        float distanceUntilCleanup,
        float hitboxWidth,
        float hitboxHeight,
        float hitboxLength,
        float enemyKnockbackDistance,
        float enemyKnockbackDuration,
        LayerMask enemyLayerMask,
        float damage,
        AudioClip sirenSound,
        float sirenVolume,
        AudioClip spawnSound,
        float spawnSoundVolume,
        AudioClip enemyImpactSound,
        float impactVolume,
        float minimumImpactSoundInterval,
        float audioMinDistance,
        float audioMaxDistance,
        float audioDopplerLevel)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        cleanupDistance = distanceUntilCleanup;
        hitboxSize = new Vector3(hitboxWidth, hitboxHeight, hitboxLength);
        knockbackDistance = enemyKnockbackDistance;
        knockbackDuration = enemyKnockbackDuration;
        monsterLayerMask = enemyLayerMask;
        truckDamage = damage;
        truckShotId = nextTruckShotId++;
        impactSound = enemyImpactSound;
        impactSoundMinInterval = minimumImpactSoundInterval;

        InitializeAudio(
            sirenSound,
            sirenVolume,
            spawnSound,
            spawnSoundVolume,
            impactVolume,
            audioMinDistance,
            audioMaxDistance,
            audioDopplerLevel
        );

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        float step = moveSpeed * Time.deltaTime;
        transform.position += moveDirection * step;
        travelledDistance += step;

        DetectAndKnockbackEnemies();

        if (travelledDistance >= cleanupDistance)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudio(
        AudioClip sirenSound,
        float sirenVolume,
        AudioClip spawnSound,
        float spawnSoundVolume,
        float impactVolume,
        float minDistance,
        float maxDistance,
        float dopplerLevel)
    {
        if (spawnSound != null)
        {
            spawnAudioSource = CreateSpatialAudioSource(
                "FireTruck_Spawn_Audio",
                spawnSoundVolume,
                false,
                minDistance,
                maxDistance,
                dopplerLevel
            );
            spawnAudioSource.clip = spawnSound;
            spawnAudioSource.Play();
        }

        if (sirenSound != null)
        {
            sirenAudioSource = CreateSpatialAudioSource(
                "FireTruck_Siren_Audio",
                sirenVolume,
                true,
                minDistance,
                maxDistance,
                dopplerLevel
            );
            sirenAudioSource.clip = sirenSound;
            sirenAudioSource.Play();
        }

        if (impactSound != null)
        {
            impactAudioSource = CreateSpatialAudioSource(
                "FireTruck_Impact_Audio",
                impactVolume,
                false,
                minDistance,
                maxDistance,
                dopplerLevel
            );
        }
    }

    private AudioSource CreateSpatialAudioSource(
        string objectName,
        float volume,
        bool loop,
        float minDistance,
        float maxDistance,
        float dopplerLevel)
    {
        GameObject audioObject = new GameObject(objectName);
        audioObject.transform.SetParent(transform, false);

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.dopplerLevel = dopplerLevel;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        return source;
    }

    private void DetectAndKnockbackEnemies()
    {
        Vector3 hitboxCenter = GetHitboxCenter();
        Quaternion hitboxRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        int hitCount = Physics.OverlapBoxNonAlloc(
            hitboxCenter,
            hitboxSize * 0.5f,
            hitBuffer,
            hitboxRotation,
            monsterLayerMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            hitBuffer[i] = null;

            if (hit == null)
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || enemyHealth.IsDead)
            {
                continue;
            }

            EnemyCoreMover enemyMover = enemyHealth.GetComponent<EnemyCoreMover>();
            if (enemyMover == null ||
                !enemyMover.isActiveAndEnabled ||
                !knockedBackEnemies.Add(enemyMover))
            {
                continue;
            }

            PlayImpactSound();

            Vector3 hitPoint = hit.ClosestPoint(GetHitboxCenter());
            DamageInfo damageInfo = new DamageInfo(
                truckDamage,
                "FIREFIGHTER_FIRE_TRUCK_SKILL",
                PlayerRole.Firefighter,
                truckShotId,
                hitPoint,
                true
            );

            enemyHealth.TakeDamage(damageInfo);

            // TakeDamage의 사망 이벤트가 적을 즉시 제거/비활성화할 수 있으므로
            // 넉백 전에 참조와 생존 상태를 다시 확인합니다.
            if (enemyHealth != null &&
                !enemyHealth.IsDead &&
                enemyMover != null &&
                enemyMover.isActiveAndEnabled)
            {
                enemyMover.ApplyKnockback(
                    moveDirection,
                    knockbackDistance,
                    knockbackDuration
                );
            }
        }
    }

    private void PlayImpactSound()
    {
        if (impactAudioSource == null ||
            impactSound == null ||
            Time.time < nextImpactSoundTime)
        {
            return;
        }

        impactAudioSource.PlayOneShot(impactSound);
        nextImpactSoundTime = Time.time + impactSoundMinInterval;
    }

    private Vector3 GetHitboxCenter()
    {
        return transform.position +
               Vector3.up * (hitboxSize.y * 0.5f) +
               moveDirection * (hitboxSize.z * 0.5f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 direction = initialized && moveDirection.sqrMagnitude > 0.0001f
            ? moveDirection
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        Vector3 size = initialized
            ? hitboxSize
            : new Vector3(2.5f, 2f, 3f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position +
            Vector3.up * (size.y * 0.5f) +
            direction * (size.z * 0.5f),
            Quaternion.LookRotation(direction, Vector3.up),
            Vector3.one
        );
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = previousMatrix;
    }
#endif

    private void OnDestroy()
    {
        if (sirenAudioSource != null)
        {
            sirenAudioSource.Stop();
        }

        if (spawnAudioSource != null)
        {
            spawnAudioSource.Stop();
        }

        if (impactAudioSource != null)
        {
            impactAudioSource.Stop();
        }
    }
}
