using UnityEngine;
using DreamGuardians;

/// <summary>
/// Stage 1과 Stage 2의 공격 진행에 따라
/// 적 포탈의 개수와 크기를 관리한다.
///
/// 기존 이벤트를 받아 포탈 연출만 변경한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPortalStageController : MonoBehaviour
{
    [Header("진행 시스템 연결")]

    [Tooltip("Stage 2의 내부 공격 진행을 관리하는 컨트롤러")]
    [SerializeField]
    private Stage2WaveController stage2WaveController;


    [Header("적 포탈 A~H")]

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

    [Tooltip("다섯 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalE;

    [Tooltip("여섯 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalF;

    [Tooltip("일곱 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalG;

    [Tooltip("여덟 번째 적 포탈")]
    [SerializeField]
    private EnemyPortalGrowthController portalH;


    [Header("시작 설정")]

    [Tooltip("Play 시작 시 Stage 1 첫 공격 상태인 포탈 1개를 적용한다.")]
    [SerializeField]
    private bool applyStage1StartOnPlay = true;


    private void OnEnable()
    {
        // Stage 1에서 사용 중인 기존 환경 변화 이벤트를 받는다.
        DreamGameEvents.EnvironmentPhaseRequested +=
            HandleStage1EnvironmentPhase;

        // Stage 2 내부 웨이브 시작 이벤트를 받는다.
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

        if (stage2WaveController != null)
        {
            stage2WaveController.WaveStarted -= HandleStage2WaveStarted;
        }
    }

    /// <summary>
    /// Stage 1의 공격 종료 후 전달되는 환경 단계 신호를 받는다.
    ///
    /// 1단계: 첫 번째 공격 종료 후 포탈 2개
    /// 2단계: 두 번째 공격 종료 후 포탈 4개
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
        SetPortalActive(portalA, true);
        SetPortalActive(portalB, false);
        SetPortalActive(portalC, false);
        SetPortalActive(portalD, false);
        SetPortalActive(portalE, false);
        SetPortalActive(portalF, false);
        SetPortalActive(portalG, false);
        SetPortalActive(portalH, false);

        portalA?.ApplySmallPortal();

        Debug.Log("[PortalStage] Stage 1 첫 공격: 포탈 1개");
    }

    /// <summary>
    /// Stage 1 두 번째 공격:
    /// 포탈 A와 B를 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 두 번째 공격")]
    public void ApplyStage1SecondAttack()
    {
        SetPortalActive(portalA, true);
        SetPortalActive(portalB, true);
        SetPortalActive(portalC, false);
        SetPortalActive(portalD, false);
        SetPortalActive(portalE, false);
        SetPortalActive(portalF, false);
        SetPortalActive(portalG, false);
        SetPortalActive(portalH, false);

        portalA?.ApplyMediumPortal();
        portalB?.ApplySmallPortal();

        Debug.Log("[PortalStage] Stage 1 두 번째 공격: 포탈 2개");
    }

    /// <summary>
    /// Stage 1 최종 공격:
    /// 포탈 A~D 총 4개를 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 1 최종 공격")]
    public void ApplyStage1FinalAttack()
    {
        SetPortalActive(portalA, true);
        SetPortalActive(portalB, true);
        SetPortalActive(portalC, true);
        SetPortalActive(portalD, true);
        SetPortalActive(portalE, false);
        SetPortalActive(portalF, false);
        SetPortalActive(portalG, false);
        SetPortalActive(portalH, false);

        portalA?.ApplyMediumPortal();
        portalB?.ApplyMediumPortal();
        portalC?.ApplySmallPortal();
        portalD?.ApplySmallPortal();

        Debug.Log("[PortalStage] Stage 1 최종 공격: 포탈 4개");
    }

    /// <summary>
    /// Stage 2 첫 번째 공격:
    /// 포탈 A~E 총 5개를 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 첫 공격")]
    public void ApplyStage2FirstAttack()
    {
        SetFirstPortalCount(5);

        portalA?.ApplyLargePortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplyMediumPortal();
        portalD?.ApplyMediumPortal();
        portalE?.ApplySmallPortal();

        Debug.Log("[PortalStage] Stage 2 첫 공격: 포탈 5개");
    }

    /// <summary>
    /// Stage 2 두 번째 공격:
    /// 포탈 A~F 총 6개를 표시한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 두 번째 공격")]
    public void ApplyStage2SecondAttack()
    {
        SetFirstPortalCount(6);

        portalA?.ApplyLargePortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplyLargePortal();
        portalD?.ApplyLargePortal();
        portalE?.ApplyMediumPortal();
        portalF?.ApplySmallPortal();

        Debug.Log("[PortalStage] Stage 2 두 번째 공격: 포탈 6개");
    }

    /// <summary>
    /// Stage 2 최종 공격:
    /// 포탈 A~H 총 8개를 표시하고,
    /// 크기를 불규칙하게 섞어 종말 직전의 균열 상태를 표현한다.
    /// </summary>
    [ContextMenu("테스트 - Stage 2 최종 공격")]
    public void ApplyStage2FinalAttack()
    {
        SetFirstPortalCount(8);

        portalA?.ApplyFinalPortal();
        portalB?.ApplyLargePortal();
        portalC?.ApplyFinalPortal();
        portalD?.ApplyMediumPortal();
        portalE?.ApplyLargePortal();
        portalF?.ApplyFinalPortal();
        portalG?.ApplySmallPortal();
        portalH?.ApplyLargePortal();

        Debug.Log("[PortalStage] Stage 2 최종 공격: 포탈 8개");
    }

    /// <summary>
    /// A부터 지정된 개수만큼 포탈을 활성화한다.
    /// </summary>
    private void SetFirstPortalCount(int count)
    {
        SetPortalActive(portalA, count >= 1);
        SetPortalActive(portalB, count >= 2);
        SetPortalActive(portalC, count >= 3);
        SetPortalActive(portalD, count >= 4);
        SetPortalActive(portalE, count >= 5);
        SetPortalActive(portalF, count >= 6);
        SetPortalActive(portalG, count >= 7);
        SetPortalActive(portalH, count >= 8);
    }

    /// <summary>
    /// 포탈 컨트롤러가 붙은 오브젝트를 켜거나 끈다.
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