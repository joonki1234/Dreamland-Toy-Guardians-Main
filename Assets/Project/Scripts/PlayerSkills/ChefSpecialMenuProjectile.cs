using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

public sealed class ChefSpecialMenuProjectile : MonoBehaviour
{
    private enum State
    {
        Falling,
        WaitingToExplode,
        Exploded
    }

    private Vector3 targetPoint;
    private float initialFallSpeed;
    private float currentFallSpeed;
    private float fallAcceleration;
    private float maxFallSpeed;
    private float explosionDelay;
    private float explosionRadius;
    private float explosionDamage;
    private LayerMask monsterLayerMask;
    private AudioClip fallSound;
    private float fallSoundVolume;
    private float fallPitchMin;
    private float fallPitchMax;
    private AudioClip landingSound;
    private float landingSoundVolume;
    private AudioClip explosionSound;
    private float explosionSoundVolume;
    private float audioMinDistance;
    private float audioMaxDistance;
    private float audioDopplerLevel;
    private GameObject explosionVfxPrefab;
    private float explosionVfxScale;
    private float explosionVfxHeightOffset;
    private AudioSource fallAudioSource;
    private float explosionTimer;
    private State state;

    private static int nextShotId = 600000;

    public void Initialize(
        Vector3 targetPoint,
        float initialFallSpeed,
        float fallAcceleration,
        float maxFallSpeed,
        float explosionDelay,
        float explosionRadius,
        float explosionDamage,
        LayerMask monsterLayerMask,
        AudioClip fallSound,
        float fallSoundVolume,
        float fallPitchMin,
        float fallPitchMax,
        AudioClip landingSound,
        float landingSoundVolume,
        AudioClip explosionSound,
        float explosionSoundVolume,
        float audioMinDistance,
        float audioMaxDistance,
        float audioDopplerLevel,
        GameObject explosionVfxPrefab,
        float explosionVfxScale,
        float explosionVfxHeightOffset)
    {
        this.targetPoint = targetPoint;
        this.initialFallSpeed = Mathf.Max(0.01f, initialFallSpeed);
        currentFallSpeed = this.initialFallSpeed;
        this.fallAcceleration = Mathf.Max(0f, fallAcceleration);
        this.maxFallSpeed = Mathf.Max(currentFallSpeed, maxFallSpeed);
        this.explosionDelay = Mathf.Max(0f, explosionDelay);
        this.explosionRadius = Mathf.Max(0.01f, explosionRadius);
        this.explosionDamage = Mathf.Max(0f, explosionDamage);
        this.monsterLayerMask = monsterLayerMask;
        this.fallSound = fallSound;
        this.fallSoundVolume = Mathf.Clamp01(fallSoundVolume);
        this.fallPitchMin = Mathf.Max(0.01f, fallPitchMin);
        this.fallPitchMax = Mathf.Max(this.fallPitchMin, fallPitchMax);
        this.landingSound = landingSound;
        this.landingSoundVolume = Mathf.Clamp01(landingSoundVolume);
        this.explosionSound = explosionSound;
        this.explosionSoundVolume = Mathf.Clamp01(explosionSoundVolume);
        this.audioMinDistance = Mathf.Max(0f, audioMinDistance);
        this.audioMaxDistance = Mathf.Max(this.audioMinDistance + 0.01f, audioMaxDistance);
        this.audioDopplerLevel = Mathf.Clamp(audioDopplerLevel, 0f, 0.2f);
        this.explosionVfxPrefab = explosionVfxPrefab;
        this.explosionVfxScale = Mathf.Max(0.01f, explosionVfxScale);
        this.explosionVfxHeightOffset = explosionVfxHeightOffset;
        state = State.Falling;
    }

