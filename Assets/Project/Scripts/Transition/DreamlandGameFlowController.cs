using System;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 각 단계의 최종 완료 이벤트만 받아 전체 게임 진행을 연결합니다.
///
/// TutorialStage1Director.Stage1Completed
///     -> Stage 2
/// Stage2Director.Stage2Completed
///     -> 적 흡수 연출
/// DreamlandTransitionController.EnemyAbsorptionCompleted
///     -> 완전 꿈나라 전환
/// DreamlandTransitionController.FullVRTransitionCompleted
///     -> 최종 보스전
/// FinalBossDirector.BossDefeated
///     -> 엔딩
/// EndingDirector.EndingCompleted
///     -> Finished
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamlandGameFlowController : MonoBehaviour
{
    public enum GameFlowState
    {
        WaitingForStage1Complete,

        // Stage2WaveController가 내부 공격 단계를 직접 관리합니다.
        Stage2Wave1,

        // 기존 테스트 코드와의 호환을 위해 남겨둔 상태입니다.
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
    private Stage2Director stage2Director;

    [SerializeField]
    private DreamlandTransitionController transitionController;

    [SerializeField]
    private FinalBossDirector finalBossDirector;

    [SerializeField]
    private EndingDirector endingDirector;

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

        currentStateElapsedTime += Time.deltaTime;
        totalElapsedTime += Time.deltaTime;

        // 전투와 단계 완료는 이벤트로 판정합니다.
        // 강제 종료 타이머는 사용하지 않습니다.
        currentStateRemainingTime = 0f;
    }

    private void ResolveFlowComponents()
    {
        if (stage1Director == null)
        {
            stage1Director =
                UnityEngine.Object.FindAnyObjectByType<TutorialStage1Director>();
        }

        if (stage2Director == null)
        {
            stage2Director =
                UnityEngine.Object.FindAnyObjectByType<Stage2Director>();
        }

        if (transitionController == null)
        {
            transitionController =
                UnityEngine.Object.FindAnyObjectByType<DreamlandTransitionController>();
        }

        if (finalBossDirector == null)
        {
            finalBossDirector =
                UnityEngine.Object.FindAnyObjectByType<FinalBossDirector>();
        }

        if (endingDirector == null)
        {
            endingDirector =
                UnityEngine.Object.FindAnyObjectByType<EndingDirector>();
        }
    }

    private void SubscribeEvents()
    {
        if (stage1Director != null)
        {
            stage1Director.Stage1Completed -= HandleStage1Completed;
            stage1Director.Stage1Completed += HandleStage1Completed;
        }
        else
        {
            Debug.LogWarning(
                "[GameFlow] TutorialStage1Director를 찾지 못했습니다.",
                this);
        }

        if (stage2Director != null)
        {
            stage2Director.Stage2Completed -= HandleStage2Completed;
            stage2Director.Stage2Completed += HandleStage2Completed;

            stage2Director.Stage2Failed -= HandleStage2Failed;
            stage2Director.Stage2Failed += HandleStage2Failed;
        }
        else
        {
            Debug.LogWarning(
                "[GameFlow] Stage2Director를 찾지 못했습니다.",
                this);
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
        else
        {
            Debug.LogWarning(
                "[GameFlow] DreamlandTransitionController를 찾지 못했습니다. " +
                "Stage 2 이후 전환이 멈출 수 있습니다.",
                this);
        }

        if (finalBossDirector != null)
        {
            finalBossDirector.BossDefeated -= HandleBossDefeated;
            finalBossDirector.BossDefeated += HandleBossDefeated;

            finalBossDirector.BossFailed -= HandleBossFailed;
            finalBossDirector.BossFailed += HandleBossFailed;
        }
        else
        {
            Debug.LogWarning(
                "[GameFlow] FinalBossDirector를 찾지 못했습니다.",
                this);
        }

        if (endingDirector != null)
        {
            endingDirector.EndingCompleted -= HandleEndingCompleted;
            endingDirector.EndingCompleted += HandleEndingCompleted;
        }
        else
        {
            Debug.LogWarning(
                "[GameFlow] EndingDirector를 찾지 못했습니다.",
                this);
        }
    }

    private void UnsubscribeEvents()
    {
        if (stage1Director != null)
        {
            stage1Director.Stage1Completed -= HandleStage1Completed;
        }

        if (stage2Director != null)
        {
            stage2Director.Stage2Completed -= HandleStage2Completed;
            stage2Director.Stage2Failed -= HandleStage2Failed;
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
            finalBossDirector.BossDefeated -= HandleBossDefeated;
            finalBossDirector.BossFailed -= HandleBossFailed;
        }

        if (endingDirector != null)
        {
            endingDirector.EndingCompleted -= HandleEndingCompleted;
        }
    }

    private void HandleStage1Completed()
    {
        if (stage1CompletionHandled)
        {
            Debug.LogWarning(
                "[GameFlow] Stage1Completed가 중복 전달되어 무시했습니다.",
                this);
            return;
        }

        if (currentState != GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] Stage 1 완료 대기 상태가 아니므로 " +
                "Stage1Completed를 무시했습니다. 현재 상태: " + currentState,
                this);
            return;
        }

        stage1CompletionHandled = true;

        Debug.Log(
            "[GameFlow] Stage1Completed 수신. Stage 2를 시작합니다.",
            this);

        StartStage2();
    }

    private void HandleStage2Completed()
    {
        if (stage2CompletionHandled || stage2FailureHandled)
        {
            return;
        }

        if (currentState != GameFlowState.Stage2Wave1)
        {
            Debug.LogWarning(
                "[GameFlow] Stage 2 전투 상태가 아니므로 " +
                "Stage2Completed를 무시했습니다. 현재 상태: " + currentState,
                this);
            return;
        }

        stage2CompletionHandled = true;
        absorptionCompletionHandled = false;
        fullVRCompletionHandled = false;

        Debug.Log(
            "[GameFlow] Stage2Completed 수신. 적 흡수 연출을 시작합니다.",
            this);

        ChangeState(GameFlowState.EnemyAbsorption);
    }

    private void HandleStage2Failed()
    {
        if (stage2FailureHandled || stage2CompletionHandled)
        {
            return;
        }

        stage2FailureHandled = true;
        EnterGameOver("Stage 2 실패");
    }

    private void HandleEnemyAbsorptionCompleted()
    {
        if (absorptionCompletionHandled)
        {
            return;
        }

        if (currentState != GameFlowState.EnemyAbsorption)
        {
            Debug.LogWarning(
                "[GameFlow] EnemyAbsorption 상태가 아니므로 " +
                "흡수 완료 신호를 무시했습니다. 현재 상태: " + currentState,
                this);
            return;
        }

        absorptionCompletionHandled = true;

        Debug.Log(
            "[GameFlow] 적 흡수 연출 완료. 완전 꿈나라 전환을 시작합니다.",
            this);

        ChangeState(GameFlowState.FullVRTransition);
    }

    private void HandleFullVRTransitionCompleted()
    {
        if (fullVRCompletionHandled)
        {
            return;
        }

        if (currentState != GameFlowState.FullVRTransition)
        {
            Debug.LogWarning(
                "[GameFlow] FullVRTransition 상태가 아니므로 " +
                "전환 완료 신호를 무시했습니다. 현재 상태: " + currentState,
                this);
            return;
        }

        fullVRCompletionHandled = true;
        bossCompletionHandled = false;
        bossFailureHandled = false;

        Debug.Log(
            "[GameFlow] 완전 꿈나라 전환 완료. 최종 보스전을 시작합니다.",
            this);

        ChangeState(GameFlowState.BossBattle);
    }

    private void HandleBossDefeated()
    {
        if (bossCompletionHandled || bossFailureHandled)
        {
            return;
        }

        if (currentState != GameFlowState.BossBattle)
        {
            Debug.LogWarning(
                "[GameFlow] BossBattle 상태가 아니므로 " +
                "보스 처치 신호를 무시했습니다. 현재 상태: " + currentState,
                this);
            return;
        }

        bossCompletionHandled = true;
        endingCompletionHandled = false;

        Debug.Log(
            "[GameFlow] BossDefeated 수신. 엔딩을 시작합니다.",
            this);

        ChangeState(GameFlowState.Ending);
    }

    private void HandleBossFailed()
    {
        if (bossFailureHandled || bossCompletionHandled)
        {
            return;
        }

        bossFailureHandled = true;
        EnterGameOver("최종 보스전 실패");
    }

    private void HandleEndingCompleted()
    {
        if (endingCompletionHandled)
        {
            return;
        }

        if (currentState != GameFlowState.Ending)
        {
            Debug.LogWarning(
                "[GameFlow] Ending 상태가 아니므로 " +
                "엔딩 완료 신호를 무시했습니다. 현재 상태: " + currentState,
                this);
            return;
        }

        endingCompletionHandled = true;
        ChangeState(GameFlowState.Finished);
        isRunning = false;

        Debug.Log(
            "[GameFlow] 전체 게임 진행이 Finished 상태로 완료됐습니다.",
            this);
    }

    private void PrepareForStage1Completion()
    {
        currentState = GameFlowState.WaitingForStage1Complete;
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
        if (currentState != GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] Stage 2 시작 요청을 무시했습니다. 현재 상태: " +
                currentState,
                this);
            return;
        }

        totalElapsedTime = 0f;
        stage2CompletionHandled = false;
        stage2FailureHandled = false;
        isRunning = true;

        ChangeState(GameFlowState.Stage2Wave1);

        Debug.Log(
            "[GameFlow] Stage 2 전투 상태를 시작했습니다.",
            this);
    }

    /// <summary>
    /// 기존 외부 코드와의 호환용 연결 지점입니다.
    /// 새 구조에서는 FinalBossDirector.BossDefeated를 직접 구독합니다.
    /// </summary>
    public void NotifyBossDefeated()
    {
        HandleBossDefeated();
    }

    /// <summary>
    /// 기존 외부 코드와의 호환용 연결 지점입니다.
    /// 새 구조에서는 EndingDirector.EndingCompleted를 직접 구독합니다.
    /// </summary>
    public void FinishEnding()
    {
        HandleEndingCompleted();
    }

    private void EnterGameOver(string reason)
    {
        isRunning = false;
        ChangeState(GameFlowState.GameOver);

        Debug.Log(
            "[GameFlow] GameOver 상태로 전환했습니다. 사유: " + reason,
            this);
    }

    private void ChangeState(GameFlowState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        GameFlowState previousState = currentState;

        currentState = nextState;
        currentStateElapsedTime = 0f;
        currentStateRemainingTime = 0f;

        Debug.Log(
            "[GameFlow] 상태 변경: " +
            previousState + " → " + currentState,
            this);

        OnStateChanged?.Invoke(currentState);
    }

    [ContextMenu("테스트 - Stage 2 시작")]
    private void TestStartStage2()
    {
        if (currentState != GameFlowState.WaitingForStage1Complete)
        {
            PrepareForStage1Completion();
        }

        StartStage2();
    }

    [ContextMenu("테스트 - Stage 2 이후 전환 시작")]
    private void TestStartPostStage2Transition()
    {
        isRunning = true;
        absorptionCompletionHandled = false;
        fullVRCompletionHandled = false;
        ChangeState(GameFlowState.EnemyAbsorption);
    }

    [ContextMenu("테스트 - 보스전 직접 시작")]
    private void TestStartBossBattle()
    {
        isRunning = true;
        bossCompletionHandled = false;
        bossFailureHandled = false;

        if (transitionController != null)
        {
            transitionController.ApplyFullDreamlandState();
        }

        ChangeState(GameFlowState.BossBattle);
    }

    [ContextMenu("테스트 - 엔딩 직접 시작")]
    private void TestStartEnding()
    {
        isRunning = true;
        endingCompletionHandled = false;
        ChangeState(GameFlowState.Ending);
    }

    [ContextMenu("게임 진행 일시 정지")]
    public void PauseGameFlow()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        Debug.Log("[GameFlow] 게임 진행 일시 정지", this);
    }

    [ContextMenu("게임 진행 재개")]
    public void ResumeGameFlow()
    {
        if (currentState == GameFlowState.WaitingForStage1Complete ||
            currentState == GameFlowState.Finished ||
            currentState == GameFlowState.GameOver)
        {
            Debug.LogWarning(
                "[GameFlow] 현재 상태에서는 진행을 재개할 수 없습니다: " +
                currentState,
                this);
            return;
        }

        isRunning = true;
        Debug.Log("[GameFlow] 게임 진행 재개", this);
    }

    [ContextMenu("게임 진행 초기화")]
    public void ResetGameFlow()
    {
        PrepareForStage1Completion();
    }
}
