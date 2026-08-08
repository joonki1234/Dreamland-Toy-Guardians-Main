using System;
using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 최종 선물상자 보스의 등장, 페이즈, 하수인 소환, 전투 UI,
/// 처치/실패 판정을 담당합니다.
///
/// 보스전 컨셉:
/// 오염된 장난감을 계속 만들어내는 선물상자가 성을 부수고 등장합니다.
/// 보스는 근접/원거리/비행 적을 계속 생성하며 저항하고,
/// HP가 2/3, 1/3 남는 지점마다 코어 쪽으로 날뛰며 접근합니다.
/// 상자를 파괴하면 보스가 생성한 적들도 함께 사라집니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalBossDirector : MonoBehaviour
{
    public enum FinalBossState
    {
        Idle,
        Intro,
        Fighting,
        Defeating,
        Completed,
        Failed
    }

    [Header("References")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [SerializeField]
    private MissionBannerUI missionUI;

    [SerializeField]
    private CoreState core;

    [SerializeField]
    private DreamEnemySpawner enemySpawner;

    [Header("Boss Spawn / Castle")]
    [SerializeField]
    private GameObject bossPrefab;

    [SerializeField]
    private Transform bossSpawnPoint;

    [Tooltip("맵의 Castle 오브젝트. 연결되어 있으면 기존 BossSpawnPoint보다 우선합니다.")]
    [SerializeField]
    private Transform castleAnchor;

    [SerializeField]
    private bool createPrototypeBossWhenPrefabMissing = true;

    [Min(1f)]
    [SerializeField]
    private float fallbackSpawnDistance = 12f;

    [SerializeField]
    private Vector3 prototypeBossScale = new Vector3(2.5f, 2.5f, 2.5f);

    [Tooltip("보스 프리팹/프로토타입 전체 크기에 곱할 배율")]
    [SerializeField, Min(1f)]
    private float bossScaleMultiplier = 2.6f;

    [Tooltip("성 위치에서 코어 방향으로 얼마나 앞으로 당겨서 스폰할지")]
    [SerializeField, Min(0f)]
    private float castleSpawnForwardOffset = 12f;

    [Tooltip("루트 보스 피격 판정을 더 쉽게 하기 위한 히트박스 배율")]
    [SerializeField, Min(1f)]
    private float easyHitboxMultiplier = 1.12f;

    [SerializeField, Min(0.5f)]
    private float minimumHitboxRadius = 1.25f;

    [Header("Castle Break Intro")]
    [SerializeField, Min(0.1f)]
    private float castleBreakDuration = 0.65f;

    [SerializeField, Min(0f)]
    private float castleDebrisLifetime = 2.8f;

    [SerializeField, Min(1)]
    private int castleDebrisCount = 48;

    [SerializeField, Min(0.1f)]
    private float bossRevealDuration = 0.8f;

    [Header("Boss Stats")]
    [Min(1f)]
    [SerializeField]
    private float bossMaxHealth = 500f;

    [Min(0f)]
    [SerializeField]
    private float bossCoreDamage = 20f;

    [Min(0.1f)]
    [SerializeField]
    private float bossAttackInterval = 2.5f;

    [Min(0f)]
    [SerializeField]
    private float firstAttackDelay = 4f;

    [Header("Boss Minion Spawning")]
    [Tooltip("Stage 1에서 사용하던 미니건 원거리 적")]
    [SerializeField]
    private GameObject rangedEnemyPrefab;

    [Tooltip("Stage 2에서 사용하던 Waspy 비행 적")]
    [SerializeField]
    private GameObject droneEnemyPrefab;

    [SerializeField, Min(0.5f)]
    private float minionSpawnInterval = 3.5f;

    [SerializeField, Min(0f)]
    private float firstMinionSpawnDelay = 3.0f;

    [SerializeField, Min(1)]
    private int maxActiveMinions = 10;

    [SerializeField, Min(0.1f)]
    private float minionHealthMultiplier = 1.15f;

    [SerializeField, Min(0.5f)]
    private float minionSpawnRadius = 7.5f;

    [SerializeField, Min(0f)]
    private float minionGroundHeight = 0.8f;

    [SerializeField, Min(0f)]
    private float droneSpawnHeight = 2.5f;

    [Header("Boss UI")]
    [SerializeField]
    private string introTitle = "FINAL BOSS";

    [SerializeField]
    private string introSubtitle = "오염된 장난감 상자를 파괴하라";

    [SerializeField]
    private string objectiveText = "오염된 상자를 파괴하고 코어를 지켜라";

    [SerializeField]
    private string introSpeaker = "장난감 친구";

    [TextArea(2, 4)]
    [SerializeField]
    private string introMessage =
        "저 상자가 오염된 장난감을 계속 만들어내고 있어! 상자를 부수면 바이러스도 사라질 거야!";

    [SerializeField]
    private string defeatedTitle = "BOSS DEFEATED";

    [SerializeField]
    private string defeatedSubtitle = "오염의 근원이 정화되었습니다";

    [TextArea(2, 4)]
    [SerializeField]
    private string defeatedMessage =
        "상자가 부서졌어! 오염된 기운도 사라지고 있어!";

    [SerializeField]
    private string failedTitle = "MISSION FAILED";

    [SerializeField]
    private string failedSubtitle = "최종 전투에서 코어가 무너졌습니다";

    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float introDuration = 3f;

    [Min(0f)]
    [SerializeField]
    private float defeatDuration = 3f;

    [Min(0f)]
    [SerializeField]
    private float defeatVisualDuration = 1.5f;

    [Header("Runtime")]
    [SerializeField]
    private FinalBossState currentState = FinalBossState.Idle;

    private Coroutine bossRoutine;
    private Coroutine minionRoutine;
    private GameObject bossObject;
    private EnemyHealth bossHealth;
    private FinalBossAttackController bossAttack;
    private bool bossDefeatedEventRaised;
    private bool bossFailedEventRaised;
    private bool firstPhaseAdvanceTriggered;
    private bool secondPhaseAdvanceTriggered;
    private int minionSpawnIndex;
    private bool hasCachedCastleSpawnPose;
    private Vector3 cachedCastleSpawnPosition;
    private Quaternion cachedCastleSpawnRotation;

    private readonly List<EnemyHealth> bossSpawnedEnemies =
        new List<EnemyHealth>();

    private static Material summonBurstMaterial;
    private static Material fallbackDebrisMaterial;

    public FinalBossState CurrentState => currentState;
    public EnemyHealth BossHealth => bossHealth;

    public event Action BossDefeated;
    public event Action BossFailed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopBossRoutine();
        StopMinionRoutine();
        UnsubscribeBossHealth();
        CleanupBossObject();
    }

    private void ResolveReferences()
    {
        if (gameFlowController == null)
        {
            gameFlowController =
                UnityEngine.Object.FindAnyObjectByType<DreamlandGameFlowController>();
        }

        if (missionUI == null)
        {
            missionUI =
                UnityEngine.Object.FindAnyObjectByType<MissionBannerUI>();
        }

        if (core == null)
        {
            core = UnityEngine.Object.FindAnyObjectByType<CoreState>();
        }

        if (enemySpawner == null)
        {
            enemySpawner =
                UnityEngine.Object.FindAnyObjectByType<DreamEnemySpawner>();
        }

        if (castleAnchor == null)
        {
            GameObject castle = GameObject.Find("Castle");
            if (castle != null)
            {
                castleAnchor = castle.transform;
            }
        }
    }

    private void SubscribeEvents()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
            gameFlowController.OnStateChanged += HandleStateChanged;
        }

        if (core != null)
        {
            core.CoreDestroyed -= HandleCoreDestroyed;
            core.CoreDestroyed += HandleCoreDestroyed;
        }
    }

    private void UnsubscribeEvents()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }

        if (core != null)
        {
            core.CoreDestroyed -= HandleCoreDestroyed;
        }
    }

    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        if (newState == DreamlandGameFlowController.GameFlowState.BossBattle)
        {
            BeginBossBattle();
            return;
        }

        if (newState == DreamlandGameFlowController.GameFlowState.GameOver)
        {
            StopBossRoutine();
            StopMinionRoutine();

            if (bossAttack != null)
            {
                bossAttack.enabled = false;
            }
        }
    }

    public void BeginBossBattle()
    {
        if (currentState == FinalBossState.Intro ||
            currentState == FinalBossState.Fighting ||
            currentState == FinalBossState.Defeating)
        {
            Debug.LogWarning(
                "[FinalBoss] 보스전이 이미 진행 중입니다.",
                this);
            return;
        }

        ResolveReferences();

        bossDefeatedEventRaised = false;
        bossFailedEventRaised = false;
        firstPhaseAdvanceTriggered = false;
        secondPhaseAdvanceTriggered = false;
        minionSpawnIndex = 0;

        StopBossRoutine();
        StopMinionRoutine();
        CleanupBossSpawnedEnemies();
        UnsubscribeBossHealth();
        CleanupBossObject();

        if (castleAnchor != null &&
            !castleAnchor.gameObject.activeSelf)
        {
            castleAnchor.gameObject.SetActive(true);
        }

        if (core != null && core.IsDestroyed)
        {
            FailBossBattle();
            return;
        }

        bossRoutine = StartCoroutine(BossIntroRoutine());
    }

    private IEnumerator BossIntroRoutine()
    {
        currentState = FinalBossState.Intro;

        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            introTitle,
            introSubtitle,
            Mathf.Max(0.1f, introDuration));

        if (!string.IsNullOrWhiteSpace(introMessage))
        {
            missionUI?.ShowDialogue(
                introSpeaker,
                introMessage,
                Mathf.Max(0.1f, introDuration));
        }

        float introElapsed = 0f;
        CacheCastleBossPose();

        if (castleAnchor != null &&
            castleAnchor.gameObject.activeInHierarchy)
        {
            float start = Time.time;
            yield return CastleBreakRoutine();
            introElapsed += Time.time - start;
        }

        bossObject = SpawnBossObject();
        if (bossObject == null)
        {
            Debug.LogError(
                "[FinalBoss] 보스 오브젝트를 생성하지 못했습니다.",
                this);
            FailBossBattle();
            yield break;
        }

        ConfigureBossComponents();

        float revealStart = Time.time;
        yield return BossRevealRoutine();
        introElapsed += Time.time - revealStart;

        float remainingIntro = Mathf.Max(0f, introDuration - introElapsed);
        if (remainingIntro > 0f)
        {
            yield return new WaitForSeconds(remainingIntro);
        }

        if (currentState != FinalBossState.Intro ||
            bossHealth == null ||
            bossHealth.IsDead)
        {
            bossRoutine = null;
            yield break;
        }

        currentState = FinalBossState.Fighting;
        bossHealth.SetDamageEnabled(true);

        if (bossAttack != null)
        {
            bossAttack.Configure(
                core,
                bossCoreDamage,
                bossAttackInterval,
                firstAttackDelay);
        }

        StartMinionRoutine();

        missionUI?.SetObjective(objectiveText);
        RefreshBossProgress();

        Debug.Log(
            "[FinalBoss] 오염된 선물상자 보스전 시작. 보스 HP: " +
            bossMaxHealth.ToString("0") +
            " / 근접·원거리·비행 적 순환 소환 활성화",
            this);

        bossRoutine = null;
    }

    private IEnumerator CastleBreakRoutine()
    {
        if (castleAnchor == null)
        {
            yield break;
        }

        GameObject castleObject = castleAnchor.gameObject;
        Bounds castleBounds = CalculateRendererBounds(castleAnchor);

        CreateCastleDebrisEffect(castleBounds, castleAnchor);

        Vector3 originalLocalPosition = castleAnchor.localPosition;
        Quaternion originalLocalRotation = castleAnchor.localRotation;
        Vector3 originalLocalScale = castleAnchor.localScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, castleBreakDuration);

        while (elapsed < duration && castleObject != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float shake = (1f - t) * 0.22f;

            castleAnchor.localPosition =
                originalLocalPosition +
                new Vector3(
                    Mathf.Sin(elapsed * 58f) * shake,
                    Mathf.Abs(Mathf.Sin(elapsed * 43f)) * shake * 0.35f,
                    Mathf.Cos(elapsed * 51f) * shake);

            castleAnchor.localRotation =
                originalLocalRotation *
                Quaternion.Euler(
                    Mathf.Sin(elapsed * 37f) * shake * 8f,
                    Mathf.Sin(elapsed * 31f) * shake * 12f,
                    Mathf.Cos(elapsed * 41f) * shake * 8f);

            float pulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * 0.015f;
            castleAnchor.localScale = originalLocalScale * pulse;

            yield return null;
        }

        if (castleObject != null)
        {
            castleAnchor.localPosition = originalLocalPosition;
            castleAnchor.localRotation = originalLocalRotation;
            castleAnchor.localScale = originalLocalScale;
            castleObject.SetActive(false);
        }
    }

    private GameObject SpawnBossObject()
    {
        Vector3 position;
        Quaternion rotation;
        CalculateBossPose(out position, out rotation);

        if (bossPrefab != null)
        {
            GameObject instance = Instantiate(
                bossPrefab,
                position,
                rotation);
            instance.name = "FinalBoss";
            ApplyBossScale(instance.transform);
            instance.SetActive(true);
            return instance;
        }

        if (!createPrototypeBossWhenPrefabMissing)
        {
            return null;
        }

        GameObject prototype = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        prototype.name = "PrototypeFinalBoss";
        prototype.transform.SetPositionAndRotation(position, rotation);
        prototype.transform.localScale = prototypeBossScale;
        ApplyBossScale(prototype.transform);

        ApplyPrototypeBossMaterial(prototype);
        AddPrototypeCrown(prototype);

        return prototype;
    }

    private void ConfigureBossComponents()
    {
        bossHealth = GetOrAdd<EnemyHealth>(bossObject);
        GetOrAdd<RoleSynergyTracker>(bossObject);
        GetOrAdd<EnemyWorldHealthBar>(bossObject);

        if (bossObject.GetComponentInChildren<Collider>(true) == null)
        {
            bossObject.AddComponent<CapsuleCollider>();
        }

        bossAttack = GetOrAdd<FinalBossAttackController>(bossObject);
        bossAttack.enabled = true;

        bossHealth.Configure(bossMaxHealth, true);
        EnsureBossHitbox();
        SubscribeBossHealth();
    }

    private IEnumerator BossRevealRoutine()
    {
        if (bossObject == null)
        {
            yield break;
        }

        Transform bossTransform = bossObject.transform;
        Vector3 finalScale = bossTransform.localScale;
        Vector3 startScale = finalScale * 0.18f;
        Vector3 basePosition = bossTransform.position;

        bossTransform.localScale = startScale;
        bossTransform.position = basePosition - Vector3.up * 0.45f;

        bossAttack?.PlaySummonPulse();

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, bossRevealDuration);

        while (elapsed < duration && bossObject != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            float overshoot = Mathf.Sin(t * Mathf.PI) * 0.12f;

            bossTransform.localScale =
                Vector3.Lerp(startScale, finalScale, smooth) *
                (1f + overshoot);

            bossTransform.position =
                Vector3.Lerp(
                    basePosition - Vector3.up * 0.45f,
                    basePosition,
                    smooth);

            yield return null;
        }

        if (bossObject != null)
        {
            bossTransform.localScale = finalScale;
            bossTransform.position = basePosition;
        }
    }

    private void SubscribeBossHealth()
    {
        if (bossHealth == null)
        {
            return;
        }

        bossHealth.HealthChanged -= HandleBossHealthChanged;
        bossHealth.HealthChanged += HandleBossHealthChanged;

        bossHealth.Died -= HandleBossDied;
        bossHealth.Died += HandleBossDied;
    }

    private void UnsubscribeBossHealth()
    {
        if (bossHealth == null)
        {
            return;
        }

        bossHealth.HealthChanged -= HandleBossHealthChanged;
        bossHealth.Died -= HandleBossDied;
    }

    private void HandleBossHealthChanged(
        EnemyHealth _,
        float current,
        float maximum)
    {
        RefreshBossProgress(current, maximum);

        if (currentState != FinalBossState.Fighting ||
            maximum <= 0f ||
            bossAttack == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(current / maximum);

        if (!firstPhaseAdvanceTriggered && normalized <= (2f / 3f))
        {
            firstPhaseAdvanceTriggered = true;
            StartCoroutine(RequestPhaseAdvanceWhenReady(1));
        }

        if (!secondPhaseAdvanceTriggered && normalized <= (1f / 3f))
        {
            secondPhaseAdvanceTriggered = true;
            StartCoroutine(RequestPhaseAdvanceWhenReady(2));
        }
    }

    private IEnumerator RequestPhaseAdvanceWhenReady(int phaseIndex)
    {
        while (currentState == FinalBossState.Fighting &&
               bossAttack != null &&
               bossAttack.IsPhaseMoving)
        {
            yield return null;
        }

        if (currentState != FinalBossState.Fighting ||
            bossAttack == null ||
            bossHealth == null ||
            bossHealth.IsDead)
        {
            yield break;
        }

        bossAttack.AdvanceTowardCore(phaseIndex);
        bossAttack.PlaySummonPulse();

        missionUI?.SetObjective(
            phaseIndex == 1
                ? "보스가 코어 쪽으로 접근한다! 오염된 장난감을 막아라"
                : "보스가 광폭화했다! 상자를 파괴하라");
    }

    private void StartMinionRoutine()
    {
        StopMinionRoutine();

        if (enemySpawner == null)
        {
            Debug.LogWarning(
                "[FinalBoss] DreamEnemySpawner가 없어 보스 하수인을 생성할 수 없습니다.",
                this);
            return;
        }

        minionRoutine = StartCoroutine(BossMinionSpawnRoutine());
    }

    private IEnumerator BossMinionSpawnRoutine()
    {
        if (firstMinionSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(firstMinionSpawnDelay);
        }

        while (currentState == FinalBossState.Fighting &&
               bossObject != null &&
               bossHealth != null &&
               !bossHealth.IsDead &&
               core != null &&
               !core.IsDestroyed)
        {
            bossSpawnedEnemies.RemoveAll(enemy => enemy == null || enemy.IsDead);

            // 보스가 직접 생성한 적만 제한합니다. 이전 웨이브에 남은 적 때문에
            // 보스 소환이 멈추는 현상을 방지합니다.
            int activeCount = bossSpawnedEnemies.Count;

            if (activeCount < maxActiveMinions)
            {
                SpawnNextBossMinion();
            }

            float healthRatio = bossHealth.MaxHealth > 0f
                ? bossHealth.CurrentHealth / bossHealth.MaxHealth
                : 0f;

            float phaseSpeedMultiplier =
                healthRatio > 2f / 3f
                    ? 1f
                    : healthRatio > 1f / 3f
                        ? 0.86f
                        : 0.72f;

            yield return new WaitForSeconds(
                Mathf.Max(0.5f, minionSpawnInterval * phaseSpeedMultiplier));
        }

        minionRoutine = null;
    }

    private void SpawnNextBossMinion()
    {
        if (enemySpawner == null || bossObject == null)
        {
            return;
        }

        int typeIndex = minionSpawnIndex % 3;
        GameObject prefabOverride = null;
        string typeLabel = "MELEE";

        if (typeIndex == 1 && rangedEnemyPrefab != null)
        {
            prefabOverride = rangedEnemyPrefab;
            typeLabel = "RANGED";
        }
        else if (typeIndex == 2 && droneEnemyPrefab != null)
        {
            prefabOverride = droneEnemyPrefab;
            typeLabel = "DRONE";
        }

        float angle = minionSpawnIndex * 137.5f * Mathf.Deg2Rad;
        Vector3 radial =
            new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
            minionSpawnRadius;

        Vector3 spawnPosition = bossObject.transform.position + radial;

        Vector3 projectedGround = spawnPosition;
        if (TryProjectToGround(spawnPosition, out Vector3 groundedPosition))
        {
            projectedGround = groundedPosition;
        }

        if (typeLabel == "DRONE")
        {
            spawnPosition = projectedGround + Vector3.up * droneSpawnHeight;
        }
        else
        {
            spawnPosition = projectedGround + Vector3.up * minionGroundHeight;
        }

        Vector3 faceDirection =
            core != null
                ? core.transform.position - spawnPosition
                : -radial;
        faceDirection.y = 0f;

        Quaternion rotation =
            faceDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(faceDirection.normalized, Vector3.up)
                : Quaternion.identity;

        EnemyHealth spawned = enemySpawner.SpawnCombatEnemyAtPosition(
            spawnPosition,
            rotation,
            prefabOverride,
            minionHealthMultiplier);

        if (spawned != null)
        {
            bossSpawnedEnemies.Add(spawned);
        }

        bossAttack?.PlaySummonPulse();
        CreateSummonBurst(spawnPosition);

        minionSpawnIndex++;

        Debug.Log(
            "[FinalBoss] 오염된 상자에서 " +
            typeLabel +
            " 적 생성 / 보스 소환 순번 " +
            minionSpawnIndex,
            this);
    }

    private bool TryProjectToGround(
        Vector3 sourcePosition,
        out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = sourcePosition + Vector3.up * 25f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                60f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            groundedPosition = hit.point;
            return true;
        }

        groundedPosition = sourcePosition;
        groundedPosition.y = Mathf.Max(0f, groundedPosition.y);
        return false;
    }

    private void HandleBossDied(EnemyHealth _, DamageInfo __)
    {
        if (currentState != FinalBossState.Fighting ||
            bossDefeatedEventRaised)
        {
            return;
        }

        StopBossRoutine();
        StopMinionRoutine();
        CleanupBossSpawnedEnemies();
        bossRoutine = StartCoroutine(BossDefeatRoutine());
    }

    private IEnumerator BossDefeatRoutine()
    {
        currentState = FinalBossState.Defeating;

        DisableBossColliders();
        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            defeatedTitle,
            defeatedSubtitle,
            Mathf.Max(0.1f, defeatDuration));

        if (!string.IsNullOrWhiteSpace(defeatedMessage))
        {
            missionUI?.ShowDialogue(
                "장난감 친구",
                defeatedMessage,
                Mathf.Max(0.1f, defeatDuration));
        }

        if (bossObject != null && defeatVisualDuration > 0f)
        {
            Vector3 startScale = bossObject.transform.localScale;
            Vector3 startPosition = bossObject.transform.position;
            float elapsed = 0f;

            while (elapsed < defeatVisualDuration && bossObject != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / defeatVisualDuration);
                float remaining = 1f - t;

                bossObject.transform.localScale =
                    startScale * Mathf.Max(0.05f, remaining);
                bossObject.transform.position =
                    startPosition + Vector3.up * (t * 2f);
                bossObject.transform.Rotate(
                    Vector3.up,
                    360f * Time.deltaTime,
                    Space.World);

                yield return null;
            }
        }

        CleanupBossObject();

        float remainingDelay = Mathf.Max(
            0f,
            defeatDuration - defeatVisualDuration);

        if (remainingDelay > 0f)
        {
            yield return new WaitForSeconds(remainingDelay);
        }

        bossRoutine = null;

        if (bossDefeatedEventRaised)
        {
            yield break;
        }

        bossDefeatedEventRaised = true;
        currentState = FinalBossState.Completed;

        Debug.Log(
            "[FinalBoss] 오염 상자 파괴 완료. " +
            "BossDefeated 이벤트를 발생시킵니다.",
            this);

        BossDefeated?.Invoke();
    }

    private void HandleCoreDestroyed()
    {
        if (currentState != FinalBossState.Intro &&
            currentState != FinalBossState.Fighting)
        {
            return;
        }

        FailBossBattle();
    }

    private void FailBossBattle()
    {
        if (bossFailedEventRaised ||
            currentState == FinalBossState.Completed)
        {
            return;
        }

        StopBossRoutine();
        StopMinionRoutine();
        currentState = FinalBossState.Failed;

        if (bossAttack != null)
        {
            bossAttack.enabled = false;
        }

        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            failedTitle,
            failedSubtitle,
            3f);

        bossFailedEventRaised = true;

        Debug.Log(
            "[FinalBoss] 코어가 파괴되어 BossFailed 이벤트를 발생시킵니다.",
            this);

        BossFailed?.Invoke();
    }

    private void RefreshBossProgress()
    {
        if (bossHealth == null)
        {
            return;
        }

        RefreshBossProgress(
            bossHealth.CurrentHealth,
            bossHealth.MaxHealth);
    }

    private void RefreshBossProgress(float current, float maximum)
    {
        if (missionUI == null)
        {
            return;
        }

        missionUI.SetProgress(
            "FINAL BOSS HP  " +
            Mathf.CeilToInt(current) + " / " +
            Mathf.CeilToInt(maximum));
    }

    private void CacheCastleBossPose()
    {
        hasCachedCastleSpawnPose = false;

        if (castleAnchor == null)
        {
            return;
        }

        Bounds bounds = CalculateRendererBounds(castleAnchor);
        cachedCastleSpawnPosition = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.05f,
            bounds.center.z);

        Vector3 faceDirection =
            core != null
                ? core.transform.position - cachedCastleSpawnPosition
                : castleAnchor.forward;
        faceDirection.y = 0f;

        Vector3 flatDirection =
            faceDirection.sqrMagnitude > 0.0001f
                ? faceDirection.normalized
                : castleAnchor.forward;

        cachedCastleSpawnPosition +=
            flatDirection * Mathf.Max(0f, castleSpawnForwardOffset);

        cachedCastleSpawnRotation =
            flatDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flatDirection, Vector3.up)
                : castleAnchor.rotation;

        hasCachedCastleSpawnPose = true;
    }


    private void CalculateBossPose(
        out Vector3 position,
        out Quaternion rotation)
    {
        if (hasCachedCastleSpawnPose)
        {
            position = cachedCastleSpawnPosition;
            rotation = cachedCastleSpawnRotation;
            return;
        }

        if (castleAnchor != null)
        {
            Bounds bounds = CalculateRendererBounds(castleAnchor);
            position = new Vector3(
                bounds.center.x,
                bounds.min.y + 0.05f,
                bounds.center.z);

            Vector3 faceDirection =
                core != null
                    ? core.transform.position - position
                    : castleAnchor.forward;
            faceDirection.y = 0f;

            Vector3 flatDirection =
                faceDirection.sqrMagnitude > 0.0001f
                    ? faceDirection.normalized
                    : castleAnchor.forward;

            position +=
                flatDirection * Mathf.Max(0f, castleSpawnForwardOffset);

            rotation =
                flatDirection.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(flatDirection, Vector3.up)
                    : castleAnchor.rotation;
            return;
        }

        if (bossSpawnPoint != null)
        {
            position = bossSpawnPoint.position;
            rotation = bossSpawnPoint.rotation;
            return;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            position = camera.transform.position +
                       forward * fallbackSpawnDistance;
            position.y = Mathf.Max(1.5f, camera.transform.position.y);

            Vector3 faceDirection = camera.transform.position - position;
            faceDirection.y = 0f;
            rotation = faceDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(faceDirection.normalized, Vector3.up)
                : Quaternion.identity;
            return;
        }

        Vector3 anchor = core != null
            ? core.transform.position
            : transform.position;

        position = anchor + Vector3.forward * fallbackSpawnDistance;
        position.y = Mathf.Max(1.5f, anchor.y + 1.5f);
        rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
    }

    private void ApplyBossScale(Transform bossTransform)
    {
        if (bossTransform == null)
        {
            return;
        }

        float scaleMultiplier = Mathf.Max(1f, bossScaleMultiplier);
        bossTransform.localScale *= scaleMultiplier;
    }

    private void EnsureBossHitbox()
    {
        if (bossObject == null)
        {
            return;
        }

        Collider[] existingColliders =
            bossObject.GetComponentsInChildren<Collider>(true);

        foreach (Collider otherCollider in existingColliders)
        {
            if (otherCollider != null)
            {
                otherCollider.enabled = true;
            }
        }

        CapsuleCollider rootHitbox = bossObject.GetComponent<CapsuleCollider>();
        if (rootHitbox == null)
        {
            rootHitbox = bossObject.AddComponent<CapsuleCollider>();
        }

        Bounds bounds = CalculateRendererBounds(bossObject.transform);
        Vector3 localCenter = bossObject.transform.InverseTransformPoint(bounds.center);

        // Renderer.bounds는 월드 크기입니다. 보스 자체 Scale이 큰 상태에서
        // 이 값을 그대로 Collider 로컬 크기로 넣으면 Scale이 한 번 더 적용되어
        // 히트박스가 하수인 영역까지 덮게 됩니다.
        // 월드 크기를 lossyScale로 나누어 로컬 크기로 환산한 뒤 여유분만 줍니다.
        Vector3 lossyScale = bossObject.transform.lossyScale;
        float safeScaleX = Mathf.Max(0.001f, Mathf.Abs(lossyScale.x));
        float safeScaleY = Mathf.Max(0.001f, Mathf.Abs(lossyScale.y));
        float safeScaleZ = Mathf.Max(0.001f, Mathf.Abs(lossyScale.z));
        float horizontalScale = Mathf.Max(safeScaleX, safeScaleZ);

        float localWorldHeight = bounds.size.y / safeScaleY;
        float localWorldRadius =
            Mathf.Max(
                bounds.extents.x / safeScaleX,
                bounds.extents.z / safeScaleZ);

        float localMinimumRadius =
            Mathf.Max(0.25f, minimumHitboxRadius / horizontalScale);

        float hitboxPadding = Mathf.Clamp(easyHitboxMultiplier, 1f, 1.25f);
        float localHeight = Mathf.Max(
            0.5f,
            localWorldHeight * hitboxPadding);
        float localRadius = Mathf.Max(
            localMinimumRadius,
            localWorldRadius * hitboxPadding);

        rootHitbox.direction = 1;
        rootHitbox.center = localCenter;
        rootHitbox.height = Mathf.Max(localHeight, localRadius * 2f + 0.05f);
        rootHitbox.radius = localRadius;
        rootHitbox.isTrigger = false;
        rootHitbox.enabled = true;
    }

    public void AbortAndResetForTest()
    {
        StopBossRoutine();
        StopMinionRoutine();
        UnsubscribeBossHealth();
        CleanupBossSpawnedEnemies();
        CleanupBossObject();

        if (castleAnchor != null)
        {
            castleAnchor.gameObject.SetActive(true);
        }

        currentState = FinalBossState.Idle;
        bossDefeatedEventRaised = false;
        bossFailedEventRaised = false;
        firstPhaseAdvanceTriggered = false;
        secondPhaseAdvanceTriggered = false;
        minionSpawnIndex = 0;

        missionUI?.ClearPersistentText();
        missionUI?.SetObjective(string.Empty);
        missionUI?.SetProgress(string.Empty);
    }

    private static Bounds CalculateRendererBounds(Transform root)
    {
        if (root == null)
        {
            return new Bounds(Vector3.zero, Vector3.one * 2f);
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.position, Vector3.one * 2f);

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer == null ||
                modelRenderer is ParticleSystemRenderer ||
                modelRenderer is LineRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = modelRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(modelRenderer.bounds);
            }
        }

        return bounds;
    }

    private void CreateCastleDebrisEffect(Bounds bounds, Transform castleRoot)
    {
        GameObject debrisObject = new GameObject("CastleBreak_Debris");
        debrisObject.transform.position = bounds.center;

        ParticleSystem particles = debrisObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(16, castleDebrisCount + 8);
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.25f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(
                0f,
                (short)Mathf.Clamp(castleDebrisCount, 1, short.MaxValue))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            Mathf.Max(1f, bounds.size.x * 0.65f),
            Mathf.Max(1f, bounds.size.y * 0.55f),
            Mathf.Max(1f, bounds.size.z * 0.65f));

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rotation.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
        rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        ParticleSystemRenderer particleRenderer =
            debrisObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = GetCubeMesh();

        Material sourceMaterial = FindCastleMaterial(castleRoot);
        if (sourceMaterial != null)
        {
            particleRenderer.material = sourceMaterial;
        }
        else
        {
            particleRenderer.material = CreateFallbackDebrisMaterial();
        }

        particles.Play();
        Destroy(debrisObject, Mathf.Max(2f, castleDebrisLifetime));
    }

    private static Mesh GetCubeMesh()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.Destroy(cube);
        return mesh;
    }

    private static Material FindCastleMaterial(Transform castleRoot)
    {
        if (castleRoot == null)
        {
            return null;
        }

        Renderer[] renderers = castleRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer != null && modelRenderer.sharedMaterial != null)
            {
                return modelRenderer.sharedMaterial;
            }
        }

        return null;
    }

    private static Material CreateFallbackDebrisMaterial()
    {
        if (fallbackDebrisMaterial != null)
        {
            return fallbackDebrisMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        shader ??= Shader.Find("Standard");

        if (shader == null)
        {
            return null;
        }

        fallbackDebrisMaterial = new Material(shader)
        {
            name = "CastleDebris_Runtime",
            color = new Color(0.35f, 0.22f, 0.18f, 1f),
            hideFlags = HideFlags.DontSave
        };

        if (fallbackDebrisMaterial.HasProperty("_BaseColor"))
        {
            fallbackDebrisMaterial.SetColor(
                "_BaseColor",
                new Color(0.35f, 0.22f, 0.18f, 1f));
        }

        return fallbackDebrisMaterial;
    }

    private void CreateSummonBurst(Vector3 position)
    {
        GameObject effectObject = new GameObject("BossMinion_SummonBurst");
        effectObject.transform.position = position;

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.25f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.03f, 0.005f, 0.04f, 0.42f),
            new Color(0.32f, 0.02f, 0.45f, 0.28f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 14)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        ParticleSystemRenderer renderer =
            effectObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateSummonMaterial();

        particles.Play();
        Destroy(effectObject, 0.7f);
    }

    private static Material CreateSummonMaterial()
    {
        if (summonBurstMaterial != null)
        {
            return summonBurstMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        shader ??= Shader.Find("Particles/Standard Unlit");
        shader ??= Shader.Find("Unlit/Color");

        if (shader == null)
        {
            return null;
        }

        summonBurstMaterial = new Material(shader)
        {
            name = "BossSummonBurst_Runtime",
            hideFlags = HideFlags.DontSave
        };

        if (summonBurstMaterial.HasProperty("_BaseColor"))
        {
            summonBurstMaterial.SetColor("_BaseColor", Color.white);
        }
        else if (summonBurstMaterial.HasProperty("_Color"))
        {
            summonBurstMaterial.SetColor("_Color", Color.white);
        }

        if (summonBurstMaterial.HasProperty("_Surface"))
        {
            summonBurstMaterial.SetFloat("_Surface", 1f);
        }

        if (summonBurstMaterial.HasProperty("_SrcBlend"))
        {
            summonBurstMaterial.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (summonBurstMaterial.HasProperty("_DstBlend"))
        {
            summonBurstMaterial.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (summonBurstMaterial.HasProperty("_ZWrite"))
        {
            summonBurstMaterial.SetFloat("_ZWrite", 0f);
        }

        summonBurstMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        summonBurstMaterial.renderQueue = 3000;
        return summonBurstMaterial;
    }

    private static void ApplyPrototypeBossMaterial(GameObject target)
    {
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        shader ??= Shader.Find("Standard");

        if (shader == null)
        {
            return;
        }

        Color bossColor = new Color(0.18f, 0.01f, 0.24f, 1f);
        Color emissionColor = new Color(1f, 0.05f, 0.6f, 1f) * 2f;

        Material material = new Material(shader)
        {
            name = "PrototypeFinalBoss_Runtime",
            color = bossColor
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", bossColor);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
        }

        targetRenderer.material = material;
    }

    private static void AddPrototypeCrown(GameObject bossRoot)
    {
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crown.name = "PrototypeBossCrown";
        crown.transform.SetParent(bossRoot.transform, false);
        crown.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        crown.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        crown.transform.localScale = new Vector3(0.6f, 0.18f, 0.6f);

        Collider crownCollider = crown.GetComponent<Collider>();
        if (crownCollider != null)
        {
            UnityEngine.Object.Destroy(crownCollider);
        }
    }

    private void CleanupBossSpawnedEnemies()
    {
        if (bossSpawnedEnemies.Count == 0)
        {
            return;
        }

        for (int i = bossSpawnedEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = bossSpawnedEnemies[i];
            if (enemy == null)
            {
                continue;
            }

            if (enemySpawner != null)
            {
                enemySpawner.DespawnEnemyImmediately(enemy);
            }
            else
            {
                Destroy(enemy.gameObject);
            }
        }

        bossSpawnedEnemies.Clear();
    }

    private void DisableBossColliders()
    {
        if (bossObject == null)
        {
            return;
        }

        foreach (Collider bossCollider in
                 bossObject.GetComponentsInChildren<Collider>(true))
        {
            bossCollider.enabled = false;
        }
    }

    private void StopBossRoutine()
    {
        if (bossRoutine == null)
        {
            return;
        }

        StopCoroutine(bossRoutine);
        bossRoutine = null;
    }

    private void StopMinionRoutine()
    {
        if (minionRoutine == null)
        {
            return;
        }

        StopCoroutine(minionRoutine);
        minionRoutine = null;
    }

    private void CleanupBossObject()
    {
        UnsubscribeBossHealth();

        if (bossObject != null)
        {
            Destroy(bossObject);
        }

        bossObject = null;
        bossHealth = null;
        bossAttack = null;
    }

    private static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null
            ? component
            : target.AddComponent<T>();
    }

    private void OnValidate()
    {
        fallbackSpawnDistance = Mathf.Max(1f, fallbackSpawnDistance);
        prototypeBossScale.x = Mathf.Max(0.25f, prototypeBossScale.x);
        prototypeBossScale.y = Mathf.Max(0.25f, prototypeBossScale.y);
        prototypeBossScale.z = Mathf.Max(0.25f, prototypeBossScale.z);
        bossScaleMultiplier = Mathf.Max(1f, bossScaleMultiplier);
        castleSpawnForwardOffset = Mathf.Max(0f, castleSpawnForwardOffset);
        easyHitboxMultiplier = Mathf.Max(1f, easyHitboxMultiplier);
        minimumHitboxRadius = Mathf.Max(0.5f, minimumHitboxRadius);
        castleBreakDuration = Mathf.Max(0.1f, castleBreakDuration);
        castleDebrisLifetime = Mathf.Max(0f, castleDebrisLifetime);
        castleDebrisCount = Mathf.Max(1, castleDebrisCount);
        bossRevealDuration = Mathf.Max(0.1f, bossRevealDuration);
        bossMaxHealth = Mathf.Max(1f, bossMaxHealth);
        bossCoreDamage = Mathf.Max(0f, bossCoreDamage);
        bossAttackInterval = Mathf.Max(0.1f, bossAttackInterval);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
        minionSpawnInterval = Mathf.Max(0.5f, minionSpawnInterval);
        firstMinionSpawnDelay = Mathf.Max(0f, firstMinionSpawnDelay);
        maxActiveMinions = Mathf.Max(1, maxActiveMinions);
        minionHealthMultiplier = Mathf.Max(0.1f, minionHealthMultiplier);
        minionSpawnRadius = Mathf.Max(0.5f, minionSpawnRadius);
        minionGroundHeight = Mathf.Max(0f, minionGroundHeight);
        droneSpawnHeight = Mathf.Max(0f, droneSpawnHeight);
        introDuration = Mathf.Max(0f, introDuration);
        defeatDuration = Mathf.Max(0f, defeatDuration);
        defeatVisualDuration = Mathf.Max(0f, defeatVisualDuration);
    }
}
