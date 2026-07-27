using System.Collections;
using UnityEngine;

/// <summary>
/// 적 포탈의 등장과 단계별 크기 변화를 관리한다.
///
/// 포탈 오브젝트가 활성화되면
/// 거의 보이지 않는 크기에서 작은 포탈 크기까지
/// 자동으로 부드럽게 등장한다.
///
/// 이 스크립트를 Portal_A부터 Portal_H까지
/// 모든 포탈 오브젝트에 붙여서 사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPortalGrowthController : MonoBehaviour
{
    [Header("포탈 연결")]

    [Tooltip(
        "실제로 크기와 위치가 변할 포탈 자식 오브젝트. " +
        "각 EnemyPortal의 자식 EnemyPortals를 연결한다.")]
    [SerializeField]
    private Transform portalVisualTarget;


    [Header("처음 등장 설정")]

    [Tooltip("포탈이 활성화될 때 자동으로 등장 연출을 실행한다.")]
    [SerializeField]
    private bool appearOnEnable = true;

    [Tooltip("포탈이 처음 나타나는 데 걸리는 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float appearanceDuration = 1.5f;

    [Tooltip(
        "포탈이 등장하기 시작할 때의 크기. " +
        "0으로 하지 않고 아주 작은 값을 사용한다.")]
    [SerializeField]
    private Vector3 appearanceStartScale =
        new Vector3(0.03f, 0.03f, 0.03f);


    [Header("작은 포탈 설정")]

    [Tooltip("처음 등장한 포탈의 기본 크기")]
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


    [Header("단계 변경 속도")]

    [Tooltip("작은·중간·큰·최종 상태로 변하는 데 걸리는 시간")]
    [Min(0.01f)]
    [SerializeField]
    private float growthDuration = 1.5f;


    private Coroutine portalRoutine;

    // 포탈의 최초 등장 연출이 끝났는지 확인한다.
    public bool IsAppearanceComplete { get; private set; }


    /// <summary>
    /// 각 포탈 오브젝트가 활성화될 때 자동으로 실행된다.
    /// 따라서 A부터 H까지 모두 동일하게 등장 연출이 적용된다.
    /// </summary>
    private void OnEnable()
    {
        IsAppearanceComplete = false;

        if (appearOnEnable)
        {
            PlayAppearance();
        }
        else
        {
            ApplySmallImmediately();
        }
    }


    /// <summary>
    /// 포탈을 아주 작은 크기에서 작은 포탈 크기까지
    /// 부드럽게 등장시킨다.
    /// </summary>
    [ContextMenu("테스트 - 포탈 등장")]
    public void PlayAppearance()
    {
        if (!CheckPortalTarget())
        {
            return;
        }

        StopCurrentRoutine();

        IsAppearanceComplete = false;

        // 활성화된 첫 순간부터 작은 크기를 적용한다.
        // 기존 큰 크기가 잠깐 보이는 현상을 방지한다.
        portalVisualTarget.localScale =
            appearanceStartScale;

        Vector3 startPosition =
            portalVisualTarget.localPosition;

        startPosition.y = smallLocalY;

        portalVisualTarget.localPosition =
            startPosition;

        portalRoutine =
            StartCoroutine(AppearanceRoutine());
    }


    /// <summary>
    /// 포탈 최초 등장 애니메이션.
    /// </summary>
    private IEnumerator AppearanceRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < appearanceDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / appearanceDuration);

            // 시작과 끝이 자연스럽게 느려지도록 보간한다.
            float smoothProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            portalVisualTarget.localScale =
                Vector3.Lerp(
                    appearanceStartScale,
                    smallScale,
                    smoothProgress);

            yield return null;
        }

        portalVisualTarget.localScale =
            smallScale;

        IsAppearanceComplete = true;
        portalRoutine = null;

        Debug.Log(
            "[EnemyPortalGrowth] " +
            gameObject.name +
            " 등장 완료",
            this);
    }


    /// <summary>
    /// 외부 웨이브 코드에서 포탈 등장 완료까지
    /// 기다릴 때 사용할 수 있다.
    /// </summary>
    public IEnumerator WaitForAppearance()
    {
        while (!IsAppearanceComplete)
        {
            yield return null;
        }
    }


    /// <summary>
    /// 작은 포탈 상태로 부드럽게 변경한다.
    /// </summary>
    [ContextMenu("테스트 - 작은 포탈")]
    public void ApplySmallPortal()
    {
        StartGrowth(
            smallScale,
            smallLocalY);
    }


    /// <summary>
    /// 중간 포탈 상태로 부드럽게 변경한다.
    /// </summary>
    [ContextMenu("테스트 - 중간 포탈")]
    public void ApplyMediumPortal()
    {
        StartGrowth(
            mediumScale,
            mediumLocalY);
    }


    /// <summary>
    /// 큰 포탈 상태로 부드럽게 변경한다.
    /// </summary>
    [ContextMenu("테스트 - 큰 포탈")]
    public void ApplyLargePortal()
    {
        StartGrowth(
            largeScale,
            largeLocalY);
    }


    /// <summary>
    /// 최종 포탈 상태로 부드럽게 변경한다.
    /// </summary>
    [ContextMenu("테스트 - 최종 포탈")]
    public void ApplyFinalPortal()
    {
        StartGrowth(
            finalScale,
            finalLocalY);
    }


    /// <summary>
    /// 목표 크기와 높이로 변경을 시작한다.
    /// </summary>
    private void StartGrowth(
        Vector3 targetScale,
        float targetLocalY)
    {
        if (!CheckPortalTarget())
        {
            return;
        }

        StopCurrentRoutine();

        portalRoutine =
            StartCoroutine(
                GrowthRoutine(
                    targetScale,
                    targetLocalY));
    }


    /// <summary>
    /// 현재 크기와 위치에서 목표 상태까지
    /// 부드럽게 변경한다.
    /// </summary>
    private IEnumerator GrowthRoutine(
        Vector3 targetScale,
        float targetLocalY)
    {
        Vector3 startScale =
            portalVisualTarget.localScale;

        Vector3 startPosition =
            portalVisualTarget.localPosition;

        Vector3 targetPosition =
            startPosition;

        targetPosition.y =
            targetLocalY;

        float elapsedTime = 0f;

        while (elapsedTime < growthDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / growthDuration);

            float smoothProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            portalVisualTarget.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    smoothProgress);

            portalVisualTarget.localPosition =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    smoothProgress);

            yield return null;
        }

        portalVisualTarget.localScale =
            targetScale;

        portalVisualTarget.localPosition =
            targetPosition;

        portalRoutine = null;

        Debug.Log(
            "[EnemyPortalGrowth] " +
            gameObject.name +
            " 크기 변경 완료 / Scale: " +
            targetScale +
            " / Local Y: " +
            targetLocalY.ToString("0.00"),
            this);
    }


    /// <summary>
    /// 애니메이션 없이 작은 포탈 상태를 즉시 적용한다.
    /// </summary>
    private void ApplySmallImmediately()
    {
        if (!CheckPortalTarget())
        {
            return;
        }

        portalVisualTarget.localScale =
            smallScale;

        Vector3 localPosition =
            portalVisualTarget.localPosition;

        localPosition.y =
            smallLocalY;

        portalVisualTarget.localPosition =
            localPosition;

        IsAppearanceComplete = true;
    }


    /// <summary>
    /// Portal Visual Target 연결 여부를 확인한다.
    /// </summary>
    private bool CheckPortalTarget()
    {
        if (portalVisualTarget != null)
        {
            return true;
        }

        Debug.LogError(
            "[EnemyPortalGrowth] " +
            gameObject.name +
            "의 Portal Visual Target이 연결되지 않았습니다.",
            this);

        return false;
    }


    /// <summary>
    /// 현재 실행 중인 등장 또는 크기 변경을 중단한다.
    /// </summary>
    private void StopCurrentRoutine()
    {
        if (portalRoutine == null)
        {
            return;
        }

        StopCoroutine(portalRoutine);
        portalRoutine = null;
    }


    private void OnDisable()
    {
        StopCurrentRoutine();

        IsAppearanceComplete = false;
    }


    private void OnValidate()
    {
        appearanceStartScale.x =
            Mathf.Max(0.001f, appearanceStartScale.x);

        appearanceStartScale.y =
            Mathf.Max(0.001f, appearanceStartScale.y);

        appearanceStartScale.z =
            Mathf.Max(0.001f, appearanceStartScale.z);

        smallScale.x =
            Mathf.Max(0.01f, smallScale.x);

        smallScale.y =
            Mathf.Max(0.01f, smallScale.y);

        smallScale.z =
            Mathf.Max(0.01f, smallScale.z);

        mediumScale.x =
            Mathf.Max(0.01f, mediumScale.x);

        mediumScale.y =
            Mathf.Max(0.01f, mediumScale.y);

        mediumScale.z =
            Mathf.Max(0.01f, mediumScale.z);

        largeScale.x =
            Mathf.Max(0.01f, largeScale.x);

        largeScale.y =
            Mathf.Max(0.01f, largeScale.y);

        largeScale.z =
            Mathf.Max(0.01f, largeScale.z);

        finalScale.x =
            Mathf.Max(0.01f, finalScale.x);

        finalScale.y =
            Mathf.Max(0.01f, finalScale.y);

        finalScale.z =
            Mathf.Max(0.01f, finalScale.z);

        appearanceDuration =
            Mathf.Max(0.01f, appearanceDuration);

        growthDuration =
            Mathf.Max(0.01f, growthDuration);
    }
}