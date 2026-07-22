using System;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 각 게임 단계의 최종 완료 이벤트를 받아
/// 다음 단계로 연결하는 전체 게임 진행 컨트롤러입니다.
///
/// 현재 단계에서는 TutorialStage1Director.Stage1Completed를 받아
/// Stage 2 첫 상태로 진입하는 연결만 실제로 사용합니다.
/// Stage 2 전투 완료와 보스 처치 기반 전환은 이후 별도 Director와 연결합니다.
/// </summary>
[DisallowMultipleComponent]
public class DreamlandGameFlowController : MonoBehaviour
{
    /// <summary>
    /// Stage 1 이후의 전체 게임 진행 상태입니다.
    /// </summary>
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
        Finished
    }

    /// <summary>
    /// 상태가 변경될 때 다른 스크립트에 알려주는 이벤트입니다.
    /// </summary>
    public event Action<GameFlowState> OnStateChanged;

    [Header("Stage 1 연결")]
    [Tooltip("튜토리얼과 Stage 1 마무리를 담당하는 Director입니다.")]
    [SerializeField]
    private TutorialStage1Director stage1Director;

    [Header("현재 진행 상태")]
    [Tooltip("현재 게임 진행 상태")]
    [SerializeField]
    private GameFlowState currentState =
        GameFlowState.WaitingForStage1Complete;

    [Tooltip("현재 상태가 시작된 후 경과한 시간")]
    [SerializeField]
    private float currentStateElapsedTime;

    [Tooltip("현재 상태가 끝나기까지 남은 시간")]
    [SerializeField]
    private float currentStateRemainingTime;

    [Tooltip("Stage 2 시작 이후 전체 경과 시간")]
    [SerializeField]
    private float totalElapsedTime;

    [Header("게임 실행 상태")]
    [Tooltip("현재 Stage 2 이후 진행 타이머가 작동 중인지 표시")]
    [SerializeField]
    private bool isRunning;

    [Tooltip(
        "기존 시간제 Stage 2 진행을 임시로 사용할 때만 켭니다. " +
        "최종 구조에서는 전투 완료 이벤트를 사용하므로 기본값은 꺼둡니다.")]
    [SerializeField]
    private bool enableLegacyTimedAutoAdvance;

    [Header("Stage 2 시간 - 임시 레거시")]
    [Tooltip("Stage 2 첫 번째 공격 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float stage2Wave1Duration = 80f;

    [Tooltip("Stage 2 두 번째 공격 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float stage2Wave2Duration = 80f;

    [Tooltip("Stage 2 마지막 공격 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float stage2FinalDuration = 80f;

    [Header("중간 전환 시간")]
    [Tooltip("남은 일반 적이 검은 입자로 흡수되는 연출 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float enemyAbsorptionDuration = 5f;

    [Tooltip("Passthrough 종료와 전체 꿈나라 활성화 전환 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float fullVRTransitionDuration = 5f;

    [Header("보스전 및 엔딩 시간 - 임시 레거시")]
    [Tooltip("보스전 임시 시간. 최종 구조에서는 보스 처치 이벤트를 사용합니다.")]
    [Min(0.1f)]
    [SerializeField]
    private float bossBattleDuration = 240f;

    [Tooltip("엔딩 진행 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float endingDuration = 60f;

    private bool stage1CompletionHandled;

    public GameFlowState CurrentState => currentState;
    public bool IsRunning => isRunning;
    public float CurrentStateElapsedTime => currentStateElapsedTime;
    public float CurrentStateRemainingTime => currentStateRemainingTime;
    public float TotalElapsedTime => totalElapsedTime;

    private void Awake()
    {
        ResolveStage1Director();
    }

    private void OnEnable()
    {
        SubscribeToStage1Director();
    }

    private void Start()
    {
        PrepareForStage1Completion();
    }

    private void OnDisable()
    {
        UnsubscribeFromStage1Director();
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        currentStateElapsedTime += Time.deltaTime;
        totalElapsedTime += Time.deltaTime;
        UpdateRemainingTime();

        /*
         * Stage 2 전투는 최종적으로 시간 경과가 아니라
         * Stage2Director.Stage2Completed 같은 완료 이벤트로 전환해야 합니다.
         *
         * 따라서 기존 시간제 자동 진행은 Inspector에서 명시적으로 켠 경우에만
         * 임시 테스트 용도로 작동합니다.
         */
        if (enableLegacyTimedAutoAdvance &&
            currentStateElapsedTime >= GetCurrentStateDuration())
        {
            MoveToNextState();
        }
    }

    /// <summary>
    /// Inspector 참조가 비어 있으면 현재 활성 씬에서 Director를 자동으로 찾습니다.
    /// </summary>
    private void ResolveStage1Director()
    {
        if (stage1Director != null)
        {
            return;
        }

        stage1Director =
            UnityEngine.Object.FindAnyObjectByType<TutorialStage1Director>();

        if (stage1Director == null)
        {
            Debug.LogWarning(
                "[GameFlow] TutorialStage1Director를 찾지 못했습니다. " +
                "Inspector의 Stage 1 Director 참조를 확인하세요.",
                this);
        }
    }

    private void SubscribeToStage1Director()
    {
        ResolveStage1Director();

        if (stage1Director == null)
        {
            return;
        }

        // 중복 구독을 방지합니다.
        stage1Director.Stage1Completed -= HandleStage1Completed;
        stage1Director.Stage1Completed += HandleStage1Completed;
    }

    private void UnsubscribeFromStage1Director()
    {
        if (stage1Director != null)
        {
            stage1Director.Stage1Completed -= HandleStage1Completed;
        }
    }

    /// <summary>
    /// TutorialStage1Director의 최종 완료 이벤트를 받아 Stage 2를 시작합니다.
    /// Stage1WaveController의 내부 전투 완료 이벤트는 직접 구독하지 않습니다.
    /// </summary>
    private void HandleStage1Completed()
    {
        if (stage1CompletionHandled)
        {
            Debug.LogWarning(
                "[GameFlow] Stage1Completed가 중복으로 전달되어 무시했습니다.",
                this);
            return;
        }

        if (currentState != GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] Stage 1 완료 대기 상태가 아니므로 " +
                "Stage1Completed 신호를 무시했습니다. 현재 상태: " +
                currentState,
                this);
            return;
        }

        stage1CompletionHandled = true;

        Debug.Log(
            "[GameFlow] TutorialStage1Director.Stage1Completed 수신. " +
            "Stage 2로 전환합니다.",
            this);

        StartStage2();
    }

    /// <summary>
    /// Stage 1 완료 이벤트를 기다리는 초기 상태로 준비합니다.
    /// </summary>
    private void PrepareForStage1Completion()
    {
        currentState = GameFlowState.WaitingForStage1Complete;
        currentStateElapsedTime = 0f;
        currentStateRemainingTime = 0f;
        totalElapsedTime = 0f;
        isRunning = false;
        stage1CompletionHandled = false;

        Debug.Log("[GameFlow] Stage 1 완료 이벤트를 기다리는 중입니다.", this);
    }

    /// <summary>
    /// Stage 2를 첫 번째 공격 상태부터 시작합니다.
    /// </summary>
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
        isRunning = true;

        ChangeState(GameFlowState.Stage2Wave1);

        Debug.Log(
            "[GameFlow] Stage 2 첫 번째 상태를 시작했습니다. " +
            "시간제 자동 진행은 " +
            (enableLegacyTimedAutoAdvance ? "활성화" : "비활성화") +
            " 상태입니다.",
            this);
    }

    private float GetCurrentStateDuration()
    {
        switch (currentState)
        {
            case GameFlowState.Stage2Wave1:
                return stage2Wave1Duration;

            case GameFlowState.Stage2Wave2:
                return stage2Wave2Duration;

            case GameFlowState.Stage2Final:
                return stage2FinalDuration;

            case GameFlowState.EnemyAbsorption:
                return enemyAbsorptionDuration;

            case GameFlowState.FullVRTransition:
                return fullVRTransitionDuration;

            case GameFlowState.BossBattle:
                return bossBattleDuration;

            case GameFlowState.Ending:
                return endingDuration;

            case GameFlowState.WaitingForStage1Complete:
            case GameFlowState.Finished:
            default:
                return 0f;
        }
    }

    private void UpdateRemainingTime()
    {
        if (!enableLegacyTimedAutoAdvance)
        {
            currentStateRemainingTime = 0f;
            return;
        }

        float duration = GetCurrentStateDuration();

        currentStateRemainingTime =
            Mathf.Max(0f, duration - currentStateElapsedTime);
    }

    private void MoveToNextState()
    {
        switch (currentState)
        {
            case GameFlowState.Stage2Wave1:
                ChangeState(GameFlowState.Stage2Wave2);
                break;

            case GameFlowState.Stage2Wave2:
                ChangeState(GameFlowState.Stage2Final);
                break;

            case GameFlowState.Stage2Final:
                ChangeState(GameFlowState.EnemyAbsorption);
                break;

            case GameFlowState.EnemyAbsorption:
                ChangeState(GameFlowState.FullVRTransition);
                break;

            case GameFlowState.FullVRTransition:
                ChangeState(GameFlowState.BossBattle);
                break;

            case GameFlowState.BossBattle:
                ChangeState(GameFlowState.Ending);
                break;

            case GameFlowState.Ending:
                ChangeState(GameFlowState.Finished);
                break;
        }
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

        UpdateRemainingTime();

        Debug.Log(
            "[GameFlow] 상태 변경: " +
            previousState +
            " → " +
            currentState,
            this);

        OnStateChanged?.Invoke(currentState);

        if (currentState == GameFlowState.Finished)
        {
            isRunning = false;
            currentStateRemainingTime = 0f;

            Debug.Log(
                "[GameFlow] Stage 2 이후 전체 진행이 완료되었습니다.",
                this);
        }
    }

    [ContextMenu("테스트 - Stage 2 시작")]
    private void TestStartStage2()
    {
        StartStage2();
    }

    [ContextMenu("테스트 - 다음 상태로 이동")]
    private void TestMoveToNextState()
    {
        if (currentState == GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] 먼저 '테스트 - Stage 2 시작'을 실행하세요.",
                this);
            return;
        }

        if (currentState == GameFlowState.Finished)
        {
            Debug.LogWarning("[GameFlow] 이미 Finished 상태입니다.", this);
            return;
        }

        MoveToNextState();
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
        if (currentState == GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] 아직 Stage 2가 시작되지 않았습니다.",
                this);
            return;
        }

        if (currentState == GameFlowState.Finished)
        {
            Debug.LogWarning("[GameFlow] 이미 전체 진행이 완료되었습니다.", this);
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

    private void OnValidate()
    {
        stage2Wave1Duration = Mathf.Max(0.1f, stage2Wave1Duration);
        stage2Wave2Duration = Mathf.Max(0.1f, stage2Wave2Duration);
        stage2FinalDuration = Mathf.Max(0.1f, stage2FinalDuration);

        enemyAbsorptionDuration =
            Mathf.Max(0.1f, enemyAbsorptionDuration);

        fullVRTransitionDuration =
            Mathf.Max(0.1f, fullVRTransitionDuration);

        bossBattleDuration = Mathf.Max(0.1f, bossBattleDuration);
        endingDuration = Mathf.Max(0.1f, endingDuration);
    }
}
