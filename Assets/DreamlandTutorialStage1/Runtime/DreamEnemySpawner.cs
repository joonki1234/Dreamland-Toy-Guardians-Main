using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class DreamEnemySpawner : MonoBehaviour
    {
        [Header("Enemy")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1f)] private float baseEnemyHealth = 100f;
        [SerializeField, Min(0f)] private float energyRewardPerEnemy = 10f;

        [Header("Movement To Core")]
        [SerializeField, Min(0f)] private float moveSpeed = 0.35f;
        [SerializeField, Min(0.5f)] private float attackRingRadius = 4.2f;
        [SerializeField, Min(0f)] private float attackSlotSpreadAngle = 12f;
        [SerializeField, Min(0f)] private float coreDamage = 10f;
        [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;

        [Header("Floor Rift Spawn")]
        [SerializeField] private bool useFloorRift = true;
        [SerializeField, Min(0f)] private float enemyGroundOffset = 0.8f;
        [SerializeField, Min(0f)] private float riseDepth = 0.9f;
        [SerializeField, Min(0.05f)] private float riseDuration = 1.25f;

        [Header("Scene References")]
        [SerializeField] private CoreState targetCore;
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        private readonly HashSet<EnemyPurification> activeEnemies = new HashSet<EnemyPurification>();
        private int nextSpawnPointIndex;
        private int spawnedCombatEnemyCount;

        public int ActiveEnemyCount => activeEnemies.Count;
        public GameObject EnemyPrefab => enemyPrefab;
        public CoreState TargetCore => targetCore;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;

        public event Action<EnemyHealth> EnemySpawned;
        public event Action AllEnemiesCleared;

        public void Configure(
            GameObject prefab,
            CoreState core,
            IEnumerable<Transform> points)
        {
            enemyPrefab = prefab;
            targetCore = core;
            spawnPoints.Clear();

            if (points != null)
            {
                foreach (Transform point in points)
                {
                    if (point != null)
                    {
                        spawnPoints.Add(point);
                    }
                }
            }
        }

        public void ApplyPrototypeDefaultsV6()
        {
            ApplyPrototypeDefaultsV7();
        }

        public void ApplyPrototypeDefaultsV7()
        {
            moveSpeed = 0.35f;
            attackRingRadius = 4.2f;
            attackSlotSpreadAngle = 12f;
            coreDamage = 10f;
            attackInterval = 1.5f;
            useFloorRift = true;
            enemyGroundOffset = 0.8f;
            riseDepth = 0.9f;
            riseDuration = 1.25f;
        }

        public IEnumerator SpawnGroup(
            int enemyCount,
            float spawnInterval,
            float healthMultiplier = 1f)
        {
            int safeCount = Mathf.Max(0, enemyCount);
            float safeInterval = Mathf.Max(0f, spawnInterval);

            for (int i = 0; i < safeCount; i++)
            {
                SpawnCombatEnemy(healthMultiplier);

                if (safeInterval > 0f && i < safeCount - 1)
                {
                    yield return new WaitForSeconds(safeInterval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        public EnemyHealth SpawnCombatEnemy(float healthMultiplier = 1f)
        {
            Transform spawnPoint = GetNextSpawnPoint();
            Vector3 position = spawnPoint != null
                ? spawnPoint.position + Vector3.up * enemyGroundOffset
                : transform.position + transform.forward * 10f + Vector3.up * enemyGroundOffset;
            Quaternion rotation = spawnPoint != null
                ? spawnPoint.rotation
                : transform.rotation;

            return SpawnEnemy(position, rotation, false, healthMultiplier, spawnPoint);
        }

        public EnemyHealth SpawnTutorialEnemy(Transform tutorialSpawnPoint)
        {
            Vector3 position = tutorialSpawnPoint != null
                ? tutorialSpawnPoint.position + Vector3.up * enemyGroundOffset
                : transform.position + transform.forward * 9f + Vector3.up * enemyGroundOffset;
            Quaternion rotation = tutorialSpawnPoint != null
                ? tutorialSpawnPoint.rotation
                : transform.rotation;

            return SpawnEnemy(position, rotation, true, 1f, tutorialSpawnPoint);
        }

        private EnemyHealth SpawnEnemy(
            Vector3 position,
            Quaternion rotation,
            bool tutorialEnemy,
            float healthMultiplier,
            Transform spawnPoint)
        {
            GameObject enemyObject;

            if (enemyPrefab != null)
            {
                enemyObject = Instantiate(enemyPrefab, position, rotation);
            }
            else
            {
                enemyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                enemyObject.transform.SetPositionAndRotation(position, rotation);
                enemyObject.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            }

            enemyObject.name = tutorialEnemy
                ? "TutorialEnemy"
                : "WaveEnemy";

            if (tutorialEnemy)
            {
                MakeTutorialEnemyHighlyVisible(enemyObject);
            }

            EnemyHealth health = GetOrAdd<EnemyHealth>(enemyObject);
            GetOrAdd<RoleSynergyTracker>(enemyObject);
            GetOrAdd<EnemyWorldHealthBar>(enemyObject);

            EnemyCoreMover mover = GetOrAdd<EnemyCoreMover>(enemyObject);
            if (tutorialEnemy)
            {
                // The first tutorial target is intentionally stationary.
                mover.Configure(targetCore, 0f, 0f, attackInterval);
                mover.enabled = false;
                StartTutorialRiftSpawn(enemyObject, mover, spawnPoint, position);
            }
            else
            {
                Vector3 attackDestination = CalculateAttackDestination(position);
                mover.Configure(targetCore, attackDestination, moveSpeed, coreDamage, attackInterval);
                StartRiftSpawn(enemyObject, mover, spawnPoint, position);
            }

            EnemyPurification purification = GetOrAdd<EnemyPurification>(enemyObject);
            purification.Configure(targetCore, energyRewardPerEnemy);
            purification.Completed += HandlePurificationCompleted;

            float configuredHealth = baseEnemyHealth * Mathf.Max(0.1f, healthMultiplier);
            health.Configure(configuredHealth, !tutorialEnemy);

            activeEnemies.Add(purification);
            EnemySpawned?.Invoke(health);
            return health;
        }

        private Vector3 CalculateAttackDestination(Vector3 spawnPosition)
        {
            if (targetCore == null)
            {
                return spawnPosition;
            }

            Vector3 corePosition = targetCore.transform.position;
            Vector3 outwardDirection = spawnPosition - corePosition;
            outwardDirection.y = 0f;

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = Vector3.forward;
            }

            outwardDirection.Normalize();

            int spreadIndex = spawnedCombatEnemyCount % 3 - 1;
            spawnedCombatEnemyCount++;
            float spread = spreadIndex * attackSlotSpreadAngle;
            outwardDirection = Quaternion.Euler(0f, spread, 0f) * outwardDirection;

            Vector3 destination = corePosition + outwardDirection * attackRingRadius;
            destination.y = spawnPosition.y;
            return destination;
        }


        private void StartTutorialRiftSpawn(
            GameObject enemyObject,
            EnemyCoreMover mover,
            Transform spawnPoint,
            Vector3 finalPosition)
        {
            if (!useFloorRift || spawnPoint == null)
            {
                mover.enabled = false;
                return;
            }

            FloorRiftMarker rift = GetOrAdd<FloorRiftMarker>(spawnPoint.gameObject);
            EnemySpawnRise rise = GetOrAdd<EnemySpawnRise>(enemyObject);
            rise.Begin(
                finalPosition,
                riseDepth,
                riseDuration,
                mover,
                rift,
                enableMoverAfterRise: false,
                usePortalDirection: false
            );
        }

        private void StartRiftSpawn(
            GameObject enemyObject,
            EnemyCoreMover mover,
            Transform spawnPoint,
            Vector3 finalPosition)
        {
            if (!useFloorRift || spawnPoint == null)
            {
                mover.enabled = true;
                return;
            }

            FloorRiftMarker rift = GetOrAdd<FloorRiftMarker>(spawnPoint.gameObject);
            EnemySpawnRise rise = GetOrAdd<EnemySpawnRise>(enemyObject);
            Vector3 portalForward = spawnPoint.forward;

            rise.Begin(
                finalPosition,
                riseDepth,
                riseDuration,
                mover,
                rift,
                enableMoverAfterRise: true,
                usePortalDirection: true,
                portalForward: portalForward
            );
        }

        private static void MakeTutorialEnemyHighlyVisible(GameObject enemyObject)
        {
            if (enemyObject == null)
            {
                return;
            }

            enemyObject.transform.localScale = Vector3.one * 1.5f;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            shader ??= Shader.Find("Unlit/Color");
            shader ??= Shader.Find("Standard");

            if (shader == null)
            {
                return;
            }

            Color visibleColor = new Color(1f, 0.08f, 0.65f, 1f);
            Material material = new Material(shader)
            {
                name = "TutorialEnemy_Visible_Runtime",
                color = visibleColor
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", visibleColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", visibleColor * 2f);
            }

            foreach (Renderer targetRenderer in enemyObject.GetComponentsInChildren<Renderer>(true))
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.sharedMaterial = material;
                targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                targetRenderer.receiveShadows = false;
                targetRenderer.enabled = true;
            }
        }

        private void HandlePurificationCompleted(EnemyPurification purification)
        {
            if (purification == null)
            {
                return;
            }

            purification.Completed -= HandlePurificationCompleted;
            activeEnemies.Remove(purification);

            if (activeEnemies.Count == 0)
            {
                AllEnemiesCleared?.Invoke();
            }
        }

      private Transform GetNextSpawnPoint()
{
    // 삭제된 스폰 포인트를 목록에서 제거한다.
    spawnPoints.RemoveAll(point => point == null);

    if (spawnPoints.Count == 0)
    {
        return null;
    }

    // 등록된 스폰 포인트 수만큼 확인한다.
    // 현재 활성화된 포탈 아래의 스폰 포인트만 사용한다.
    for (int i = 0; i < spawnPoints.Count; i++)
    {
        int index = nextSpawnPointIndex % spawnPoints.Count;
        Transform candidate = spawnPoints[index];

        nextSpawnPointIndex =
            (nextSpawnPointIndex + 1) % spawnPoints.Count;

        if (candidate != null &&
            candidate.gameObject.activeInHierarchy)
        {
            return candidate;
        }
    }

    Debug.LogWarning(
        "[DreamEnemySpawner] 현재 활성화된 스폰 포인트가 없습니다.");

    return null;
}

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private void OnValidate()
        {
            baseEnemyHealth = Mathf.Max(1f, baseEnemyHealth);
            energyRewardPerEnemy = Mathf.Max(0f, energyRewardPerEnemy);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            attackRingRadius = Mathf.Max(0.5f, attackRingRadius);
            attackSlotSpreadAngle = Mathf.Max(0f, attackSlotSpreadAngle);
            coreDamage = Mathf.Max(0f, coreDamage);
            attackInterval = Mathf.Max(0.1f, attackInterval);
            enemyGroundOffset = Mathf.Max(0f, enemyGroundOffset);
            riseDepth = Mathf.Max(0f, riseDepth);
            riseDuration = Mathf.Max(0.05f, riseDuration);
        }
    }
}
