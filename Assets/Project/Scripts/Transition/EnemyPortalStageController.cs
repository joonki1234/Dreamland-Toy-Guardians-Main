using UnityEngine;
using DreamGuardians;

/// <summary>
/// Stage 1과 Stage 2의 공격 진행에 따라
/// 적 포탈 A~D의 개수와 크기를 관리합니다.
///
/// 각 포탈이 실제 공격에 처음 사용될 때
/// 해당 방향의 꿈나라 길도 함께 등장시킵니다.
///
/// 포탈과 길 연결:
/// Portal A → Road_1
/// Portal B → Road_2
/// Portal C → Road_3
/// Portal D → Road_4
///
/// Road_0은 아군 포탈과 코어 등장 연출이 완료된 뒤
/// AllyPortalCoreRevealController에서 처리합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPortalStageController : MonoBehaviour
{
    [Header("진행 시스템 연결")]

    [Tooltip("Stage 1 진행을 관리하는 컨트롤러")]
    [SerializeField]
    private Stage1WaveController stage1WaveController;

    [Tooltip("Stage 2의 내부 공격 진행을 관리하는 컨트롤러")]
    [SerializeField]
    private Stage2WaveController stage2WaveController;

    [Tooltip("꿈나라 길 등장 연출을 관리하는 컨트롤러")]
    [SerializeField]
    private DreamRoadRevealController roadRevealController;


    [Header("Stage 1~2 사용 포탈 A~D")]

    [Tooltip("첫 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalA;

    [Tooltip("두 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalB;

    [Tooltip("세 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalC;

    [Tooltip("네 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalD;


    [Header("보류 포탈 E~H")]

    [Tooltip("현재는 사용하지 않으며, 이후 보스전 등에 사용할 수 있는 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalE;

    [Tooltip("현재는 사용하지 않으며, 이후 보스전 등에 사용할 수 있는 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalF;

    [Tooltip("현재는 사용하지 않으며, 이후 보스전 등에 사용할 수 있는 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalG;

    [Tooltip("현재는 사용하지 않으며, 이후 보스전 등에 사용할 수 있는 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalH;


    [Header("시작 설정")]

    [Tooltip(
        "Play 시작 시 포탈 A의 기본 상태만 미리 적용합니다. " +
        "Road_1은 Stage 1이 실제로 시작될 때 등장합니다.")]
    [SerializeField]
    private bool applyStage1StartOnPlay = true;


    /*
     * 같은 진행 신호가 중복으로 들어오더라도
     * 이미 나타난 길을 다시 연출하지 않기 위한 값입니다.
     */
    private bool road1Revealed;
    private bool road2Revealed;
    private bool road3Revealed;
    private bool road4Revealed;


    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        /*
         * Stage 1 공격 사이에 발생하는
         * 환경 변화 신호를 받습니다.
         */
        DreamGameEvents.EnvironmentPhaseRequested +=
            HandleStage1EnvironmentPhase;

        /*
         * Stage 1이 실제로 시작됐을 때
         * 포탈 A와 Road_1을 적용합니다.
         */
        if (stage1WaveController != null)
        {
            stage1WaveController.Started +=
                HandleStage1Started;
        }

        /*
         * Stage 2의 각 웨이브 시작 신호를 받습니다.
         */
        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted +=
                HandleStage2WaveStarted;
        }
    }

    private void Start()
    {
        if (applyStage1StartOnPlay)
        {
            /*
             * 게임 시작 시에는 포탈 A의 상태만 준비합니다.
             *
             * 아직 Stage 1이 실제로 시작된 것은 아니므로
             * Road_1은 여기서 등장시키지 않습니다.
             */
            ApplyStage1FirstAttack();
        }
    }

    private void OnDisable()
    {
        DreamGameEvents.EnvironmentPhaseRequested -=
            HandleStage1EnvironmentPhase;

        if (stage1WaveController != null)
        {
            stage1WaveController.Started -=
                HandleStage1Started;
        }

        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted -=
                HandleStage2WaveStarted;
        }
    }


    /// <summary>
    /// Inspector 참조가 비어 있을 경우
    /// 씬에서 길 컨트롤러를 자동으로 찾습니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (roadRevealController == null)
        {
            roadRevealController =
                UnityEngine.Object.FindAnyObjectByType
                    <DreamRoadRevealController>();
        }
    }


    /// <summary>
    /// Stage 1이 실제로 시작될 때 호출됩니다.
    ///
    /// 포탈 A의 상태를 적용하고
    /// Portal A에서 중앙으로 이어지는 Road_1을 등장시킵니다.
    /// </summary>
    private void HandleStage1Started()
    {
        ApplyStage1FirstAttack();
        RevealRoad1Once();
    }


    /// <summary>
    /// Stage 1의 공격 사이에서 전달되는
    /// 환경 변화 신호를 받습니다.
    ///
    /// 1단계:
    /// 포탈 B 추가 및 Road_2 등장
    ///
    /// 2단계:
    /// 새로운 포탈 없이 A와 B 크기만 증가
    /// </summary>
    private void HandleStage1EnvironmentPhase(
        int phaseIndex)
    {
        switch (phaseIndex)
        {
            case 1:
                ApplyStage1SecondAttack();
                RevealRoad2Once();
                break;

            case 2:
                ApplyStage1FinalAttack();
                break;
        }
    }


    /// <summary>
    /// Stage 2의 각 공격이 시작될 때 호출됩니다.
    /// </summary>
    private void HandleStage2WaveStarted(
        Stage2WaveController.Stage2WavePhase phase,
        int enemyCount)
    {
        switch (phase)
        {
            case Stage2WaveController
                .Stage2WavePhase.First:

                ApplyStage2FirstAttack();
                RevealRoad3Once();
                break;

            case Stage2WaveController
                .Stage2WavePhase.Second:

                ApplyStage2SecondAttack();
                RevealRoad4Once();
                break;

            case Stage2WaveController
                .Stage2WavePhase.Final:

                ApplyStage2FinalAttack();
                break;
        }
    }


    /// <summary>
    /// Stage 1 첫 번째 공격:
    /// 포탈 A 하나만 작은 크기로 표시합니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 첫 공격")]
    public void ApplyStage1FirstAttack()
    {
        SetActivePortalCount(1);

        portalA?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 1 첫 공격: " +
            "포탈 A 활성화",
            this);
    }


    /// <summary>
    /// Stage 1 두 번째 공격:
    /// 포탈 B를 추가하여 A와 B를 표시합니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 두 번째 공격")]
    public void ApplyStage1SecondAttack()
    {
        SetActivePortalCount(2);

        portalA?.ApplyMediumPortal();
        portalB?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 1 두 번째 공격: " +
            "포탈 A, B 활성화",
            this);
    }


    /// <summary>
    /// Stage 1 최종 공격:
    /// 새로운 포탈은 추가하지 않고
    /// 포탈 A와 B의 크기만 증가시킵니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 최종 공격")]
    public void ApplyStage1FinalAttack()
    {
        SetActivePortalCount(2);

        portalA?.ApplyLargePortal();
        portalB?.ApplyMediumPortal();

        Debug.Log(
            "[PortalStage] Stage 1 최종 공격: " +
            "포탈 A, B 유지 및 성장",
            this);
    }


    /// <summary>
    /// Stage 2 첫 번째 공격:
    /// 포탈 C를 추가하여 A~C 총 3개를 표시합니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 첫 공격")]
    public void ApplyStage2FirstAttack()
    {
        SetActivePortalCount(3);

        portalA?.ApplyLargePortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 2 첫 공격: " +
            "포탈 C 추가, 총 3개",
            this);
    }


    /// <summary>
    /// Stage 2 두 번째 공격:
    /// 포탈 D를 추가하여 A~D 총 4개를 표시합니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 두 번째 공격")]
    public void ApplyStage2SecondAttack()
    {
        SetActivePortalCount(4);

        portalA?.ApplyLargePortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplyMediumPortal();
        portalD?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 2 두 번째 공격: " +
            "포탈 D 추가, 총 4개",
            this);
    }


    /// <summary>
    /// Stage 2 최종 공격:
    /// 포탈 A~D를 유지하고 크기를 최종 단계로 증가시킵니다.
    ///
    /// 포탈 E~H는 활성화하지 않습니다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 최종 공격")]
    public void ApplyStage2FinalAttack()
    {
        SetActivePortalCount(4);

        portalA?.ApplyFinalPortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplyFinalPortal();
        portalD?.ApplyLargePortal();

        Debug.Log(
            "[PortalStage] Stage 2 최종 공격: " +
            "포탈 A~D 유지 및 최종 성장",
            this);
    }


    /// <summary>
    /// 테스트용:
    /// 현재 Stage 1 첫 포탈과 Road_1을 함께 적용합니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈 A와 Road 1")]
    private void TestPortalAAndRoad1()
    {
        ApplyStage1FirstAttack();
        RevealRoad1Once();
    }


    /// <summary>
    /// 테스트용:
    /// 현재 Stage 1 두 번째 포탈과 Road_2를 함께 적용합니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈 B와 Road 2")]
    private void TestPortalBAndRoad2()
    {
        ApplyStage1SecondAttack();
        RevealRoad2Once();
    }


    /// <summary>
    /// 테스트용:
    /// 현재 Stage 2 첫 포탈과 Road_3을 함께 적용합니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈 C와 Road 3")]
    private void TestPortalCAndRoad3()
    {
        ApplyStage2FirstAttack();
        RevealRoad3Once();
    }


    /// <summary>
    /// 테스트용:
    /// 현재 Stage 2 두 번째 포탈과 Road_4를 함께 적용합니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈 D와 Road 4")]
    private void TestPortalDAndRoad4()
    {
        ApplyStage2SecondAttack();
        RevealRoad4Once();
    }


    /// <summary>
    /// Road_1을 처음 한 번만 등장시킵니다.
    /// </summary>
    private void RevealRoad1Once()
    {
        if (road1Revealed)
        {
            return;
        }

        road1Revealed = true;

        roadRevealController?.RevealRoad1();

        Debug.Log(
            "[PortalStage] 포탈 A 방향 Road_1 등장",
            this);
    }


    /// <summary>
    /// Road_2를 처음 한 번만 등장시킵니다.
    /// </summary>
    private void RevealRoad2Once()
    {
        if (road2Revealed)
        {
            return;
        }

        road2Revealed = true;

        roadRevealController?.RevealRoad2();

        Debug.Log(
            "[PortalStage] 포탈 B 방향 Road_2 등장",
            this);
    }


    /// <summary>
    /// Road_3을 처음 한 번만 등장시킵니다.
    /// </summary>
    private void RevealRoad3Once()
    {
        if (road3Revealed)
        {
            return;
        }

        road3Revealed = true;

        roadRevealController?.RevealRoad3();

        Debug.Log(
            "[PortalStage] 포탈 C 방향 Road_3 등장",
            this);
    }


    /// <summary>
    /// Road_4를 처음 한 번만 등장시킵니다.
    /// </summary>
    private void RevealRoad4Once()
    {
        if (road4Revealed)
        {
            return;
        }

        road4Revealed = true;

        roadRevealController?.RevealRoad4();

        Debug.Log(
            "[PortalStage] 포탈 D 방향 Road_4 등장",
            this);
    }


    /// <summary>
    /// 포탈 A부터 지정된 개수만큼 활성화합니다.
    ///
    /// 현재 Stage 1과 Stage 2에서는
    /// 최대 4개까지만 사용합니다.
    ///
    /// 포탈 E~H는 항상 비활성화합니다.
    /// </summary>
    private void SetActivePortalCount(
        int count)
    {
        int clampedCount =
            Mathf.Clamp(
                count,
                0,
                4);

        SetPortalActive(
            portalA,
            clampedCount >= 1);

        SetPortalActive(
            portalB,
            clampedCount >= 2);

        SetPortalActive(
            portalC,
            clampedCount >= 3);

        SetPortalActive(
            portalD,
            clampedCount >= 4);

        DisableReservedPortals();
    }


    /// <summary>
    /// 현재 보류 중인 포탈 E~H를 모두 비활성화합니다.
    /// 참조와 게임 오브젝트는 삭제하지 않습니다.
    /// </summary>
    private void DisableReservedPortals()
    {
        SetPortalActive(portalE, false);
        SetPortalActive(portalF, false);
        SetPortalActive(portalG, false);
        SetPortalActive(portalH, false);
    }


    /// <summary>
    /// 포탈 컨트롤러가 붙은 게임 오브젝트를
    /// 켜거나 끕니다.
    /// </summary>
    private void SetPortalActive(
        EnemyPortalGrowthController portal,
        bool active)
    {
        if (portal == null)
        {
            return;
        }

        portal.gameObject.SetActive(active);
    }
}