using UnityEngine;

/// <summary>
/// DreamlandGameFlowController의 상태 변경 신호를 받아
/// Stage 2 각 단계가 정상적으로 시작되는지 확인하는 테스트 스크립트
/// </summary>
[DisallowMultipleComponent]
public class Stage2FlowListener : MonoBehaviour
{
    [Header("References")]

    [Tooltip("Stage 2 이후의 전체 진행을 관리하는 컨트롤러")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;


    private void OnEnable()
    {
        // 컨트롤러가 연결되어 있다면 상태 변경 이벤트를 구독한다.
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼지거나 제거될 때 이벤트 구독을 해제한다.
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }
    }

    /// <summary>
    /// 게임 진행 상태가 변경될 때 호출된다.
    /// 현재는 Console 메시지만 출력한다.
    /// </summary>
    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        switch (newState)
        {
            case DreamlandGameFlowController.GameFlowState.Stage2Wave1:
                Debug.Log("[Stage2Listener] Stage 2 첫 번째 공격 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.Stage2Wave2:
                Debug.Log("[Stage2Listener] Stage 2 두 번째 공격 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.Stage2Final:
                Debug.Log("[Stage2Listener] Stage 2 최종 공격 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.EnemyAbsorption:
                Debug.Log("[Stage2Listener] 적 흡수 연출 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.FullVRTransition:
                Debug.Log("[Stage2Listener] 완전 VR 전환 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.BossBattle:
                Debug.Log("[Stage2Listener] 보스전 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.Ending:
                Debug.Log("[Stage2Listener] 엔딩 시작");
                break;

            case DreamlandGameFlowController.GameFlowState.Finished:
                Debug.Log("[Stage2Listener] 전체 진행 완료");
                break;
        }
    }
}