    private void Start()
    {
        CreateFallAudio();
        if (fallAudioSource != null && fallSound != null)
        {
            fallAudioSource.Play();
        }

        if (TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        if (TryGetComponent(out ChefFoodProjectile basicProjectile))
        {
            basicProjectile.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private void Update()
    {
        if (state == State.Exploded)
        {
            return;
        }

        if (state == State.WaitingToExplode)
        {
            explosionTimer -= Time.deltaTime;
            if (explosionTimer <= 0f)
            {
                TriggerExplosion();
            }
            return;
        }

        currentFallSpeed = Mathf.Min(
            currentFallSpeed + fallAcceleration * Time.deltaTime,
            maxFallSpeed
        );
        if (fallAudioSource != null)
        {
            float pitchRatio = Mathf.InverseLerp(initialFallSpeed, maxFallSpeed, currentFallSpeed);
            fallAudioSource.pitch = Mathf.Lerp(fallPitchMin, fallPitchMax, pitchRatio);
        }
        float step = currentFallSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint,
            step
        );

        if (Vector3.Distance(transform.position, targetPoint) <= 0.05f)
        {
            transform.position = targetPoint;
            BeginExplosionWait();
        }
    }

    private void CreateFallAudio()
    {
        fallAudioSource = gameObject.GetComponent<AudioSource>();
        if (fallAudioSource == null)
        {
            fallAudioSource = gameObject.AddComponent<AudioSource>();
        }

        fallAudioSource.clip = fallSound;
        fallAudioSource.loop = false;
        fallAudioSource.playOnAwake = false;
        fallAudioSource.spatialBlend = 1f;
        fallAudioSource.dopplerLevel = audioDopplerLevel;
        fallAudioSource.minDistance = audioMinDistance;
        fallAudioSource.maxDistance = audioMaxDistance;
        fallAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        fallAudioSource.volume = fallSoundVolume;
        fallAudioSource.pitch = fallPitchMin;
    }

    private void BeginExplosionWait()
    {
        state = State.WaitingToExplode;
        explosionTimer = explosionDelay;

        if (fallAudioSource != null)
        {
            fallAudioSource.Stop();
        }

        PlaySpatialOneShot(landingSound, targetPoint, landingSoundVolume);

        if (TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void TriggerExplosion()
    {
        if (state == State.Exploded)
        {
            return;
        }

        state = State.Exploded;

        SpawnExplosionVfx();
        PlaySpatialOneShot(
            explosionSound,
            targetPoint + Vector3.up * explosionVfxHeightOffset,
            explosionSoundVolume);

        ApplyExplosionDamage();
        Destroy(gameObject);
    }

    private void SpawnExplosionVfx()
    {
        if (explosionVfxPrefab == null)
        {
            Debug.LogWarning("[ChefSkill] Explosion VFX prefab is null.");
            return;
        }

        Vector3 explosionPosition =
            targetPoint + Vector3.up * explosionVfxHeightOffset;
        GameObject vfxObject = UnityEngine.Object.Instantiate(
            (UnityEngine.Object)explosionVfxPrefab,
            explosionPosition,
            Quaternion.identity) as GameObject;
        if (vfxObject == null)
        {
            Debug.LogWarning("[ChefSkill] Explosion VFX Instantiate returned null.");
            return;
        }

        vfxObject.SetActive(true);
        Debug.Log($"[ChefSkill] Explosion VFX spawned: {explosionVfxPrefab.name}");
        vfxObject.transform.localScale *= explosionVfxScale;

        Component[] components =
            vfxObject.GetComponentsInChildren(typeof(ParticleSystem), true);
        List<ParticleSystem> particleSystemList = new List<ParticleSystem>();
        for (int i = 0; i < components.Length; i++)
        {
            ParticleSystem particles = components[i] as ParticleSystem;
            if (particles == null)
            {
                continue;
            }

            particleSystemList.Add(particles);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            particles.Play(true);
            ParticleSystemRenderer renderer =
                particles.GetComponent<ParticleSystemRenderer>();
            Debug.Log(
                $"[ChefSkill] ParticleSystem: {particles.name}, " +
                $"activeSelf={particles.gameObject.activeSelf}, " +
                $"activeInHierarchy={particles.gameObject.activeInHierarchy}, " +
                $"rendererEnabled={(renderer != null && renderer.enabled)}, " +
                $"emissionEnabled={particles.emission.enabled}, " +
                $"isPlaying={particles.isPlaying}");
        }

        Debug.Log($"[ChefSkill] Particle count: {particleSystemList.Count}");
        StartCoroutine(DestroyVfxWhenFinished(vfxObject, particleSystemList.ToArray()));
    }

    private IEnumerator DestroyVfxWhenFinished(
        GameObject vfxObject,
        ParticleSystem[] particleSystems)
    {
        yield return null;

        if (particleSystems == null || particleSystems.Length == 0)
        {
            Destroy(vfxObject);
            yield break;
        }

        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null && particleSystems[i].IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            if (anyAlive)
            {
                yield return null;
            }
        }

        Destroy(vfxObject);
    }

    private void PlaySpatialOneShot(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject audioObject = new GameObject("ChefSpecialMenu_OneShotAudio");
        audioObject.transform.position = position;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = audioMinDistance;
        source.maxDistance = audioMaxDistance;
        source.dopplerLevel = audioDopplerLevel;
        source.playOnAwake = false;
        source.Play();
        Destroy(audioObject, clip.length + 0.1f);
    }

    private void ApplyExplosionDamage()
    {
        Collider[] hits = Physics.OverlapSphere(
            targetPoint,
            explosionRadius,
            monsterLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (hits == null || hits.Length == 0)
        {
            return;
        }

        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            if (!damagedEnemies.Add(enemy))
            {
                continue;
            }

            int shotId = nextShotId++;
            DamageInfo damageInfo = new DamageInfo(
                explosionDamage,
                "CHEF_SPECIAL_MENU_SKILL",
                PlayerRole.Chef,
                shotId,
                targetPoint,
                true
            );

            enemy.TakeDamage(damageInfo);
        }
    }
}
