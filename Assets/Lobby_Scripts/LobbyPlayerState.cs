using Fusion;

/// <summary>
/// 로비 단계에서만 쓰이는 가벼운 네트워크 상태 오브젝트.
///
/// 로비에서는 캐릭터 모델이나 무기 같은 무거운 게임플레이 요소가 필요 없고,
/// "이 플레이어가 어떤 직업을 골랐는지 / 준비 버튼을 눌렀는지"만 모두에게
/// 동기화되면 된다. 그래서 PlayerJobController(실제 게임 캐릭터 컴포넌트)와는
/// 완전히 분리된 작은 전용 오브젝트로 만들었다.
///
/// RoomManager.OnPlayerJoined에서 각 플레이어마다 하나씩 스폰되고,
/// Dreamland_map_3로 씬이 바뀐 뒤에도 파괴되지 않는다.
/// (Fusion에서 런타임에 Spawn한 NetworkObject는 씬이 바뀌어도 기본적으로 유지된다.)
/// 맵에 도착하면 RoomManager.OnSceneLoadDone이 이 값을 읽어서
/// 실제 게임 캐릭터(PlayerJobController)에 그대로 적용해 준다.
/// </summary>
public class LobbyPlayerState : NetworkBehaviour
{
    [Networked] public PlayerJob SelectedJob { get; set; }
    [Networked] public NetworkBool HasSelectedJob { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }

    /// <summary>
    /// 직업을 선택한다. 이미 준비 상태였다면 직업이 바뀌는 것이므로
    /// 준비 상태는 다시 눌러야 하도록 초기화한다.
    /// </summary>
    public void SetJob(PlayerJob job)
    {
        if (!Object.HasStateAuthority) return;

        SelectedJob = job;
        HasSelectedJob = true;
        IsReady = false;
    }

    /// <summary>
    /// 같은 직업 버튼을 다시 눌렀을 때 선택을 취소한다.
    /// </summary>
    public void ClearJob()
    {
        if (!Object.HasStateAuthority) return;

        HasSelectedJob = false;
        IsReady = false;
    }

    /// <summary>
    /// 준비 상태를 켜거나 끈다. 직업을 아직 선택하지 않았다면 켤 수 없다.
    /// </summary>
    public void SetReady(bool ready)
    {
        if (!Object.HasStateAuthority) return;
        if (ready && !HasSelectedJob) return;

        IsReady = ready;
    }
}
