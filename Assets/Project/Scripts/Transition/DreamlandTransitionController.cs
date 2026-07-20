using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 진행 상태에 따라 현실 화면에서 완전한 꿈나라 화면으로 전환한다.
///
/// 현재 단계에서는 오브젝트 ON/OFF 방식으로 전환을 테스트한다.
/// 나중에 Passthrough 종료, 포탈 균열, 페이드 효과 등을 추가한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamlandTransitionController : MonoBehaviour
{
    [Header("진행 컨트롤러")]

    [Tooltip("Stage 2 이후의 시간을 관리하는 GameManager")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;


    [Header("현실에서 숨길 오브젝트")]

    [Tooltip("완전 VR 전환 시 숨길 현실 길과 현실 소품을 등록한다.")]
    [SerializeField]
    private List<GameObject> realityVisualObjects = new List<GameObject>();


    [Header("꿈나라에서 표시할 오브젝트")]

    [Tooltip("반구 내부에 들어올 꿈나라 바닥과 길이 들어 있는 부모")]
    [SerializeField]
    private GameObject interiorDreamRoot;

    [Tooltip("성, 나무, 외부 배경 등 완성된 전체 꿈나라 부모")]
    [SerializeField]
    private GameObject finalDreamlandRoot;


    [Header("전환 설정")]

    [Tooltip("FullVRTransition 상태가 시작된 후 맵을 바꾸기까지 기다리는 시간")]
    [Min(0f)]
    [SerializeField]
    private float transitionDelay = 1f;

    [Tooltip("Play 시작 시 현실 상태로 자동 초기화한다.")]
    [SerializeField]
    private bool applyRealityStateOnStart = true;


    // 현재 실행 중인 전환 작업
    private Coroutine transitionRoutine;


    private void OnEnable()
    {
        // 전체 진행 상태 변경 신호를 받는다.
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged += HandleStateChanged;
        }
    }

    private void Start()
    {
        // 게임 시작 상태를 현실 영역으로 초기화한다.
        if (applyRealityStateOnStart)
        {
            ApplyRealityState();
        }
    }

    private void OnDisable()
    {
        // 이벤트 연결을 해제한다.
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    /// <summary>
    /// 전체 게임 상태가 변경됐을 때 호출된다.
    /// </summary>
    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        // FullVRTransition 상태에서 실제 맵 전환을 시작한다.
        if (newState ==
            DreamlandGameFlowController.GameFlowState.FullVRTransition)
        {
            StartFullVRTransition();
        }
    }

    /// <summary>
    /// 완전 VR 전환을 시작한다.
    /// </summary>
    private void StartFullVRTransition()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(FullVRTransitionRoutine());
    }

    /// <summary>
    /// 지정된 시간만큼 기다린 뒤 꿈나라 상태를 적용한다.
    /// </summary>
    private IEnumerator FullVRTransitionRoutine()
    {
        Debug.Log("[DreamTransition] 완전 VR 전환을 시작합니다.");

        if (transitionDelay > 0f)
        {
            yield return new WaitForSeconds(transitionDelay);
        }

        ApplyFullDreamlandState();

        Debug.Log("[DreamTransition] 전체 꿈나라 맵이 활성화되었습니다.");

        transitionRoutine = null;
    }

    /// <summary>
    /// 실제 MR 시작 상태를 적용한다.
    /// 시작할 때는 가상 바닥, 길, 꿈나라 배경이 보이지 않게 한다.
    /// </summary>
    [ContextMenu("테스트 - 현실 상태 적용")]
    public void ApplyRealityState()
    {
        // 현실용 시각 오브젝트도 시작 시 숨긴다.
        // 실제 MR에서는 현실 공간이 그대로 보이기 때문이다.
        SetRealityVisualsActive(false);

        // 내부 꿈나라 바닥과 길 숨김
        if (interiorDreamRoot != null)
        {
            interiorDreamRoot.SetActive(false);
        }

        // 완성된 외부 꿈나라 전체 숨김
        if (finalDreamlandRoot != null)
        {
            finalDreamlandRoot.SetActive(false);
        }

        Debug.Log("[DreamTransition] 실제 MR 시작 상태를 적용했습니다.");
    }

    /// <summary>
    /// 완전한 꿈나라 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 완전 꿈나라 상태 적용")]
    public void ApplyFullDreamlandState()
    {
        // 현실 길과 현실 소품만 숨긴다.
        // RealityFloor는 이 목록에 넣지 않아서 Collider를 유지한다.
        SetRealityVisualsActive(false);

        if (interiorDreamRoot != null)
        {
            interiorDreamRoot.SetActive(true);
        }

        if (finalDreamlandRoot != null)
        {
            finalDreamlandRoot.SetActive(true);
        }
    }

    /// <summary>
    /// 등록된 현실 시각 오브젝트들의 활성 상태를 변경한다.
    /// </summary>
    private void SetRealityVisualsActive(bool active)
    {
        foreach (GameObject target in realityVisualObjects)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}