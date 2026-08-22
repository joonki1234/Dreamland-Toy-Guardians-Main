using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

/// <summary>집중포화의 일회성 전기 연출과 기존 범위 Tick 피해를 담당합니다.</summary>
public sealed class PoliceFocusedFireEffect : MonoBehaviour
{
    private sealed class LightningBeam
    {
        public Mesh Mesh;
        public MeshRenderer Renderer;
    }

    private static int nextFocusedFireShotId = 800000;

    private readonly List<Transform> muzzlePoints = new List<Transform>();
    private readonly List<LightningBeam> lightningBeams = new List<LightningBeam>();
    private readonly HashSet<EnemyHealth> enemiesInTick = new HashSet<EnemyHealth>();

    private Vector3 targetPosition;
    private Vector3 aimForward;
    private GameObject gunVisualSource;
    private int gunCount;
    private float formationRadius;
    private float gunHeight;
    private Vector3 rotationOffset;
    private float preparationTime;
    private float firingDuration;
    private float attackRadius;
    private int damageTickCount;
    private float damagePerTick;
    private LayerMask enemyMask;
    private Material lightningBeamMaterial;
    private int lightningSegmentCount;
    private float lightningJitter;
    private float lightningRefreshInterval;
    private float lightningBeamWidth;
    private AudioClip electricCrackSound;
    private float electricCrackVolume;
    private AudioClip electricImpactSound;
    private float electricImpactVolume;
    private AudioClip electricFireSound;
    private float electricFireVolume;
    private float electricSoundMinDistance;
    private float electricSoundMaxDistance;
    private AudioSource crackAudioSource;
    private AudioSource impactAudioSource;
    private AudioSource electricAudioSource;
    private float nextLightningRefreshTime;

    public void Initialize(
        Vector3 targetPosition,
        Vector3 aimForward,
        GameObject gunVisualSource,
        int gunCount,
        float formationRadius,
        float gunHeight,
        Vector3 rotationOffset,
        float preparationTime,
        float firingDuration,
        float attackRadius,
        int damageTickCount,
        float damagePerTick,
        LayerMask enemyMask,
        Material lightningBeamMaterial,
        int lightningSegmentCount,
        float lightningJitter,
        float lightningRefreshInterval,
        float lightningBeamWidth,
        AudioClip electricCrackSound,
        float electricCrackVolume,
        AudioClip electricImpactSound,
        float electricImpactVolume,
        AudioClip electricFireSound,
        float electricFireVolume,
        float electricSoundMinDistance,
        float electricSoundMaxDistance)
    {
        this.targetPosition = targetPosition;
        this.aimForward = aimForward;
        this.gunVisualSource = gunVisualSource;
        this.gunCount = gunCount;
        this.formationRadius = formationRadius;
        this.gunHeight = gunHeight;
        this.rotationOffset = rotationOffset;
        this.preparationTime = preparationTime;
        this.firingDuration = firingDuration;
        this.attackRadius = attackRadius;
        this.damageTickCount = damageTickCount;
        this.damagePerTick = damagePerTick;
        this.enemyMask = enemyMask;
        this.lightningBeamMaterial = lightningBeamMaterial;
        this.lightningSegmentCount = lightningSegmentCount;
        this.lightningJitter = lightningJitter;
        this.lightningRefreshInterval = lightningRefreshInterval;
        this.lightningBeamWidth = lightningBeamWidth;
        this.electricCrackSound = electricCrackSound;
        this.electricCrackVolume = electricCrackVolume;
        this.electricImpactSound = electricImpactSound;
        this.electricImpactVolume = electricImpactVolume;
        this.electricFireSound = electricFireSound;
        this.electricFireVolume = electricFireVolume;
        this.electricSoundMinDistance = electricSoundMinDistance;
        this.electricSoundMaxDistance = electricSoundMaxDistance;
        transform.position = targetPosition;

        StartCoroutine(FocusedFireRoutine());
    }

    private IEnumerator FocusedFireRoutine()
    {
        CreateVisualGuns();

        if (preparationTime > 0f)
        {
            yield return new WaitForSeconds(preparationTime);
        }

        BeginFiringEffects();

        float tickInterval = firingDuration / damageTickCount;
        float elapsed = 0f;
        int appliedTicks = 0;

        while (elapsed < firingDuration)
        {
            elapsed += Time.deltaTime;
            UpdateLightningArcs();

            while (appliedTicks < damageTickCount && elapsed >= appliedTicks * tickInterval)
            {
                ApplyDamageTick();
                appliedTicks++;
            }

            yield return null;
        }

        while (appliedTicks < damageTickCount)
        {
            ApplyDamageTick();
            appliedTicks++;
        }

        EndFiringEffects();
        Destroy(gameObject);
    }

