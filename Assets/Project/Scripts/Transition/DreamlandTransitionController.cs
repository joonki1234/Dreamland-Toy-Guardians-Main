using System;
using System.Collections;
using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 현실 상태에서 완전한 꿈나라 상태로 전환하는 연출을 담당합니다.
///
/// 진행 순서:
///
/// 게임 시작 / Stage 1
/// - 현실 오브젝트 활성화
/// - 꿈나라 바닥 비활성화
/// - 하늘 비활성화
///
/// Stage 2
/// - 파란 하늘 활성화
/// - Part_1~4, fence 순차 등장
/// - 꿈나라 바닥은 계속 비활성화
///
/// Full VR Transition
/// - 분홍 하늘 전환
/// - 꿈나라 바닥 등장
/// - 성 등장
/// - 외곽 나무 등장
/// - 완료 이벤트 발생
///
/// BossBattle
/// - 완성된 꿈나라 상태 유지
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

    [Tooltip("보스 등장 전 24~25번 대사를 직접 말할 3D 장난감 친구")]
    [SerializeField]
    private ToyFriendController toyFriend;

    [SerializeField, Min(0f)]
    private float toyFriendStoryTransitionDuration = 0.35f;

    [Tooltip("하늘 전환을 담당하는 컨트롤러")]
    [SerializeField]
    private DreamSkyTransitionController skyTransitionController;


    [Header("World Groups")]

    [Tooltip("03_REALITY_WORLD")]
    [SerializeField]
    private GameObject realityWorld;

    [Tooltip(
        "기존 04_INTERIOR_DREAM입니다. " +
        "삭제했다면 None으로 둬도 됩니다.")]
    [SerializeField]
    private GameObject interiorDream;

    [Tooltip("04_DREAM_ROAD")]
    [SerializeField]
    private GameObject dreamRoadRoot;

    [Tooltip("05_FINAL_DREAMLAND")]
    [SerializeField]
    private GameObject finalDreamland;

    [Tooltip("06_PORTAL_EFFECTS")]
    [SerializeField]
    private GameObject portalEffects;


    [Header("Full VR Reveal Objects")]

    [Tooltip(
        "05_FINAL_DREAMLAND 아래로 이동한 DreamFloor")]
    [SerializeField]
    private Transform dreamFloor;

    [Tooltip("05_FINAL_DREAMLAND 아래의 CastleScene")]
    [SerializeField]
    private Transform castleScene;

    [Tooltip("05_FINAL_DREAMLAND 아래의 Tree_Border")]
    [SerializeField]
    private Transform treeBorder;

    [Tooltip(
        "보스가 실제로 부수고 등장하는 성(FinalBossDirector의 castleAnchor와 " +
        "동일한 오브젝트). CastleScene이 등장할 때 함께 미리 보여줘서 " +
        "보스전 시작과 동시에 등장&파괴가 겹쳐 임팩트가 약해지는 것을 방지합니다.")]
    [SerializeField]
    private Transform bossCastle;


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
        "잠깐... 이상해. 장난감들은 사라졌는데 오염된 기운이 더 강해지고 있어...";

    [TextArea(2, 4)]
    [SerializeField]
    private string bossSuspenseMessage =
        "이 기운은...? 설마...!";

    [Min(0.1f)]
    [SerializeField]
    private float bossSuspenseDuration = 2.2f;


    [Header("완전 꿈나라 전환")]

    [Tooltip(
        "분홍빛 하늘 전환을 시작한 뒤 " +
        "바닥 연출을 시작하기 전 대기 시간")]
    [Min(0f)]
    [SerializeField]
    private float fullVRTransitionDelay = 1.5f;

    [Tooltip("바닥 완료 후 성이 등장하기 전 시간")]
    [Min(0f)]
    [SerializeField]
    private float floorToCastleDelay = 0.2f;

    [Tooltip("성 완료 후 나무가 등장하기 전 시간")]
    [Min(0f)]
    [SerializeField]
    private float castleToTreeDelay = 0.2f;

    [Tooltip("모든 연출 후 보스전 진입 전 대기 시간")]
    [Min(0f)]
    [SerializeField]
    private float postTransitionHold = 1f;

    [SerializeField]
    private string transitionTitle =
        "DREAMLAND OPEN";

    [SerializeField]
    private string transitionSubtitle =
        "현실의 경계가 사라집니다";


    [Header("바닥 등장 연출")]

    [Tooltip("바닥이 원래 위치보다 아래에서 시작하는 거리")]
    [Min(0f)]
    [SerializeField]
    private float floorStartYOffset = 0.7f;

    [Tooltip("바닥의 시작 크기")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float floorStartScaleMultiplier = 0.92f;

    [Tooltip("바닥이 순간적으로 도달하는 최대 크기")]
    [Min(1f)]
    [SerializeField]
    private float floorOvershootScaleMultiplier = 1.02f;

    [Tooltip("바닥 등장 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float floorRevealDuration = 0.55f;


    [Header("성 등장 연출")]

    [Tooltip("성이 원래 위치보다 아래에서 시작하는 거리")]
    [Min(0f)]
    [SerializeField]
    private float castleStartYOffset = 1.2f;

    [Tooltip("성의 시작 크기")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float castleStartScaleMultiplier = 0.7f;

    [Tooltip("성이 순간적으로 도달하는 최대 크기")]
    [Min(1f)]
    [SerializeField]
    private float castleOvershootScaleMultiplier = 1.04f;

    [Tooltip("성 등장 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float castleRevealDuration = 0.65f;


    [Header("나무 등장 연출")]

    [Tooltip("나무가 원래 위치보다 아래에서 시작하는 거리")]
    [Min(0f)]
    [SerializeField]
    private float treeStartYOffset = 0.8f;

    [Tooltip("나무의 시작 크기")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float treeStartScaleMultiplier = 0.65f;

    [Tooltip("나무가 순간적으로 도달하는 최대 크기")]
    [Min(1f)]
    [SerializeField]
    private float treeOvershootScaleMultiplier = 1.03f;

    [Tooltip("나무 한 개의 등장 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float treeRevealDuration = 0.45f;

    [Tooltip("다음 나무가 등장하는 간격")]
    [Min(0f)]
    [SerializeField]
    private float delayBetweenTrees = 0.025f;


    [Header("Start State")]

    [Tooltip("게임 시작 시 현실 상태를 자동 적용합니다.")]
    [SerializeField]
    private bool applyInitialStateOnStart = true;


    private Coroutine transitionRoutine;

    private Vector3 portalBaseScale = Vector3.one;

    private bool absorptionEventRaised;
    private bool fullVREventRaised;

    private TransformState dreamFloorOriginalState;
    private TransformState castleOriginalState;

    private readonly Dictionary<Transform, TransformState>
        treeOriginalStates =
            new Dictionary<Transform, TransformState>();


    public event Action EnemyAbsorptionCompleted;
    public event Action FullVRTransitionCompleted;


    private sealed class TransformState
    {
        public Vector3 LocalPosition;
        public Vector3 LocalScale;

        public TransformState(
            Vector3 localPosition,
            Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
        }
    }


    private void Awake()
    {
        ApplyStoryDialogueRevision();
        ResolveReferences();
        CapturePortalBaseScale();
        CaptureRevealObjectStates();

        /*
         * Awake에서 먼저 꺼 둡니다.
         *
         * DreamFloor가 05_FINAL_DREAMLAND 아래에 있으므로
         * 부모가 켜져도 Full VR 전환 전까지 나타나지 않습니다.
         */
        HideFullVRObjectsImmediately();
    }


    private void OnEnable()
    {
        ResolveReferences();
        CaptureRevealObjectStates();

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

        if (toyFriend == null)
        {
            toyFriend =
                UnityEngine.Object.FindAnyObjectByType
                    <ToyFriendController>();
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

        dreamFloor ??=
            FindSceneTransform("DreamFloor");

        castleScene ??=
            FindSceneTransform("CastleScene");

        treeBorder ??=
            FindSceneTransform("Tree_Border");
    }


    private void CaptureRevealObjectStates()
    {
        if (dreamFloor != null &&
            dreamFloorOriginalState == null)
        {
            dreamFloorOriginalState =
                new TransformState(
                    dreamFloor.localPosition,
                    dreamFloor.localScale);
        }

        if (castleScene != null &&
            castleOriginalState == null)
        {
            castleOriginalState =
                new TransformState(
                    castleScene.localPosition,
                    castleScene.localScale);
        }

        if (treeBorder == null)
        {
            return;
        }

        for (int i = 0;
             i < treeBorder.childCount;
             i++)
        {
            Transform tree =
                treeBorder.GetChild(i);

            if (tree == null ||
                treeOriginalStates.ContainsKey(tree))
            {
                continue;
            }

            treeOriginalStates.Add(
                tree,
                new TransformState(
                    tree.localPosition,
                    tree.localScale));
        }
    }


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


    private void BeginEnemyAbsorption()
    {
        StopTransitionRoutine();

        absorptionEventRaised = false;

        transitionRoutine =
            StartCoroutine(
                EnemyAbsorptionRoutine());
    }


    private IEnumerator EnemyAbsorptionRoutine()
    {
        // ApplyStage2PortalState()는 finalDreamland를 일단 꺼서
        // DreamWorldRevealController가 Part_1~4/fence를 보여줄 때만 다시 켜지도록
        // 하는 "Stage 2 시작" 전용 초기화다. Stage 2 웨이브가 끝난 지금 다시 호출하면
        // 이미 등장해 있던 소품들이 잠깐 꺼졌다가(FullVRTransition에서 finalDreamland가
        // 다시 켜질 때) 되살아나는 것처럼 보인다. Stage 2 진입 시 이미 적용된 상태이므로
        // 여기서는 다시 호출하지 않는다.
        CapturePortalBaseScale();

        missionUI?.ClearPersistentText();

        if (toyFriend != null)
        {
            yield return toyFriend.ShowForStory(
                toyFriendStoryTransitionDuration);
        }

        missionUI?.ShowBanner(
            absorptionTitle,
            absorptionSubtitle,
            Mathf.Max(
                0.1f,
                enemyAbsorptionDuration));

        if (!string.IsNullOrWhiteSpace(
                absorptionMessage))
        {
            float storyDuration = Mathf.Max(
                0.1f,
                enemyAbsorptionDuration);

            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(
                    absorptionMessage,
                    storyDuration,
                    false);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    absorptionMessage,
                    storyDuration);
            }
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

        if (!string.IsNullOrWhiteSpace(bossSuspenseMessage))
        {
            float suspenseDuration =
                Mathf.Max(0.1f, bossSuspenseDuration);

            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(
                    bossSuspenseMessage,
                    suspenseDuration,
                    false);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    bossSuspenseMessage,
                    suspenseDuration);
            }

            yield return new WaitForSeconds(suspenseDuration);
        }

        transitionRoutine = null;

        if (absorptionEventRaised)
        {
            yield break;
        }

        absorptionEventRaised = true;

        Debug.Log(
            "[DreamTransition] 적 흡수 연출 완료",
            this);

        EnemyAbsorptionCompleted?.Invoke();
    }


    private void BeginFullVRTransition()
    {
        StopTransitionRoutine();

        fullVREventRaised = false;

        transitionRoutine =
            StartCoroutine(
                FullVRTransitionRoutine());
    }


    private IEnumerator FullVRTransitionRoutine()
    {
        missionUI?.ClearPersistentText();

        float bannerDuration =
            fullVRTransitionDelay +
            floorRevealDuration +
            floorToCastleDelay +
            castleRevealDuration +
            castleToTreeDelay +
            CalculateTreeSequenceDuration() +
            postTransitionHold;

        missionUI?.ShowBanner(
            transitionTitle,
            transitionSubtitle,
            Mathf.Max(
                0.1f,
                bannerDuration));


        /*
         * 1. 분홍 하늘 전환 시작
         */
        skyTransitionController?.
            TransitionToPinkSky();


        /*
         * 2. 하늘이 변하는 모습을 먼저 보여줍니다.
         */
        if (fullVRTransitionDelay > 0f)
        {
            yield return new WaitForSeconds(
                fullVRTransitionDelay);
        }


        /*
         * 3. 현실을 제거하고 꿈나라 부모 활성화
         */
        PrepareFullDreamlandRevealState();


        /*
         * 4. DreamFloor 등장
         */
        if (dreamFloor != null &&
            dreamFloorOriginalState != null)
        {
            yield return RevealSingleTransformRoutine(
                dreamFloor,
                dreamFloorOriginalState,
                floorStartYOffset,
                floorStartScaleMultiplier,
                floorOvershootScaleMultiplier,
                floorRevealDuration);
        }


        if (floorToCastleDelay > 0f)
        {
            yield return new WaitForSeconds(
                floorToCastleDelay);
        }


        /*
         * 5. 성 등장
         */
        if (castleScene != null &&
            castleOriginalState != null)
        {
            yield return RevealSingleTransformRoutine(
                castleScene,
                castleOriginalState,
                castleStartYOffset,
                castleStartScaleMultiplier,
                castleOvershootScaleMultiplier,
                castleRevealDuration);
        }

        /*
         * 보스가 부수고 등장할 진짜 성도 이 시점에 미리 세워둡니다.
         * 이렇게 하면 플레이어가 성을 한동안 눈으로 본 뒤 보스전이
         * 시작되면서 극적으로 부서지는 흐름이 되고, 예전처럼 등장하자마자
         * 바로 부서져서 임팩트가 약해지는 문제가 사라집니다.
         */
        if (bossCastle == null)
        {
            Debug.LogWarning(
                "[DreamTransition] bossCastle이 연결되어 있지 않아 " +
                "보스가 부술 성을 미리 세워두지 못했습니다.",
                this);
        }
        else if (!bossCastle.gameObject.activeSelf)
        {
            bossCastle.gameObject.SetActive(true);

            Debug.Log(
                "[DreamTransition] 보스가 부술 성(" +
                bossCastle.name +
                ")을 FullVR 전환 중 미리 세워뒀습니다.",
                this);
        }


        if (castleToTreeDelay > 0f)
        {
            yield return new WaitForSeconds(
                castleToTreeDelay);
        }


        /*
         * 6. 외곽 나무 등장
         */
        yield return RevealTreeBorderRoutine();


        /*
         * 7. 완성된 꿈나라 잠시 유지
         */
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
            "[DreamTransition] 완전 꿈나라 전환 완료",
            this);

        FullVRTransitionCompleted?.Invoke();
    }


    private void PrepareFullDreamlandRevealState()
    {
        CaptureRevealObjectStates();

        /*
         * 현실 오브젝트 제거
         */
        SetActiveSafe(
            realityWorld,
            false);

        /*
         * 기존 Interior Dream이 없어도 문제없습니다.
         */
        SetActiveSafe(
            interiorDream,
            true);

        /*
         * Stage 1에서 만들어진 길 유지
         */
        SetActiveSafe(
            dreamRoadRoot,
            true);

        /*
         * 바닥, 성, 나무를 우선 숨깁니다.
         */
        HideFullVRObjectsImmediately();

        /*
         * Part_1~4와 fence가 들어 있는 부모 활성화
         */
        SetActiveSafe(
            finalDreamland,
            true);

        /*
         * 흡수 포탈 효과 제거
         */
        SetActiveSafe(
            portalEffects,
            false);
    }


    /// <summary>
    /// Full VR 전환 전용 오브젝트를 즉시 숨깁니다.
    ///
    /// DreamFloor가 FinalDreamland 자식이어도
    /// Stage 2에서 미리 나타나지 않게 합니다.
    /// </summary>
    private void HideFullVRObjectsImmediately()
    {
        if (dreamFloor != null)
        {
            dreamFloor.gameObject.SetActive(false);
        }

        if (castleScene != null)
        {
            castleScene.gameObject.SetActive(false);
        }

        if (treeBorder != null)
        {
            treeBorder.gameObject.SetActive(false);
        }
    }


    private IEnumerator RevealTreeBorderRoutine()
    {
        if (treeBorder == null)
        {
            yield break;
        }

        CaptureRevealObjectStates();

        treeBorder.gameObject.SetActive(true);

        List<Transform> trees =
            new List<Transform>();

        for (int i = 0;
             i < treeBorder.childCount;
             i++)
        {
            Transform tree =
                treeBorder.GetChild(i);

            if (tree == null)
            {
                continue;
            }

            trees.Add(tree);

            if (treeOriginalStates.TryGetValue(
                    tree,
                    out TransformState state))
            {
                tree.localPosition =
                    state.LocalPosition;

                tree.localScale =
                    state.LocalScale;
            }

            tree.gameObject.SetActive(false);
        }

        foreach (Transform tree in trees)
        {
            if (!treeOriginalStates.TryGetValue(
                    tree,
                    out TransformState state))
            {
                continue;
            }

            StartCoroutine(
                RevealSingleTransformRoutine(
                    tree,
                    state,
                    treeStartYOffset,
                    treeStartScaleMultiplier,
                    treeOvershootScaleMultiplier,
                    treeRevealDuration));

            if (delayBetweenTrees > 0f)
            {
                yield return new WaitForSeconds(
                    delayBetweenTrees);
            }
        }

        if (treeRevealDuration > 0f)
        {
            yield return new WaitForSeconds(
                treeRevealDuration);
        }
    }


    private IEnumerator RevealSingleTransformRoutine(
        Transform target,
        TransformState originalState,
        float startYOffset,
        float startScaleMultiplier,
        float overshootScaleMultiplier,
        float revealDuration)
    {
        if (target == null ||
            originalState == null)
        {
            yield break;
        }

        Vector3 originalPosition =
            originalState.LocalPosition;

        Vector3 originalScale =
            originalState.LocalScale;

        Vector3 startPosition =
            originalPosition +
            Vector3.down *
            Mathf.Max(
                0f,
                startYOffset);

        Vector3 startScale =
            originalScale *
            Mathf.Clamp(
                startScaleMultiplier,
                0.01f,
                1f);

        Vector3 overshootScale =
            originalScale *
            Mathf.Max(
                1f,
                overshootScaleMultiplier);

        target.localPosition =
            startPosition;

        target.localScale =
            startScale;

        target.gameObject.SetActive(true);

        float duration =
            Mathf.Max(
                0.01f,
                revealDuration);

        float riseDuration =
            duration * 0.8f;

        float settleDuration =
            duration * 0.2f;

        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                riseDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed / riseDuration);

            float eased =
                1f -
                Mathf.Pow(
                    1f - normalized,
                    3f);

            target.localPosition =
                Vector3.Lerp(
                    startPosition,
                    originalPosition,
                    eased);

            target.localScale =
                Vector3.Lerp(
                    startScale,
                    overshootScale,
                    eased);

            yield return null;
        }

        target.localPosition =
            originalPosition;

        target.localScale =
            overshootScale;

        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                settleDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed / settleDuration);

            target.localScale =
                Vector3.Lerp(
                    overshootScale,
                    originalScale,
                    normalized);

            yield return null;
        }

        target.localPosition =
            originalPosition;

        target.localScale =
            originalScale;
    }


    private float CalculateTreeSequenceDuration()
    {
        if (treeBorder == null)
        {
            return 0f;
        }

        int treeCount =
            treeBorder.childCount;

        if (treeCount <= 0)
        {
            return 0f;
        }

        return
            Mathf.Max(
                0f,
                delayBetweenTrees) *
            Mathf.Max(
                0,
                treeCount - 1) +
            Mathf.Max(
                0.01f,
                treeRevealDuration);
    }


    [ContextMenu("테스트 - 현실 상태 적용")]
    public void ApplyRealityState()
    {
        RestoreRevealObjectStates();

        SetActiveSafe(
            realityWorld,
            true);

        SetActiveSafe(
            interiorDream,
            false);

        SetActiveSafe(
            dreamRoadRoot,
            true);

        SetActiveSafe(
            finalDreamland,
            false);

        SetActiveSafe(
            portalEffects,
            false);

        /*
         * 게임 시작과 Stage 1에서는
         * Full VR 오브젝트를 강제로 숨깁니다.
         */
        HideFullVRObjectsImmediately();

        /*
         * 게임 시작과 Stage 1에서는
         * 가상 하늘을 숨깁니다.
         */
        skyTransitionController?.
            HideSkyImmediately();

        Debug.Log(
            "[DreamTransition] 현실 시작 상태 적용 완료",
            this);
    }


    [ContextMenu("테스트 - Stage 2 상태 적용")]
    public void ApplyStage2PortalState()
    {
        SetActiveSafe(
            realityWorld,
            true);

        SetActiveSafe(
            interiorDream,
            false);

        SetActiveSafe(
            dreamRoadRoot,
            true);

        /*
         * DreamWorldRevealController가
         * Part_1 등을 보여줄 때 다시 활성화합니다.
         */
        SetActiveSafe(
            finalDreamland,
            false);

        SetActiveSafe(
            portalEffects,
            true);

        /*
         * DreamFloor가 05_FINAL_DREAMLAND 아래에 있으므로
         * Stage 2에서 부모가 켜져도 바닥이 나오지 않게 합니다.
         */
        HideFullVRObjectsImmediately();

        /*
         * Stage 2에서는 파란 하늘 적용
         */
        skyTransitionController?.
            ApplyBlueSkyImmediately();

        Debug.Log(
            "[DreamTransition] Stage 2 상태 적용 완료",
            this);
    }


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

        RestoreRevealObjectStates();

        if (dreamFloor != null)
        {
            dreamFloor.gameObject.SetActive(true);
        }

        if (castleScene != null)
        {
            castleScene.gameObject.SetActive(true);
        }

        if (treeBorder != null)
        {
            treeBorder.gameObject.SetActive(true);

            for (int i = 0;
                 i < treeBorder.childCount;
                 i++)
            {
                treeBorder.GetChild(i)
                    .gameObject.SetActive(true);
            }
        }

        skyTransitionController?.
            ApplyPinkSkyImmediately();

        Debug.Log(
            "[DreamTransition] 완전 꿈나라 상태 적용 완료",
            this);
    }


    private void RestoreRevealObjectStates()
    {
        if (dreamFloor != null &&
            dreamFloorOriginalState != null)
        {
            dreamFloor.localPosition =
                dreamFloorOriginalState.LocalPosition;

            dreamFloor.localScale =
                dreamFloorOriginalState.LocalScale;
        }

        if (castleScene != null &&
            castleOriginalState != null)
        {
            castleScene.localPosition =
                castleOriginalState.LocalPosition;

            castleScene.localScale =
                castleOriginalState.LocalScale;
        }

        foreach (
            KeyValuePair<Transform, TransformState> pair
            in treeOriginalStates)
        {
            Transform tree =
                pair.Key;

            TransformState state =
                pair.Value;

            if (tree == null ||
                state == null)
            {
                continue;
            }

            tree.localPosition =
                state.LocalPosition;

            tree.localScale =
                state.LocalScale;
        }
    }


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


    private void CapturePortalBaseScale()
    {
        if (portalEffects == null)
        {
            return;
        }

        portalBaseScale =
            portalEffects.transform.localScale;
    }


    private void RestorePortalScale()
    {
        if (portalEffects == null)
        {
            return;
        }

        portalEffects.transform.localScale =
            portalBaseScale;
    }


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


    private static GameObject FindSceneObject(
        string objectName)
    {
        Transform transform =
            FindSceneTransform(
                objectName);

        return transform != null
            ? transform.gameObject
            : null;
    }


    private static Transform FindSceneTransform(
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
                return candidate;
            }
        }

        return null;
    }


    private void ApplyStoryDialogueRevision()
    {
        absorptionMessage =
            "잠깐... 이상해. 장난감들은 사라졌는데 오염된 기운이 더 강해지고 있어...";
        bossSuspenseMessage = "이 기운은...? 설마...!";
    }

    private void OnValidate()
    {
        enemyAbsorptionDuration =
            Mathf.Max(
                0f,
                enemyAbsorptionDuration);

        bossSuspenseDuration = Mathf.Max(0.1f, bossSuspenseDuration);
        toyFriendStoryTransitionDuration =
            Mathf.Max(0f, toyFriendStoryTransitionDuration);

        portalPulseAmount =
            Mathf.Max(
                0f,
                portalPulseAmount);

        fullVRTransitionDelay =
            Mathf.Max(
                0f,
                fullVRTransitionDelay);

        floorToCastleDelay =
            Mathf.Max(
                0f,
                floorToCastleDelay);

        castleToTreeDelay =
            Mathf.Max(
                0f,
                castleToTreeDelay);

        postTransitionHold =
            Mathf.Max(
                0f,
                postTransitionHold);

        floorStartYOffset =
            Mathf.Max(
                0f,
                floorStartYOffset);

        floorStartScaleMultiplier =
            Mathf.Clamp(
                floorStartScaleMultiplier,
                0.01f,
                1f);

        floorOvershootScaleMultiplier =
            Mathf.Max(
                1f,
                floorOvershootScaleMultiplier);

        floorRevealDuration =
            Mathf.Max(
                0.01f,
                floorRevealDuration);

        castleStartYOffset =
            Mathf.Max(
                0f,
                castleStartYOffset);

        castleStartScaleMultiplier =
            Mathf.Clamp(
                castleStartScaleMultiplier,
                0.01f,
                1f);

        castleOvershootScaleMultiplier =
            Mathf.Max(
                1f,
                castleOvershootScaleMultiplier);

        castleRevealDuration =
            Mathf.Max(
                0.01f,
                castleRevealDuration);

        treeStartYOffset =
            Mathf.Max(
                0f,
                treeStartYOffset);

        treeStartScaleMultiplier =
            Mathf.Clamp(
                treeStartScaleMultiplier,
                0.01f,
                1f);

        treeOvershootScaleMultiplier =
            Mathf.Max(
                1f,
                treeOvershootScaleMultiplier);

        treeRevealDuration =
            Mathf.Max(
                0.01f,
                treeRevealDuration);

        delayBetweenTrees =
            Mathf.Max(
                0f,
                delayBetweenTrees);
    }
}