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
        [SerializeField, Min(0f)] private float coreDamage = 1f;
        [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;

        [Header("Floor Rift Spawn")]
        [SerializeField] private bool useFloorRift = true;
        [SerializeField, Min(0f)] private float enemyGroundOffset = 0.8f;
        [SerializeField, Min(0f)] private float riseDepth = 0.9f;
        [SerializeField, Min(0.05f)] private float riseDuration = 1.25f;

        [Header("Scene References")]
        [SerializeField] private CoreState targetCore;
        [SerializeField]
        private List<Transform> spawnPoints =
            new List<Transform>();

        private readonly HashSet<EnemyPurification> activeEnemies =
            new HashSet<EnemyPurification>();

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

            if (points == null)
            {
                return;
            }

            foreach (Transform point in points)
            {
                if (point != null)
                {
                    spawnPoints.Add(point);
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
            coreDamage = 1f;
            attackInterval = 1.5f;

            useFloorRift = true;
            enemyGroundOffset = 0.8f;
            riseDepth = 0.9f;
            riseDuration = 1.25f;
        }

        /// <summary>
        /// 웨이브가 시작되는 순간 활성화된 스폰 포인트를 고정하고
        /// 해당 웨이브가 끝날 때까지 그 목록만 사용합니다.
        /// </summary>
        public IEnumerator SpawnGroup(
            int enemyCount,
            float spawnInterval,
            float healthMultiplier = 1f)
        {
            return SpawnMixedGroup(
                null,
                enemyCount,
                0,
                spawnInterval,
                healthMultiplier);
        }

        /// <summary>
        /// 기본 근접 적과 추가 프리팹을 한 웨이브 안에서 고르게 섞어
        /// 동일한 포탈과 전투 추적 시스템으로 생성합니다.
        /// </summary>
        public IEnumerator SpawnMixedGroup(
            GameObject additionalPrefab,
            int primaryEnemyCount,
            int additionalEnemyCount,
            float spawnInterval,
            float healthMultiplier = 1f)
        {
            int safePrimaryCount =
                Mathf.Max(0, primaryEnemyCount);

            int safeAdditionalCount =
                additionalPrefab != null
                    ? Mathf.Max(0, additionalEnemyCount)
                    : 0;

            if (additionalPrefab == null &&
                additionalEnemyCount > 0)
            {
                Debug.LogWarning(
                    "[DreamEnemySpawner] 추가 적 프리팹이 연결되지 않아 " +
                    "기본 근접 적만 생성합니다.",
                    this);
            }

            int safeCount =
                safePrimaryCount + safeAdditionalCount;

            float safeInterval = Mathf.Max(0f, spawnInterval);

            List<Transform> waveSpawnPoints =
                GetActiveSpawnPointsSnapshot();

            if (waveSpawnPoints.Count == 0)
            {
                Debug.LogWarning(
                    "[DreamEnemySpawner] 웨이브 시작 시 " +
                    "활성화된 스폰 포인트가 없습니다.",
                    this);
            }
            else
            {
                Debug.Log(
                    "[DreamEnemySpawner] 이번 웨이브에서 사용할 " +
                    "스폰 포인트를 고정했습니다. 개수: " +
                    waveSpawnPoints.Count,
                    this);
            }

            int waveSpawnIndex = 0;
            int additionalSpawned = 0;
            int additionalAccumulator = 0;

            for (int i = 0; i < safeCount; i++)
            {
                Transform selectedSpawnPoint = null;

                if (waveSpawnPoints.Count > 0)
                {
                    int pointIndex =
                        waveSpawnIndex % waveSpawnPoints.Count;

                    selectedSpawnPoint =
                        waveSpawnPoints[pointIndex];

                    waveSpawnIndex++;
                }

                // 웨이브 중 포탈이 강제로 꺼진 경우를 대비합니다.
                if (selectedSpawnPoint != null &&
                    !selectedSpawnPoint.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning(
                        "[DreamEnemySpawner] 웨이브에 저장된 스폰 포인트가 " +
                        "비활성화되어 현재 활성 포인트를 다시 찾습니다: " +
                        selectedSpawnPoint.name,
                        selectedSpawnPoint);

                    selectedSpawnPoint =
                        GetNextSpawnPoint();
                }

                additionalAccumulator +=
                    safeAdditionalCount;

                bool spawnAdditional =
                    additionalSpawned < safeAdditionalCount &&
                    additionalAccumulator >= safeCount;

                if (spawnAdditional)
                {
                    additionalAccumulator -= safeCount;
                    additionalSpawned++;
                }

                SpawnCombatEnemyAtPoint(
                    selectedSpawnPoint,
                    healthMultiplier,
                    spawnAdditional
                        ? additionalPrefab
                        : null);

                if (safeInterval > 0f &&
                    i < safeCount - 1)
                {
                    yield return new WaitForSeconds(
                        safeInterval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        private List<Transform> GetActiveSpawnPointsSnapshot()
        {
            spawnPoints.RemoveAll(point => point == null);

            List<Transform> activePoints =
                new List<Transform>();

            foreach (Transform point in spawnPoints)
            {
                if (point != null &&
                    point.gameObject.activeInHierarchy)
                {
                    activePoints.Add(point);
                }
            }

            return activePoints;
        }

        private EnemyHealth SpawnCombatEnemyAtPoint(
            Transform spawnPoint,
            float healthMultiplier,
            GameObject prefabOverride = null)
        {
            Vector3 position;
            Quaternion rotation;

            if (spawnPoint != null)
            {
                position =
                    spawnPoint.position +
                    Vector3.up * enemyGroundOffset;

                rotation = spawnPoint.rotation;
            }
            else
            {
                position =
                    transform.position +
                    transform.forward * 10f +
                    Vector3.up * enemyGroundOffset;

                rotation = transform.rotation;
            }

            return SpawnEnemy(
                position,
                rotation,
                false,
                healthMultiplier,
                spawnPoint,
                prefabOverride);
        }

        /// <summary>
        /// 보스처럼 포탈이 아닌 임의의 월드 위치에서 전투 적을 생성합니다.
        /// prefabOverride가 null이면 기본 근접 적을 사용합니다.
        /// </summary>
        public EnemyHealth SpawnCombatEnemyAtPosition(
            Vector3 position,
            Quaternion rotation,
            GameObject prefabOverride = null,
            float healthMultiplier = 1f)
        {
            return SpawnEnemy(
                position,
                rotation,
                false,
                healthMultiplier,
                null,
                prefabOverride);
        }


        public EnemyHealth SpawnCombatEnemy(
            float healthMultiplier = 1f)
        {
            Transform spawnPoint =
                GetNextSpawnPoint();

            Vector3 position =
                spawnPoint != null
                    ? spawnPoint.position +
                      Vector3.up * enemyGroundOffset
                    : transform.position +
                      transform.forward * 10f +
                      Vector3.up * enemyGroundOffset;

            Quaternion rotation =
                spawnPoint != null
                    ? spawnPoint.rotation
                    : transform.rotation;

            return SpawnEnemy(
                position,
                rotation,
                false,
                healthMultiplier,
                spawnPoint,
                null);
        }

        public EnemyHealth SpawnTutorialEnemy(
            Transform tutorialSpawnPoint)
        {
            Vector3 position =
                tutorialSpawnPoint != null
                    ? tutorialSpawnPoint.position +
                      Vector3.up * enemyGroundOffset
                    : transform.position +
                      transform.forward * 9f +
                      Vector3.up * enemyGroundOffset;

            Quaternion rotation =
                tutorialSpawnPoint != null
                    ? tutorialSpawnPoint.rotation
                    : transform.rotation;

            return SpawnEnemy(
                position,
                rotation,
                true,
                1f,
                tutorialSpawnPoint,
                null);
        }

        /// <summary>
        /// 테스트 스킵 시 튜토리얼 몹을 즉시 제거합니다.
        ///
        /// 단순히 Destroy만 하면 activeEnemies에 정보가 남아
        /// Stage 1 완료 판정이 막힐 수 있으므로 목록에서도 제거합니다.
        /// </summary>
        public void DespawnEnemyImmediately(
            EnemyHealth enemy)
        {
            if (enemy == null)
            {
                return;
            }

            EnemyPurification purification =
                enemy.GetComponent<EnemyPurification>();

            if (purification != null)
            {
                purification.Completed -=
                    HandlePurificationCompleted;

                activeEnemies.Remove(
                    purification);
            }

            Destroy(enemy.gameObject);

            if (activeEnemies.Count == 0)
            {
                AllEnemiesCleared?.Invoke();
            }

            Debug.Log(
                "[DreamEnemySpawner] 테스트 진행을 위해 " +
                "적을 즉시 제거했습니다.",
                this);
        }


        public void DespawnAllEnemiesImmediately()
        {
            if (activeEnemies.Count == 0)
            {
                return;
            }

            EnemyPurification[] enemies =
                new EnemyPurification[activeEnemies.Count];
            activeEnemies.CopyTo(enemies);

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyPurification purification = enemies[i];
                if (purification == null)
                {
                    continue;
                }

                purification.Completed -= HandlePurificationCompleted;
                activeEnemies.Remove(purification);
                Destroy(purification.gameObject);
            }

            if (activeEnemies.Count == 0)
            {
                AllEnemiesCleared?.Invoke();
            }

            Debug.Log(
                "[DreamEnemySpawner] 테스트 진행을 위해 " +
                "활성 적을 모두 즉시 제거했습니다.",
                this);
        }

        private EnemyHealth SpawnEnemy(
            Vector3 position,
            Quaternion rotation,
            bool tutorialEnemy,
            float healthMultiplier,
            Transform spawnPoint,
            GameObject prefabOverride)
        {
            GameObject enemyObject;
            GameObject selectedPrefab =
                prefabOverride != null
                    ? prefabOverride
                    : enemyPrefab;

            if (selectedPrefab != null)
            {
                enemyObject =
                    Instantiate(
                        selectedPrefab,
                        position,
                        rotation);
            }
            else
            {
                enemyObject =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                enemyObject.transform.SetPositionAndRotation(
                    position,
                    rotation);

                enemyObject.transform.localScale =
                    new Vector3(
                        0.8f,
                        1.6f,
                        0.8f);
            }

            enemyObject.name =
                tutorialEnemy
                    ? "TutorialEnemy"
                    : "WaveEnemy";

            DroneEnemyWaspy droneEnemy =
                enemyObject.GetComponent<DroneEnemyWaspy>();

            if (droneEnemy != null && !tutorialEnemy)
            {
                enemyObject.name = "WaveEnemy_DroneWaspy";

                // 원본 Waspy 프리팹에 Collider가 없어도
                // 기존 무기 Raycast가 드론을 맞힐 수 있게 합니다.
                droneEnemy.EnsureHitCollider();
            }

            if (tutorialEnemy)
            {
                MakeTutorialEnemyHighlyVisible(
                    enemyObject);
            }

            EnemyHealth health =
                GetOrAdd<EnemyHealth>(
                    enemyObject);

            GetOrAdd<RoleSynergyTracker>(
                enemyObject);

            GetOrAdd<EnemyWorldHealthBar>(
                enemyObject);

            EnemyCoreMover mover =
                GetOrAdd<EnemyCoreMover>(
                    enemyObject);

            // 원거리 미니건 적은 프리팹에 붙은 전용 설정값을 사용합니다.
            // 해당 컴포넌트가 없으면 기존 근접 적 설정을 그대로 유지합니다.
            RangedMinigunEnemy rangedEnemy =
                enemyObject.GetComponent<RangedMinigunEnemy>();

            if (droneEnemy != null && !tutorialEnemy)
            {
                // 비행 높이와 원거리 공격은 드론 전용 컴포넌트가
                // 처리하므로 지상 적 이동 컴포넌트는 실행하지 않습니다.
                mover.enabled = false;
                droneEnemy.Configure(targetCore);
            }
            else if (tutorialEnemy)
            {
                mover.Configure(
                    targetCore,
                    0f,
                    0f,
                    attackInterval);

                mover.enabled = false;

                StartTutorialRiftSpawn(
                    enemyObject,
                    mover,
                    spawnPoint,
                    position);
            }
            else
            {
                float configuredMoveSpeed =
                    rangedEnemy != null
                        ? rangedEnemy.MoveSpeed
                        : moveSpeed;

                // 원거리 미니건은 실제 탄환이 코어에 도착했을 때
                // RangedMinigunEnemy가 데미지를 처리합니다.
                // EnemyCoreMover의 즉시 데미지는 0으로 막아 중복 피해를 방지합니다.
                float configuredCoreDamage =
                    rangedEnemy != null
                        ? 0f
                        : coreDamage;

                float configuredAttackInterval =
                    rangedEnemy != null
                        ? rangedEnemy.AttackInterval
                        : attackInterval;

                float configuredAttackRingRadius =
                    rangedEnemy != null
                        ? rangedEnemy.AttackRange
                        : attackRingRadius;

                float configuredModelYawOffset =
                    rangedEnemy != null
                        ? rangedEnemy.ModelYawOffset
                        : 0f;

                Vector3 attackDestination =
                    CalculateAttackDestination(
                        position,
                        configuredAttackRingRadius);

                mover.Configure(
                    targetCore,
                    attackDestination,
                    configuredMoveSpeed,
                    configuredCoreDamage,
                    configuredAttackInterval,
                    configuredModelYawOffset);

                // 원거리 미니건 적만 접근 중 좌우로 흔들리고, 도착해서 쏠 때
                // 그 자리에서 사이드스텝하게 합니다. 근접 로봇은 대상이 아닙니다.
                if (rangedEnemy != null)
                {
                    // 처음 값(1.2, 1.2)은 너무 빠르게 흔들려서 반으로 줄였습니다.
                    mover.SetZigzagMovement(true, 0.7f, 0.55f);
                }

                StartRiftSpawn(
                    enemyObject,
                    mover,
                    spawnPoint,
                    position);
            }

            if (droneEnemy != null && !tutorialEnemy)
            {
                // 과거 저장본에 근접 전용 모션이 붙어 있어도
                // 드론 Animator와 충돌하지 않게 합니다.
                ToyRobotMotion oldRobotMotion =
                    enemyObject.GetComponent<ToyRobotMotion>();

                if (oldRobotMotion != null)
                {
                    oldRobotMotion.enabled = false;
                }
            }
            else if (rangedEnemy != null)
            {
                // 이동/공격 위치 판정은 EnemyCoreMover가 담당하고,
                // 실제 코어 피해는 원거리 전용 탄환이 담당합니다.
                rangedEnemy.Configure(mover);

                // 과거 저장본에 근접 전용 모션이 붙어 있어도 충돌하지 않게 합니다.
                ToyRobotMotion oldRobotMotion =
                    enemyObject.GetComponent<ToyRobotMotion>();

                if (oldRobotMotion != null)
                {
                    oldRobotMotion.enabled = false;
                }
            }
            else
            {
                // 기존 근접 로봇은 파츠 기반 걷기 동작을 그대로 사용합니다.
                ToyRobotMotion robotMotion =
                    GetOrAdd<ToyRobotMotion>(
                        enemyObject);

                robotMotion.enabled = true;
                robotMotion.ForceInitialize();
            }


            EnemyPurification purification =
                GetOrAdd<EnemyPurification>(
                    enemyObject);

            purification.Configure(
                targetCore,
                energyRewardPerEnemy);

            purification.Completed +=
                HandlePurificationCompleted;

            float configuredHealth =
                baseEnemyHealth *
                Mathf.Max(
                    0.1f,
                    healthMultiplier);

            health.Configure(
                configuredHealth,
                !tutorialEnemy);

            activeEnemies.Add(
                purification);

            EnemySpawned?.Invoke(
                health);

            return health;
        }

        private Vector3 CalculateAttackDestination(
            Vector3 spawnPosition,
            float desiredRingRadius)
        {
            if (targetCore == null)
            {
                return spawnPosition;
            }

            Vector3 corePosition =
                targetCore.transform.position;

            Vector3 outwardDirection =
                spawnPosition - corePosition;

            outwardDirection.y = 0f;

            if (outwardDirection.sqrMagnitude <=
                0.0001f)
            {
                outwardDirection =
                    Vector3.forward;
            }

            outwardDirection.Normalize();

            int spreadIndex =
                spawnedCombatEnemyCount % 3 - 1;

            spawnedCombatEnemyCount++;

            float spread =
                spreadIndex *
                attackSlotSpreadAngle;

            outwardDirection =
                Quaternion.Euler(
                    0f,
                    spread,
                    0f) *
                outwardDirection;

            Vector3 destination =
                corePosition +
                outwardDirection *
                Mathf.Max(0.5f, desiredRingRadius);

            destination.y =
                spawnPosition.y;

            return destination;
        }

        private void StartTutorialRiftSpawn(
            GameObject enemyObject,
            EnemyCoreMover mover,
            Transform spawnPoint,
            Vector3 finalPosition)
        {
            if (!useFloorRift ||
                spawnPoint == null ||
                !spawnPoint.gameObject.activeInHierarchy)
            {
                mover.enabled = false;
                return;
            }

            FloorRiftMarker rift =
                GetOrAdd<FloorRiftMarker>(
                    spawnPoint.gameObject);

            EnemySpawnRise rise =
                GetOrAdd<EnemySpawnRise>(
                    enemyObject);

            rise.Begin(
                finalPosition,
                riseDepth,
                riseDuration,
                mover,
                rift,
                enableMoverAfterRise: false,
                usePortalDirection: false);
        }

        private void StartRiftSpawn(
            GameObject enemyObject,
            EnemyCoreMover mover,
            Transform spawnPoint,
            Vector3 finalPosition)
        {
            if (!useFloorRift ||
                spawnPoint == null ||
                !spawnPoint.gameObject.activeInHierarchy)
            {
                mover.enabled = true;

                if (spawnPoint != null &&
                    !spawnPoint.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning(
                        "[DreamEnemySpawner] 비활성 스폰 포인트에서 " +
                        "등장 연출을 실행하지 않습니다: " +
                        spawnPoint.name,
                        spawnPoint);
                }

                return;
            }

            FloorRiftMarker rift =
                GetOrAdd<FloorRiftMarker>(
                    spawnPoint.gameObject);

            EnemySpawnRise rise =
                GetOrAdd<EnemySpawnRise>(
                    enemyObject);

            Vector3 portalForward =
                spawnPoint.forward;

            rise.Begin(
                finalPosition,
                riseDepth,
                riseDuration,
                mover,
                rift,
                enableMoverAfterRise: true,
                usePortalDirection: true,
                portalForward: portalForward);
        }

        private static void MakeTutorialEnemyHighlyVisible(
            GameObject enemyObject)
        {
            if (enemyObject == null)
            {
                return;
            }

            enemyObject.transform.localScale =
                Vector3.one * 1.5f;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            shader ??=
                Shader.Find("Unlit/Color");

            shader ??=
                Shader.Find("Standard");

            if (shader == null)
            {
                return;
            }

            Color visibleColor =
                new Color(
                    1f,
                    0.08f,
                    0.65f,
                    1f);

            Material material =
                new Material(shader)
                {
                    name =
                        "TutorialEnemy_Visible_Runtime",

                    color =
                        visibleColor
                };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    visibleColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");

                material.SetColor(
                    "_EmissionColor",
                    visibleColor * 2f);
            }

            foreach (
                Renderer targetRenderer
                in enemyObject
                    .GetComponentsInChildren<Renderer>(true))
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.sharedMaterial =
                    material;

                targetRenderer.shadowCastingMode =
                    UnityEngine.Rendering
                        .ShadowCastingMode.Off;

                targetRenderer.receiveShadows = false;
                targetRenderer.enabled = true;
            }
        }

        private void HandlePurificationCompleted(
            EnemyPurification purification)
        {
            if (purification == null)
            {
                return;
            }

            purification.Completed -=
                HandlePurificationCompleted;

            activeEnemies.Remove(
                purification);

            if (activeEnemies.Count == 0)
            {
                AllEnemiesCleared?.Invoke();
            }
        }

        private Transform GetNextSpawnPoint()
        {
            spawnPoints.RemoveAll(
                point => point == null);

            if (spawnPoints.Count == 0)
            {
                return null;
            }

            for (int i = 0;
                 i < spawnPoints.Count;
                 i++)
            {
                int index =
                    nextSpawnPointIndex %
                    spawnPoints.Count;

                Transform candidate =
                    spawnPoints[index];

                nextSpawnPointIndex =
                    (nextSpawnPointIndex + 1) %
                    spawnPoints.Count;

                if (candidate != null &&
                    candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            Debug.LogWarning(
                "[DreamEnemySpawner] 현재 활성화된 " +
                "스폰 포인트가 없습니다.",
                this);

            return null;
        }

        private static T GetOrAdd<T>(
            GameObject target)
            where T : Component
        {
            T component =
                target.GetComponent<T>();

            return component != null
                ? component
                : target.AddComponent<T>();
        }

        private void OnValidate()
        {
            baseEnemyHealth =
                Mathf.Max(1f, baseEnemyHealth);

            energyRewardPerEnemy =
                Mathf.Max(0f, energyRewardPerEnemy);

            moveSpeed =
                Mathf.Max(0f, moveSpeed);

            attackRingRadius =
                Mathf.Max(0.5f, attackRingRadius);

            attackSlotSpreadAngle =
                Mathf.Max(0f, attackSlotSpreadAngle);

            coreDamage =
                Mathf.Max(0f, coreDamage);

            attackInterval =
                Mathf.Max(0.1f, attackInterval);

            enemyGroundOffset =
                Mathf.Max(0f, enemyGroundOffset);

            riseDepth =
                Mathf.Max(0f, riseDepth);

            riseDuration =
                Mathf.Max(0.05f, riseDuration);
        }

        public void SpawnOneEnemyAfterPortal()
        {
            SpawnCombatEnemy(1f);

            Debug.Log(
                "[DreamEnemySpawner] 포탈 등장 완료 후 " +
                "적 1마리 생성",
                this);
        }
    }
}
