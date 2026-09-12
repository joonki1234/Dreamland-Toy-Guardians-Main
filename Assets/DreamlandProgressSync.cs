using Fusion;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// Dreamland_map_3의 "게임 진행 상태"(코어 체력, DreamlandGameFlowController의
/// 현재 단계)를 방에 있는 모든 클라이언트에게 동일하게 맞춰주는 다리
/// (Bridge) 역할의 NetworkBehaviour.
///
/// 왜 필요한가:
/// CoreState.TakeDamage()와 DreamlandGameFlowController의 상태 전환은
/// 원래 평범한 로컬 MonoBehaviour 메서드였다. 그런데 실제로 이걸 호출하는
/// 쪽(EnemyCoreMover의 코어 공격, FinalBossAttackController 등)은 "그
/// 적의 State Authority를 가진 클라이언트에서만" 실행되도록 되어 있다
/// (Shared Mode에서 같은 적을 여러 클라이언트가 동시에 시뮬레이션하면
/// 위치가 겹치거나 튀기 때문에 이동/공격 로직 전체가
/// Object.HasStateAuthority로 감싸여 있다).
///
/// 그 결과 코어 체력이 깎이는 것도, 그로 인해 이어지는 게임 진행 단계
/// (미션 배너 문구, DreamlandTransitionController가 순서대로 열어주는
/// 꿈나라 맵 구성 요소들)도 전부 "그 적을 맡은 클라이언트" 화면에서만
/// 로컬로 바뀌고, 같은 방에 있는 다른 플레이어는 그 메서드 호출 자체를
/// 전혀 받지 못한다 - 그래서 한 명은 정상적으로 진행되는데 다른 한 명은
/// UI/맵 구성 요소가 처음 상태 그대로 멈춰 있는 것처럼 보이는 문제가
/// 생긴다.
///
/// 이 스크립트는 EnemyHealth.NetworkedTutorialHitCount / GameDifficultyState
/// 와 완전히 동일한 패턴(요청 -&gt; State Authority에게 RPC -&gt; [Networked]
/// 값 갱신 -&gt; 모든 클라이언트에 OnChangedRender로 재적용)을 CoreState와
/// DreamlandGameFlowController에도 적용해서, 누가 호출했든 모든 클라이언트가
/// 동시에 같은 상태를 보게 만든다.
///
/// 배치 방법: 새 프리팹을 따로 만들 필요 없이, RoomManager가 방 생성 시
/// 이미 한 번만 스폰하는 GameDifficultyState 프리팹(Assets/GameDifficultyState.prefab)
/// 의 같은 GameObject에 이 컴포넌트를 추가로 붙여서 쓴다. 그 오브젝트는
/// LobbyScene에서 스폰되어 Dreamland_map_3로 씬이 바뀌어도 그대로 유지되므로,
/// 방마다 정확히 하나만 존재한다.
///
/// 주의: 이 오브젝트는 로비에서 스폰되므로 Spawned() 시점에는 아직
/// Dreamland_map_3의 CoreState/DreamlandGameFlowController가 존재하지
/// 않는다. 실제 연결은 RoomManager.OnSceneLoadDone()이 맵 로딩을 끝낸
/// 뒤 OnEnteredGameplayScene()을 호출해줄 때 이뤄진다.
/// </summary>
public sealed class DreamlandProgressSync : NetworkBehaviour
{
    public enum MapRevealStage
    {
        None,
        Road0,
        Road1,
        Road2,
        Road3,
        Road4,
        Part1,
        Part2,
        Part3,
        Part4,
        Fence
    }

    /// <summary>
    /// 같은 클라이언트 안에서 CoreState.TakeDamage() /
    /// DreamlandGameFlowController.ChangeState()가 바로 참조할 수 있도록
    /// 노출한다. 방에 이 오브젝트가 아직 복제되지 않았을 때는 null이다
    /// (그런 경우 각 클래스는 예전처럼 로컬 전용으로 동작한다).
    /// </summary>
    public static DreamlandProgressSync Instance { get; private set; }

    [Networked, OnChangedRender(nameof(HandleCoreHealthChanged))]
    private float NetworkedCoreHealth { get; set; }

    [Networked, OnChangedRender(nameof(HandleGameFlowStateChanged))]
    private DreamlandGameFlowController.GameFlowState NetworkedGameFlowState { get; set; }

    [Networked, OnChangedRender(nameof(HandleMapRevealStageChanged))]
    private MapRevealStage NetworkedMapRevealStage { get; set; }

    [Networked]
    private NetworkBool Initialized { get; set; }

