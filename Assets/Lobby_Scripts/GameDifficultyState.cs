using Fusion;

public enum GameDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>
/// 로비 전체가 공유하는 난이도 설정.
///
/// LobbyPlayerState(직업 선택 등)와 달리 개별 플레이어 값이 아니라
/// 방에 딱 하나만 존재하는 "공용 설정"이다. RoomManager가 마스터
/// 클라이언트(가장 먼저 방을 만든 쪽)에서만 한 번 스폰한다.
///
/// Shared Mode 규칙상 값을 실제로 바꿀 수 있는 건 이 오브젝트의
/// State Authority(=스폰한 마스터 클라이언트)뿐이다. 다른 클라이언트가
/// 화살표를 누르면 RPC로 "이걸로 바꿔줘"라고 요청만 하고, State
/// Authority가 값을 바꾸면 그 결과가 다시 모두에게 동기화된다
/// (EnemyHealth의 데미지 요청 패턴과 동일).
///
/// Fusion에서 런타임에 Spawn한 오브젝트는 씬이 바뀌어도 유지되므로,
/// Dreamland_map_3로 넘어간 뒤에도 이 값을 그대로 읽어서 몬스터 체력
/// 배율 등 실제 게임플레이 난이도에 반영할 수 있다(추후 확장 지점).
/// </summary>
public class GameDifficultyState : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(HandleDifficultyChanged))]
    public GameDifficulty CurrentDifficulty { get; set; }

    /// <summary>
    /// 실제로 재현된 오류: 참가자가 접속한 직후(호스트 콘솔에
    /// "InvalidOperationException: Error when accessing
    /// GameDifficultyState.CurrentDifficulty..." 로 정확히 찍힘) 이
    /// 오브젝트가 FindAnyObjectByType으로는 이미 "찾아지지만" Fusion이
    /// 아직 [Networked] 값을 읽을 수 있는 상태로 완전히 붙여놓기 전인
    /// 짧은 순간이 있다. 이 틈에 CurrentDifficulty를 읽으면 예외가 나고,
    /// 그 예외가 호출한 쪽 메서드를 중간에서 끊어버려서 로비 흐름(난이도
    /// UI 갱신 -> 게임 시작 진행)이 그 프레임에서 깨진다. 읽기 전에
    /// 항상 이 값으로 먼저 확인해야 안전하다.
    /// </summary>
    public bool IsReady => Object != null && Object.IsValid;

    public event System.Action<GameDifficulty> DifficultyChanged;

    /// <summary>
    /// 누가 호출하든 안전하게 난이도를 바꾼다.
    /// 내가 State Authority면 바로 적용하고, 아니면 RPC로 요청만 보낸다.
    /// </summary>
    public void RequestSetDifficulty(GameDifficulty difficulty)
    {
        if (Object == null)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            CurrentDifficulty = difficulty;
        }
        else
        {
            RPC_RequestSetDifficulty(difficulty);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetDifficulty(GameDifficulty difficulty)
    {
        CurrentDifficulty = difficulty;
    }

    private void HandleDifficultyChanged()
    {
        DifficultyChanged?.Invoke(CurrentDifficulty);
    }
}
