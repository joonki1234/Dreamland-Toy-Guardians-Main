using System.Collections;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// Stage 1:
///
/// 준비 단계 0
/// → Portal A
/// → Road_1
///
/// 준비 단계 1
/// → Portal B
/// → Road_2
/// → 적 스폰 허용
///
/// 준비 단계 2
/// → Portal C
/// → Road_3
/// → 적 스폰 허용
///
/// 준비 단계 3
/// → Portal D
/// → Road_4
/// → 적 스폰 허용
///
/// Stage 2:
///
/// 첫 공격 시작
/// → Part_1
///
/// 첫 공격 스폰 완료
/// → Part_2
///
/// 두 번째 공격 시작
/// → Part_3
///
/// 두 번째 공격 스폰 완료
/// → Part_4
///
/// 최종 공격 시작
/// → fence
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPortalStageController : MonoBehaviour
{
    [Header("진행 시스템 연결")]

    [SerializeField]
    private Stage1WaveController stage1WaveController;

    [SerializeField]
    private Stage2WaveController stage2WaveController;

    [SerializeField]
    private DreamRoadRevealController roadRevealController;

    [SerializeField]
    private DreamWorldRevealController worldRevealController;


    [Header("Stage 1~2 사용 포탈 A~D")]

    [SerializeField]
    private EnemyPortalGrowthController portalA;

    [SerializeField]
    private EnemyPortalGrowthController portalB;

    [SerializeField]
    private EnemyPortalGrowthController portalC;

    [SerializeField]
    private EnemyPortalGrowthController portalD;


    [Header("보류 포탈 E~H")]

    [SerializeField]
    private EnemyPortalGrowthController portalE;

    [SerializeField]
    private EnemyPortalGrowthController portalF;

    [SerializeField]
    private EnemyPortalGrowthController portalG;

    [SerializeField]
    private EnemyPortalGrowthController portalH;


    [Header("포탈 → 길 → 적 타이밍")]

    [Tooltip(
        "포탈을 활성화한 뒤 길이 나오기 시작할 때까지의 시간")]
    [Min(0f)]
    [SerializeField]
    private float portalToRoadDelay = 0.9f;

    [Tooltip(
        "길 등장 함수를 실행한 뒤 길이 완성되기를 기다리는 시간")]
    [Min(0f)]
    [SerializeField]
    private float roadRevealWaitDuration = 1.5f;

    [Tooltip(
        "길이 완성된 뒤 Stage1WaveController에 " +
        "적 스폰 준비 완료를 보내기 전 추가 대기 시간")]
    [Min(0f)]
    [SerializeField]
    private float roadToEnemyDelay = 0.5f;

    [Tooltip(
        "8인 협동용 4방향 동시 개방 시, Road_1~4를 완전히 동시(0초)가 " +
        "아니라 아주 짧은 간격으로 살짝 캐스케이드 느낌만 주며 여는 " +
        "간격입니다.")]
    [Min(0f)]
    [SerializeField]
    private float allDirectionsRoadCascadeInterval = 0.18f;


    [Header("시작 설정")]

    [Tooltip(
        "Play 시작 즉시 Portal A를 표시하는 개발용 옵션입니다. " +
        "현재 정상 진행에서는 체크 해제합니다.")]
    [SerializeField]
    private bool applyStage1StartOnPlay = false;


    private Coroutine stage1PreparationRoutine;

    private bool road1Revealed;
    private bool road2Revealed;
    private bool road3Revealed;
    private bool road4Revealed;

    private bool part1Revealed;
    private bool part2Revealed;
    private bool part3Revealed;
    private bool part4Revealed;

    private bool fenceRevealed;


    private void Awake()
    {
        ResolveReferences();
    }


    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }


    private void Start()
    {
        if (applyStage1StartOnPlay)
        {
            ApplyStage1PortalState(0);
        }
    }


    private void OnDisable()
    {
        UnsubscribeEvents();

        if (stage1PreparationRoutine != null)
        {
            StopCoroutine(
                stage1PreparationRoutine);

            stage1PreparationRoutine = null;
        }
    }


    private void ResolveReferences()
    {
        if (stage1WaveController == null)
        {
            stage1WaveController =
                UnityEngine.Object.FindAnyObjectByType
                    <Stage1WaveController>();
        }

        if (stage2WaveController == null)
        {
            stage2WaveController =
                UnityEngine.Object.FindAnyObjectByType
                    <Stage2WaveController>();
        }

        if (roadRevealController == null)
        {
            roadRevealController =
                UnityEngine.Object.FindAnyObjectByType
                    <DreamRoadRevealController>();
        }

        if (worldRevealController == null)
        {
            worldRevealController =
                UnityEngine.Object.FindAnyObjectByType
                    <DreamWorldRevealController>();
        }
    }


    private void SubscribeEvents()
    {
        if (stage1WaveController != null)
        {
            stage1WaveController.EnvironmentPreparationRequested -=
                HandleStage1PreparationRequested;

            stage1WaveController.EnvironmentPreparationRequested +=
                HandleStage1PreparationRequested;
        }


        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted -=
                HandleStage2WaveStarted;

            stage2WaveController.WaveStarted +=
                HandleStage2WaveStarted;


            stage2WaveController.WaveSpawnCompleted -=
                HandleStage2WaveSpawnCompleted;

            stage2WaveController.WaveSpawnCompleted +=
                HandleStage2WaveSpawnCompleted;
        }
    }


    private void UnsubscribeEvents()
    {
        if (stage1WaveController != null)
        {
            stage1WaveController.EnvironmentPreparationRequested -=
                HandleStage1PreparationRequested;
        }


        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted -=
                HandleStage2WaveStarted;

            stage2WaveController.WaveSpawnCompleted -=
                HandleStage2WaveSpawnCompleted;
        }
    }


    // =========================================================
    // Stage 1
    // =========================================================

    private void HandleStage1PreparationRequested(
        int preparationStep)
    {
        if (stage1PreparationRoutine != null)
        {
            StopCoroutine(
                stage1PreparationRoutine);
        }

        stage1PreparationRoutine =
            StartCoroutine(
                preparationStep ==
                    Stage1WaveController.AllDirectionsPreparationStep
                    ? RunStage1PreparationAll()
                    : RunStage1Preparation(
                        preparationStep));
    }


    /// <summary>
    /// 8인 협동 기준으로 Portal A~D + Road_1~4를 1차 공격 시작 전에
    /// 한 번에(살짝 캐스케이드를 주며) 엽니다. 웨이브가 진행될 때마다
    /// 방향을 하나씩 여는 예전 RunStage1Preparation()과 달리, 여기서는
    /// 4방향을 전부 같은 흐름 안에서 처리합니다.
    /// </summary>
    private IEnumerator RunStage1PreparationAll()
    {
        Debug.Log(
            "[PortalStage] Stage 1 4방향(Portal A~D + Road_1~4) " +
            "동시 준비 시작.",
            this);


        /*
         * 1. 포탈 A~D를 한 번에 활성화합니다.
         * 지금은 웨이브가 진행되며 커지는 연출이 아니라 4개가 동시에
         * 새로 열리는 것이므로, 전부 같은(작은) 크기로 통일합니다.
         */
        SetActivePortalCount(4);

        portalA?.ApplySmallPortal();
        portalB?.ApplySmallPortal();
        portalC?.ApplySmallPortal();
        portalD?.ApplySmallPortal();


        /*
         * 2. 포탈이 먼저 보이는 시간을 확보
         */
        if (portalToRoadDelay > 0f)
        {
            yield return new WaitForSeconds(
                portalToRoadDelay);
        }


        /*
         * 3. Road_1~4를 완전히 동시가 아니라 아주 짧은 간격으로
         * 살짝 캐스케이드 느낌만 주며 엽니다.
         */
        RevealRoad1Once();

        if (allDirectionsRoadCascadeInterval > 0f)
        {
            yield return new WaitForSeconds(
                allDirectionsRoadCascadeInterval);
        }

        RevealRoad2Once();

        if (allDirectionsRoadCascadeInterval > 0f)
        {
            yield return new WaitForSeconds(
                allDirectionsRoadCascadeInterval);
        }

        RevealRoad3Once();

        if (allDirectionsRoadCascadeInterval > 0f)
        {
            yield return new WaitForSeconds(
                allDirectionsRoadCascadeInterval);
        }

        RevealRoad4Once();


        /*
         * 4. 길 연출이 충분히 완료될 때까지 대기
         */
        if (roadRevealWaitDuration > 0f)
        {
            yield return new WaitForSeconds(
                roadRevealWaitDuration);
        }


        /*
         * 5. 길이 완성된 화면을 잠깐 보여준 뒤 적 스폰을 허용
         */
        if (roadToEnemyDelay > 0f)
        {
            yield return new WaitForSeconds(
                roadToEnemyDelay);
        }


        stage1WaveController?.
            NotifyEnvironmentPreparationCompleted(
                Stage1WaveController.AllDirectionsPreparationStep);


        Debug.Log(
            "[PortalStage] Stage 1 4방향 동시 준비 완료. " +
            "이제 적 스폰을 시작할 수 있습니다.",
            this);


        stage1PreparationRoutine = null;
    }


    /// <summary>
    /// 반드시 다음 순서로 실행합니다.
    ///
    /// 1. 포탈 활성화
    /// 2. 포탈 등장 대기
    /// 3. 길 등장
    /// 4. 길 완성 대기
    /// 5. 적 스폰 허용
    /// </summary>
    private IEnumerator RunStage1Preparation(
        int preparationStep)
    {
        Debug.Log(
            $"[PortalStage] Stage 1 준비 단계 {preparationStep} 시작.",
            this);


        /*
         * 1. 포탈 활성화 및 크기 적용
         */
        ApplyStage1PortalState(
            preparationStep);


        /*
         * 2. 포탈이 먼저 보이는 시간을 확보
         */
        if (portalToRoadDelay > 0f)
        {
            yield return new WaitForSeconds(
                portalToRoadDelay);
        }


        /*
         * 3. 해당 포탈에서 길 생성
         */
        RevealStage1Road(
            preparationStep);


        /*
         * 4. 길 연출이 충분히 완료될 때까지 대기
         */
        if (roadRevealWaitDuration > 0f)
        {
            yield return new WaitForSeconds(
                roadRevealWaitDuration);
        }


        /*
         * 5. 길이 완성된 화면을 잠깐 보여준 뒤
         * 적 스폰을 허용
         */
        if (roadToEnemyDelay > 0f)
        {
            yield return new WaitForSeconds(
                roadToEnemyDelay);
        }


        stage1WaveController?.
            NotifyEnvironmentPreparationCompleted(
                preparationStep);


        Debug.Log(
            $"[PortalStage] Stage 1 준비 단계 {preparationStep} 완료. " +
            "이제 적 스폰을 시작할 수 있습니다.",
            this);


        stage1PreparationRoutine = null;
    }


    private void ApplyStage1PortalState(
        int preparationStep)
    {
        switch (preparationStep)
        {
            /*
             * Stage 1 시작:
             * Portal A
             */
            case 0:
                SetActivePortalCount(1);

                portalA?.ApplySmallPortal();

                Debug.Log(
                    "[PortalStage] Portal A 등장",
                    this);
                break;


            /*
             * Stage 1 1차 공격:
             * Portal B 추가
             */
            case 1:
                SetActivePortalCount(2);

                portalA?.ApplyMediumPortal();
                portalB?.ApplySmallPortal();

                Debug.Log(
                    "[PortalStage] Portal B 등장",
                    this);
                break;


            /*
             * Stage 1 2차 공격:
             * Portal C 추가
             */
            case 2:
                SetActivePortalCount(3);

                portalA?.ApplyMediumPortal();
                portalB?.ApplyMediumPortal();
                portalC?.ApplySmallPortal();

                Debug.Log(
                    "[PortalStage] Portal C 등장",
                    this);
                break;


            /*
             * Stage 1 최종 공격:
             * Portal D 추가
             */
            case 3:
                SetActivePortalCount(4);

                portalA?.ApplyLargePortal();
                portalB?.ApplyLargePortal();
                portalC?.ApplyMediumPortal();
                portalD?.ApplySmallPortal();

                Debug.Log(
                    "[PortalStage] Portal D 등장",
                    this);
                break;


            default:
                Debug.LogWarning(
                    $"[PortalStage] 알 수 없는 Stage 1 준비 단계: " +
                    preparationStep,
                    this);
                break;
        }
    }


    private void RevealStage1Road(
        int preparationStep)
    {
        switch (preparationStep)
        {
            case 0:
                RevealRoad1Once();
                break;

            case 1:
                RevealRoad2Once();
                break;

            case 2:
                RevealRoad3Once();
                break;

            case 3:
                RevealRoad4Once();
                break;

            default:
                Debug.LogWarning(
                    $"[PortalStage] 준비 단계 {preparationStep}에 " +
                    "대응하는 길이 없습니다.",
                    this);
                break;
        }
    }


    // =========================================================
    // Stage 2
    // =========================================================

    private void HandleStage2WaveStarted(
        Stage2WaveController.Stage2WavePhase phase,
        int enemyCount)
    {
        switch (phase)
        {
            case Stage2WaveController.Stage2WavePhase.First:
                /*
                 * 길과 포탈은 이미 Stage 1에서 완성됐습니다.
                 * Stage 2부터는 마을 침식만 진행합니다.
                 */
                RevealPart1Once();
                break;


            case Stage2WaveController.Stage2WavePhase.Second:
                RevealPart3Once();
                break;


            case Stage2WaveController.Stage2WavePhase.Final:
                RevealFenceOnce();
                break;
        }


        Debug.Log(
            $"[PortalStage] Stage 2 {phase} 시작 처리. " +
            $"예정 적 수: {enemyCount}",
            this);
    }


    private void HandleStage2WaveSpawnCompleted(
        Stage2WaveController.Stage2WavePhase phase)
    {
        switch (phase)
        {
            case Stage2WaveController.Stage2WavePhase.First:
                RevealPart2Once();
                break;


            case Stage2WaveController.Stage2WavePhase.Second:
                RevealPart4Once();
                break;


            case Stage2WaveController.Stage2WavePhase.Final:
                /*
                 * FinalProps 오브젝트가 없으므로
                 * 여기서는 추가 오브젝트를 등장시키지 않습니다.
                 */
                Debug.Log(
                    "[PortalStage] Stage 2 최종 공격 스폰 완료. " +
                    "FinalProps가 없어 추가 침식은 실행하지 않습니다.",
                    this);
                break;
        }
    }


    // =========================================================
    // Road
    // =========================================================

    private void RevealRoad1Once()
    {
        if (road1Revealed)
        {
            return;
        }

        road1Revealed = true;

        roadRevealController?.RevealRoad1();

        Debug.Log(
            "[PortalStage] Portal A에서 Road_1 생성",
            this);
    }


    private void RevealRoad2Once()
    {
        if (road2Revealed)
        {
            return;
        }

        road2Revealed = true;

        roadRevealController?.RevealRoad2();

        Debug.Log(
            "[PortalStage] Portal B에서 Road_2 생성",
            this);
    }


    private void RevealRoad3Once()
    {
        if (road3Revealed)
        {
            return;
        }

        road3Revealed = true;

        roadRevealController?.RevealRoad3();

        Debug.Log(
            "[PortalStage] Portal C에서 Road_3 생성",
            this);
    }


    private void RevealRoad4Once()
    {
        if (road4Revealed)
        {
            return;
        }

        road4Revealed = true;

        roadRevealController?.RevealRoad4();

        Debug.Log(
            "[PortalStage] Portal D에서 Road_4 생성",
            this);
    }


    // =========================================================
    // Village
    // =========================================================

    private void RevealPart1Once()
    {
        if (part1Revealed)
        {
            return;
        }

        part1Revealed = true;

        worldRevealController?.RevealPart1();

        Debug.Log(
            "[PortalStage] Stage 2 첫 침식: Part_1",
            this);
    }


    private void RevealPart2Once()
    {
        if (part2Revealed)
        {
            return;
        }

        part2Revealed = true;

        worldRevealController?.RevealPart2();

        Debug.Log(
            "[PortalStage] Stage 2 두 번째 침식: Part_2",
            this);
    }


    private void RevealPart3Once()
    {
        if (part3Revealed)
        {
            return;
        }

        part3Revealed = true;

        worldRevealController?.RevealPart3();

        Debug.Log(
            "[PortalStage] Stage 2 세 번째 침식: Part_3",
            this);
    }


    private void RevealPart4Once()
    {
        if (part4Revealed)
        {
            return;
        }

        part4Revealed = true;

        worldRevealController?.RevealPart4();

        Debug.Log(
            "[PortalStage] Stage 2 네 번째 침식: Part_4",
            this);
    }


    private void RevealFenceOnce()
    {
        if (fenceRevealed)
        {
            return;
        }

        fenceRevealed = true;

        worldRevealController?.RevealFence();

        Debug.Log(
            "[PortalStage] Stage 2 최종 침식: fence",
            this);
    }


    // =========================================================
    // Portal
    // =========================================================

    /// <summary>
    /// 최종 보스 등장 시 시야를 가리는 Stage 1/2 적 포탈을 모두 숨깁니다.
    /// 이후 Stage 1/2 테스트를 다시 시작하면 각 웨이브 이벤트가 필요한 포탈을
    /// 정상적으로 다시 활성화합니다.
    /// </summary>
    public void HideAllPortalsForBoss()
    {
        SetPortalActive(portalA, false);
        SetPortalActive(portalB, false);
        SetPortalActive(portalC, false);
        SetPortalActive(portalD, false);
        SetPortalActive(portalE, false);
        SetPortalActive(portalF, false);
        SetPortalActive(portalG, false);
        SetPortalActive(portalH, false);

        Debug.Log(
            "[PortalStage] 최종 보스 시야 확보를 위해 적 포탈을 모두 숨겼습니다.",
            this);
    }

    /// <summary>
    /// "테스트 - Stage 2부터 시작" 같은 테스트 진입점이 Stage 1의 실제
    /// 준비 코루틴(RunStage1PreparationAll)을 거치지 않고 곧바로 Stage 2로
    /// 넘어갈 때 사용합니다. Portal A~D와 Road_1~4를 즉시 모두 연 상태로
    /// 만들어, DreamEnemySpawner가 활성화된 스폰 포인트를 찾지 못해
    /// 적을 하나도 생성하지 못하는 문제를 막습니다.
    /// </summary>
    public void OpenAllStage1PortalsImmediatelyForTest()
    {
        SetActivePortalCount(4);

        portalA?.ApplyLargePortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplyLargePortal();
        portalD?.ApplyLargePortal();

        RevealRoad1Once();
        RevealRoad2Once();
        RevealRoad3Once();
        RevealRoad4Once();

        Debug.Log(
            "[PortalStage][TEST] Stage 2 직접 테스트를 위해 " +
            "Portal A~D와 Road_1~4를 즉시 모두 열었습니다.",
            this);
    }

    private void SetActivePortalCount(
        int count)
    {
        int safeCount =
            Mathf.Clamp(
                count,
                0,
                4);


        SetPortalActive(
            portalA,
            safeCount >= 1);

        SetPortalActive(
            portalB,
            safeCount >= 2);

        SetPortalActive(
            portalC,
            safeCount >= 3);

        SetPortalActive(
            portalD,
            safeCount >= 4);


        SetPortalActive(
            portalE,
            false);

        SetPortalActive(
            portalF,
            false);

        SetPortalActive(
            portalG,
            false);

        SetPortalActive(
            portalH,
            false);
    }


    private static void SetPortalActive(
        EnemyPortalGrowthController portal,
        bool active)
    {
        if (portal == null)
        {
            return;
        }

        portal.gameObject.SetActive(
            active);
    }


    private void OnValidate()
    {
        portalToRoadDelay =
            Mathf.Max(
                0f,
                portalToRoadDelay);

        roadRevealWaitDuration =
            Mathf.Max(
                0f,
                roadRevealWaitDuration);

        roadToEnemyDelay =
            Mathf.Max(
                0f,
                roadToEnemyDelay);

        allDirectionsRoadCascadeInterval =
            Mathf.Max(
                0f,
                allDirectionsRoadCascadeInterval);
    }
}