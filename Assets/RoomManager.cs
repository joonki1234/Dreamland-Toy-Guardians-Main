using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전체 접속/로비/입장 흐름을 담당하는 중앙 매니저.
///
/// 1) LobbyScene에 배치해 두면 Start()에서 자동으로 방을 생성/접속한다.
/// 2) 접속한 로컬 플레이어마다 가벼운 LobbyPlayerState를 스폰하고,
///    LobbyIntroController에게 "연결 완료, 직업 선택 화면 보여줘"라고 알린다.
/// 3) 전원 준비 완료 후 LobbySelectionController가 LoadGameplayScene()을 호출하면
///    Dreamland_map_3로 씬을 전환한다.
/// 4) Dreamland_map_3에 도착하면(OnSceneLoadDone) 로비에서 고른 직업 그대로
///    실제 게임 캐릭터(gameplayPlayerPrefab)를 스폰한다.
///
/// RoomManager 자신과 NetworkRunner는 DontDestroyOnLoad로 유지되므로
/// 씬이 바뀌어도 같은 접속이 계속 이어진다.
/// </summary>
public class RoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fusion 연결 설정")]
    [Tooltip("NetworkRunner 컴포넌트만 붙어 있는 빈 프리팹")]
    [SerializeField] private NetworkRunner runnerPrefab;

    [Tooltip(
        "같은 이름으로 접속하면 같은 방에 모인다. " +
        "같은 공간에서 여러 기기가 함께 체험하는 XR 특성상 " +
        "방 코드 입력 없이 고정된 이름을 사용한다.")]
    [SerializeField] private string sessionName = "DreamlandRoom";

    [Tooltip("이 오브젝트가 활성화되자마자 자동으로 방을 생성/접속한다.")]
    [SerializeField] private bool autoConnectOnStart = true;

    [Header("로비 단계 프리팹")]
    [Tooltip("NetworkObject + LobbyPlayerState가 붙은 가벼운 로비 상태 프리팹")]
    [SerializeField] private GameObject lobbyPlayerStatePrefab;

    [Header("게임플레이 단계 프리팹")]
    [Tooltip("NetworkObject + NetworkPlayerMovement + PlayerJobController가 붙은 실제 캐릭터 프리팹")]
    [SerializeField] private GameObject gameplayPlayerPrefab;

    [Header("씬 경로")]
    [Tooltip("Build Settings에 등록된 게임 플레이 맵의 씬 파일 경로")]
    [SerializeField] private string gameplayScenePath = "Assets/GameScene/Dreamland_map_3.unity";

    [Header("연동 (선택)")]
    [Tooltip("접속이 끝나면 자동으로 ShowJobSelectionScreen()을 호출해 줄 로비 인트로 컨트롤러")]
    [SerializeField] private LobbyIntroController lobbyIntroController;

    private NetworkRunner _runner;
    private bool _isStarting;
    private bool _gameplaySpawned;
    private bool _devDirectMode;
    private PlayerJob _devDefaultJob;
    private bool _hasPendingJob;
    private PlayerJob _pendingJob;

    /// <summary>다른 스크립트(로비 UI 등)가 참조할 수 있도록 노출한다.</summary>
    public NetworkRunner Runner => _runner;

    /// <summary>로컬 플레이어의 로비 상태. 아직 접속 전이면 null이다.</summary>
    public LobbyPlayerState LocalLobbyPlayerState { get; private set; }


    /// <summary>
    /// Dreamland_map_3를 로비를 거치지 않고 단독으로 열어서 테스트할 때 사용한다.
    /// (DreamlandMapDevEntry.cs가 호출한다) 이 모드에서는 로비 상태/직업 선택 화면을
    /// 건너뛰고 접속하자마자 바로 지정한 직업으로 게임 캐릭터를 스폰한다.
    /// </summary>
    public void EnableDevDirectMode(PlayerJob defaultJob)
    {
        _devDirectMode = true;
        _devDefaultJob = defaultJob;
    }


    private void Start()
    {
        if (autoConnectOnStart)
        {
            CreateOrJoinRoom(sessionName);
        }
    }


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

        // 씬이 바뀌어도(로비 -> 맵) 같은 접속을 계속 유지한다.
        DontDestroyOnLoad(_runner.gameObject);
        DontDestroyOnLoad(gameObject);

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
    /// 전원 준비 완료 후 로비 UI(LobbySelectionController)가 호출한다.
    /// Dreamland_map_3로 씬을 전환한다.
    /// </summary>
    public void LoadGameplayScene()
    {
        if (_runner == null)
        {
            Debug.LogError("[RoomManager] 아직 접속되지 않아 씬을 전환할 수 없습니다.");
            return;
        }

        // LobbyPlayerState는 LobbyScene 안에서 스폰된 오브젝트라, 씬을 전환하면
        // (Unity가 LobbyScene을 통째로 언로드하면서) 같이 파괴되어 버린다.
        // 그래서 Dreamland_map_3에서 OnSceneLoadDone이 불릴 때는 이미 늦다 -
        // 여기, 아직 LobbyScene에 있을 때 값만 미리 복사해 둔다.
        if (LocalLobbyPlayerState != null)
        {
            if (LocalLobbyPlayerState.HasSelectedJob)
            {
                _pendingJob = LocalLobbyPlayerState.SelectedJob;
                _hasPendingJob = true;
            }

            // 씬과 함께 자동으로 사라지게 두면 Fusion 쪽 동기화 상태가 꼬여서
            // tick 관련 AssertException이 날 수 있다 - 미리 정상적으로 정리한다.
            _runner.Despawn(LocalLobbyPlayerState.Object);
            LocalLobbyPlayerState = null;
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(gameplayScenePath);

        if (buildIndex < 0)
        {
            Debug.LogError(
                $"[RoomManager] '{gameplayScenePath}'가 Build Settings에 등록되어 있지 않습니다. " +
                "File > Build Profiles > Scene List에 추가하세요.");
            return;
        }

        // LocalPhysicsMode.Physics3D를 쓰면 이 씬 전용의 "격리된" PhysicsScene이 생성되는데,
        // 이건 Multiple Peer 모드(러너마다 물리를 따로 시뮬레이션)를 위한 옵션이라
        // 우리처럼 Single Peer(Shared Mode, 러너 1개)에서는 아무도 이 격리된 PhysicsScene을
        // Simulate()해주지 않는다. 그 결과 AddForce/velocity로 초기 속도는 걸리지만
        // 실제 위치 갱신(중력 포함)이 전혀 일어나지 않아 총알/음식/흙덩이가 허공에 멈춰버렸다.
        // None으로 두면 Unity 기본(자동 시뮬레이션되는) PhysicsScene을 그대로 사용한다.
        _runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single, LocalPhysicsMode.None, true);
    }


    /// <summary>
    /// Shared Mode에서는 새 플레이어가 들어올 때마다 이미 접속해 있는
    /// 모든 클라이언트에게 OnPlayerJoined가 호출된다.
    /// 반드시 "그게 나 자신일 때만" 내 로비 상태를 스폰해야 한다.
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player != runner.LocalPlayer) return;

        // Dreamland_map_3를 단독으로 열어서 테스트하는 중이면 로비 단계를 통째로 건너뛴다.
        if (_devDirectMode)
        {
            SpawnGameplayCharacter(runner, _devDefaultJob);
            return;
        }

        if (lobbyPlayerStatePrefab == null)
        {
            Debug.LogError("[RoomManager] Lobby Player State Prefab이 연결되지 않았습니다.");
            return;
        }

        NetworkObject stateObject = runner.Spawn(
            lobbyPlayerStatePrefab,
            Vector3.zero,
            Quaternion.identity,
            player);

        runner.SetPlayerObject(player, stateObject);
        LocalLobbyPlayerState = stateObject.GetComponent<LobbyPlayerState>();

        if (lobbyIntroController != null)
        {
            lobbyIntroController.ShowJobSelectionScreen();
        }
    }


    /// <summary>
    /// 씬 로드가 끝날 때마다 호출된다. Dreamland_map_3에 도착한 경우에만
    /// 로비에서 고른 직업으로 실제 게임 캐릭터를 스폰한다.
    ///
    /// (개발용 단독 실행 모드에서는 씬을 "로드"한 게 아니라 이미 그 씬에서
    /// 시작한 것이므로 이 콜백이 아니라 OnPlayerJoined에서 바로 스폰한다.)
    /// </summary>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (_devDirectMode) return;

        string activeSceneName = SceneManager.GetActiveScene().name;
        string targetSceneName = System.IO.Path.GetFileNameWithoutExtension(gameplayScenePath);

        if (activeSceneName != targetSceneName) return;

        // LobbyPlayerState는 LoadGameplayScene()에서 이미 파괴됐으므로,
        // 그때 미리 복사해 둔 값(_pendingJob)을 사용한다.
        PlayerJob job = _hasPendingJob ? _pendingJob : PlayerJob.Police; // 못 골랐을 경우를 대비한 안전한 기본값

        SpawnGameplayCharacter(runner, job);
    }


    /// <summary>
    /// 실제 게임 캐릭터(gameplayPlayerPrefab)를 지정한 직업으로 스폰한다.
    /// 정상 흐름(OnSceneLoadDone)과 개발용 단독 실행(OnPlayerJoined) 양쪽에서 공용으로 쓴다.
    /// </summary>
    private void SpawnGameplayCharacter(NetworkRunner runner, PlayerJob job)
    {
        if (_gameplaySpawned) return;

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[RoomManager] Gameplay Player Prefab이 연결되지 않았습니다.");
            return;
        }

        _gameplaySpawned = true;

        Vector3 spawnPosition = new Vector3(
            UnityEngine.Random.Range(-2f, 2f),
            1f,
            UnityEngine.Random.Range(-2f, 2f));

        runner.Spawn(
            gameplayPlayerPrefab,
            spawnPosition,
            Quaternion.identity,
            runner.LocalPlayer,
            (r, obj) =>
            {
                var jobController = obj.GetComponent<PlayerJobController>();
                jobController?.SetJob(job);
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
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
