using System.Collections;
using UnityEngine;

/// <summary>
/// 적 포탈의 크기를 단계별로 변경한다.
///
/// 포탈이 원형으로 전체 확대되는 방식이 아니라
/// 가로 방향으로 크게 늘어나고,
/// 세로 방향으로는 조금씩 늘어나도록 만든다.
///
/// 현재는 Stage 진행과 연결하기 전의 테스트용 스크립트다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPortalGrowthController : MonoBehaviour
{
    [Header("포탈 연결")]

    [Tooltip("실제로 크기가 변할 포탈 오브젝트를 연결한다. EnemyPortal_A의 자식 EnemyPortals를 넣는다.")]
    [SerializeField]
    private Transform portalVisualTarget;


    [Header("작은 포탈 크기")]

    [Tooltip("처음 등장한 작은 포탈의 크기")]
    [SerializeField]
    private Vector3 smallScale = new Vector3(1f, 1f, 1f);


    [Header("중간 포탈 크기")]

    [Tooltip("Stage 1 진행 중 조금 확장된 포탈 크기")]
    [SerializeField]
    private Vector3 mediumScale = new Vector3(2f, 1.2f, 1f);


    [Header("큰 포탈 크기")]

    [Tooltip("Stage 2에서 더 크게 확장된 포탈 크기")]
    [SerializeField]
    private Vector3 largeScale = new Vector3(3.5f, 1.5f, 1f);


    [Header("최종 포탈 크기")]

    [Tooltip("여러 균열이 연결되기 직전의 최종 포탈 크기")]
    [SerializeField]
    private Vector3 finalScale = new Vector3(5f, 1.8f, 1f);


    [Header("확장 속도")]

    [Tooltip("현재 크기에서 목표 크기까지 변하는 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float growthDuration = 1.5f;


    // 현재 실행 중인 크기 변경 작업
    private Coroutine growthRoutine;


    /// <summary>
    /// 작은 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 작은 포탈")]
    public void ApplySmallPortal()
    {
        StartGrowth(smallScale);
    }

    /// <summary>
    /// 중간 크기의 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 중간 포탈")]
    public void ApplyMediumPortal()
    {
        StartGrowth(mediumScale);
    }

    /// <summary>
    /// 큰 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 큰 포탈")]
    public void ApplyLargePortal()
    {
        StartGrowth(largeScale);
    }

    /// <summary>
    /// 균열 연결 직전의 최종 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 최종 확장 포탈")]
    public void ApplyFinalPortal()
    {
        StartGrowth(finalScale);
    }

    /// <summary>
    /// 목표 크기로 포탈 확장을 시작한다.
    /// </summary>
    private void StartGrowth(Vector3 targetScale)
    {
        if (portalVisualTarget == null)
        {
            Debug.LogError(
                "[EnemyPortalGrowth] Portal Visual Target이 연결되지 않았습니다.",
                this);

            return;
        }

        // 이전 크기 변경 작업이 실행 중이면 중지한다.
        if (growthRoutine != null)
        {
            StopCoroutine(growthRoutine);
        }

        growthRoutine = StartCoroutine(
            GrowthRoutine(targetScale)
        );
    }

    /// <summary>
    /// 현재 크기에서 목표 크기까지 부드럽게 변경한다.
    /// </summary>
    private IEnumerator GrowthRoutine(Vector3 targetScale)
    {
        Vector3 startScale = portalVisualTarget.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < growthDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / growthDuration
            );

            portalVisualTarget.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                progress
            );

            yield return null;
        }

        // 마지막 크기를 정확하게 적용한다.
        portalVisualTarget.localScale = targetScale;

        Debug.Log(
            "[EnemyPortalGrowth] 포탈 크기 변경 완료: "
            + targetScale,
            this);

        growthRoutine = null;
    }

    private void OnDisable()
    {
        if (growthRoutine != null)
        {
            StopCoroutine(growthRoutine);
            growthRoutine = null;
        }
    }

    private void OnValidate()
    {
        // 크기가 0 이하가 되지 않도록 보정한다.
        smallScale.x = Mathf.Max(0.01f, smallScale.x);
        smallScale.y = Mathf.Max(0.01f, smallScale.y);
        smallScale.z = Mathf.Max(0.01f, smallScale.z);

        mediumScale.x = Mathf.Max(0.01f, mediumScale.x);
        mediumScale.y = Mathf.Max(0.01f, mediumScale.y);
        mediumScale.z = Mathf.Max(0.01f, mediumScale.z);

        largeScale.x = Mathf.Max(0.01f, largeScale.x);
        largeScale.y = Mathf.Max(0.01f, largeScale.y);
        largeScale.z = Mathf.Max(0.01f, largeScale.z);

        finalScale.x = Mathf.Max(0.01f, finalScale.x);
        finalScale.y = Mathf.Max(0.01f, finalScale.y);
        finalScale.z = Mathf.Max(0.01f, finalScale.z);

        growthDuration = Mathf.Max(0.01f, growthDuration);
    }
}