using System;
using System.Collections;
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

    [Tooltip(
        "NetworkObject + GameDifficultyState가 붙은 프리팹. 방 전체가 공유하는 " +
        "난이도(하/중/상) 값을 담는다. 개별 플레이어마다 스폰되는 " +
        "lobbyPlayerStatePrefab과 달리, 방에 딱 하나만 스폰된다.")]
    [SerializeField] private GameObject difficultyStatePrefab;

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
    private PlayMode _pendingPlayMode;
    private GameDifficultyState _difficultyState;

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
        // Direct-map entry can supply a runner-only template. The tutorial's scene
        // NetworkObject must be registered by the same scene manager as normal lobby entry.
        var sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
            sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        // 씬이 바뀌어도(로비 -> 맵) 같은 접속을 계속 유지한다.
        DontDestroyOnLoad(_runner.gameObject);
        DontDestroyOnLoad(gameObject);

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var startArguments = new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = roomName,
            PlayerCount = 8,
            Scene = sceneInfo,
            SceneManager = sceneManager,
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

            _pendingPlayMode = LocalLobbyPlayerState.SelectedPlayMode;

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
        // 개발용 단독 실행은 기존 VR 테스트 흐름을 그대로 유지한다(PlayMode.VR).
        if (_devDirectMode)
        {
            SpawnGameplayCharacter(runner, _devDefaultJob, PlayMode.VR);
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

        // 난이도는 개별 플레이어 값이 아니라 방 전체가 공유하는 하나의 값이라,
        // 아무나 스폰하면 안 되고 딱 한 번만 만들어져야 한다. 방을 만든
        // 마스터 클라이언트만 스폰하도록 제한한다 - 나중에 들어오는
        // 플레이어들은 이미 존재하는 걸 GetOrFindDifficultyState()로 찾아서 쓴다.
        if (difficultyStatePrefab != null &&
            runner.IsSharedModeMasterClient &&
            GetOrFindDifficultyState() == null)
        {
            runner.Spawn(
                difficultyStatePrefab,
                Vector3.zero,
                Quaternion.identity,
                PlayerRef.None,
                (spawnRunner, networkObject) =>
                {
                    GameDifficultyState state = networkObject.GetComponent<GameDifficultyState>();

                    if (state != null && networkObject.HasStateAuthority)
                    {
                        state.CurrentDifficulty = GameDifficulty.Medium;
                    }
                });
        }
    }


    /// <summary>
    /// 방 전체가 공유하는 난이도 상태 오브젝트를 찾아서 캐싱해 반환한다.
    /// 마스터 클라이언트가 스폰하기 전이거나, 아직 이 클라이언트에 복제되지
    /// 않았다면 null을 반환할 수 있다(호출하는 쪽에서 매 프레임 다시 시도해도 안전).
    /// </summary>
    public GameDifficultyState GetOrFindDifficultyState()
    {
        if (_difficultyState != null)
        {
            return _difficultyState;
        }

        _difficultyState = FindAnyObjectByType<GameDifficultyState>();
        return _difficultyState;
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

        // 로비에서 스폰되어 씬이 바뀌어도 유지되는 DreamlandProgressSync에게
        // "이제 Dreamland_map_3에 도착했다"고 알려준다. 이 클라이언트가
        // CoreState/DreamlandGameFlowController를 찾아 연결하고, 마스터
        // 클라이언트라면 방금 로드된 맵의 초기값으로 네트워크 상태를
        // 시작한다. 이게 없으면 코어 체력/게임 진행 단계(미션 배너, 맵
        // 구성 요소 등장 등)가 State Authority를 가진 클라이언트에서만
        // 바뀌고 다른 플레이어에게는 전혀 전달되지 않는다.
        StartCoroutine(ConnectProgressSyncWhenReady());

        // LobbyPlayerState는 LoadGameplayScene()에서 이미 파괴됐으므로,
        // 그때 미리 복사해 둔 값(_pendingJob/_pendingPlayMode)을 사용한다.
        PlayerJob job = _hasPendingJob ? _pendingJob : PlayerJob.Police; // 못 골랐을 경우를 대비한 안전한 기본값

        SpawnGameplayCharacter(runner, job, _pendingPlayMode);
    }


    /// <summary>
    /// 씬 로드가 막 끝난 시점에는 DreamlandProgressSync의 네트워크 복제가
    /// 아직 이 클라이언트에 도착 안 했을 수 있다(특히 나중에 합류한
    /// 플레이어이거나 네트워크 지연이 있는 경우). 못 찾으면 재시도해서,
    /// 있는데 잠깐 못 찾은 경우까지 최대한 커버한다.
    ///
    /// 예전에는 30프레임(약 0.5초)만 재시도하고 포기했는데, 실제 2인
    /// 테스트 로그에서 정확히 이 경로("DreamlandProgressSync를 찾지
    /// 못해...")가 찍히는 게 확인됐다 - 0.5초 안에 복제가 안 끝나면
    /// 이 클라이언트는 그 뒤로 영영 코어 체력/게임 진행 단계가
    /// 로컬 전용(State Authority가 아니면 아무것도 안 바뀌는) 모드로
    /// 고정돼 버렸고, 이게 "나중에 들어온 사람만 계속 진행이 멈춘 것처럼
    /// 보이는" 증상의 실제 원인이었다. 실시간 기준 15초까지, 그리고 3초마다
    /// 아직도 못 찾았다는 경고를 남기며 재시도하도록 넉넉하게 늘린다.
    /// </summary>
    private IEnumerator ConnectProgressSyncWhenReady()
    {
        const float maxWaitSeconds = 15f;
        const float warnIntervalSeconds = 3f;

        float startTime = Time.unscaledTime;
        float nextWarnTime = startTime + warnIntervalSeconds;

        while (Time.unscaledTime - startTime < maxWaitSeconds)
        {
            DreamlandProgressSync sync =
                FindAnyObjectByType<DreamlandProgressSync>(FindObjectsInactive.Include);

            // sync가 "찾아졌다"는 것과 "Fusion이 [Networked] 값을 읽을 준비를
            // 마쳤다"는 건 다르다 - GameDifficultyState에서 실제로 재현된
            // InvalidOperationException과 동일한 함정이라, IsReady까지
            // 확인한 뒤에만 진행한다. 아직 준비 안 됐으면 이번 프레임은
            // 넘기고 계속 재시도한다(아래 while 루프가 계속 돈다).
            if (sync != null && sync.IsReady)
            {
                sync.OnEnteredGameplayScene();

                Debug.Log(
                    "[RoomManager] DreamlandProgressSync 연결 완료 " +
                    $"({Time.unscaledTime - startTime:0.00}초 소요).",
                    this);

                yield break;
            }

            if (Time.unscaledTime >= nextWarnTime)
            {
                Debug.LogWarning(
                    "[RoomManager] DreamlandProgressSync를 아직 찾지 못했습니다 " +
                    $"({Time.unscaledTime - startTime:0.0}초 경과, 계속 재시도 중). " +
                    "이 경고가 계속 반복되면 difficultyStatePrefab이 마스터 " +
                    "클라이언트에서 스폰되지 않았거나 네트워크 복제가 비정상입니다.",
                    this);

                nextWarnTime = Time.unscaledTime + warnIntervalSeconds;
            }

            yield return null;
        }

        Debug.LogError(
            "[RoomManager] DreamlandProgressSync를 " +
            $"{maxWaitSeconds:0}초 동안 찾지 못해 게임 진행 상태 동기화를 " +
            "연결하지 못했습니다. 이 클라이언트는 코어 체력/게임 진행 단계가 " +
            "다른 플레이어와 계속 어긋납니다.",
            this);
    }


    /// <summary>
    /// 실제 게임 캐릭터(gameplayPlayerPrefab)를 지정한 직업/플레이 모드로 스폰한다.
    /// 정상 흐름(OnSceneLoadDone)과 개발용 단독 실행(OnPlayerJoined) 양쪽에서 공용으로 쓴다.
    /// </summary>
    private void SpawnGameplayCharacter(NetworkRunner runner, PlayerJob job, PlayMode playMode)
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

        // 장난감 친구(ToyFriend)가 걸어가서 서는 TalkPoint를 바라보는 방향으로
        // 스폰시켜, 방향을 돌리지 않아도 처음부터 로봇이 눈앞에 보이게 한다.
        // TalkPoint를 찾지 못하면 기존처럼 identity 회전으로 안전하게 대체한다.
        //
        // spawnPosition 자체(무작위 -2~2 범위)를 기준으로 방향을 계산하면
        // 어쩌다 TalkPoint 바로 위/근처에 스폰될 때 방향이 거의 0벡터가 되어
        // 회전이 애매해질 수 있으므로, 항상 스폰 영역의 중심(원점)을
        // 기준으로 방향을 계산해 매번 안정적으로 로봇 쪽을 보게 한다.
        Quaternion spawnRotation =
            ComputeSpawnRotationTowardToyFriend(Vector3.zero);

        runner.Spawn(
            gameplayPlayerPrefab,
            spawnPosition,
            spawnRotation,
            runner.LocalPlayer,
            (r, obj) =>
            {
                var jobController = obj.GetComponent<PlayerJobController>();
                jobController?.SetJob(job);
                jobController?.SetPlayMode(playMode);
            });
    }


    /// <summary>
    /// map_3의 "TalkPoint"(장난감 친구가 최종적으로 서는 위치)를 바라보는
    /// 회전값을 계산한다. 씬에서 TalkPoint를 찾지 못하면 기존 기본값
    /// (Quaternion.identity)을 그대로 반환한다.
    /// </summary>
    private Quaternion ComputeSpawnRotationTowardToyFriend(Vector3 spawnPosition)
    {
        GameObject talkPointObject = GameObject.Find("TalkPoint");

        if (talkPointObject == null)
        {
            return Quaternion.identity;
        }

        Vector3 direction = talkPointObject.transform.position - spawnPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
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
