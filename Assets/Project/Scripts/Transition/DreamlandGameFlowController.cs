using System;
using UnityEngine;

/// <summary>
/// Stage 1 완료 이후의 전체 게임 진행을 관리하는 컨트롤러
///
/// 현재는 테스트 메뉴를 통해 Stage 2를 시작한다.
/// 나중에 TutorialStage1Director의 완료 이벤트를 연결할 예정이다.
/// </summary>
[DisallowMultipleComponent]
public class DreamlandGameFlowController : MonoBehaviour
{
    /// <summary>
    /// Stage 1 이후의 전체 게임 진행 상태
    /// </summary>
    public enum GameFlowState
    {
        // 팀원의 Stage 1 완료 이벤트를 기다리는 상태
        WaitingForStage1Complete,

        // Stage 2 내부 공격 단계
        Stage2Wave1,
        Stage2Wave2,
        Stage2Final,

        // 남은 일반 적이 검은 기운으로 흡수되는 단계
        EnemyAbsorption,

        // MR에서 완전 VR로 전환하는 단계
        FullVRTransition,

        // 보스전
        BossBattle,

        // 엔딩
        Ending,

        // 전체 진행 완료
        Finished
    }

    /// <summary>
    /// 상태가 변경될 때 다른 스크립트에 알려주는 이벤트
    ///
    /// 나중에 적 스폰, 포탈, 침식, VR 전환 스크립트가
    /// 이 이벤트를 구독해서 각 상태에 맞는 기능을 실행한다.
    /// </summary>
    public event Action<GameFlowState> OnStateChanged;


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


    [Header("Stage 2 시간")]

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


    [Header("보스전 및 엔딩 시간")]

    [Tooltip("보스전 임시 제한 시간. 나중에는 보스 처치를 우선으로 처리한다.")]
    [Min(0.1f)]
    [SerializeField]
    private float bossBattleDuration = 240f;

