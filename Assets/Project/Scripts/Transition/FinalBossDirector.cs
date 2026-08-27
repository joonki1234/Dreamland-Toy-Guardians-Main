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
/// 상자를 정화하면 보스가 생성한 적들도 함께 사라집니다.
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

    [Tooltip("보스 등장 설명과 정화 완료 대사를 직접 말할 3D 장난감 친구")]
    [SerializeField]
    private ToyFriendController toyFriend;

    [SerializeField, Min(0f)]
    private float toyFriendStoryTransitionDuration = 0.35f;

    [SerializeField]
    private CoreState core;

    [SerializeField]
    private DreamEnemySpawner enemySpawner;

    [SerializeField]
    private EnemyPortalStageController enemyPortalStageController;

    [Header("Boss Spawn / Castle")]
    [SerializeField]
    private GameObject bossPrefab;

    [SerializeField]
    private Transform bossSpawnPoint;

    [Tooltip("맵의 Castle 오브젝트. 연결되어 있으면 기존 BossSpawnPoint보다 우선합니다.")]
    [SerializeField]
    private Transform castleAnchor;

    [Tooltip("보스전에서 성 주변 시야를 정리할 나무 루트. 비어 있으면 Tree_Border를 자동 탐색합니다.")]
    [SerializeField]
    private Transform treeBorder;

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

    [Header("Boss Arena Focus")]
    [SerializeField]
    private bool hideTreesNearBoss = true;

    [Tooltip("성/보스 근처에서 숨길 나무 반경")]
    [SerializeField, Min(1f)]
    private float bossFocusTreeHideRadius = 20f;

    [Header("Boss Spawn Camera Shake")]
    [SerializeField, Min(0f)]
    private float bossSpawnShakeDuration = 0.58f;

    [SerializeField, Min(0f)]
    private float bossSpawnShakeStrength = 0.085f;

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
    [Tooltip("Stage 2에서 사용하던 Waspy 비행 적. 보스전에서는 이 드론만 소환합니다.")]
    [SerializeField]
    private GameObject droneEnemyPrefab;

    [Tooltip("드론이 한 번에 몇 마리씩 무리 지어 등장할지")]
    [SerializeField, Min(1)]
    private int droneBurstCount = 6;

    [Tooltip("드론 무리가 등장하는 간격(초)")]
    [SerializeField, Min(0.5f)]
    private float droneBurstInterval = 10f;

    [SerializeField, Min(0f)]
    private float firstMinionSpawnDelay = 3.0f;

    [SerializeField, Min(1)]
    private int maxActiveMinions = 24;

    [SerializeField, Min(0.1f)]
    private float minionHealthMultiplier = 1.15f;

    [SerializeField, Min(0.5f)]
    private float minionSpawnRadius = 7.5f;

    [SerializeField, Min(0f)]
    private float droneSpawnHeight = 2.5f;

    [Header("Boss UI")]
    [SerializeField]
    private string introTitle = "FINAL BOSS";

    [SerializeField]
    private string introSubtitle = "오염된 선물 상자를 정화하라";

    [SerializeField]
    private string objectiveText = "오염된 선물 상자를 정화하고 코어를 지켜라";

    [SerializeField]
    private string introSpeaker = "장난감 친구";

    [TextArea(2, 4)]
    [SerializeField]
    private string bossIdentityMessage =
        "저건... 꿈나라의 장난감들이 태어나는 선물 상자야!";

    [TextArea(2, 4)]
    [SerializeField]
    private string bossInfectionMessage =
        "선물 상자까지 악몽 바이러스에 감염됐어... 그래서 태어나는 장난감들까지 모두 오염되고 있었던 거야!";

    [TextArea(2, 4)]
    [SerializeField]
    private string bossSourceMessage =
        "꿈나라가 계속 오염됐던 것도 저 상자에서 오염된 장난감들이 계속 태어나고 있었기 때문이야.";

    [TextArea(2, 4)]
    [SerializeField]
    private string bossPurifyGoalMessage =
        "저 선물 상자를 정화하면 오염의 근원도 사라질 거야. 그러면 꿈나라도 다시 원래대로 돌아올 수 있어!";

    [TextArea(2, 4)]
    [SerializeField]
    private string bossBattleStartMessage =
        "조심해! 상자가 또 오염된 장난감들을 만들어내고 있어! 저 상자를 정화하자!";

    [SerializeField, Min(0.1f)]
    private float bossStoryLineDuration = 3.8f;

    [SerializeField]
    private string defeatedTitle = "BOSS PURIFIED";

    [SerializeField]
    private string defeatedSubtitle = "오염의 근원이 정화되었습니다";

    [TextArea(2, 4)]
    [SerializeField]
    private string defeatedMessage =
        "해냈어! 선물 상자의 오염이 사라지고 있어... 꿈나라도 다시 빛을 되찾고 있어!";

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
    private Coroutine cameraShakeRoutine;
    private Transform shakenCameraTransform;
    private Vector3 shakenCameraOriginalLocalPosition;
    private bool hasCameraShakeOrigin;

    private readonly List<GameObject> hiddenBossArenaObjects =
        new List<GameObject>();

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
        ApplyStoryDialogueRevision();
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
        StopCameraShake();
        RestoreBossArenaFocus();
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

        if (toyFriend == null)
        {
            toyFriend =
                UnityEngine.Object.FindAnyObjectByType<ToyFriendController>();
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

        if (enemyPortalStageController == null)
        {
            enemyPortalStageController =
                UnityEngine.Object.FindAnyObjectByType<EnemyPortalStageController>();
        }

        if (castleAnchor == null)
        {
            GameObject castle = GameObject.Find("Castle");
            if (castle != null)
            {
                castleAnchor = castle.transform;
            }
        }

        if (treeBorder == null)
        {
            GameObject treeRoot = GameObject.Find("Tree_Border");
            if (treeRoot != null)
            {
                treeBorder = treeRoot.transform;
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
            Debug.Log("[F8] BossBattle started", this);
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
        StopCameraShake();
        RestoreBossArenaFocus();
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
        CacheCastleBossPose();
        ApplyBossArenaFocus();

        if (castleAnchor != null &&
            castleAnchor.gameObject.activeInHierarchy)
        {
            yield return CastleBreakRoutine();
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
        StartBossSpawnCameraShake();
        yield return BossRevealRoutine();

        // 26~30번은 보스 앞에서 3D 장난감 친구가 직접 설명합니다.
        if (toyFriend != null)
        {
            yield return toyFriend.ShowForStory(
                toyFriendStoryTransitionDuration);
        }

        // 보스가 완전히 모습을 드러낸 뒤 정체와 오염 원인을 순서대로 설명합니다.
        yield return PlayBossStoryLine(bossIdentityMessage, 2.8f);
        yield return PlayBossStoryLine(bossInfectionMessage, 4.6f);
        yield return PlayBossStoryLine(bossSourceMessage, 4.3f);
        yield return PlayBossStoryLine(bossPurifyGoalMessage, 4.6f);

        missionUI?.ShowBanner(
            introTitle,
            introSubtitle,
            Mathf.Max(0.1f, introDuration));
        yield return PlayBossStoryLine(bossBattleStartMessage, 3.8f, true);

        // 실제 보스 전투가 시작되면 장난감 친구는 다시 전투 화면에서 빠집니다.
        if (toyFriend != null)
        {
            yield return toyFriend.HideForCombat(
                toyFriendStoryTransitionDuration);
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

    private IEnumerator PlayBossStoryLine(
        string message,
        float preferredDuration,
        bool celebratory = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            yield break;
        }

        float duration = Mathf.Max(
            0.1f,
            preferredDuration > 0f ? preferredDuration : bossStoryLineDuration);

        missionUI?.HideTransientMessages();
        if (toyFriend != null)
        {
            toyFriend.Speak(
                message,
                duration,
                celebratory);
        }
        else
        {
            missionUI?.ShowDialogue(
                introSpeaker,
                message,
                duration);
        }

        yield return new WaitForSeconds(duration);
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

        EnemyWorldHealthBar oldWorldBar =
            bossObject.GetComponent<EnemyWorldHealthBar>();
        if (oldWorldBar != null)
        {
            oldWorldBar.Hide();
            oldWorldBar.enabled = false;
        }

        bossAttack = GetOrAdd<FinalBossAttackController>(bossObject);
        bossAttack.enabled = true;

        // 등장/스토리 연출 중에는 피격되지 않도록 막습니다.
        bossHealth.Configure(bossMaxHealth, false);
        EnsureBossHitbox();
        IgnorePlayerCollisionsWithBoss();
        bossAttack.PrepareCorruptedVisuals(core);
        SubscribeBossHealth();
    }

    /// <summary>
    /// EnsureBossHitbox()가 각 렌더러에 붙인 히트박스는 무기 판정을 위해
    /// isTrigger = false(솔리드)를 유지해야 한다. 하지만 보스는 킨매틱
    /// Rigidbody라서 페이즈 전환 때 코어 쪽으로 움직이면 그 솔리드 콜라이더가
    /// 근처 플레이어의 CharacterController를 물리적으로 밀어내(PhysX push-out),
    /// "조작이 안 먹히고 미끄러지듯 이동한다"는 증상을 만든다. 콜라이더 자체를
    /// 트리거로 바꾸면 무기 판정이 깨질 수 있으므로, 대신 보스 히트박스와
    /// 플레이어 CharacterController 사이의 물리 충돌만 콕 집어 꺼서 판정은
    /// 그대로 두고 밀려나는 것만 막는다.
    /// </summary>
    private void IgnorePlayerCollisionsWithBoss()
    {
        if (bossObject == null)
        {
            return;
        }

        Collider[] bossColliders =
            bossObject.GetComponentsInChildren<Collider>(true);

        CharacterController[] playerControllers =
            FindObjectsByType<CharacterController>(
                FindObjectsSortMode.None);

        foreach (Collider bossCollider in bossColliders)
        {
            if (bossCollider == null || bossCollider.isTrigger)
            {
                continue;
            }

            foreach (CharacterController playerController in playerControllers)
            {
                if (playerController != null)
                {
                    Physics.IgnoreCollision(
                        bossCollider,
                        playerController,
                        true);
                }
            }
        }
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
                : "보스가 광폭화했다! 선물 상자를 정화하라");
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
            for (int i = 0; i < droneBurstCount; i++)
            {
                if (bossSpawnedEnemies.Count >= maxActiveMinions)
                {
                    break;
                }

                SpawnNextBossMinion();
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.5f, droneBurstInterval));
        }

        minionRoutine = null;
    }

    private void SpawnNextBossMinion()
    {
        if (enemySpawner == null ||
            bossObject == null ||
            droneEnemyPrefab == null)
        {
            return;
        }

        GameObject prefabOverride = droneEnemyPrefab;
        const string typeLabel = "DRONE";

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

        spawnPosition = projectedGround + Vector3.up * droneSpawnHeight;

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

    private static readonly RaycastHit[] GroundProjectionHitsBuffer =
        new RaycastHit[16];

    private bool TryProjectToGround(
        Vector3 sourcePosition,
        out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = sourcePosition + Vector3.up * 25f;

        // 보스 모델은 매우 큰 스케일로 임포트되어 있어(FinalBossAttackController
        // 눈알 배치 주석 참고), minionSpawnRadius가 작으면 아래로 쏜 레이가
        // 진짜 바닥보다 먼저 보스 자신의 콜라이더 표면에 맞아 그 위에
        // 소환되어 공중에 붕 떠 보이는 문제가 있었다. 보스 계층에 속한
        // 히트는 건너뛰고 그 아래에 있는 진짜 바닥을 찾는다.
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            GroundProjectionHitsBuffer,
            60f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        if (hitCount > 0)
        {
            System.Array.Sort(
                GroundProjectionHitsBuffer,
                0,
                hitCount,
                Comparer<RaycastHit>.Create(
                    (a, b) => a.distance.CompareTo(b.distance)));

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = GroundProjectionHitsBuffer[i];

                if (bossObject != null &&
                    hit.collider != null &&
                    hit.collider.transform.IsChildOf(bossObject.transform))
                {
                    continue;
                }

                groundedPosition = hit.point;
                return true;
            }
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

        // 31번 정화 완료 대사도 3D 장난감 친구가 직접 등장해 말합니다.
        if (toyFriend != null)
        {
            yield return toyFriend.ShowForStory(
                toyFriendStoryTransitionDuration);
        }

        missionUI?.ShowBanner(
            defeatedTitle,
            defeatedSubtitle,
            Mathf.Max(0.1f, defeatDuration));

        if (!string.IsNullOrWhiteSpace(defeatedMessage))
        {
            float storyDuration = Mathf.Max(0.1f, defeatDuration);
            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(
                    defeatedMessage,
                    storyDuration,
                    true);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    defeatedMessage,
                    storyDuration);
            }
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
        RestoreBossArenaFocus();

        Debug.Log(
            "[FinalBoss] 오염된 선물 상자 정화 완료. " +
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
        missionUI?.HideBossHealth();
        RestoreBossArenaFocus();
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

        missionUI.SetBossHealth(
            "오염된 선물 상자",
            current,
            maximum);
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

        // 루트 Collider는 모델 전체를 한 덩어리로 덮어 주변 하수인 사격을
        // 가로챌 수 있으므로 비활성화하고, 실제 렌더러 단위 히트박스를 사용합니다.
        Collider[] rootColliders = bossObject.GetComponents<Collider>();
        foreach (Collider rootCollider in rootColliders)
        {
            if (rootCollider != null)
            {
                rootCollider.enabled = false;
            }
        }

        bool hasUsableCollider = false;
        Renderer[] renderers = bossObject.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer == null ||
                modelRenderer is ParticleSystemRenderer ||
                modelRenderer is LineRenderer ||
                modelRenderer.name.Contains("Aura") ||
                modelRenderer.name.Contains("Eye"))
            {
                continue;
            }

            Collider existing = modelRenderer.GetComponent<Collider>();
            if (existing != null && modelRenderer.gameObject != bossObject)
            {
                existing.enabled = true;
                hasUsableCollider = true;
                continue;
            }

            Bounds localBounds;
            bool hasLocalBounds = false;

            SkinnedMeshRenderer skinned = modelRenderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                localBounds = skinned.localBounds;
                hasLocalBounds = true;
            }
            else
            {
                MeshFilter meshFilter = modelRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    localBounds = meshFilter.sharedMesh.bounds;
                    hasLocalBounds = true;
                }
                else
                {
                    localBounds = default;
                }
            }

            if (!hasLocalBounds)
            {
                continue;
            }

            BoxCollider hitbox = modelRenderer.gameObject.AddComponent<BoxCollider>();
            hitbox.center = localBounds.center;
            hitbox.size = localBounds.size * Mathf.Clamp(easyHitboxMultiplier, 1f, 1.10f);
            hitbox.isTrigger = false;
            hitbox.enabled = true;
            hasUsableCollider = true;
        }

        if (hasUsableCollider)
        {
            return;
        }

        // 메시 Collider를 만들 수 없는 예외 프리팹에서만 작은 BoxCollider를 사용합니다.
        Bounds bounds = CalculateRendererBounds(bossObject.transform);
        BoxCollider fallback = bossObject.AddComponent<BoxCollider>();
        fallback.center = bossObject.transform.InverseTransformPoint(bounds.center);

        Vector3 lossyScale = bossObject.transform.lossyScale;
        fallback.size = new Vector3(
            bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
            bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)),
            bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(lossyScale.z))) * 1.04f;
        fallback.isTrigger = false;
    }

    public void AbortAndResetForTest()
    {
        StopBossRoutine();
        StopMinionRoutine();
        StopCameraShake();
        RestoreBossArenaFocus();
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

    private void ApplyBossArenaFocus()
    {
        RestoreBossArenaFocus();
        enemyPortalStageController?.HideAllPortalsForBoss();

        if (!hideTreesNearBoss || treeBorder == null)
        {
            return;
        }

        Vector3 focusPosition = hasCachedCastleSpawnPose
            ? cachedCastleSpawnPosition
            : (castleAnchor != null ? castleAnchor.position : transform.position);
        float radius = Mathf.Max(1f, bossFocusTreeHideRadius);

        for (int groupIndex = 0; groupIndex < treeBorder.childCount; groupIndex++)
        {
            Transform group = treeBorder.GetChild(groupIndex);
            if (group == null)
            {
                continue;
            }

            for (int childIndex = 0; childIndex < group.childCount; childIndex++)
            {
                Transform candidate = group.GetChild(childIndex);
                if (candidate == null || !candidate.gameObject.activeSelf)
                {
                    continue;
                }

                Vector3 delta = candidate.position - focusPosition;
                delta.y = 0f;

                if (delta.sqrMagnitude <= radius * radius)
                {
                    hiddenBossArenaObjects.Add(candidate.gameObject);
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        Debug.Log(
            "[FinalBoss] 보스 집중 연출을 위해 성 주변 나무 " +
            hiddenBossArenaObjects.Count + "개를 숨겼습니다.",
            this);
    }

    private void RestoreBossArenaFocus()
    {
        for (int i = 0; i < hiddenBossArenaObjects.Count; i++)
        {
            GameObject hidden = hiddenBossArenaObjects[i];
            if (hidden != null)
            {
                hidden.SetActive(true);
            }
        }

        hiddenBossArenaObjects.Clear();
    }

    private void StartBossSpawnCameraShake()
    {
        StopCameraShake();

        if (bossSpawnShakeDuration <= 0f || bossSpawnShakeStrength <= 0f)
        {
            return;
        }

        cameraShakeRoutine = StartCoroutine(BossSpawnCameraShakeRoutine());
    }

    private IEnumerator BossSpawnCameraShakeRoutine()
    {
        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            cameraShakeRoutine = null;
            yield break;
        }

        Transform cameraTransform = targetCamera.transform;
        Vector3 originalLocalPosition = cameraTransform.localPosition;
        shakenCameraTransform = cameraTransform;
        shakenCameraOriginalLocalPosition = originalLocalPosition;
        hasCameraShakeOrigin = true;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, bossSpawnShakeDuration);

        while (elapsed < duration && cameraTransform != null)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float strength = bossSpawnShakeStrength * (1f - normalized);
            Vector2 random = UnityEngine.Random.insideUnitCircle * strength;

            cameraTransform.localPosition =
                originalLocalPosition + new Vector3(random.x, random.y, 0f);

            yield return null;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = originalLocalPosition;
        }

        hasCameraShakeOrigin = false;
        shakenCameraTransform = null;
        cameraShakeRoutine = null;
    }

    private void StopCameraShake()
    {
        if (cameraShakeRoutine != null)
        {
            StopCoroutine(cameraShakeRoutine);
            cameraShakeRoutine = null;
        }

        if (hasCameraShakeOrigin && shakenCameraTransform != null)
        {
            shakenCameraTransform.localPosition = shakenCameraOriginalLocalPosition;
        }

        hasCameraShakeOrigin = false;
        shakenCameraTransform = null;
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

    private void ApplyStoryDialogueRevision()
    {
        introSubtitle = "오염된 선물 상자를 정화하라";
        objectiveText = "오염된 선물 상자를 정화하고 코어를 지켜라";
        bossIdentityMessage = "저건... 꿈나라의 장난감들이 태어나는 선물 상자야!";
        bossInfectionMessage =
            "선물 상자까지 악몽 바이러스에 감염됐어... 그래서 태어나는 장난감들까지 모두 오염되고 있었던 거야!";
        bossSourceMessage =
            "꿈나라가 계속 오염됐던 것도 저 상자에서 오염된 장난감들이 계속 태어나고 있었기 때문이야.";
        bossPurifyGoalMessage =
            "저 선물 상자를 정화하면 오염의 근원도 사라질 거야. 그러면 꿈나라도 다시 원래대로 돌아올 수 있어!";
        bossBattleStartMessage =
            "조심해! 상자가 또 오염된 장난감들을 만들어내고 있어! 저 상자를 정화하자!";
        defeatedTitle = "BOSS PURIFIED";
        defeatedSubtitle = "오염의 근원이 정화되었습니다";
        defeatedMessage =
            "해냈어! 선물 상자의 오염이 사라지고 있어... 꿈나라도 다시 빛을 되찾고 있어!";
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
        bossFocusTreeHideRadius = Mathf.Max(1f, bossFocusTreeHideRadius);
        bossSpawnShakeDuration = Mathf.Max(0f, bossSpawnShakeDuration);
        bossSpawnShakeStrength = Mathf.Max(0f, bossSpawnShakeStrength);
        bossMaxHealth = Mathf.Max(1f, bossMaxHealth);
        bossCoreDamage = Mathf.Max(0f, bossCoreDamage);
        bossAttackInterval = Mathf.Max(0.1f, bossAttackInterval);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
        droneBurstCount = Mathf.Max(1, droneBurstCount);
        droneBurstInterval = Mathf.Max(0.5f, droneBurstInterval);
        firstMinionSpawnDelay = Mathf.Max(0f, firstMinionSpawnDelay);
        maxActiveMinions = Mathf.Max(1, maxActiveMinions);
        minionHealthMultiplier = Mathf.Max(0.1f, minionHealthMultiplier);
        minionSpawnRadius = Mathf.Max(0.5f, minionSpawnRadius);
        droneSpawnHeight = Mathf.Max(0f, droneSpawnHeight);
        introDuration = Mathf.Max(0f, introDuration);
        bossStoryLineDuration = Mathf.Max(0.1f, bossStoryLineDuration);
        toyFriendStoryTransitionDuration =
            Mathf.Max(0f, toyFriendStoryTransitionDuration);
        defeatDuration = Mathf.Max(0f, defeatDuration);
        defeatVisualDuration = Mathf.Max(0f, defeatVisualDuration);
    }
}
