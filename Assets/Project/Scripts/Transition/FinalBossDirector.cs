using System;
using System.Collections;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 최종 보스의 등장, 전투 UI, 처치 판정, 실패 판정을 담당합니다.
/// Boss Prefab이 비어 있으면 테스트 가능한 런타임 프로토타입 보스를 생성합니다.
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

    [Header("Boss Spawn")]
    [SerializeField]
    private GameObject bossPrefab;

    [SerializeField]
    private Transform bossSpawnPoint;

    [SerializeField]
    private bool createPrototypeBossWhenPrefabMissing = true;

    [Min(1f)]
    [SerializeField]
    private float fallbackSpawnDistance = 12f;

    [SerializeField]
    private Vector3 prototypeBossScale = new Vector3(2.5f, 2.5f, 2.5f);

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

    [Header("Boss UI")]
    [SerializeField]
    private string introTitle = "FINAL BOSS";

    [SerializeField]
    private string introSubtitle = "악몽의 근원을 정화하라";

    [SerializeField]
    private string objectiveText = "최종 보스를 쓰러뜨리고 코어를 지켜라";

    [SerializeField]
    private string introSpeaker = "장난감 친구";

    [TextArea(2, 4)]
    [SerializeField]
    private string introMessage =
        "저 검은 형체가 모든 악몽의 근원이야. 함께 끝내자!";

    [SerializeField]
    private string defeatedTitle = "BOSS DEFEATED";

    [SerializeField]
    private string defeatedSubtitle = "악몽의 근원이 정화되었습니다";

    [TextArea(2, 4)]
    [SerializeField]
    private string defeatedMessage =
        "해냈어! 꿈나라의 빛이 다시 돌아오고 있어!";

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
    private GameObject bossObject;
    private EnemyHealth bossHealth;
    private FinalBossAttackController bossAttack;
    private bool bossDefeatedEventRaised;
    private bool bossFailedEventRaised;

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

        StopBossRoutine();
        UnsubscribeBossHealth();
        CleanupBossObject();

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

        if (introDuration > 0f)
        {
            yield return new WaitForSeconds(introDuration);
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

        missionUI?.SetObjective(objectiveText);
        RefreshBossProgress();

        Debug.Log(
            "[FinalBoss] 최종 보스전이 시작됐습니다. 보스 HP: " +
            bossMaxHealth.ToString("0"),
            this);

        bossRoutine = null;
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
        bossAttack.enabled = false;

        bossHealth.Configure(bossMaxHealth, false);
        SubscribeBossHealth();
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
    }

    private void HandleBossDied(EnemyHealth _, DamageInfo __)
    {
        if (currentState != FinalBossState.Fighting ||
            bossDefeatedEventRaised)
        {
            return;
        }

        StopBossRoutine();
        bossRoutine = StartCoroutine(BossDefeatRoutine());
    }

    private IEnumerator BossDefeatRoutine()
    {
        currentState = FinalBossState.Defeating;

        if (bossAttack != null)
        {
            bossAttack.enabled = false;
        }

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
                    240f * Time.deltaTime,
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
            "[FinalBoss] 보스 처치 연출 완료. " +
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

    private void CalculateBossPose(
        out Vector3 position,
        out Quaternion rotation)
    {
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

            // 기본 캡슐의 하단이 지면 근처에 오도록 카메라 높이를 사용합니다.
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

        Renderer crownRenderer = crown.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        shader ??= Shader.Find("Standard");

        if (crownRenderer == null || shader == null)
        {
            return;
        }

        Color crownColor = new Color(1f, 0.3f, 0.05f, 1f);
        Material crownMaterial = new Material(shader)
        {
            name = "PrototypeBossCrown_Runtime",
            color = crownColor
        };

        if (crownMaterial.HasProperty("_BaseColor"))
        {
            crownMaterial.SetColor("_BaseColor", crownColor);
        }

        if (crownMaterial.HasProperty("_EmissionColor"))
        {
            crownMaterial.EnableKeyword("_EMISSION");
            crownMaterial.SetColor("_EmissionColor", crownColor * 2f);
        }

        crownRenderer.material = crownMaterial;
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
        bossMaxHealth = Mathf.Max(1f, bossMaxHealth);
        bossCoreDamage = Mathf.Max(0f, bossCoreDamage);
        bossAttackInterval = Mathf.Max(0.1f, bossAttackInterval);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
        introDuration = Mathf.Max(0f, introDuration);
        defeatDuration = Mathf.Max(0f, defeatDuration);
        defeatVisualDuration = Mathf.Max(0f, defeatVisualDuration);
    }
}