    /// <summary>
    /// GameDifficultyState에서 실제로 재현된 InvalidOperationException
    /// ("Error when accessing ...CurrentDifficulty...")과 동일한 함정이
    /// 이 클래스의 [Networked] 프로퍼티(Initialized/NetworkedCoreHealth/
    /// NetworkedGameFlowState)에도 있다 - FindAnyObjectByType으로는
    /// 찾아지지만 Fusion이 아직 값을 읽을 준비를 마치기 전인 짧은 순간이
    /// 있다. 읽기 전에 항상 이 값으로 먼저 확인한다.
    /// </summary>
    public bool IsReady => Object != null && Object.IsValid;

    private CoreState _core;
    private DreamlandGameFlowController _flowController;
    private DreamRoadRevealController _roadRevealController;
    private DreamWorldRevealController _worldRevealController;
    private MapRevealStage _locallyAppliedRevealStage;

    public override void Spawned()
    {
        Instance = this;

        // 이 오브젝트는 로비 씬에서 딱 한 번 스폰되고 Dreamland_map_3로 씬이
        // 바뀐 뒤에도 계속 살아있어야 한다(그래야 두 씬의 CoreState/
        // DreamlandGameFlowController를 계속 이어줄 수 있다). RoomManager와
        // NetworkRunner는 DontDestroyOnLoad로 보호되는데 이 오브젝트는 그런
        // 보호가 없어서, 씬 전환 때(Unity가 이전 씬을 언로드하면서) 파괴되고
        // Instance가 null로 돌아가 버릴 수 있었다 - 그러면 아래 두 값이
        // 조용히 "로컬 전용" 예전 동작으로 폴백해서, 이 오브젝트를 스폰한
        // 클라이언트(주로 첫 번째로 들어온 플레이어) 화면만 정상 진행되고
        // 나머지 플레이어는 게임 진행이 멈춘 것처럼 보이는 버그로 이어졌다.
        // 매 클라이언트가 이 콜백을 받을 때(스폰한 쪽이든, 나중에 복제받은
        // 쪽이든) 자기 로컬 인스턴스를 직접 보호해야 하므로 여기서 건다.
        DontDestroyOnLoad(gameObject);

        // 로비에서 막 스폰된 시점에는 아직 게임플레이 씬(Dreamland_map_3)이
        // 아니라서 CoreState/DreamlandGameFlowController를 찾아도 없다.
        // OnEnteredGameplayScene()이 호출될 때 다시 찾는다.
        _core = FindAnyObjectByType<CoreState>(FindObjectsInactive.Include);
        _flowController =
            FindAnyObjectByType<DreamlandGameFlowController>(
                FindObjectsInactive.Include);
        ResolveRevealControllers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// RoomManager.OnSceneLoadDone()에서, 이 클라이언트가 Dreamland_map_3에
    /// 도착해 씬 로드가 끝난 직후 호출한다.
    /// </summary>
    public void OnEnteredGameplayScene()
    {
        if (!IsReady)
        {
            // 아직 Fusion이 [Networked] 값을 읽을 준비를 마치지 않았다.
            // 여기서 강행하면 GameDifficultyState에서 실제로 재현된 것과
            // 같은 InvalidOperationException이 나서 RoomManager의 재시도
            // 코루틴이 죽어버린다 - 호출한 쪽(RoomManager)이 IsReady를
            // 먼저 확인하고 준비될 때까지 다시 부르게 하는 게 안전하다.
            return;
        }

        _core = FindAnyObjectByType<CoreState>(FindObjectsInactive.Include);
        _flowController =
            FindAnyObjectByType<DreamlandGameFlowController>(
                FindObjectsInactive.Include);

        if (Object != null &&
            Object.HasStateAuthority &&
            !Initialized)
        {
            // 씬에 배치된 CoreState/DreamlandGameFlowController는 모든
            // 클라이언트가 동일한 초기값(Awake/Start에서 정한 기본값)을
            // 갖고 있으므로, 마스터 클라이언트가 그 값을 그대로 네트워크
            // 상태의 시작값으로 삼는다.
            if (_core != null)
            {
                NetworkedCoreHealth = _core.CurrentHealth;
            }

            NetworkedGameFlowState =
                _flowController != null
                    ? _flowController.CurrentState
                    : DreamlandGameFlowController.GameFlowState
                        .WaitingForStage1Complete;

            NetworkedMapRevealStage = MapRevealStage.None;

            Initialized = true;
        }

        // OnChangedRender는 "값이 실제로 바뀔 때"만 호출된다. 이미
        // Initialized된 뒤에 이 메서드가 불린 경우(늦게 합류했거나, 참조를
        // 방금 찾은 경우)에는 콜백이 한 번도 안 불릴 수 있으므로 현재
        // 값을 직접 한 번 반영해 준다.
        HandleCoreHealthChanged();
        HandleGameFlowStateChanged();
        ApplyMapRevealState(animateLatestStage: false);
    }

    /// <summary>
    /// 코어 피해를 요청한다. 누가(어떤 클라이언트가) 호출하든 안전하다 -
    /// 내가 State Authority면 바로 적용하고, 아니면 RPC로 요청만 보낸다
    /// (GameDifficultyState.RequestSetDifficulty와 동일한 패턴).
    /// </summary>
    public void RequestCoreDamage(float amount)
    {
        if (Object == null || amount <= 0f)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyCoreDamage(amount);
        }
        else
        {
            RPC_RequestCoreDamage(amount);
        }
    }

