using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DreamEnemySpawner : NetworkBehaviour
    {
        // 협동 플레이 동기화: 몬스터는 이제 Runner.Spawn()으로 생성되는
        // 진짜 네트워크 오브젝트다(전에는 Instantiate로 각 클라이언트가
        // 따로 만들어서 서로에게 보이지 않았다). Shared Mode에서는
        // "방장(마스터 클라이언트)" 한 명만 실제로 스폰하고, 나머지
        // 클라이언트는 그 결과를 그대로 받아서 보게 된다 - 그래야
        // 인원수만큼 몬스터가 중복 생성되지 않는다.
        private RoomManager roomManager;

        [Networked] public NetworkId TutorialEnemyId { get; private set; }
        // Retain the attempt even if Spawn throws or the enemy later disappears.
        // Missing replication must never authorize another Spawn.
        [Networked] public NetworkBool TutorialSpawnIssued { get; private set; }
        private bool tutorialSpawnInProgress;

        public bool IsTutorialSessionReady
        {
            get
            {
                var runner = GetRunner();
                return runner != null && Object != null && Object.IsValid && Object.Runner == runner;
            }
        }

        public bool CanSpawnTutorialEnemy => IsTutorialSessionReady &&
            Runner.IsSharedModeMasterClient && Object.HasStateAuthority;

        public bool TryFindTutorialEnemy(out EnemyHealth enemy)
        {
            enemy = null;
            if (!IsTutorialSessionReady || !TutorialEnemyId.IsValid ||
                !Runner.TryFindObject(TutorialEnemyId, out var networkObject)) return false;
            enemy = networkObject.GetComponent<EnemyHealth>();
            return enemy != null;
        }

        private NetworkRunner GetRunner()
        {
            if (roomManager == null)
                roomManager = FindAnyObjectByType<RoomManager>();
            var runner = roomManager != null ? roomManager.Runner : null;
            return runner != null && runner.IsRunning && runner.IsConnectedToServer &&
                runner.GameMode == GameMode.Shared && !runner.IsSceneManagerBusy ? runner : null;
        }
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

        [Header("Police + Firefighter Synergy Audio")]
        [SerializeField] private AudioClip emergencySuppressionSfx;
        [SerializeField, Range(0f, 1f)] private float emergencySuppressionSfxVolume = 0.65f;
        [SerializeField, Min(0.01f)] private float synergyAudioMinDistance = 3f;
        [SerializeField, Min(0.01f)] private float synergyAudioMaxDistance = 30f;
        [SerializeField, Range(0f, 1f)] private float synergyAudioDopplerLevel;

        [Header("Editor Test Damage")]
        [Tooltip("Unity Editor Play Mode에서만 플레이어의 적 대상 피해를 강화합니다.")]
        [SerializeField] private bool enableTestDamageBoost = true;
        [Tooltip("1이면 원래 밸런스이며, 실제 빌드에서는 이 값과 무관하게 항상 1배입니다.")]
        [SerializeField, Min(1f)] private float testDamageMultiplier = 5f;

        [Header("Scene References")]
        [SerializeField] private CoreState targetCore;
        [SerializeField]
        private List<Transform> spawnPoints =
            new List<Transform>();

        private readonly HashSet<EnemyPurification> activeEnemies =
            new HashSet<EnemyPurification>();

        private int nextSpawnPointIndex;
        private int spawnedCombatEnemyCount;
        private int spawnCancellationVersion;

        public int ActiveEnemyCount => activeEnemies.Count;
        public GameObject EnemyPrefab => enemyPrefab;
        public CoreState TargetCore => targetCore;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;

        public event Action<EnemyHealth> EnemySpawned;
        public event Action AllEnemiesCleared;


        private void Awake()
        {
            ApplyEditorTestDamageSettings();
        }


        private void ApplyEditorTestDamageSettings()
        {
#if UNITY_EDITOR
            EnemyHealth.ConfigureEditorTestDamage(
                enableTestDamageBoost,
                testDamageMultiplier
            );
#endif
        }

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
            int cancellationVersion = spawnCancellationVersion;

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

            // 근접/원거리를 각자 독립된 스폰 포인트 커서로 순환시킵니다.
            // 예전에는 하나의 공용 커서(waveSpawnIndex)를 근접/원거리 모두가
            // 같이 썼는데, 타입 교대 주기(예: 12:12 = 2칸마다 교대)가 방향 개수
            // (4)와 딱 맞아떨어지면 짝수 방향은 항상 근접만, 홀수 방향은 항상
            // 원거리만 걸리는 편중이 생겼습니다. 타입별로 커서를 분리하면
            // 어떤 비율/개수여도 각 타입이 4방향에 고르게 퍼집니다.
            int primarySpawnIndex = 0;
            int additionalSpawnIndex = 0;
            int additionalSpawned = 0;
            int additionalAccumulator = 0;

            for (int i = 0; i < safeCount; i++)
            {
                if (cancellationVersion != spawnCancellationVersion)
                {
                    yield break;
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

                Transform selectedSpawnPoint = null;

                if (waveSpawnPoints.Count > 0)
                {
                    int pointIndex;

                    if (spawnAdditional)
                    {
                        pointIndex =
                            additionalSpawnIndex % waveSpawnPoints.Count;

                        additionalSpawnIndex++;
                    }
                    else
                    {
                        pointIndex =
                            primarySpawnIndex % waveSpawnPoints.Count;

                        primarySpawnIndex++;
                    }

                    selectedSpawnPoint =
                        waveSpawnPoints[pointIndex];
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

                SpawnCombatEnemyAtPoint(
                    selectedSpawnPoint,
                    healthMultiplier,
                    spawnAdditional
                        ? additionalPrefab
                        : null);

                if (safeInterval > 0f &&
                    i < safeCount - 1)
                {
                    yield return WaitForSpawnInterval(
                        safeInterval,
                        cancellationVersion);
                }
                else
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 활성화된 각 방향(스폰 포인트)마다 근접/원거리/드론을
        /// 정확히 지정한 마릿수만큼 생성합니다. 방향 수를 라운드로빈으로
        /// 순회하면서 방향별로 남은 근접→원거리→드론 순으로 하나씩
        /// 소진시켜, 모든 방향이 비슷한 타이밍에 끝나도록 합니다.
        /// </summary>
        public IEnumerator SpawnDirectionalMixedGroup(
            GameObject rangedPrefab,
            GameObject dronePrefab,
            int meleePerDirection,
            int rangedPerDirection,
            int dronePerDirection,
            float spawnInterval,
            float healthMultiplier = 1f)
        {
            int cancellationVersion = spawnCancellationVersion;

            List<Transform> waveSpawnPoints =
                GetActiveSpawnPointsSnapshot();

            if (waveSpawnPoints.Count == 0)
            {
                Debug.LogWarning(
                    "[DreamEnemySpawner] 방향별 스폰 시작 시 " +
                    "활성화된 스폰 포인트가 없습니다.",
                    this);
                yield break;
            }

            int directionCount = waveSpawnPoints.Count;

            int safeMelee = Mathf.Max(0, meleePerDirection);
            int safeRanged =
                rangedPrefab != null
                    ? Mathf.Max(0, rangedPerDirection)
                    : 0;
            int safeDrone =
                dronePrefab != null
                    ? Mathf.Max(0, dronePerDirection)
                    : 0;

            int[] remainingMelee = new int[directionCount];
            int[] remainingRanged = new int[directionCount];
            int[] remainingDrone = new int[directionCount];

            for (int d = 0; d < directionCount; d++)
            {
                remainingMelee[d] = safeMelee;
                remainingRanged[d] = safeRanged;
                remainingDrone[d] = safeDrone;
            }

            float safeInterval = Mathf.Max(0f, spawnInterval);
            int totalToSpawn =
                directionCount * (safeMelee + safeRanged + safeDrone);
            int spawned = 0;

            while (spawned < totalToSpawn)
            {
                for (int d = 0; d < directionCount; d++)
                {
                    if (cancellationVersion != spawnCancellationVersion)
                    {
                        yield break;
                    }

                    GameObject prefabOverride;

                    if (remainingMelee[d] > 0)
                    {
                        remainingMelee[d]--;
                        prefabOverride = null;
                    }
                    else if (remainingRanged[d] > 0)
                    {
                        remainingRanged[d]--;
                        prefabOverride = rangedPrefab;
                    }
                    else if (remainingDrone[d] > 0)
                    {
                        remainingDrone[d]--;
                        prefabOverride = dronePrefab;
                    }
                    else
                    {
                        continue;
                    }

                    Transform selectedSpawnPoint =
                        waveSpawnPoints[d];

                    if (selectedSpawnPoint != null &&
                        !selectedSpawnPoint.gameObject.activeInHierarchy)
                    {
                        selectedSpawnPoint = GetNextSpawnPoint();
                    }

                    SpawnCombatEnemyAtPoint(
                        selectedSpawnPoint,
                        healthMultiplier,
                        prefabOverride);

                    spawned++;

                    if (spawned < totalToSpawn)
                    {
                        if (safeInterval > 0f)
                        {
                            yield return WaitForSpawnInterval(
                                safeInterval,
                                cancellationVersion);
                        }
                        else
                        {
                            yield return null;
                        }
                    }
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
            if (!CanSpawnTutorialEnemy || tutorialSpawnInProgress) return null;
            if (TryFindTutorialEnemy(out var existing)) return existing;
            if (TutorialSpawnIssued) return null;
            if (enemyPrefab == null || enemyPrefab.GetComponent<NetworkObject>() == null ||
                enemyPrefab.GetComponent<EnemyHealth>() == null)
            {
                Debug.LogError("[TutorialNetwork] Enemy 프리팹의 NetworkObject/EnemyHealth가 필요합니다.", this);
                return null;
            }

            TutorialSpawnIssued = true;
            tutorialSpawnInProgress = true;
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

            try
            {
                return SpawnEnemy(position, rotation, true, 1f, tutorialSpawnPoint, null);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[TutorialNetwork] Spawn 예외; 중복 생성을 방지하기 위해 재생성하지 않습니다: {exception}", this);
                return null;
            }
            finally
            {
                tutorialSpawnInProgress = false;
            }
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
                Debug.Log(
                    "[F8] DespawnAllEnemiesImmediately" +
                    " / activeEnemies.Count = 0" +
                    " / ActiveEnemyCount = " + ActiveEnemyCount,
                    this);
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
                "[F8] DespawnAllEnemiesImmediately" +
                " / activeEnemies.Count = " + activeEnemies.Count +
                " / ActiveEnemyCount = " + ActiveEnemyCount,
                this);

            Debug.Log(
                "[DreamEnemySpawner] 테스트 진행을 위해 " +
                "활성 적을 모두 즉시 제거했습니다.",
                this);
        }


        private IEnumerator WaitForSpawnInterval(
            float duration,
            int cancellationVersion)
        {
            float elapsed = 0f;

            while (elapsed < duration &&
                   cancellationVersion == spawnCancellationVersion)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }


        /// <summary>
        /// 현재 실행 중인 웨이브 스폰 열거만 종료시킨다.
        /// 이후 시작되는 새 웨이브는 새 버전을 사용하므로 영향을 받지 않는다.
        /// </summary>
        public void CancelCurrentSpawnRoutinesForTest()
        {
            spawnCancellationVersion++;
        }

        private EnemyHealth SpawnEnemy(
            Vector3 position,
            Quaternion rotation,
            bool tutorialEnemy,
            float healthMultiplier,
            Transform spawnPoint,
            GameObject prefabOverride)
        {
            GameObject selectedPrefab =
                prefabOverride != null
                    ? prefabOverride
                    : enemyPrefab;

            if (selectedPrefab == null)
            {
                // 프리팹이 아예 연결되지 않은 개발용 디버그 상황.
                // 네트워크 오브젝트로 만들 방법이 없으므로(에셋이 없음)
                // 예전처럼 이 클라이언트에만 보이는 임시 큐브로 대체한다.
                GameObject fallbackObject =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                fallbackObject.transform.SetPositionAndRotation(
                    position,
                    rotation);

                fallbackObject.transform.localScale =
                    new Vector3(0.8f, 1.6f, 0.8f);

                return ConfigureSpawnedEnemy(
                    fallbackObject,
                    tutorialEnemy,
                    healthMultiplier,
                    spawnPoint);
            }

            NetworkRunner runner = GetRunner();

            if (runner == null || !runner.IsSharedModeMasterClient)
            {
                // 몬스터는 방장(마스터 클라이언트) 한 명만 실제로
                // 스폰한다. 다른 클라이언트에서 호출된 웨이브 트리거는
                // 여기서 조용히 무시된다 - 실제 몬스터는 방장이 스폰한
                // 네트워크 오브젝트를 통해 이미 이 클라이언트에도
                // 똑같이 보인다. (호출부인 SpawnTutorialEnemy /
                // SpawnCombatEnemyAtPosition은 null 반환을 이미
                // 안전하게 처리하고 있고, SpawnMixedGroup 등 웨이브
                // 스폰 루프는 반환값을 쓰지 않는다.)
                return null;
            }

            EnemyHealth spawnedHealth = null;

            var spawnedObject = runner.Spawn(
                selectedPrefab,
                position,
                rotation,
                PlayerRef.None,
                (spawnRunner, networkObject) =>
                {
                    spawnedHealth = ConfigureSpawnedEnemy(
                        networkObject.gameObject,
                        tutorialEnemy,
                        healthMultiplier,
                        spawnPoint);
                });

            if (tutorialEnemy && spawnedObject != null)
            {
                TutorialEnemyId = spawnedObject.Id;
                Debug.Log($"[TutorialNetwork] Spawn master={runner.LocalPlayer}, enemy={TutorialEnemyId}", this);
            }

            return spawnedHealth;
        }

        /// <summary>
        /// Instantiate/Runner.Spawn 어느 경로로 만들어졌든, 생성된
        /// enemyObject 하나에 필요한 컴포넌트를 구성하고 초기화한다.
        /// EnemyHealth만 예외적으로 "이미 프리팹에 붙어 있어야" 한다 -
        /// Fusion은 NetworkBehaviour를 런타임에 AddComponent로 붙이는
        /// 것을 지원하지 않기 때문이다(다른 컴포넌트들은 평범한
        /// MonoBehaviour라 예전처럼 여기서 동적으로 붙여도 된다).
        /// </summary>
        private EnemyHealth ConfigureSpawnedEnemy(
            GameObject enemyObject,
            bool tutorialEnemy,
            float healthMultiplier,
            Transform spawnPoint)
        {
            Vector3 position = enemyObject.transform.position;

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

            // EnemyHealth는 NetworkBehaviour라서 GetOrAdd(런타임
            // AddComponent)로 붙일 수 없다 - 프리팹 자체에 미리 붙어
            // 있어야 한다(팀원이 에디터에서 NetworkObject/
            // NetworkTransform과 함께 미리 설정).
            EnemyHealth health =
                enemyObject.GetComponent<EnemyHealth>();

            if (health == null)
            {
                Debug.LogError(
                    $"[DreamEnemySpawner] '{enemyObject.name}' 프리팹에 " +
                    "EnemyHealth 컴포넌트가 없습니다. 이 적 프리팹 에셋에 " +
                    "NetworkObject + NetworkTransform + EnemyHealth를 " +
                    "미리 붙여야 협동 플레이에서 정상 동작합니다.",
                    enemyObject);

                return null;
            }

            RoleSynergyTracker synergyTracker =
                GetOrAdd<RoleSynergyTracker>(
                    enemyObject);

            synergyTracker.ConfigureAudio(
                emergencySuppressionSfx,
                emergencySuppressionSfxVolume,
                synergyAudioMinDistance,
                synergyAudioMaxDistance,
                synergyAudioDopplerLevel
            );

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
            testDamageMultiplier =
                Mathf.Max(1f, testDamageMultiplier);

            if (Application.isPlaying)
            {
                ApplyEditorTestDamageSettings();
            }

            synergyAudioMinDistance =
                Mathf.Max(0.01f, synergyAudioMinDistance);

            synergyAudioMaxDistance =
                Mathf.Max(
                    synergyAudioMinDistance,
                    synergyAudioMaxDistance
                );

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
