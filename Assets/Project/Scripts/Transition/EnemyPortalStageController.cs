using UnityEngine;
using DreamGuardians;

/// <summary>
/// Stage 1과 Stage 2의 공격 진행에 따라
/// 적 포탈 A~D의 개수와 크기를 관리한다.
///
/// Stage 1에서는 포탈 A와 B를 순차적으로 사용하고,
/// Stage 2에서는 포탈 C와 D를 추가한다.
///
/// 포탈 E~H는 보스전 등 이후 콘텐츠를 위해 참조를 유지하지만,
/// 현재 Stage 1과 Stage 2에서는 활성화하지 않는다.
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

    [Tooltip("Play 시작 시 Stage 1 첫 공격 상태를 바로 적용한다.")]
    [SerializeField]
    private bool applyStage1StartOnPlay = true;


    private void OnEnable()
    {
        // Stage 1 공격 종료 후 전달되는 환경 변화 신호를 받는다.
        DreamGameEvents.EnvironmentPhaseRequested +=
            HandleStage1EnvironmentPhase;

        // Stage 1이 실제로 시작될 때 첫 포탈 상태를 적용한다.
        if (stage1WaveController != null)
        {
            stage1WaveController.Started += ApplyStage1FirstAttack;
        }

        // Stage 2의 각 웨이브 시작 신호를 받는다.
        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted += HandleStage2WaveStarted;
        }
    }

    private void Start()
    {
        if (applyStage1StartOnPlay)
        {
            ApplyStage1FirstAttack();
        }
    }

    private void OnDisable()
    {
        DreamGameEvents.EnvironmentPhaseRequested -=
            HandleStage1EnvironmentPhase;

        if (stage1WaveController != null)
        {
            stage1WaveController.Started -= ApplyStage1FirstAttack;
        }

        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted -= HandleStage2WaveStarted;
        }
    }


    /// <summary>
    /// Stage 1의 공격 종료 후 전달되는 환경 단계 신호를 받는다.
    ///
    /// 1단계: 포탈 B 추가
    /// 2단계: 포탈 A와 B의 크기만 증가
    /// </summary>
    private void HandleStage1EnvironmentPhase(int phaseIndex)
    {
        switch (phaseIndex)
        {
            case 1:
                ApplyStage1SecondAttack();
                break;

            case 2:
                ApplyStage1FinalAttack();
                break;
        }
    }


    /// <summary>
    /// Stage 2의 각 공격이 시작될 때 호출된다.
    /// </summary>
    private void HandleStage2WaveStarted(
        Stage2WaveController.Stage2WavePhase phase,
        int enemyCount)
    {
        switch (phase)
        {
            case Stage2WaveController.Stage2WavePhase.First:
                ApplyStage2FirstAttack();
                break;

            case Stage2WaveController.Stage2WavePhase.Second:
                ApplyStage2SecondAttack();
                break;

            case Stage2WaveController.Stage2WavePhase.Final:
                ApplyStage2FinalAttack();
                break;
        }
    }


    /// <summary>
    /// Stage 1 첫 번째 공격:
    /// 포탈 A 하나만 작은 크기로 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 첫 공격")]
    public void ApplyStage1FirstAttack()
    {
        SetActivePortalCount(1);

        portalA?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 1 첫 공격: 포탈 A 활성화");
    }


    /// <summary>
    /// Stage 1 두 번째 공격:
    /// 포탈 B를 추가하여 A와 B를 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 두 번째 공격")]
    public void ApplyStage1SecondAttack()
    {
        SetActivePortalCount(2);

        portalA?.ApplyMediumPortal();
        portalB?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 1 두 번째 공격: 포탈 A, B 활성화");
    }


    /// <summary>
    /// Stage 1 최종 공격:
    /// 새로운 포탈은 추가하지 않고
    /// 포탈 A와 B의 크기만 증가시킨다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 최종 공격")]
    public void ApplyStage1FinalAttack()
    {
        SetActivePortalCount(2);

        portalA?.ApplyLargePortal();
        portalB?.ApplyMediumPortal();

        Debug.Log(
            "[PortalStage] Stage 1 최종 공격: 포탈 A, B 유지 및 성장");
    }


    /// <summary>
    /// Stage 2 첫 번째 공격:
    /// 포탈 C를 추가하여 A~C 총 3개를 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 첫 공격")]
    public void ApplyStage2FirstAttack()
    {
        SetActivePortalCount(3);

        portalA?.ApplyLargePortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplySmallPortal();

        Debug.Log(
            "[PortalStage] Stage 2 첫 공격: 포탈 C 추가, 총 3개");
    }


    /// <summary>
    /// Stage 2 두 번째 공격:
    /// 포탈 D를 추가하여 A~D 총 4개를 표시한다.
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
            "[PortalStage] Stage 2 두 번째 공격: 포탈 D 추가, 총 4개");
    }


    /// <summary>
    /// Stage 2 최종 공격:
    /// 포탈 A~D를 유지하고 크기를 최종 단계로 증가시킨다.
    ///
    /// 포탈 E~H는 활성화하지 않는다.
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
            "[PortalStage] Stage 2 최종 공격: 포탈 A~D 유지 및 최종 성장");
    }


    /// <summary>
    /// 포탈 A부터 지정된 개수만큼 활성화한다.
    ///
    /// 현재 Stage 1과 Stage 2에서는 최대 4개까지만 사용한다.
    /// 포탈 E~H는 항상 비활성화한다.
    /// </summary>
    private void SetActivePortalCount(int count)
    {
        int clampedCount = Mathf.Clamp(count, 0, 4);

        SetPortalActive(portalA, clampedCount >= 1);
        SetPortalActive(portalB, clampedCount >= 2);
        SetPortalActive(portalC, clampedCount >= 3);
        SetPortalActive(portalD, clampedCount >= 4);

        DisableReservedPortals();
    }


    /// <summary>
    /// 현재 보류 중인 포탈 E~H를 모두 비활성화한다.
    /// 참조와 게임 오브젝트는 삭제하지 않는다.
    /// </summary>
    private void DisableReservedPortals()
    {
        SetPortalActive(portalE, false);
        SetPortalActive(portalF, false);
        SetPortalActive(portalG, false);
        SetPortalActive(portalH, false);
    }


    /// <summary>
    /// 포탈 컨트롤러가 붙은 게임 오브젝트를 켜거나 끈다.
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