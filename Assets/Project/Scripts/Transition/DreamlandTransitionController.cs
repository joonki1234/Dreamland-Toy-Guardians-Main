using System;
using System.Collections;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// Stage 2 이후의 적 흡수 연출과
/// 현실/MR 표현에서 완전한 꿈나라 표현으로의
/// 시각 전환을 담당합니다.
///
/// 꿈나라 길은 04_INTERIOR_DREAM과 분리된
/// 04_DREAM_ROAD 아래에서 별도로 관리합니다.
///
/// 게임 시작 시:
/// - 현실 표현 활성화
/// - 내부 꿈나라 비활성화
/// - 길 부모는 활성화
/// - 실제 Road_0~4는 DreamRoadRevealController가 숨김
/// - 최종 꿈나라 비활성화
/// - 포탈 전환 효과 비활성화
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamlandTransitionController : MonoBehaviour
{
    [Header("References")]

    [Tooltip("전체 게임 진행 상태를 관리하는 컨트롤러")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [Tooltip("미션 배너와 대사를 출력하는 UI")]
    [SerializeField]
    private MissionBannerUI missionUI;

    [Tooltip("파란 하늘에서 분홍빛 꿈나라 하늘로 전환하는 컨트롤러")]
    [SerializeField]
    private DreamSkyTransitionController skyTransitionController;


    [Header("World Groups")]

    [Tooltip("03_REALITY_WORLD 오브젝트")]
    [SerializeField]
    private GameObject realityWorld;

    [Tooltip(
        "04_INTERIOR_DREAM 오브젝트. " +
        "DreamFloor, DreamSkyDome, DreamLighting, DreamDust가 들어갑니다.")]
    [SerializeField]
    private GameObject interiorDream;

    [Tooltip(
        "04_DREAM_ROAD 오브젝트. " +
        "DreamRoad와 Road_0~4가 들어갑니다.")]
    [SerializeField]
    private GameObject dreamRoadRoot;

    [Tooltip("05_FINAL_DREAMLAND 오브젝트")]
    [SerializeField]
    private GameObject finalDreamland;

    [Tooltip("06_PORTAL_EFFECTS 오브젝트")]
    [SerializeField]
    private GameObject portalEffects;


    [Header("적 흡수 연출")]

    [Tooltip("남은 적 에너지가 포탈로 흡수되는 연출 시간")]
    [Min(0f)]
    [SerializeField]
    private float enemyAbsorptionDuration = 3f;

    [Tooltip("흡수 중 포탈이 커졌다 작아지는 정도")]
    [Min(0f)]
    [SerializeField]
    private float portalPulseAmount = 0.12f;

    [SerializeField]
    private string absorptionTitle =
        "DREAM ENERGY CONVERGENCE";

    [SerializeField]
    private string absorptionSubtitle =
        "남은 꿈의 기운이 하나로 모입니다";

    [TextArea(2, 4)]
    [SerializeField]
    private string absorptionMessage =
        "검은 기운이 균열로 빨려 들어가고 있어. " +
        "곧 완전한 꿈나라가 열릴 거야!";


    [Header("완전 꿈나라 전환")]

    [Tooltip("하늘 전환 시작 후 꿈나라 오브젝트를 켜기까지의 시간")]
    [Min(0f)]
    [SerializeField]
    private float fullVRTransitionDelay = 1.5f;

    [Tooltip("전체 꿈나라 적용 후 다음 상태로 넘어가기 전 대기 시간")]
    [Min(0f)]
    [SerializeField]
    private float postTransitionHold = 1f;

    [SerializeField]
    private string transitionTitle =
        "DREAMLAND OPEN";

    [SerializeField]
    private string transitionSubtitle =
        "현실의 경계가 사라집니다";


    [Header("Start State")]

    [Tooltip("게임 시작 시 현실 시작 상태를 자동으로 적용합니다.")]
    [SerializeField]
    private bool applyInitialStateOnStart = true;


    private Coroutine transitionRoutine;

    private Vector3 portalBaseScale =
        Vector3.one;

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
            gameFlowController.OnStateChanged -=
                HandleStateChanged;

            gameFlowController.OnStateChanged +=
                HandleStateChanged;
        }
    }


    private void Start()
    {
        if (!applyInitialStateOnStart)
        {
            return;
        }

        if (gameFlowController == null ||
            gameFlowController.CurrentState ==
            DreamlandGameFlowController.GameFlowState
                .WaitingForStage1Complete)
        {
            ApplyRealityState();
        }
    }


    private void OnDisable()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -=
                HandleStateChanged;
        }

        StopTransitionRoutine();
        RestorePortalScale();
    }


    /// <summary>
    /// Inspector 참조가 비어 있는 경우
    /// 씬의 오브젝트 이름을 기준으로 자동 탐색합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (gameFlowController == null)
        {
            gameFlowController =
                UnityEngine.Object.FindAnyObjectByType
                    <DreamlandGameFlowController>();
        }

        if (missionUI == null)
        {
            missionUI =
                UnityEngine.Object.FindAnyObjectByType
                    <MissionBannerUI>();
        }

        if (skyTransitionController == null)
        {
            skyTransitionController =
                UnityEngine.Object.FindAnyObjectByType
                    <DreamSkyTransitionController>();
        }

        realityWorld ??=
            FindSceneObject("03_REALITY_WORLD");

        interiorDream ??=
            FindSceneObject("04_INTERIOR_DREAM");

        dreamRoadRoot ??=
            FindSceneObject("04_DREAM_ROAD");

        finalDreamland ??=
            FindSceneObject("05_FINAL_DREAMLAND");

        portalEffects ??=
            FindSceneObject("06_PORTAL_EFFECTS");
    }


    /// <summary>
    /// 전체 게임 진행 상태가 변경됐을 때
    /// 해당 상태에 맞는 월드 표현을 적용합니다.
    /// </summary>
    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        switch (newState)
        {
            case DreamlandGameFlowController
                .GameFlowState.WaitingForStage1Complete:

                StopTransitionRoutine();
                ApplyRealityState();
                break;


            case DreamlandGameFlowController
                .GameFlowState.Stage2Wave1:

                StopTransitionRoutine();
                ApplyStage2PortalState();
                break;


            case DreamlandGameFlowController
                .GameFlowState.EnemyAbsorption:

                BeginEnemyAbsorption();
                break;


            case DreamlandGameFlowController
                .GameFlowState.FullVRTransition:

                BeginFullVRTransition();
                break;


            case DreamlandGameFlowController
                .GameFlowState.BossBattle:

                StopTransitionRoutine();
                ApplyFullDreamlandState();
                break;


            case DreamlandGameFlowController
                .GameFlowState.GameOver:

                StopTransitionRoutine();
                RestorePortalScale();
                break;
        }
    }


    /// <summary>
    /// 적 흡수 연출을 시작합니다.
    /// </summary>
    private void BeginEnemyAbsorption()
    {
        StopTransitionRoutine();

        absorptionEventRaised = false;

        transitionRoutine =
            StartCoroutine(
                EnemyAbsorptionRoutine());
    }


    /// <summary>
    /// 남은 적 에너지가 포탈에 모이는 동안
    /// 포탈을 반복해서 확대·축소합니다.
    /// </summary>
    private IEnumerator EnemyAbsorptionRoutine()
    {
        ApplyStage2PortalState();
        CapturePortalBaseScale();

        missionUI?.ClearPersistentText();

        missionUI?.ShowBanner(
            absorptionTitle,
            absorptionSubtitle,
            Mathf.Max(
                0.1f,
                enemyAbsorptionDuration));

        if (!string.IsNullOrWhiteSpace(
                absorptionMessage))
        {
            missionUI?.ShowDialogue(
                "장난감 친구",
                absorptionMessage,
                Mathf.Max(
                    0.1f,
                    enemyAbsorptionDuration));
        }

        float duration =
            Mathf.Max(
                0f,
                enemyAbsorptionDuration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed / duration);

            if (portalEffects != null)
            {
                float pulse =
                    1f +
                    Mathf.Sin(
                        normalized *
                        Mathf.PI *
                        6f) *
                    Mathf.Max(
                        0f,
                        portalPulseAmount);

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


    /// <summary>
    /// 완전한 꿈나라 전환 연출을 시작합니다.
    /// </summary>
    private void BeginFullVRTransition()
    {
        StopTransitionRoutine();

        fullVREventRaised = false;

        transitionRoutine =
            StartCoroutine(
                FullVRTransitionRoutine());
    }


    /// <summary>
    /// 하늘을 분홍빛 꿈나라 하늘로 변경한 후
    /// 내부 꿈나라와 최종 꿈나라를 표시합니다.
    /// </summary>
    private IEnumerator FullVRTransitionRoutine()
    {
        missionUI?.ClearPersistentText();

        missionUI?.ShowBanner(
            transitionTitle,
            transitionSubtitle,
            Mathf.Max(
                0.1f,
                fullVRTransitionDelay +
                postTransitionHold));

        /*
         * 파란 가상 하늘에서
         * 분홍빛 꿈나라 하늘로 전환을 시작합니다.
         */
        skyTransitionController?.
            TransitionToPinkSky();

        if (fullVRTransitionDelay > 0f)
        {
            yield return new WaitForSeconds(
                fullVRTransitionDelay);
        }

        ApplyFullDreamlandState();

        if (postTransitionHold > 0f)
        {
            yield return new WaitForSeconds(
                postTransitionHold);
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


    /// <summary>
    /// 게임의 현실 시작 상태를 적용합니다.
    ///
    /// 04_DREAM_ROAD 부모는 활성화하지만,
    /// 실제 Road_0~4는 DreamRoadRevealController가
    /// 게임 시작 시 모두 숨깁니다.
    ///
    /// 따라서 시작 화면에는 길이 전혀 보이지 않습니다.
    /// </summary>
    [ContextMenu("테스트 - 현실 상태 적용")]
    public void ApplyRealityState()
    {
        SetActiveSafe(
            realityWorld,
            true);

        /*
         * 바닥, 조명, 먼지 등 꿈나라 내부 표현은
         * 시작할 때 전부 숨깁니다.
         */
        SetActiveSafe(
            interiorDream,
            false);

        /*
         * 길을 나중에 개별적으로 등장시키기 위해
         * 길의 상위 부모만 활성화합니다.
         *
         * Road_0~4는 DreamRoadRevealController의
         * HideAllRoads()가 비활성화합니다.
         */
        SetActiveSafe(
            dreamRoadRoot,
            true);

        /*
         * 나무, 건물, 성 등 최종 꿈나라 사물은
         * 시작할 때 모두 숨깁니다.
         */
        SetActiveSafe(
            finalDreamland,
            false);

        SetActiveSafe(
            portalEffects,
            false);

        Debug.Log(
            "[DreamTransition] 현실 시작 상태 적용. " +
            "꿈나라 내부와 최종 사물은 숨겼으며, " +
            "Road_0~4도 길 컨트롤러가 숨긴 상태입니다.",
            this);
    }


    /// <summary>
    /// Stage 2 포탈 확장 상태를 적용합니다.
    ///
    /// Stage 1에서 이미 생성된 길은 유지하고,
    /// 내부 꿈나라와 최종 꿈나라 사물은 아직 숨깁니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 포탈 상태 적용")]
    public void ApplyStage2PortalState()
    {
        SetActiveSafe(
            realityWorld,
            true);

        SetActiveSafe(
            interiorDream,
            false);

        /*
         * Stage 1에서 생성된 Road_0~2와
         * Stage 2에서 생성될 Road_3~4가 보일 수 있도록
         * 길의 부모는 활성화 상태로 유지합니다.
         */
        SetActiveSafe(
            dreamRoadRoot,
            true);

        SetActiveSafe(
            finalDreamland,
            false);

        SetActiveSafe(
            portalEffects,
            true);

        /*
         * 가상 세계의 경계가 열리면서
         * 기본 파란 하늘을 적용합니다.
         */
        skyTransitionController?.
            ApplyBlueSkyImmediately();

        Debug.Log(
            "[DreamTransition] Stage 2 포탈 확장 상태를 적용했습니다.",
            this);
    }


    /// <summary>
    /// 완전한 꿈나라 상태를 적용합니다.
    ///
    /// 현실 표현을 숨기고,
    /// 꿈나라 바닥·조명·먼지·최종 사물을 모두 표시합니다.
    /// Stage 1~2에서 등장한 길도 그대로 유지합니다.
    /// </summary>
    [ContextMenu("테스트 - 완전 꿈나라 상태 적용")]
    public void ApplyFullDreamlandState()
    {
        SetActiveSafe(
            realityWorld,
            false);

        SetActiveSafe(
            interiorDream,
            true);

        SetActiveSafe(
            dreamRoadRoot,
            true);

        SetActiveSafe(
            finalDreamland,
            true);

        SetActiveSafe(
            portalEffects,
            false);

        /*
         * 테스트 메뉴로 직접 실행해도
         * 분홍빛 꿈나라 하늘 전환을 확인할 수 있습니다.
         */
        skyTransitionController?.
            TransitionToPinkSky();

        Debug.Log(
            "[DreamTransition] 완전한 꿈나라 상태를 적용했습니다.",
            this);
    }


    /// <summary>
    /// 현재 실행 중인 전환 코루틴을 중단합니다.
    /// </summary>
    private void StopTransitionRoutine()
    {
        if (transitionRoutine == null)
        {
            return;
        }

        StopCoroutine(
            transitionRoutine);

        transitionRoutine = null;
    }


    /// <summary>
    /// 포탈 효과의 기본 크기를 저장합니다.
    /// </summary>
    private void CapturePortalBaseScale()
    {
        if (portalEffects == null)
        {
            return;
        }

        portalBaseScale =
            portalEffects.transform.localScale;
    }


    /// <summary>
    /// 포탈 효과의 크기를 원래 상태로 복원합니다.
    /// </summary>
    private void RestorePortalScale()
    {
        if (portalEffects == null)
        {
            return;
        }

        portalEffects.transform.localScale =
            portalBaseScale;
    }


    /// <summary>
    /// null 여부와 현재 활성 상태를 확인한 후
    /// GameObject를 안전하게 켜거나 끕니다.
    /// </summary>
    private static void SetActiveSafe(
        GameObject target,
        bool active)
    {
        if (target == null)
        {
            return;
        }

        if (target.activeSelf == active)
        {
            return;
        }

        target.SetActive(active);
    }


    /// <summary>
    /// 비활성 오브젝트를 포함해
    /// 씬에서 이름이 일치하는 오브젝트를 찾습니다.
    /// </summary>
    private static GameObject FindSceneObject(
        string objectName)
    {
        Transform[] transforms =
            UnityEngine.Object.FindObjectsByType
                <Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
            {
                continue;
            }

            if (candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }


    private void OnValidate()
    {
        enemyAbsorptionDuration =
            Mathf.Max(
                0f,
                enemyAbsorptionDuration);

        portalPulseAmount =
            Mathf.Max(
                0f,
                portalPulseAmount);

        fullVRTransitionDelay =
            Mathf.Max(
                0f,
                fullVRTransitionDelay);

        postTransitionHold =
            Mathf.Max(
                0f,
                postTransitionHold);
    }
}