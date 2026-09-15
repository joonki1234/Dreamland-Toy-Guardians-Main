using Fusion;
using UnityEngine;

public enum GameDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2,
    Extreme = 3
}

public readonly struct DifficultySettings
{
    public readonly float EnemyCount, EnemyHealth, CoreDamage, BossHealth;
    public DifficultySettings(float count, float health, float damage, float boss)
    {
        EnemyCount = count; EnemyHealth = health; CoreDamage = damage; BossHealth = boss;
    }

    // Zero means an absent enemy type. Positive waves round halves upward, minimum one.
    public int ScaleCount(int count) => count <= 0 ? 0 : Mathf.Max(1, Mathf.FloorToInt(count * EnemyCount + 0.5f));

    // Round cumulative totals so mixed types/directions sum to the exact scaled wave count.
    public int AllocateCount(int count, ref int originalTotal)
    {
        int previous = ScaleCount(originalTotal);
        originalTotal += Mathf.Max(0, count);
        return ScaleCount(originalTotal) - previous;
    }

    public static DifficultySettings For(GameDifficulty difficulty)
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy: return new DifficultySettings(0.5f, 0.7f, 0.7f, 0.3f);
            case GameDifficulty.Hard: return new DifficultySettings(1.5f, 1.1f, 1.1f, 1.5f);
            case GameDifficulty.Extreme: return new DifficultySettings(2f, 1.2f, 1.2f, 2f);
            default: return new DifficultySettings(1f, 1f, 1f, 1f);
        }
    }
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
/// 난이도 버튼을 누르면 RPC로 "이걸로 바꿔줘"라고 요청만 하고, State
/// Authority가 값을 바꾸면 그 결과가 다시 모두에게 동기화된다
/// (EnemyHealth의 데미지 요청 패턴과 동일).
///
/// Spawned에서 Runner의 DontDestroyOnLoad로 등록해 씬 전환에도 유지한다.
/// 게임 시작 시 선택을 잠그고, 전투 권한 주체가 원본 수치에 Settings를 적용한다.
/// </summary>
public class GameDifficultyState : NetworkBehaviour
{
    public static GameDifficultyState Instance { get; private set; }
    public static DifficultySettings Settings => DifficultySettings.For(
        Instance != null && Instance.IsReady ? Instance.CurrentDifficulty : GameDifficulty.Medium);

    [Networked] public NetworkBool SelectionLocked { get; private set; }

    public override void Spawned()
    {
        Instance = this;
        Runner.MakeDontDestroyOnLoad(gameObject);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    public void LockSelection()
    {
        if (IsReady && Object.HasStateAuthority) SelectionLocked = true;
    }

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
        if (!IsReady || SelectionLocked || (int)difficulty < 0 || (int)difficulty > 3)
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
        if (!SelectionLocked && (int)difficulty >= 0 && (int)difficulty <= 3)
            CurrentDifficulty = difficulty;
    }

    private void HandleDifficultyChanged()
    {
        DifficultyChanged?.Invoke(CurrentDifficulty);
    }
}
