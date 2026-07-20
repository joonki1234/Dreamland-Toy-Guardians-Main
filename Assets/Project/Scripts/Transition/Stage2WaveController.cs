using System.Collections;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// Stage 2의 시간별 적 생성을 관리하는 컨트롤러
///
/// DreamlandGameFlowController의 상태 변경 신호를 받아
/// Stage2Wave1, Stage2Wave2, Stage2Final에서 적을 생성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class Stage2WaveController : MonoBehaviour
{
    [Header("References")]

    [Tooltip("Stage 2의 시간과 상태를 관리하는 전체 진행 컨트롤러")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [Tooltip("팀원이 만든 기존 적 생성 스크립트")]
    [SerializeField]
    private DreamEnemySpawner enemySpawner;


    [Header("Stage 2 첫 번째 공격")]

    [Tooltip("첫 번째 공격에서 생성할 기본 적 수")]
    [Min(1)]
    [SerializeField]
    private int wave1EnemyCount = 6;

    [Tooltip("첫 번째 공격의 적 생성 간격")]
    [Min(0f)]
    [SerializeField]
    private float wave1SpawnInterval = 1.5f;

    [Tooltip("첫 번째 공격 적의 체력 배율")]
    [Min(0.1f)]
    [SerializeField]
    private float wave1HealthMultiplier = 1.5f;


    [Header("Stage 2 두 번째 공격")]

    [Tooltip("두 번째 공격에서 생성할 기본 적 수")]
    [Min(1)]
    [SerializeField]
    private int wave2EnemyCount = 8;

    [Tooltip("두 번째 공격의 적 생성 간격")]
    [Min(0f)]
    [SerializeField]
    private float wave2SpawnInterval = 1.2f;

    [Tooltip("두 번째 공격 적의 체력 배율")]
    [Min(0.1f)]
    [SerializeField]
    private float wave2HealthMultiplier = 1.8f;


    [Header("Stage 2 최종 공격")]

    [Tooltip("최종 공격에서 생성할 기본 적 수")]
    [Min(1)]
    [SerializeField]
    private int finalWaveEnemyCount = 10;

    [Tooltip("최종 공격의 적 생성 간격")]
    [Min(0f)]
    [SerializeField]
    private float finalWaveSpawnInterval = 1f;

    [Tooltip("최종 공격 적의 체력 배율")]
    [Min(0.1f)]
    [SerializeField]
    private float finalWaveHealthMultiplier = 2.2f;


    [Header("생존 적 수에 따른 생성 조절")]

    [Tooltip("현재 생존 적이 이 수 이하라면 예정된 적을 모두 생성한다.")]
    [Min(0)]
    [SerializeField]
    private int fullSpawnMaximumAlive = 5;

    [Tooltip("현재 생존 적이 이 수 이하라면 예정된 적의 절반만 생성한다.")]
    [Min(1)]
    [SerializeField]
    private int halfSpawnMaximumAlive = 8;

    [Tooltip("적이 너무 많을 때 다시 생존 적 수를 확인하는 시간")]
    [Min(0.1f)]
    [SerializeField]
    private float retryInterval = 2f;


    // 현재 실행 중인 적 생성 코루틴
    private Coroutine spawnRoutine;


    private void OnEnable()
    {
        // 전체 진행 컨트롤러의 상태 변경 이벤트를 받는다.
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 이벤트 연결을 해제한다.
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }

        StopCurrentSpawnRoutine();
    }

    /// <summary>
    /// 전체 게임 진행 상태가 변경되었을 때 호출된다.
    /// </summary>
    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        // 이전 단계에서 실행 중이던 적 생성 작업을 중지한다.
        StopCurrentSpawnRoutine();

        switch (newState)
        {
            case DreamlandGameFlowController.GameFlowState.Stage2Wave1:
                StartWave1();
                break;

            case DreamlandGameFlowController.GameFlowState.Stage2Wave2:
                StartWave2();
                break;

            case DreamlandGameFlowController.GameFlowState.Stage2Final:
                StartFinalWave();
                break;

            case DreamlandGameFlowController.GameFlowState.EnemyAbsorption:
                // 흡수 연출이 시작되면 새로운 일반 적 생성을 중지한다.
                Debug.Log(
                    "[Stage2Wave] 적 흡수 단계가 시작되어 새 적 생성을 중지합니다.");
                break;
        }
    }

    /// <summary>
    /// Stage 2 첫 번째 공격을 시작한다.
    /// </summary>
    private void StartWave1()
    {
        if (!CanSpawnEnemies())
        {
            return;
        }

        spawnRoutine = StartCoroutine(
            SpawnWaveRoutine(
                DreamlandGameFlowController.GameFlowState.Stage2Wave1,
                "Stage 2 첫 번째 공격",
                wave1EnemyCount,
                wave1SpawnInterval,
                wave1HealthMultiplier
            )
        );
    }

    /// <summary>
    /// Stage 2 두 번째 공격을 시작한다.
    /// </summary>
    private void StartWave2()
    {
        if (!CanSpawnEnemies())
        {
            return;
        }

        spawnRoutine = StartCoroutine(
            SpawnWaveRoutine(
                DreamlandGameFlowController.GameFlowState.Stage2Wave2,
                "Stage 2 두 번째 공격",
                wave2EnemyCount,
                wave2SpawnInterval,
                wave2HealthMultiplier
            )
        );
    }

    /// <summary>
    /// Stage 2 최종 공격을 시작한다.
    /// </summary>
    private void StartFinalWave()
    {
        if (!CanSpawnEnemies())
        {
            return;
        }

        spawnRoutine = StartCoroutine(
            SpawnWaveRoutine(
                DreamlandGameFlowController.GameFlowState.Stage2Final,
                "Stage 2 최종 공격",
                finalWaveEnemyCount,
                finalWaveSpawnInterval,
                finalWaveHealthMultiplier
            )
        );
    }

    /// <summary>
    /// 적 생성에 필요한 오브젝트가 연결되어 있는지 확인한다.
    /// </summary>
    private bool CanSpawnEnemies()
    {
        if (gameFlowController == null)
        {
            Debug.LogError(
                "[Stage2Wave] DreamlandGameFlowController가 연결되지 않았습니다.");
            return false;
        }

        if (enemySpawner == null)
        {
            Debug.LogError(
                "[Stage2Wave] DreamEnemySpawner가 연결되지 않았습니다.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 지정된 공격 단계의 적을 생성한다.
    /// </summary>
    private IEnumerator SpawnWaveRoutine(
        DreamlandGameFlowController.GameFlowState requiredState,
        string waveLabel,
        int plannedEnemyCount,
        float spawnInterval,
        float healthMultiplier)
    {
        Debug.Log("[Stage2Wave] " + waveLabel + " 준비");

        // 생존 적이 9마리 이상이라면 추가 생성을 잠시 보류한다.
        while (
            gameFlowController.CurrentState == requiredState &&
            enemySpawner.ActiveEnemyCount > halfSpawnMaximumAlive)
        {
            Debug.Log(
                "[Stage2Wave] 현재 생존 적 "
                + enemySpawner.ActiveEnemyCount
                + "마리 - 새 적 생성을 잠시 보류합니다.");

            yield return new WaitForSeconds(retryInterval);
        }

        // 기다리는 동안 다음 상태로 넘어갔다면 적을 생성하지 않는다.
        if (gameFlowController.CurrentState != requiredState)
        {
            spawnRoutine = null;
            yield break;
        }

        // 현재 생존 적 수에 따라 실제 생성 수를 계산한다.
        int spawnCount = CalculateSpawnCount(plannedEnemyCount);

        // 생성할 적이 없다면 종료한다.
        if (spawnCount <= 0)
        {
            Debug.Log(
                "[Stage2Wave] 현재 생존 적이 많아 새 적을 생성하지 않습니다.");

            spawnRoutine = null;
            yield break;
        }

        Debug.Log(
            "[Stage2Wave] "
            + waveLabel
            + " / 현재 생존 적: "
            + enemySpawner.ActiveEnemyCount
            + "마리 / 새로 생성할 적: "
            + spawnCount
            + "마리"
        );

        // 팀원이 만든 DreamEnemySpawner의 생성 기능을 그대로 사용한다.
        yield return enemySpawner.SpawnGroup(
            spawnCount,
            spawnInterval,
            healthMultiplier
        );

        Debug.Log("[Stage2Wave] " + waveLabel + " 적 생성 완료");

        spawnRoutine = null;
    }

    /// <summary>
    /// 현재 생존 적 수에 따라 실제 생성할 적 수를 계산한다.
    /// </summary>
    private int CalculateSpawnCount(int plannedEnemyCount)
    {
        int aliveCount = enemySpawner.ActiveEnemyCount;

        // 생존 적이 0~5마리라면 예정된 수를 모두 생성한다.
        if (aliveCount <= fullSpawnMaximumAlive)
        {
            return plannedEnemyCount;
        }

        // 생존 적이 6~8마리라면 예정된 수의 절반만 생성한다.
        if (aliveCount <= halfSpawnMaximumAlive)
        {
            return Mathf.Max(
                1,
                Mathf.CeilToInt(plannedEnemyCount * 0.5f)
            );
        }

        // 생존 적이 9마리 이상이면 생성하지 않는다.
        return 0;
    }

    /// <summary>
    /// 현재 실행 중인 적 생성 작업을 중지한다.
    /// </summary>
    private void StopCurrentSpawnRoutine()
    {
        if (spawnRoutine == null)
        {
            return;
        }

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private void OnValidate()
    {
        // Inspector에서 잘못된 값이 입력되지 않도록 보정한다.
        wave1EnemyCount = Mathf.Max(1, wave1EnemyCount);
        wave1SpawnInterval = Mathf.Max(0f, wave1SpawnInterval);
        wave1HealthMultiplier = Mathf.Max(0.1f, wave1HealthMultiplier);

        wave2EnemyCount = Mathf.Max(1, wave2EnemyCount);
        wave2SpawnInterval = Mathf.Max(0f, wave2SpawnInterval);
        wave2HealthMultiplier = Mathf.Max(0.1f, wave2HealthMultiplier);

        finalWaveEnemyCount = Mathf.Max(1, finalWaveEnemyCount);
        finalWaveSpawnInterval = Mathf.Max(0f, finalWaveSpawnInterval);
        finalWaveHealthMultiplier =
            Mathf.Max(0.1f, finalWaveHealthMultiplier);

        fullSpawnMaximumAlive = Mathf.Max(
            0,
            fullSpawnMaximumAlive
        );

        halfSpawnMaximumAlive = Mathf.Max(
            fullSpawnMaximumAlive + 1,
            halfSpawnMaximumAlive
        );

        retryInterval = Mathf.Max(0.1f, retryInterval);
    }
}