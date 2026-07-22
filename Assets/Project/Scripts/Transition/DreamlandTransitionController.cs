using System;
using System.Collections;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// Stage 2 이후의 적 흡수 연출과 현실/MR 표현에서 완전한 꿈나라 표현으로의
/// 시각 전환을 담당합니다.
///
/// 현재 프로젝트에는 실제 Quest Passthrough 제어 패키지가 없으므로,
/// 이번 버전은 씬 오브젝트 활성/비활성 방식으로 전환을 검증합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamlandTransitionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [SerializeField]
    private MissionBannerUI missionUI;

    [Header("World Groups")]
    [SerializeField]
    private GameObject realityWorld;

    [SerializeField]
    private GameObject interiorDream;

    [SerializeField]
    private GameObject finalDreamland;

    [SerializeField]
    private GameObject portalEffects;

    [Header("적 흡수 연출")]
    [Min(0f)]
    [SerializeField]
    private float enemyAbsorptionDuration = 3f;

    [Min(0f)]
    [SerializeField]
    private float portalPulseAmount = 0.12f;

    [SerializeField]
    private string absorptionTitle = "DREAM ENERGY CONVERGENCE";

    [SerializeField]
    private string absorptionSubtitle = "남은 꿈의 기운이 하나로 모입니다";

    [TextArea(2, 4)]
    [SerializeField]
    private string absorptionMessage =
        "검은 기운이 균열로 빨려 들어가고 있어. 곧 완전한 꿈나라가 열릴 거야!";

    [Header("완전 꿈나라 전환")]
    [Min(0f)]
    [SerializeField]
    private float fullVRTransitionDelay = 1.5f;

    [Min(0f)]
    [SerializeField]
    private float postTransitionHold = 1f;

    [SerializeField]
    private string transitionTitle = "DREAMLAND OPEN";

    [SerializeField]
    private string transitionSubtitle = "현실의 경계가 사라집니다";

    [Header("Start State")]
    [SerializeField]
    private bool applyInitialStateOnStart = true;

    private Coroutine transitionRoutine;
    private Vector3 portalBaseScale = Vector3.one;
    private bool absorptionEventRaised;
    private bool fullVREventRaised;

    public event Action EnemyAbsorptionCompleted;
    public event Action FullVRTransitionCompleted;

    private void Awake()
    {
        ResolveReferences();
        CapturePortalBaseScale();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
            gameFlowController.OnStateChanged += HandleStateChanged;
        }
    }

    private void Start()
    {
        if (applyInitialStateOnStart &&
            (gameFlowController == null ||
             gameFlowController.CurrentState ==
             DreamlandGameFlowController.GameFlowState.WaitingForStage1Complete))
        {
            ApplyRealityState();
        }
    }

    private void OnDisable()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }

        StopTransitionRoutine();
        RestorePortalScale();
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

        realityWorld ??= FindSceneObject("03_REALITY_WORLD");
        interiorDream ??= FindSceneObject("04_INTERIOR_DREAM");
        finalDreamland ??= FindSceneObject("05_FINAL_DREAMLAND");
        portalEffects ??= FindSceneObject("06_PORTAL_EFFECTS");
    }

    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        switch (newState)
        {
            case DreamlandGameFlowController.GameFlowState.WaitingForStage1Complete:
                StopTransitionRoutine();
                ApplyRealityState();
                break;

            case DreamlandGameFlowController.GameFlowState.Stage2Wave1:
                StopTransitionRoutine();
                ApplyStage2PortalState();
                break;

            case DreamlandGameFlowController.GameFlowState.EnemyAbsorption:
                BeginEnemyAbsorption();
                break;

            case DreamlandGameFlowController.GameFlowState.FullVRTransition:
                BeginFullVRTransition();
                break;

            case DreamlandGameFlowController.GameFlowState.BossBattle:
                StopTransitionRoutine();
                ApplyFullDreamlandState();
                break;

            case DreamlandGameFlowController.GameFlowState.GameOver:
                StopTransitionRoutine();
                RestorePortalScale();
                break;
        }
    }

    private void BeginEnemyAbsorption()
    {
        StopTransitionRoutine();
        absorptionEventRaised = false;
        transitionRoutine = StartCoroutine(EnemyAbsorptionRoutine());
    }

    private IEnumerator EnemyAbsorptionRoutine()
    {
        ApplyStage2PortalState();
        CapturePortalBaseScale();

        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            absorptionTitle,
            absorptionSubtitle,
            Mathf.Max(0.1f, enemyAbsorptionDuration));

        if (!string.IsNullOrWhiteSpace(absorptionMessage))
        {
            missionUI?.ShowDialogue(
                "장난감 친구",
                absorptionMessage,
                Mathf.Max(0.1f, enemyAbsorptionDuration));
        }

        float duration = Mathf.Max(0f, enemyAbsorptionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = duration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);

            if (portalEffects != null)
            {
                float pulse = 1f +
                    Mathf.Sin(normalized * Mathf.PI * 6f) *
                    Mathf.Max(0f, portalPulseAmount);

                portalEffects.transform.localScale =
                    portalBaseScale * pulse;
            }

            yield return null;
        }

        RestorePortalScale();
        transitionRoutine = null;

        if (absorptionEventRaised)
        {
            yield break;
        }

        absorptionEventRaised = true;

        Debug.Log(
            "[DreamTransition] 적 흡수 연출 완료. " +
            "EnemyAbsorptionCompleted 이벤트를 발생시킵니다.",
            this);

        EnemyAbsorptionCompleted?.Invoke();
    }

    private void BeginFullVRTransition()
    {
        StopTransitionRoutine();
        fullVREventRaised = false;
        transitionRoutine = StartCoroutine(FullVRTransitionRoutine());
    }

    private IEnumerator FullVRTransitionRoutine()
    {
        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            transitionTitle,
            transitionSubtitle,
            Mathf.Max(0.1f, fullVRTransitionDelay + postTransitionHold));

        if (fullVRTransitionDelay > 0f)
        {
            yield return new WaitForSeconds(fullVRTransitionDelay);
        }

        ApplyFullDreamlandState();

        if (postTransitionHold > 0f)
        {
            yield return new WaitForSeconds(postTransitionHold);
        }

        transitionRoutine = null;

        if (fullVREventRaised)
        {
            yield break;
        }

        fullVREventRaised = true;

        Debug.Log(
            "[DreamTransition] 전체 꿈나라 상태 적용 완료. " +
            "FullVRTransitionCompleted 이벤트를 발생시킵니다.",
            this);

        FullVRTransitionCompleted?.Invoke();
    }

    [ContextMenu("테스트 - 현실 상태 적용")]
    public void ApplyRealityState()
    {
        SetActiveSafe(realityWorld, true);
        SetActiveSafe(interiorDream, false);
        SetActiveSafe(finalDreamland, false);
        SetActiveSafe(portalEffects, false);

        Debug.Log("[DreamTransition] 현실 시작 상태를 적용했습니다.", this);
    }

    [ContextMenu("테스트 - Stage 2 포탈 상태 적용")]
    public void ApplyStage2PortalState()
    {
        SetActiveSafe(realityWorld, true);
        SetActiveSafe(interiorDream, false);
        SetActiveSafe(finalDreamland, false);
        SetActiveSafe(portalEffects, true);

        Debug.Log("[DreamTransition] Stage 2 포탈 확장 상태를 적용했습니다.", this);
    }

    [ContextMenu("테스트 - 완전 꿈나라 상태 적용")]
    public void ApplyFullDreamlandState()
    {
        SetActiveSafe(realityWorld, false);
        SetActiveSafe(interiorDream, true);
        SetActiveSafe(finalDreamland, true);
        SetActiveSafe(portalEffects, false);

        Debug.Log("[DreamTransition] 완전한 꿈나라 상태를 적용했습니다.", this);
    }

    private void StopTransitionRoutine()
    {
        if (transitionRoutine == null)
        {
            return;
        }

        StopCoroutine(transitionRoutine);
        transitionRoutine = null;
    }

    private void CapturePortalBaseScale()
    {
        if (portalEffects != null)
        {
            portalBaseScale = portalEffects.transform.localScale;
        }
    }

    private void RestorePortalScale()
    {
        if (portalEffects != null)
        {
            portalEffects.transform.localScale = portalBaseScale;
        }
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Transform[] transforms =
            UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        enemyAbsorptionDuration = Mathf.Max(0f, enemyAbsorptionDuration);
        portalPulseAmount = Mathf.Max(0f, portalPulseAmount);
        fullVRTransitionDelay = Mathf.Max(0f, fullVRTransitionDelay);
        postTransitionHold = Mathf.Max(0f, postTransitionHold);
    }
}
