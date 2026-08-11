using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 방 생성/접속(Lobby &amp; Room Session)과, 접속한 플레이어의 캐릭터 스폰을 담당한다.
/// SimpleStartMenu.cs(FusionIntroShared 데모)에서 검증된 NetworkRunner.StartGame 패턴을 재사용했다.
///
/// 사용법:
/// 1) 씬에 빈 오브젝트를 만들고 이 스크립트를 붙인다.
/// 2) Player Prefab 필드에 NetworkObject가 붙은 플레이어 프리팹을 연결한다.
/// 3) 방 이름 입력 UI에서 CreateOrJoinRoom(방이름)을 호출한다.
/// 4) 직업 선택 UI 버튼에서 RoomManager.SelectedJob 값을 먼저 세팅한 뒤 방에 입장한다.
/// </summary>
public class RoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // 로비(직업 선택 화면)에서 미리 골라 둔 직업을 저장해 뒀다가,
    // 캐릭터 스폰 직후 PlayerJobController.SetJob()에 전달한다.
    public static PlayerJob SelectedJob = PlayerJob.Police;

    [Header("Fusion 연결 설정")]
    [Tooltip("NetworkRunner 컴포넌트만 붙어 있는 빈 프리팹")]
    [SerializeField] private NetworkRunner runnerPrefab;

    [Tooltip("NetworkObject가 붙어 있는 플레이어 프리팹")]
    [SerializeField] private GameObject playerPrefab;

    private NetworkRunner _runner;
    private bool _isStarting;


    /// <summary>
    /// 주어진 이름의 방을 생성하거나, 이미 있으면 그 방에 접속한다.
    /// Shared Mode에서는 같은 SessionName으로 StartGame을 호출하면
    /// Photon이 알아서 방을 만들거나 기존 방에 붙여준다.
    /// </summary>
    public async void CreateOrJoinRoom(string roomName)
    {
        if (_isStarting || _runner != null) return;

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("[RoomManager] 방 이름이 비어 있습니다.");
            return;
        }

        if (string.IsNullOrEmpty(PhotonAppSettings.Global.AppSettings.AppIdFusion))
        {
            Debug.LogError(
                "[RoomManager] Photon App ID가 설정되지 않았습니다. " +
                "Tools > Fusion > Fusion Hub에서 설정을 확인하세요.");
            return;
        }

        _isStarting = true;

        _runner = Instantiate(runnerPrefab);
        _runner.name = "NetworkRunner";
        _runner.AddCallbacks(this);

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var startArguments = new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = roomName,
            Scene = sceneInfo,
        };

        var result = await _runner.StartGame(startArguments);

        if (!result.Ok)
        {
            Debug.LogError($"[RoomManager] 방 생성/접속 실패: {result.ShutdownReason}");
            Destroy(_runner.gameObject);
            _runner = null;
        }

        _isStarting = false;
    }


    /// <summary>
    /// Shared Mode에서는 새 플레이어가 들어올 때마다 이미 접속해 있는
    /// 모든 클라이언트에게 OnPlayerJoined가 호출된다.
    /// 따라서 반드시 "그게 나 자신일 때만" 내 캐릭터를 스폰해야 한다.
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player != runner.LocalPlayer) return;
        if (playerPrefab == null)
        {
            Debug.LogError("[RoomManager] Player Prefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPosition = new Vector3(
            UnityEngine.Random.Range(-2f, 2f),
            1f,
            UnityEngine.Random.Range(-2f, 2f));

        // onBeforeSpawned 콜백은 오브젝트가 네트워크에 올라가기(Spawned() 호출) 전에 실행되므로,
        // 여기서 CurrentJob을 세팅해야 나뿐 아니라 나중에 들어오는 다른 플레이어에게도
        // 처음부터 올바른 직업으로 보인다.
        runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player,
            (r, obj) =>
            {
                var jobController = obj.GetComponent<PlayerJobController>();
                if (jobController != null)
                {
                    jobController.SetJob(SelectedJob);
                }
            });
    }


    // ------------------------------------------------------------------
    // 아래는 INetworkRunnerCallbacks 인터페이스의 나머지 필수 구현이다.
    // 지금 당장 쓰는 기능은 없지만, 인터페이스를 구현하려면 전부 정의해야 한다.
    // (Fusion 패치 버전에 따라 멤버 목록이 달라질 수 있음 - 컴파일 에러가 나면
    //  IDE의 "인터페이스 구현" 자동 완성 기능으로 정확한 목록을 다시 채워 넣으면 된다.)
    // ------------------------------------------------------------------

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