    [Tooltip("엔딩 진행 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float endingDuration = 60f;


    /// <summary>
    /// 다른 스크립트가 현재 상태를 확인할 때 사용한다.
    /// 외부에서는 상태를 직접 변경할 수 없다.
    /// </summary>
    public GameFlowState CurrentState => currentState;

    /// <summary>
    /// 현재 타이머가 실행 중인지 알려준다.
    /// </summary>
    public bool IsRunning => isRunning;

    /// <summary>
    /// 현재 상태에서 경과한 시간을 알려준다.
    /// </summary>
    public float CurrentStateElapsedTime => currentStateElapsedTime;

    /// <summary>
    /// Stage 2 시작 이후의 전체 경과 시간을 알려준다.
    /// </summary>
    public float TotalElapsedTime => totalElapsedTime;


    private void Start()
    {
        // 게임 시작 시 Stage 1 완료를 기다리는 상태로 둔다.
        // Stage 2는 자동으로 시작하지 않는다.
        PrepareForStage1Completion();
    }

    private void Update()
    {
        // 진행 중이 아니라면 시간을 증가시키지 않는다.
        if (!isRunning)
        {
            return;
        }

        // 현재 상태 경과 시간과 전체 경과 시간을 증가시킨다.
        currentStateElapsedTime += Time.deltaTime;
        totalElapsedTime += Time.deltaTime;

        // 현재 상태의 남은 시간을 갱신한다.
        UpdateRemainingTime();

        // 현재 상태의 설정 시간이 끝났다면 다음 상태로 이동한다.
        if (currentStateElapsedTime >= GetCurrentStateDuration())
        {
            MoveToNextState();
        }
    }

    /// <summary>
    /// Stage 1 완료 이벤트를 기다리는 초기 상태로 준비한다.
    /// </summary>
    private void PrepareForStage1Completion()
    {
        currentState = GameFlowState.WaitingForStage1Complete;
        currentStateElapsedTime = 0f;
        currentStateRemainingTime = 0f;
        totalElapsedTime = 0f;
        isRunning = false;

        Debug.Log("[GameFlow] Stage 1 완료 이벤트를 기다리는 중입니다.");
    }

    /// <summary>
    /// Stage 2를 첫 번째 공격부터 시작한다.
    ///
    /// 나중에는 팀원의 Stage 1 완료 이벤트가
    /// 이 함수를 호출하도록 연결한다.
    /// </summary>
    public void StartStage2()
    {
        // 이미 Stage 2 이후 진행 중이라면 중복 시작하지 않는다.
        if (isRunning &&
            currentState != GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning("[GameFlow] Stage 2가 이미 진행 중입니다.");
            return;
        }

        totalElapsedTime = 0f;
        isRunning = true;

        ChangeState(GameFlowState.Stage2Wave1);

        Debug.Log("[GameFlow] Stage 2 진행을 시작합니다.");
    }

    /// <summary>
    /// 현재 상태에 설정된 시간을 반환한다.
    /// </summary>
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

    /// <summary>
    /// 현재 상태의 남은 시간을 계산한다.
    /// </summary>
    private void UpdateRemainingTime()
    {
        float duration = GetCurrentStateDuration();

        currentStateRemainingTime =
            Mathf.Max(0f, duration - currentStateElapsedTime);
    }

    /// <summary>
    /// 현재 상태에서 다음 상태로 이동한다.
    /// </summary>
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

    /// <summary>
    /// 새로운 진행 상태로 변경한다.
    /// </summary>
    private void ChangeState(GameFlowState nextState)
    {
        GameFlowState previousState = currentState;

        currentState = nextState;
        currentStateElapsedTime = 0f;

        UpdateRemainingTime();

        Debug.Log(
            "[GameFlow] 상태 변경: "
            + previousState
            + " → "
            + currentState
        );

        // 다른 기능 스크립트에 상태 변경을 알린다.
        OnStateChanged?.Invoke(currentState);

        // 마지막 상태에 도착하면 타이머를 정지한다.
        if (currentState == GameFlowState.Finished)
        {
            isRunning = false;
            currentStateRemainingTime = 0f;

            Debug.Log("[GameFlow] Stage 2 이후 전체 진행이 완료되었습니다.");
        }
    }

    /// <summary>
    /// 테스트를 위해 Stage 1 완료 이벤트 없이 Stage 2를 시작한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 시작")]
    private void TestStartStage2()
    {
        StartStage2();
    }

    /// <summary>
    /// 테스트를 위해 시간을 기다리지 않고 다음 상태로 이동한다.
    /// </summary>
    [ContextMenu("테스트 - 다음 상태로 이동")]
    private void TestMoveToNextState()
    {
        if (currentState == GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning(
                "[GameFlow] 먼저 '테스트 - Stage 2 시작'을 실행하세요.");
            return;
        }

        if (currentState == GameFlowState.Finished)
        {
            Debug.LogWarning("[GameFlow] 이미 Finished 상태입니다.");
            return;
        }

        MoveToNextState();
    }

    /// <summary>
    /// 진행 타이머를 일시 정지한다.
    /// </summary>
    [ContextMenu("게임 진행 일시 정지")]
    public void PauseGameFlow()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;

        Debug.Log("[GameFlow] 게임 진행 일시 정지");
    }

    /// <summary>
    /// 일시 정지된 진행 타이머를 다시 작동시킨다.
    /// </summary>
    [ContextMenu("게임 진행 재개")]
    public void ResumeGameFlow()
    {
        if (currentState == GameFlowState.WaitingForStage1Complete)
        {
            Debug.LogWarning("[GameFlow] 아직 Stage 2가 시작되지 않았습니다.");
            return;
        }

        if (currentState == GameFlowState.Finished)
        {
            Debug.LogWarning("[GameFlow] 이미 전체 진행이 완료되었습니다.");
            return;
        }

        isRunning = true;

        Debug.Log("[GameFlow] 게임 진행 재개");
    }

    /// <summary>
    /// Stage 1 완료 대기 상태로 초기화한다.
    /// </summary>
    [ContextMenu("게임 진행 초기화")]
    public void ResetGameFlow()
    {
        PrepareForStage1Completion();
    }

    private void OnValidate()
    {
        // Inspector에서 시간이 0 이하가 되지 않도록 보정한다.
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