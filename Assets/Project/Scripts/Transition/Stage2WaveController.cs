using System;
using System.Collections;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// Stage 2의 전투 웨이브만 담당합니다.
///
/// - 다음 웨이브 시작: 시간 기반
/// - 적 생성: 일정 간격의 분산 스폰
/// - 전투 완료: 마지막 웨이브의 모든 적 생성 완료 + 등장한 모든 적 정화
/// - 실패: 코어 파괴
///
/// 시작/웨이브/전투 완료 이벤트를 Stage2Director에 전달하며,
/// UI와 Stage 2 최종 완료 연출은 직접 처리하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class Stage2WaveController : MonoBehaviour
{
    public enum Stage2WavePhase
    {
        First,
        Second,
        Final
    }

    [Header("References")]
    [Tooltip("Stage 1 완료 후 Stage 2 시작 상태를 보내는 전체 진행 컨트롤러")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [Tooltip("Stage 1과 Stage 2에서 함께 사용하는 적 생성기")]
    [SerializeField]
    private DreamEnemySpawner enemySpawner;

    [Tooltip("Stage 2 실패 조건으로 사용할 코어. 비어 있으면 Enemy Spawner의 Target Core를 사용합니다.")]
    [SerializeField]
    private CoreState core;

    [Header("Stage 2 시작")]
    [Tooltip("Stage 2 상태 진입 후 첫 번째 공격을 시작하기까지의 시간")]
    [Min(0f)]
    [SerializeField]
    private float stageStartDelay = 2f;

    [Header("Stage 2 첫 번째 공격")]
    [Min(1)]
    [SerializeField]
    private int wave1EnemyCount = 6;

    [Min(0f)]
    [SerializeField]
    private float wave1SpawnInterval = 1.5f;

    [Min(0.1f)]
    [SerializeField]
    private float wave1HealthMultiplier = 1.5f;

    [Tooltip("첫 번째 공격 스폰 시작 후 두 번째 공격을 시작하기까지의 시간")]
    [Min(0f)]
    [SerializeField]
    private float wave1ToWave2Delay = 15f;

    [Header("Stage 2 두 번째 공격")]
    [Min(1)]
    [SerializeField]
    private int wave2EnemyCount = 8;

    [Min(0f)]
    [SerializeField]
    private float wave2SpawnInterval = 1.2f;

    [Min(0.1f)]
    [SerializeField]
    private float wave2HealthMultiplier = 1.8f;

    [Tooltip("두 번째 공격 스폰 시작 후 최종 공격을 시작하기까지의 시간")]
    [Min(0f)]
    [SerializeField]
    private float wave2ToFinalDelay = 15f;

    [Header("Stage 2 최종 공격")]
    [Min(1)]
    [SerializeField]
    private int finalWaveEnemyCount = 10;

    [Min(0f)]
    [SerializeField]
    private float finalWaveSpawnInterval = 1f;

    [Min(0.1f)]
    [SerializeField]
    private float finalWaveHealthMultiplier = 2.2f;

    private Coroutine stageRoutine;
    private int runningSpawnRoutineCount;
    private bool allWaveSpawnsCompleted;
    private bool combatCompleted;
    private bool failed;

    public bool IsRunning => stageRoutine != null;
    public bool AreAllWaveSpawnsCompleted => allWaveSpawnsCompleted;
    public bool HasFailed => failed;
    public int ActiveEnemyCount =>
        enemySpawner != null ? enemySpawner.ActiveEnemyCount : 0;

    /// <summary>
    /// Stage 2 전체 전투 진행이 시작될 때 발생합니다.
    /// </summary>
    public event Action Stage2Started;

    /// <summary>
    /// 각 웨이브의 분산 스폰이 시작되는 순간 발생합니다.
    /// </summary>
    public event Action<Stage2WavePhase, int> WaveStarted;

    /// <summary>
    /// 특정 웨이브의 모든 적 생성이 끝났을 때 발생합니다.
    /// </summary>
    public event Action<Stage2WavePhase> WaveSpawnCompleted;

    /// <summary>
    /// 마지막 웨이브를 포함한 모든 분산 스폰이 끝났을 때 발생합니다.
    /// 아직 적이 남아 있을 수 있습니다.
    /// </summary>
    public event Action AllWaveSpawnsCompleted;

    /// <summary>
    /// 모든 웨이브 스폰과 모든 적 정화가 끝났을 때 발생하는 내부 전투 완료 신호입니다.
    /// 전체 진행 컨트롤러는 이 이벤트를 직접 받지 않고 Stage2Director.Stage2Completed를 받습니다.
    /// </summary>
    public event Action CombatCompleted;

    /// <summary>
    /// 이전 테스트 코드와의 임시 호환용 별칭입니다.
    /// 새 코드는 CombatCompleted를 사용합니다.
    /// </summary>
    public event Action Stage2Completed
    {
        add => CombatCompleted += value;
        remove => CombatCompleted -= value;
    }

    /// <summary>
    /// Stage 2 진행 중 코어가 파괴됐을 때 발생합니다.
    /// </summary>
    public event Action Failed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopStage2Internal();
    }

    private void ResolveReferences()
    {
        if (gameFlowController == null)
        {
            gameFlowController =
                UnityEngine.Object.FindAnyObjectByType<DreamlandGameFlowController>();
        }

        if (enemySpawner == null)
        {
            enemySpawner =
                UnityEngine.Object.FindAnyObjectByType<DreamEnemySpawner>();
        }

        if (core == null && enemySpawner != null)
        {
            core = enemySpawner.TargetCore;
        }
    }

    private void SubscribeEvents()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
            gameFlowController.OnStateChanged += HandleStateChanged;
        }

        if (core != null)
        {
            core.CoreDestroyed -= HandleCoreDestroyed;
            core.CoreDestroyed += HandleCoreDestroyed;
        }
    }

    private void UnsubscribeEvents()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }

        if (core != null)
        {
            core.CoreDestroyed -= HandleCoreDestroyed;
        }
    }

    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        switch (newState)
        {
            case DreamlandGameFlowController.GameFlowState.Stage2Wave1:
                BeginStage2();
                break;

            case DreamlandGameFlowController.GameFlowState.Stage2Wave2:
            case DreamlandGameFlowController.GameFlowState.Stage2Final:
                Debug.LogWarning(
                    "[Stage2Wave] Stage 2 내부 웨이브는 " +
                    "Stage2WaveController가 직접 진행하므로 수동 상태 변경을 무시합니다.",
                    this);
                break;

            case DreamlandGameFlowController.GameFlowState.EnemyAbsorption:
            case DreamlandGameFlowController.GameFlowState.FullVRTransition:
            case DreamlandGameFlowController.GameFlowState.BossBattle:
            case DreamlandGameFlowController.GameFlowState.Ending:
            case DreamlandGameFlowController.GameFlowState.Finished:
                if (IsRunning)
                {
                    Debug.LogWarning(
                        "[Stage2Wave] Stage 2 전투 완료 전에 전체 상태가 변경되어 " +
                        "현재 전투 진행을 중단합니다.",
                        this);
                    StopStage2Internal();
                }
                break;
        }
    }

    /// <summary>
    /// Stage 2 전체 웨이브 진행을 시작합니다.
    /// </summary>
    public void BeginStage2()
    {
        if (stageRoutine != null)
        {
            Debug.LogWarning(
                "[Stage2Wave] Stage 2가 이미 진행 중이므로 중복 시작 요청을 무시합니다.",
                this);
            return;
        }

        ResolveReferences();

        if (enemySpawner == null)
        {
            Debug.LogError(
                "[Stage2Wave] DreamEnemySpawner가 연결되지 않아 Stage 2를 시작할 수 없습니다.",
                this);
            return;
        }

        if (core == null)
        {
            core = enemySpawner.TargetCore;
        }

        if (core != null)
        {
            core.CoreDestroyed -= HandleCoreDestroyed;
            core.CoreDestroyed += HandleCoreDestroyed;

            if (core.IsDestroyed)
            {
                Debug.LogError(
                    "[Stage2Wave] 코어가 이미 파괴되어 Stage 2를 시작할 수 없습니다.",
                    this);
                return;
            }
        }

        runningSpawnRoutineCount = 0;
        allWaveSpawnsCompleted = false;
        combatCompleted = false;
        failed = false;

        stageRoutine = StartCoroutine(RunStage2Routine());
    }

    private IEnumerator RunStage2Routine()
    {
        float stageStartedAt = Time.time;

        Debug.Log(
            "[Stage2Wave] Stage 2 전투를 시작합니다. " +
            "웨이브 시작은 시간 기반, 완료는 모든 적 정화 기반입니다.",
            this);

        Stage2Started?.Invoke();

        if (stageStartDelay > 0f)
        {
            yield return new WaitForSeconds(stageStartDelay);
        }

        if (failed)
        {
            yield break;
        }

        StartSpawnRoutine(
            Stage2WavePhase.First,
            "Stage 2 첫 번째 공격",
            wave1EnemyCount,
            wave1SpawnInterval,
            wave1HealthMultiplier);

        yield return WaitForNextWaveDelay(
            wave1ToWave2Delay,
            "두 번째 공격");

        if (failed)
        {
            yield break;
        }

        StartSpawnRoutine(
            Stage2WavePhase.Second,
            "Stage 2 두 번째 공격",
            wave2EnemyCount,
            wave2SpawnInterval,
            wave2HealthMultiplier);

        yield return WaitForNextWaveDelay(
            wave2ToFinalDelay,
            "최종 공격");

        if (failed)
        {
            yield break;
        }

        StartSpawnRoutine(
            Stage2WavePhase.Final,
            "Stage 2 최종 공격",
            finalWaveEnemyCount,
            finalWaveSpawnInterval,
            finalWaveHealthMultiplier);

        while (!failed && runningSpawnRoutineCount > 0)
        {
            yield return null;
        }

        if (failed)
        {
            yield break;
        }

        allWaveSpawnsCompleted = true;

        Debug.Log(
            "[Stage2Wave] Stage 2의 모든 웨이브 스폰이 완료됐습니다. " +
            "남아 있는 모든 적이 정화될 때까지 기다립니다.",
            this);

        AllWaveSpawnsCompleted?.Invoke();

        while (!failed && enemySpawner.ActiveEnemyCount > 0)
        {
            yield return null;
        }

        if (failed)
        {
            yield break;
        }

        CompleteCombat(stageStartedAt);
    }

    private void StartSpawnRoutine(
        Stage2WavePhase phase,
        string waveLabel,
        int enemyCount,
        float spawnInterval,
        float healthMultiplier)
    {
        runningSpawnRoutineCount++;

        WaveStarted?.Invoke(phase, enemyCount);

        StartCoroutine(
            SpawnWaveRoutine(
                phase,
                waveLabel,
                enemyCount,
                spawnInterval,
                healthMultiplier));

        Debug.Log(
            "[Stage2Wave] " + waveLabel +
            " 스폰 시작 / 총 " + enemyCount +
            "마리 / 생성 간격 " + spawnInterval.ToString("0.0") + "초",
            this);
    }

    private IEnumerator SpawnWaveRoutine(
        Stage2WavePhase phase,
        string waveLabel,
        int enemyCount,
        float spawnInterval,
        float healthMultiplier)
    {
        yield return enemySpawner.SpawnGroup(
            enemyCount,
            spawnInterval,
            healthMultiplier);

        runningSpawnRoutineCount =
            Mathf.Max(0, runningSpawnRoutineCount - 1);

        Debug.Log(
            "[Stage2Wave] " + waveLabel +
            "의 모든 적 생성 완료. 진행 중인 스폰 코루틴: " +
            runningSpawnRoutineCount,
            this);

        WaveSpawnCompleted?.Invoke(phase);
    }

    private IEnumerator WaitForNextWaveDelay(
        float duration,
        string nextWaveLabel)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (!failed && elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!failed)
        {
            Debug.Log(
                "[Stage2Wave] 이전 공격의 적 생존 여부와 관계없이 " +
                nextWaveLabel + "을 시작합니다. 현재 생존 적: " +
                enemySpawner.ActiveEnemyCount + "마리",
                this);
        }
    }

    private void CompleteCombat(float stageStartedAt)
    {
        if (failed || combatCompleted)
        {
            return;
        }

        if (!allWaveSpawnsCompleted)
        {
            Debug.LogWarning(
                "[Stage2Wave] 모든 웨이브 스폰이 끝나지 않아 전투 완료 요청을 무시했습니다.",
                this);
            return;
        }

        if (enemySpawner.ActiveEnemyCount > 0)
        {
            Debug.LogWarning(
                "[Stage2Wave] 적이 남아 있어 전투 완료 요청을 무시했습니다. 남은 적: " +
                enemySpawner.ActiveEnemyCount,
                this);
            return;
        }

        combatCompleted = true;
        stageRoutine = null;

        float elapsed = Time.time - stageStartedAt;

        Debug.Log(
            "[Stage2Wave] Stage 2 전투 완료: " +
            elapsed.ToString("0.0") +
            "초. CombatCompleted 이벤트를 발생시킵니다.",
            this);

        CombatCompleted?.Invoke();
    }

    private void HandleCoreDestroyed()
    {
        if (stageRoutine == null || failed || combatCompleted)
        {
            return;
        }

        failed = true;
        StopAllCoroutines();

        stageRoutine = null;
        runningSpawnRoutineCount = 0;
        allWaveSpawnsCompleted = false;

        Debug.Log(
            "[Stage2Wave] 코어가 파괴되어 Stage 2의 스폰과 진행을 중단합니다.",
            this);

        Failed?.Invoke();
    }

    private void StopStage2Internal()
    {
        if (stageRoutine == null && runningSpawnRoutineCount <= 0)
        {
            return;
        }

        StopAllCoroutines();

        stageRoutine = null;
        runningSpawnRoutineCount = 0;
        allWaveSpawnsCompleted = false;
    }

    [ContextMenu("테스트 - Stage 2 직접 시작")]
    private void TestBeginStage2()
    {
        BeginStage2();
    }

    private void OnValidate()
    {
        stageStartDelay = Mathf.Max(0f, stageStartDelay);

        wave1EnemyCount = Mathf.Max(1, wave1EnemyCount);
        wave1SpawnInterval = Mathf.Max(0f, wave1SpawnInterval);
        wave1HealthMultiplier = Mathf.Max(0.1f, wave1HealthMultiplier);
        wave1ToWave2Delay = Mathf.Max(0f, wave1ToWave2Delay);

        wave2EnemyCount = Mathf.Max(1, wave2EnemyCount);
        wave2SpawnInterval = Mathf.Max(0f, wave2SpawnInterval);
        wave2HealthMultiplier = Mathf.Max(0.1f, wave2HealthMultiplier);
        wave2ToFinalDelay = Mathf.Max(0f, wave2ToFinalDelay);

        finalWaveEnemyCount = Mathf.Max(1, finalWaveEnemyCount);
        finalWaveSpawnInterval = Mathf.Max(0f, finalWaveSpawnInterval);
        finalWaveHealthMultiplier = Mathf.Max(0.1f, finalWaveHealthMultiplier);
    }
}
