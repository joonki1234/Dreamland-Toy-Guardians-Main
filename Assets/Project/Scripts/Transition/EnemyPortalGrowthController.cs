using System.Collections;
using UnityEngine;

/// <summary>
/// 적 포탈의 크기와 높이를 단계별로 부드럽게 변경한다.
///
/// 포탈은 중심 기준으로 확대되므로,
/// 작은·중간·큰·최종 단계마다 Local Position Y를 따로 지정하여
/// 포탈 아래쪽이 바닥에 자연스럽게 닿도록 조절한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPortalGrowthController : MonoBehaviour
{
    [Header("포탈 연결")]

    [Tooltip(
        "실제로 크기와 위치가 변할 포탈 오브젝트. " +
        "EnemyPortal_A의 자식 EnemyPortals를 연결한다.")]
    [SerializeField]
    private Transform portalVisualTarget;


    [Header("작은 포탈 설정")]

    [Tooltip("처음 등장하는 작은 포탈의 크기")]
    [SerializeField]
    private Vector3 smallScale =
        new Vector3(1f, 1f, 1f);

    [Tooltip("작은 포탈 상태의 Local Position Y")]
    [SerializeField]
    private float smallLocalY = 0.5f;


    [Header("중간 포탈 설정")]

    [Tooltip("Stage 1 진행 중 확장된 포탈의 크기")]
    [SerializeField]
    private Vector3 mediumScale =
        new Vector3(2f, 1.2f, 1f);

    [Tooltip("중간 포탈 상태의 Local Position Y")]
    [SerializeField]
    private float mediumLocalY = 0.6f;


    [Header("큰 포탈 설정")]

    [Tooltip("Stage 2에서 크게 확장된 포탈의 크기")]
    [SerializeField]
    private Vector3 largeScale =
        new Vector3(3.5f, 1.5f, 1f);

    [Tooltip("큰 포탈 상태의 Local Position Y")]
    [SerializeField]
    private float largeLocalY = 0.7f;


    [Header("최종 포탈 설정")]

    [Tooltip("균열 연결 직전 최종 포탈의 크기")]
    [SerializeField]
    private Vector3 finalScale =
        new Vector3(5f, 1.8f, 1f);

    [Tooltip("최종 포탈 상태의 Local Position Y")]
    [SerializeField]
    private float finalLocalY = 0.8f;


    [Header("변화 속도")]

    [Tooltip("현재 상태에서 목표 상태까지 변하는 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float growthDuration = 1.5f;


    private Coroutine growthRoutine;


    /// <summary>
    /// 작은 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 작은 포탈")]
    public void ApplySmallPortal()
    {
        StartGrowth(smallScale, smallLocalY);
    }


    /// <summary>
    /// 중간 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 중간 포탈")]
    public void ApplyMediumPortal()
    {
        StartGrowth(mediumScale, mediumLocalY);
    }


    /// <summary>
    /// 큰 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 큰 포탈")]
    public void ApplyLargePortal()
    {
        StartGrowth(largeScale, largeLocalY);
    }


    /// <summary>
    /// 최종 포탈 상태를 적용한다.
    /// </summary>
    [ContextMenu("테스트 - 최종 확장 포탈")]
    public void ApplyFinalPortal()
    {
        StartGrowth(finalScale, finalLocalY);
    }


    /// <summary>
    /// 목표 크기와 목표 Local Y로 전환을 시작한다.
    /// </summary>
    private void StartGrowth(
        Vector3 targetScale,
        float targetLocalY)
    {
        if (portalVisualTarget == null)
        {
            Debug.LogError(
                "[EnemyPortalGrowth] Portal Visual Target이 연결되지 않았습니다.",
                this);

            return;
        }

        if (growthRoutine != null)
        {
            StopCoroutine(growthRoutine);
        }

        growthRoutine = StartCoroutine(
            GrowthRoutine(
                targetScale,
                targetLocalY));
    }


    /// <summary>
    /// 현재 크기와 위치에서 목표 크기와 위치까지
    /// 부드럽게 변화시킨다.
    /// </summary>
    private IEnumerator GrowthRoutine(
        Vector3 targetScale,
        float targetLocalY)
    {
        Vector3 startScale =
            portalVisualTarget.localScale;

        Vector3 startLocalPosition =
            portalVisualTarget.localPosition;

        Vector3 targetLocalPosition =
            startLocalPosition;

        targetLocalPosition.y = targetLocalY;

        float elapsedTime = 0f;

        while (elapsedTime < growthDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / growthDuration);

            portalVisualTarget.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    progress);

            portalVisualTarget.localPosition =
                Vector3.Lerp(
                    startLocalPosition,
                    targetLocalPosition,
                    progress);

            yield return null;
        }

        portalVisualTarget.localScale =
            targetScale;

        portalVisualTarget.localPosition =
            targetLocalPosition;

        Debug.Log(
            "[EnemyPortalGrowth] 포탈 변경 완료 / Scale: " +
            targetScale +
            " / Local Y: " +
            targetLocalY.ToString("0.00"),
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

        growthDuration =
            Mathf.Max(0.01f, growthDuration);
    }
}