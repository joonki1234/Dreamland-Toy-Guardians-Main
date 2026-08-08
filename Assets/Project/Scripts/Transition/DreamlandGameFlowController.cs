using System;
using DreamGuardians;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DreamlandGameFlowController : MonoBehaviour
{
    public enum GameFlowState
    {
        WaitingForStage1Complete,
        Stage2Wave1,
        Stage2Wave2,
        Stage2Final,
        EnemyAbsorption,
        FullVRTransition,
        BossBattle,
        Ending,
        Finished,
        GameOver
    }

    public event Action<GameFlowState> OnStateChanged;

    [Header("Stage 연결")]

    [SerializeField]
    private TutorialStage1Director stage1Director;

    [SerializeField]
    private Stage1WaveController stage1WaveController;

    [SerializeField]
    private Stage2Director stage2Director;

    [SerializeField]
    private Stage2WaveController stage2WaveController;

    [SerializeField]
    private DreamlandTransitionController transitionController;

    [SerializeField]
    private FinalBossDirector finalBossDirector;

    [SerializeField]
    private EndingDirector endingDirector;

    [SerializeField]
    private CoreState core;

    [SerializeField]
    private DreamEnemySpawner enemySpawner;

    [Header("현재 진행 상태")]

    [SerializeField]
    private GameFlowState currentState =
        GameFlowState.WaitingForStage1Complete;

    [SerializeField]
    private float currentStateElapsedTime;

    [SerializeField]
    private float currentStateRemainingTime;

    [SerializeField]
    private float totalElapsedTime;

    [Header("게임 실행 상태")]

    [SerializeField]
    private bool isRunning;

    private bool stage1CompletionHandled;
    private bool stage2CompletionHandled;
    private bool stage2FailureHandled;
    private bool absorptionCompletionHandled;
    private bool fullVRCompletionHandled;
    private bool bossCompletionHandled;
    private bool bossFailureHandled;
    private bool endingCompletionHandled;

    public GameFlowState CurrentState => currentState;
    public bool IsRunning => isRunning;
    public float CurrentStateElapsedTime => currentStateElapsedTime;
    public float CurrentStateRemainingTime => currentStateRemainingTime;
    public float TotalElapsedTime => totalElapsedTime;

    private void Awake()
    {
        ResolveFlowComponents();
    }

    private void OnEnable()
    {
        ResolveFlowComponents();
        SubscribeEvents();
    }

    private void Start()
    {
        PrepareForStage1Completion();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        currentStateElapsedTime +=
            Time.deltaTime;

        totalElapsedTime +=
            Time.deltaTime;

        currentStateRemainingTime = 0f;
    }

    private void ResolveFlowComponents()
    {
        if (stage1Director == null)
        {
            stage1Director =
                UnityEngine.Object
                    .FindAnyObjectByType<TutorialStage1Director>();
        }

        if (stage1WaveController == null)
        {
            stage1WaveController =
                UnityEngine.Object
                    .FindAnyObjectByType<Stage1WaveController>();
        }

        if (stage2Director == null)
        {
            stage2Director =
                UnityEngine.Object
                    .FindAnyObjectByType<Stage2Director>();
        }

        if (stage2WaveController == null)
        {
            stage2WaveController =
                UnityEngine.Object
                    .FindAnyObjectByType<Stage2WaveController>();
        }

        if (transitionController == null)
        {
            transitionController =
                UnityEngine.Object
                    .FindAnyObjectByType<DreamlandTransitionController>();
        }

        if (finalBossDirector == null)
        {
            finalBossDirector =
                UnityEngine.Object
                    .FindAnyObjectByType<FinalBossDirector>();
        }

        if (endingDirector == null)
        {
            endingDirector =
                UnityEngine.Object
                    .FindAnyObjectByType<EndingDirector>();
        }

        if (enemySpawner == null)
        {
            enemySpawner =
                UnityEngine.Object
                    .FindAnyObjectByType<DreamEnemySpawner>();
        }

        if (core == null)
        {
            core =
                UnityEngine.Object
                    .FindAnyObjectByType<CoreState>();
        }

        if (core == null && enemySpawner != null)
        {
            core = enemySpawner.TargetCore;
        }
    }

    private void SubscribeEvents()
    {
        if (stage1Director != null)
        {
            stage1Director.Stage1Completed -=
                HandleStage1Completed;

            stage1Director.Stage1Completed +=
                HandleStage1Completed;
        }

        if (stage2Director != null)
        {
            stage2Director.Stage2Completed -=
                HandleStage2Completed;

            stage2Director.Stage2Completed +=
                HandleStage2Completed;

            stage2Director.Stage2Failed -=
                HandleStage2Failed;

            stage2Director.Stage2Failed +=
                HandleStage2Failed;
        }

        if (transitionController != null)
        {
            transitionController.EnemyAbsorptionCompleted -=
                HandleEnemyAbsorptionCompleted;

            transitionController.EnemyAbsorptionCompleted +=
                HandleEnemyAbsorptionCompleted;

            transitionController.FullVRTransitionCompleted -=
                HandleFullVRTransitionCompleted;

            transitionController.FullVRTransitionCompleted +=
                HandleFullVRTransitionCompleted;
        }

        if (finalBossDirector != null)
        {
            finalBossDirector.BossDefeated -=
                HandleBossDefeated;

            finalBossDirector.BossDefeated +=
                HandleBossDefeated;

            finalBossDirector.BossFailed -=
                HandleBossFailed;

            finalBossDirector.BossFailed +=
                HandleBossFailed;
        }

        if (endingDirector != null)
        {
            endingDirector.EndingCompleted -=
                HandleEndingCompleted;

            endingDirector.EndingCompleted +=
                HandleEndingCompleted;
        }
    }

    private void UnsubscribeEvents()
    {
        if (stage1Director != null)
        {
            stage1Director.Stage1Completed -=
                HandleStage1Completed;
        }

        if (stage2Director != null)
        {
            stage2Director.Stage2Completed -=
                HandleStage2Completed;

            stage2Director.Stage2Failed -=
                HandleStage2Failed;
        }

        if (transitionController != null)
        {
            transitionController.EnemyAbsorptionCompleted -=
                HandleEnemyAbsorptionCompleted;

            transitionController.FullVRTransitionCompleted -=
                HandleFullVRTransitionCompleted;
        }

        if (finalBossDirector != null)
        {
            finalBossDirector.BossDefeated -=
                HandleBossDefeated;

            finalBossDirector.BossFailed -=
                HandleBossFailed;
        }

        if (endingDirector != null)
        {
            endingDirector.EndingCompleted -=
                HandleEndingCompleted;
        }
    }

    private void HandleStage1Completed()
    {
        if (stage1CompletionHandled)
        {
            return;
        }

        if (currentState !=
            GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] Stage 1 완료 대기 상태가 아니므로 " +
                "Stage1Completed를 무시했습니다.",
                this);

            return;
        }

        stage1CompletionHandled = true;

        Debug.Log(
            "[GameFlow] Stage 1 완료. Stage 2를 시작합니다.",
            this);

        StartStage2();
    }

    private void HandleStage2Completed()
    {
        if (stage2CompletionHandled ||
            stage2FailureHandled)
        {
            return;
        }

        if (!IsStage2WaveState(currentState))
        {
            return;
        }

        stage2CompletionHandled = true;
        absorptionCompletionHandled = false;
        fullVRCompletionHandled = false;

        ChangeState(
            GameFlowState.EnemyAbsorption);
    }

    private void HandleStage2Failed()
    {
        if (stage2FailureHandled ||
            stage2CompletionHandled)
        {
            return;
        }

        stage2FailureHandled = true;

        EnterGameOver(
            "Stage 2 실패");
    }

    private void HandleEnemyAbsorptionCompleted()
    {
        if (absorptionCompletionHandled ||
            currentState != GameFlowState.EnemyAbsorption)
        {
            return;
        }

        absorptionCompletionHandled = true;

        ChangeState(
            GameFlowState.FullVRTransition);
    }

    private void HandleFullVRTransitionCompleted()
    {
        if (fullVRCompletionHandled ||
            currentState != GameFlowState.FullVRTransition)
        {
            return;
        }

        fullVRCompletionHandled = true;
        bossCompletionHandled = false;
        bossFailureHandled = false;

        ChangeState(
            GameFlowState.BossBattle);
    }

    private void HandleBossDefeated()
    {
        if (bossCompletionHandled ||
            bossFailureHandled ||
            currentState != GameFlowState.BossBattle)
        {
            return;
        }

        bossCompletionHandled = true;
        endingCompletionHandled = false;

        ChangeState(
            GameFlowState.Ending);
    }

    private void HandleBossFailed()
    {
        if (bossFailureHandled ||
            bossCompletionHandled)
        {
            return;
        }

        bossFailureHandled = true;

        EnterGameOver(
            "최종 보스전 실패");
    }

    private void HandleEndingCompleted()
    {
        if (endingCompletionHandled ||
            currentState != GameFlowState.Ending)
        {
            return;
        }

        endingCompletionHandled = true;

        ChangeState(
            GameFlowState.Finished);

        isRunning = false;
    }

    private void PrepareForStage1Completion()
    {
        currentState =
            GameFlowState.WaitingForStage1Complete;

        currentStateElapsedTime = 0f;
        currentStateRemainingTime = 0f;
        totalElapsedTime = 0f;
        isRunning = false;

        stage1CompletionHandled = false;
        stage2CompletionHandled = false;
        stage2FailureHandled = false;
        absorptionCompletionHandled = false;
        fullVRCompletionHandled = false;
        bossCompletionHandled = false;
        bossFailureHandled = false;
        endingCompletionHandled = false;

        Debug.Log(
            "[GameFlow] Stage 1 완료 이벤트를 기다리는 중입니다.",
            this);
    }

    public void StartStage2()
    {
        if (currentState !=
            GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] Stage 2 시작 요청을 무시했습니다. " +
                "현재 상태: " + currentState,
                this);

            return;
        }

        totalElapsedTime = 0f;
        stage2CompletionHandled = false;
        stage2FailureHandled = false;
        isRunning = true;

        ChangeState(
            GameFlowState.Stage2Wave1);

        Debug.Log(
            "[GameFlow] Stage 2 전투 상태를 시작했습니다.",
            this);
    }

    public void NotifyStage2WaveStarted(
        Stage2WaveController.Stage2WavePhase phase)
    {
        if (!IsStage2WaveState(currentState))
        {
            Debug.LogWarning(
                "[GameFlow] Stage 2 웨이브 상태가 아닌 동안 " +
                "웨이브 변경 요청을 받았습니다. 현재 상태: " +
                currentState,
                this);

            return;
        }

        GameFlowState nextState;

        switch (phase)
        {
            case Stage2WaveController.Stage2WavePhase.First:
                nextState = GameFlowState.Stage2Wave1;
                break;

            case Stage2WaveController.Stage2WavePhase.Second:
                nextState = GameFlowState.Stage2Wave2;
                break;

            case Stage2WaveController.Stage2WavePhase.Final:
                nextState = GameFlowState.Stage2Final;
                break;

            default:
                return;
        }

        ChangeState(nextState);
    }

    private static bool IsStage2WaveState(
        GameFlowState state)
    {
        return
            state == GameFlowState.Stage2Wave1 ||
            state == GameFlowState.Stage2Wave2 ||
            state == GameFlowState.Stage2Final;
    }

    public void NotifyBossDefeated()
    {
        HandleBossDefeated();
    }

    public void FinishEnding()
    {
        HandleEndingCompleted();
    }

    private void EnterGameOver(
        string reason)
    {
        isRunning = false;

        ChangeState(
            GameFlowState.GameOver);

        Debug.Log(
            "[GameFlow] GameOver 상태 전환: " +
            reason,
            this);
    }

    private void ChangeState(
        GameFlowState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        GameFlowState previousState =
            currentState;

        currentState = nextState;
        currentStateElapsedTime = 0f;
        currentStateRemainingTime = 0f;

        Debug.Log(
            "[GameFlow] 상태 변경: " +
            previousState +
            " → " +
            currentState,
            this);

        OnStateChanged?.Invoke(
            currentState);
    }

    private void PrepareDirectScenarioTest(
        bool applyRealityState,
        bool applyFullDreamlandState)
    {
        ResolveFlowComponents();

        stage1Director?.StopForStage2Test();
        stage1WaveController?.StopForStage2Test();
        stage2Director?.AbortAndResetForTest();
        stage2WaveController?.AbortAndResetForTest();
        finalBossDirector?.AbortAndResetForTest();
        endingDirector?.AbortAndResetForTest();
        enemySpawner?.DespawnAllEnemiesImmediately();
        core?.ResetCore();

        if (applyFullDreamlandState)
        {
            transitionController?.ApplyFullDreamlandState();
        }
        else if (applyRealityState)
        {
            transitionController?.ApplyRealityState();
        }
        else
        {
            transitionController?.ApplyStage2PortalState();
        }

        PrepareForStage1Completion();
    }

    [ContextMenu("테스트 - 튜토리얼부터 시작")]
    private void TestStartTutorial()
    {
        PrepareDirectScenarioTest(
            applyRealityState: true,
            applyFullDreamlandState: false);

        stage1Director?.Begin();

        Debug.Log(
            "[GameFlow] 튜토리얼부터 테스트를 시작했습니다.",
            this);
    }

    /// <summary>
    /// 튜토리얼을 건너뛰고 Stage 1부터 시작합니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1부터 시작")]
    private void TestSkipTutorialAndStartStage1()
    {
        PrepareDirectScenarioTest(
            applyRealityState: true,
            applyFullDreamlandState: false);

        if (stage1Director == null)
        {
            Debug.LogError(
                "[GameFlow] TutorialStage1Director가 연결되지 않았습니다.",
                this);

            return;
        }

        stage1Director.SkipTutorialAndStartStage1();

        Debug.Log(
            "[GameFlow] 튜토리얼을 스킵하고 Stage 1 테스트를 시작했습니다.",
            this);
    }

    /// <summary>
    /// 튜토리얼과 Stage 1을 모두 중단하고 Stage 2부터 테스트합니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2부터 시작")]
    private void TestStartStage2()
    {
        PrepareDirectScenarioTest(
            applyRealityState: false,
            applyFullDreamlandState: false);

        StartStage2();

        Debug.Log(
            "[GameFlow] Stage 2부터 테스트를 시작했습니다.",
            this);
    }

    [ContextMenu("테스트 - Stage 2 이후 전환 시작")]
    private void TestStartPostStage2Transition()
    {
        PrepareDirectScenarioTest(
            applyRealityState: false,
            applyFullDreamlandState: false);

        isRunning = true;
        absorptionCompletionHandled = false;
        fullVRCompletionHandled = false;

        ChangeState(
            GameFlowState.EnemyAbsorption);
    }

    [ContextMenu("테스트 - 보스전부터 시작")]
    private void TestStartBossBattle()
    {
        PrepareDirectScenarioTest(
            applyRealityState: false,
            applyFullDreamlandState: true);

        isRunning = true;
        bossCompletionHandled = false;
        bossFailureHandled = false;

        ChangeState(
            GameFlowState.BossBattle);

        Debug.Log(
            "[GameFlow] 보스전부터 테스트를 시작했습니다.",
            this);
    }

    [ContextMenu("테스트 - 엔딩 직접 시작")]
    private void TestStartEnding()
    {
        PrepareDirectScenarioTest(
            applyRealityState: false,
            applyFullDreamlandState: true);

        isRunning = true;
        endingCompletionHandled = false;

        ChangeState(
            GameFlowState.Ending);
    }

    [ContextMenu("게임 진행 일시 정지")]
    public void PauseGameFlow()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;

        Debug.Log(
            "[GameFlow] 게임 진행 일시 정지",
            this);
    }

    [ContextMenu("게임 진행 재개")]
    public void ResumeGameFlow()
    {
        if (currentState ==
                GameFlowState.WaitingForStage1Complete ||
            currentState ==
                GameFlowState.Finished ||
            currentState ==
                GameFlowState.GameOver)
        {
            Debug.LogWarning(
                "[GameFlow] 현재 상태에서는 진행을 재개할 수 없습니다: " +
                currentState,
                this);

            return;
        }

        isRunning = true;

        Debug.Log(
            "[GameFlow] 게임 진행 재개",
            this);
    }

    [ContextMenu("게임 진행 초기화")]
    public void ResetGameFlow()
    {
        PrepareForStage1Completion();
    }
}