    /// <summary>
    /// 게임 진행 단계 전환을 요청한다. 코어 피해 요청과 동일한 패턴.
    /// </summary>
    public void RequestSetGameFlowState(
        DreamlandGameFlowController.GameFlowState state)
    {
        if (Object == null)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            NetworkedGameFlowState = state;
        }
        else
        {
            RPC_RequestSetGameFlowState(state);
        }
    }

    public void RequestMapRevealStage(MapRevealStage stage)
    {
        if (Object == null || stage <= MapRevealStage.None)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            AdvanceMapRevealStage(stage);
        }
        else
        {
            RPC_RequestMapRevealStage(stage);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCoreDamage(float amount)
    {
        ApplyCoreDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetGameFlowState(
        DreamlandGameFlowController.GameFlowState state)
    {
        NetworkedGameFlowState = state;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestMapRevealStage(MapRevealStage stage)
    {
        AdvanceMapRevealStage(stage);
    }

    private void AdvanceMapRevealStage(MapRevealStage stage)
    {
        if (stage > NetworkedMapRevealStage)
        {
            NetworkedMapRevealStage = stage;
        }
    }

    private void ApplyCoreDamage(float amount)
    {
        NetworkedCoreHealth = Mathf.Max(0f, NetworkedCoreHealth - amount);
    }

    /// <summary>
    /// [Networked] NetworkedCoreHealth가 바뀔 때마다(State Authority 본인
    /// 클라이언트를 포함해) 모든 클라이언트에서 호출된다.
    /// </summary>
    private void HandleCoreHealthChanged()
    {
        _core ??= FindAnyObjectByType<CoreState>(FindObjectsInactive.Include);
        _core?.ApplyNetworkedDamageState(NetworkedCoreHealth);
    }

    /// <summary>
    /// [Networked] NetworkedGameFlowState가 바뀔 때마다 모든 클라이언트에서
    /// 호출된다.
    /// </summary>
    private void HandleGameFlowStateChanged()
    {
        _flowController ??=
            FindAnyObjectByType<DreamlandGameFlowController>(
                FindObjectsInactive.Include);

        _flowController?.ApplyNetworkedState(NetworkedGameFlowState);
    }

    private void HandleMapRevealStageChanged()
    {
        ApplyMapRevealState(animateLatestStage: true);
    }

    private void ApplyMapRevealState(bool animateLatestStage)
    {
        ResolveRevealControllers();

        if (_roadRevealController == null || _worldRevealController == null)
        {
            return;
        }

        MapRevealStage target = NetworkedMapRevealStage;
        if (target <= _locallyAppliedRevealStage)
        {
            return;
        }

        for (int value = (int)_locallyAppliedRevealStage + 1;
             value <= (int)target;
             value++)
        {
            MapRevealStage stage = (MapRevealStage)value;
            bool animate = animateLatestStage && stage == target;
            ApplySingleMapRevealStage(stage, animate);
        }

        _locallyAppliedRevealStage = target;
    }

    private void ApplySingleMapRevealStage(MapRevealStage stage, bool animate)
    {
        int roadIndex = (int)stage - (int)MapRevealStage.Road0;
        if (roadIndex >= 0 && roadIndex <= 4)
        {
            if (animate)
            {
                _roadRevealController.RevealRoad(roadIndex);
            }
            else
            {
                _roadRevealController.ShowRoadImmediately(roadIndex);
            }

            return;
        }

        int worldStep = (int)stage - (int)MapRevealStage.Part1;
        if (worldStep >= 0 && worldStep <= 4)
        {
            if (animate)
            {
                _worldRevealController.RevealStep(worldStep);
            }
            else
            {
                _worldRevealController.ShowStepImmediately(worldStep);
            }
        }
    }

    private void ResolveRevealControllers()
    {
        _roadRevealController ??=
            FindAnyObjectByType<DreamRoadRevealController>(
                FindObjectsInactive.Include);
        _worldRevealController ??=
            FindAnyObjectByType<DreamWorldRevealController>(
                FindObjectsInactive.Include);
    }
}