    private void CreateVisualGuns()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(aimForward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;

        for (int i = 0; i < gunCount; i++)
        {
            float normalized = gunCount == 1 ? 0.5f : (float)i / (gunCount - 1);
            float angle = Mathf.Lerp(15f, 165f, normalized) * Mathf.Deg2Rad;
            Vector3 offset =
                right * (Mathf.Cos(angle) * formationRadius) -
                flatForward * (Mathf.Sin(angle) * formationRadius) +
                Vector3.up * (gunHeight + Mathf.Sin(angle) * formationRadius * 0.25f);

            Transform muzzlePoint;
            GameObject gun = CreateVisualGun(i, out muzzlePoint);
            gun.transform.SetParent(transform, false);
            gun.transform.position = targetPosition + offset;
            gun.transform.rotation =
                Quaternion.LookRotation(targetPosition - gun.transform.position, Vector3.up) *
                Quaternion.Euler(rotationOffset);

            muzzlePoints.Add(muzzlePoint);
            lightningBeams.Add(CreateLightningBeam(i));
        }
    }

    private GameObject CreateVisualGun(int index, out Transform muzzlePoint)
    {
        GameObject gun;

        if (gunVisualSource != null)
        {
            gun = Instantiate(gunVisualSource);
            gun.name = "Dreamlight_Gun_" + (index + 1);

            GunController clonedController = gun.GetComponent<GunController>();
            muzzlePoint = clonedController != null ? clonedController.firePoint : null;
            if (muzzlePoint == null)
            {
                muzzlePoint = FindChildByName(gun.transform, "FirePoint");
            }

            if (muzzlePoint == null)
            {
                muzzlePoint = gun.transform;
                Debug.LogWarning($"{gun.name}: 복제된 Police 총에서 FirePoint를 찾지 못했습니다.");
            }

            SanitizeVisualClone(gun);
            gun.SetActive(true);
        }
        else
        {
            gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "Dreamlight_Gun_Fallback_" + (index + 1);
            gun.transform.localScale = new Vector3(0.12f, 0.18f, 0.7f);
            Collider fallbackCollider = gun.GetComponent<Collider>();
            if (fallbackCollider != null)
            {
                Destroy(fallbackCollider);
            }

            muzzlePoint = gun.transform;
        }

        return gun;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void SanitizeVisualClone(GameObject gun)
    {
        foreach (MonoBehaviour behaviour in gun.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }

        foreach (Collider collider in gun.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in gun.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (AudioSource audioSource in gun.GetComponentsInChildren<AudioSource>(true))
        {
            audioSource.enabled = false;
        }
    }

    private LightningBeam CreateLightningBeam(int index)
    {
        GameObject beamObject = new GameObject("Police_Vefects_Lightning_Beam_" + (index + 1));
        beamObject.transform.SetParent(transform, false);
        beamObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        MeshFilter meshFilter = beamObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = beamObject.AddComponent<MeshRenderer>();
        Mesh beamMesh = new Mesh
        {
            name = "Police_Vefects_Lightning_Ribbon_" + (index + 1)
        };
        beamMesh.MarkDynamic();
        meshFilter.sharedMesh = beamMesh;
        meshRenderer.sharedMaterial = lightningBeamMaterial;
        meshRenderer.sortingOrder = 1;
        meshRenderer.enabled = false;

        if (lightningBeamMaterial == null)
        {
            Debug.LogError("Police 집중포화: Lightning Beam Material이 연결되지 않았습니다.");
        }

        return new LightningBeam
        {
            Mesh = beamMesh,
            Renderer = meshRenderer
        };
    }

    private void BeginFiringEffects()
    {
        foreach (LightningBeam beam in lightningBeams)
        {
            if (beam?.Renderer != null)
            {
                beam.Renderer.enabled = true;
            }
        }

        nextLightningRefreshTime = -1f;
        UpdateLightningArcs();

        if (electricCrackSound != null)
        {
            crackAudioSource = CreateSpatialAudioSource(
                electricCrackSound,
                electricCrackVolume,
                false
            );
            crackAudioSource.Play();
        }

        if (electricImpactSound != null)
        {
            impactAudioSource = CreateSpatialAudioSource(
                electricImpactSound,
                electricImpactVolume,
                false
            );
            impactAudioSource.Play();
        }

        if (electricFireSound != null)
        {
            electricAudioSource = CreateSpatialAudioSource(
                electricFireSound,
                electricFireVolume,
                true
            );
            electricAudioSource.Play();
        }
    }

    private AudioSource CreateSpatialAudioSource(AudioClip clip, float volume, bool loop)
    {
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = electricSoundMinDistance;
        audioSource.maxDistance = electricSoundMaxDistance;
        return audioSource;
    }

    private void EndFiringEffects()
    {
        foreach (LightningBeam beam in lightningBeams)
        {
            if (beam?.Renderer != null)
            {
                beam.Renderer.enabled = false;
            }
        }

        if (electricAudioSource != null)
        {
            electricAudioSource.Stop();
            Destroy(electricAudioSource);
            electricAudioSource = null;
        }

        if (impactAudioSource != null)
        {
            impactAudioSource.Stop();
            Destroy(impactAudioSource);
            impactAudioSource = null;
        }

        if (crackAudioSource != null)
        {
            crackAudioSource.Stop();
            Destroy(crackAudioSource);
            crackAudioSource = null;
        }

    }

    private void UpdateLightningArcs()
    {
        if (Time.time < nextLightningRefreshTime)
        {
            return;
        }

        nextLightningRefreshTime = Time.time + lightningRefreshInterval;

        for (int i = 0; i < lightningBeams.Count; i++)
        {
            LightningBeam beam = lightningBeams[i];
            Transform muzzlePoint = muzzlePoints[i];
            if (beam?.Mesh == null || beam.Renderer == null || muzzlePoint == null)
            {
                continue;
            }

            Vector3 start = muzzlePoint.position;
            Vector3 direction = targetPosition - start;
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 side = Vector3.Cross(normalizedDirection, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.Cross(normalizedDirection, Vector3.right);
            }

            side.Normalize();
            Vector3 up = Vector3.Cross(side, normalizedDirection).normalized;

            int segmentCount = lightningSegmentCount;
            Vector3[] points = new Vector3[segmentCount + 1];
            points[0] = start;
            for (int segment = 1; segment < segmentCount; segment++)
            {
                float t = (float)segment / segmentCount;
                float taper = Mathf.Sin(t * Mathf.PI);
                Vector2 randomOffset = Random.insideUnitCircle * lightningJitter * taper;
                Vector3 point = Vector3.Lerp(start, targetPosition, t) +
                                side * randomOffset.x +
                                up * randomOffset.y;
                points[segment] = point;
            }

            points[segmentCount] = targetPosition;
            RebuildLightningRibbon(beam, points);
        }
    }

    private void RebuildLightningRibbon(LightningBeam beam, Vector3[] points)
    {
        int pointCount = points.Length;
        Vector3[] vertices = new Vector3[pointCount * 2];
        Vector2[] uvs = new Vector2[pointCount * 2];
        int[] triangles = new int[(pointCount - 1) * 6];
        Camera viewCamera = Camera.main;
        float halfWidth = lightningBeamWidth * 0.5f;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 tangent;
            if (i == 0)
            {
                tangent = points[1] - points[0];
            }
            else if (i == pointCount - 1)
            {
                tangent = points[i] - points[i - 1];
            }
            else
            {
                tangent = points[i + 1] - points[i - 1];
            }

            tangent = tangent.sqrMagnitude > 0.0001f
                ? tangent.normalized
                : Vector3.forward;
            Vector3 towardView = viewCamera != null
                ? viewCamera.transform.position - points[i]
                : Vector3.up;
            Vector3 widthDirection = Vector3.Cross(tangent, towardView).normalized;
            if (widthDirection.sqrMagnitude < 0.0001f)
            {
                widthDirection = Vector3.Cross(tangent, Vector3.up).normalized;
            }
            if (widthDirection.sqrMagnitude < 0.0001f)
            {
                widthDirection = Vector3.Cross(tangent, Vector3.right).normalized;
            }

            vertices[i * 2] = points[i] - widthDirection * halfWidth;
            vertices[i * 2 + 1] = points[i] + widthDirection * halfWidth;
            float pathUv = (float)i / (pointCount - 1);
            uvs[i * 2] = new Vector2(pathUv, 0f);
            uvs[i * 2 + 1] = new Vector2(pathUv, 1f);
        }

        for (int segment = 0; segment < pointCount - 1; segment++)
        {
            int vertex = segment * 2;
            int triangle = segment * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        beam.Mesh.Clear();
        beam.Mesh.vertices = vertices;
        beam.Mesh.uv = uvs;
        beam.Mesh.triangles = triangles;
        beam.Mesh.RecalculateBounds();
    }

    private void ApplyDamageTick()
    {
        enemiesInTick.Clear();
        Collider[] hits = Physics.OverlapSphere(
            targetPosition,
            attackRadius,
            enemyMask,
            QueryTriggerInteraction.Collide
        );

        int shotId = nextFocusedFireShotId++;

        foreach (Collider hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || !enemiesInTick.Add(enemy))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(targetPosition);
            DamageInfo damageInfo = new DamageInfo(
                damagePerTick,
                "POLICE_FOCUSED_FIRE",
                PlayerRole.Police,
                shotId,
                hitPoint,
                true
            );

            enemy.TakeDamage(damageInfo);
        }
    }

    private void OnDestroy()
    {
        EndFiringEffects();

        foreach (LightningBeam beam in lightningBeams)
        {
            if (beam?.Mesh != null)
            {
                Destroy(beam.Mesh);
            }
        }
    }
}
