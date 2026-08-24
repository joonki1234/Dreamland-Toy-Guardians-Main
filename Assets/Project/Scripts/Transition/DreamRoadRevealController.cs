using System.Collections;
using UnityEngine;

/// <summary>
/// 꿈나라 길을 단계별로 등장시키는 연출을 담당합니다.
///
/// Road_0:
/// 중앙 교차로이며 벽돌 하나로 구성됩니다.
///
/// Road_1 ~ Road_4:
/// 각 적 포탈에서 중앙 방향으로 이어지는 길입니다.
/// 각 Road의 자식은 Hierarchy 위쪽부터
/// 포탈에 가까운 벽돌 → 중앙에 가까운 벽돌 순서여야 합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamRoadRevealController : MonoBehaviour
{
    [Header("Road References")]

    [Tooltip("중앙 교차로입니다. 현재 벽돌 하나로 구성되어 있습니다.")]
    [SerializeField]
    private Transform road0;

    [Tooltip("첫 번째 적 포탈에서 중앙으로 이어지는 길입니다.")]
    [SerializeField]
    private Transform road1;

    [Tooltip("두 번째 적 포탈에서 중앙으로 이어지는 길입니다.")]
    [SerializeField]
    private Transform road2;

    [Tooltip("세 번째 적 포탈에서 중앙으로 이어지는 길입니다.")]
    [SerializeField]
    private Transform road3;

    [Tooltip("네 번째 적 포탈에서 중앙으로 이어지는 길입니다.")]
    [SerializeField]
    private Transform road4;

    [Header("Extra Road Visuals")]
    [Tooltip("Road_1(남쪽) 단계와 함께 표시할 DreamRoad 직속 외곽 조각입니다.")]
    [SerializeField]
    private Transform[] extraRoadObjectsForRoad1;

    [Tooltip("Road_2(서쪽) 단계와 함께 표시할 DreamRoad 직속 외곽 조각입니다.")]
    [SerializeField]
    private Transform[] extraRoadObjectsForRoad2;

    [Tooltip("Road_3(북쪽) 단계와 함께 표시할 DreamRoad 직속 외곽 조각입니다.")]
    [SerializeField]
    private Transform[] extraRoadObjectsForRoad3;

    [Tooltip("Road_4(동쪽) 단계와 함께 표시할 DreamRoad 직속 외곽 조각입니다.")]
    [SerializeField]
    private Transform[] extraRoadObjectsForRoad4;

    [Header("Reveal Animation")]

    [Tooltip("벽돌이 원래 위치보다 얼마나 아래에서 시작할지 설정합니다.")]
    [Min(0f)]
    [SerializeField]
    private float startYOffset = 0.3f;

    [Tooltip("벽돌의 시작 크기입니다. 0.7은 원래 크기의 70%입니다.")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float startScaleMultiplier = 0.7f;

    [Tooltip("올라오면서 순간적으로 커지는 최대 크기입니다.")]
    [Min(1f)]
    [SerializeField]
    private float overshootScaleMultiplier = 1.05f;

    [Tooltip("벽돌 하나가 나타나는 데 걸리는 시간입니다.")]
    [Min(0.01f)]
    [SerializeField]
    private float brickRevealDuration = 0.18f;

    [Tooltip("다음 벽돌 연출을 시작할 때까지의 간격입니다.")]
    [Min(0f)]
    [SerializeField]
    private float delayBetweenBricks = 0.06f;

    [Header("Start State")]

    [Tooltip("게임 시작 시 모든 길을 자동으로 숨깁니다.")]
    [SerializeField]
    private bool hideAllRoadsOnStart = true;

    private Coroutine road0Routine;
    private Coroutine road1Routine;
    private Coroutine road2Routine;
    private Coroutine road3Routine;
    private Coroutine road4Routine;

    private void Start()
    {
        if (hideAllRoadsOnStart)
        {
            HideAllRoads();
        }
    }

    /// <summary>
    /// 모든 길을 즉시 숨깁니다.
    /// 각 벽돌의 위치와 크기는 바꾸지 않고
    /// GameObject 활성 상태만 끕니다.
    /// </summary>
    [ContextMenu("테스트 - 모든 길 숨기기")]
    public void HideAllRoads()
    {
        StopAllRevealRoutines();

        SetRoadActive(road0, false);
        SetRoadActive(road1, false);
        SetRoadActive(road2, false);
        SetRoadActive(road3, false);
        SetRoadActive(road4, false);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad1, false);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad2, false);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad3, false);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad4, false);

        Debug.Log(
            "[DreamRoadReveal] 모든 꿈나라 길을 숨겼습니다.",
            this);
    }

    /// <summary>
    /// 모든 길을 원래 상태로 즉시 표시합니다.
    /// 애니메이션 없이 배치 상태를 확인할 때 사용합니다.
    /// </summary>
    [ContextMenu("테스트 - 모든 길 즉시 표시")]
    public void ShowAllRoadsImmediately()
    {
        StopAllRevealRoutines();

        ShowRoadImmediately(road0);
        ShowRoadImmediately(road1);
        ShowRoadImmediately(road2);
        ShowRoadImmediately(road3);
        ShowRoadImmediately(road4);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad1, true);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad2, true);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad3, true);
        SetExtraRoadObjectsActive(extraRoadObjectsForRoad4, true);

        Debug.Log(
            "[DreamRoadReveal] 모든 꿈나라 길을 즉시 표시했습니다.",
            this);
    }

    /// <summary>
    /// 중앙 교차로인 Road_0을 등장시킵니다.
    /// Road_0 자체가 벽돌 하나라면 Road_0 오브젝트가 등장합니다.
    /// Road_0 아래에 자식이 있다면 자식들을 등장시킵니다.
    /// </summary>
    [ContextMenu("테스트 - Road 0 등장")]
    public void RevealRoad0()
    {
        RestartRoadRoutine(
            ref road0Routine,
            RevealRoadRoutine(road0));
    }

    [ContextMenu("테스트 - Road 1 등장")]
    public void RevealRoad1()
    {
        RestartRoadRoutine(
            ref road1Routine,
            RevealRoadRoutine(road1));
    }

    [ContextMenu("테스트 - Road 2 등장")]
    public void RevealRoad2()
    {
        RestartRoadRoutine(
            ref road2Routine,
            RevealRoadRoutine(road2));
    }

    [ContextMenu("테스트 - Road 3 등장")]
    public void RevealRoad3()
    {
        RestartRoadRoutine(
            ref road3Routine,
            RevealRoadRoutine(road3));
    }

    [ContextMenu("테스트 - Road 4 등장")]
    public void RevealRoad4()
    {
        RestartRoadRoutine(
            ref road4Routine,
            RevealRoadRoutine(road4));
    }

    /// <summary>
    /// Road_0부터 Road_4까지 순서대로 테스트합니다.
    /// 실제 게임 진행용이 아니라 전체 연출 확인용입니다.
    /// </summary>
    [ContextMenu("테스트 - 모든 길 순서대로 등장")]
    public void RevealAllRoadsInSequence()
    {
        StopAllRevealRoutines();
        StartCoroutine(RevealAllRoadsRoutine());
    }

    private IEnumerator RevealAllRoadsRoutine()
    {
        yield return RevealRoadRoutine(road0);
        yield return new WaitForSeconds(0.2f);

        yield return RevealRoadRoutine(road1);
        yield return new WaitForSeconds(0.2f);

        yield return RevealRoadRoutine(road2);
        yield return new WaitForSeconds(0.2f);

        yield return RevealRoadRoutine(road3);
        yield return new WaitForSeconds(0.2f);

        yield return RevealRoadRoutine(road4);
    }

    private IEnumerator RevealRoadRoutine(Transform roadRoot)
    {
        if (roadRoot == null)
        {
            Debug.LogWarning(
                "[DreamRoadReveal] Road 참조가 연결되지 않았습니다.",
                this);

            yield break;
        }

        roadRoot.gameObject.SetActive(true);
        SetExtraRoadObjectsActive(GetExtraRoadObjects(roadRoot), true);

        /*
         * Road_0처럼 부모 오브젝트 자체가 벽돌이고
         * 자식이 없는 경우에는 부모 자체를 연출합니다.
         */
        if (roadRoot.childCount == 0)
        {
            yield return RevealBrickRoutine(roadRoot);
            yield break;
        }

        /*
         * Road_1 ~ Road_4는 Hierarchy 자식 순서를 그대로 사용합니다.
         *
         * 첫 번째 자식:
         * 포탈에서 가장 가까운 벽돌
         *
         * 마지막 자식:
         * 중앙에서 가장 가까운 벽돌
         */
        Transform[] bricks = new Transform[roadRoot.childCount];

        for (int i = 0; i < roadRoot.childCount; i++)
        {
            bricks[i] = roadRoot.GetChild(i);

            if (bricks[i] != null)
            {
                bricks[i].gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < bricks.Length; i++)
        {
            Transform brick = bricks[i];

            if (brick == null)
            {
                continue;
            }

            StartCoroutine(RevealBrickRoutine(brick));

            if (delayBetweenBricks > 0f)
            {
                yield return new WaitForSeconds(delayBetweenBricks);
            }
        }

        /*
         * 마지막 벽돌 애니메이션이 끝날 때까지 기다립니다.
         */
        if (brickRevealDuration > 0f)
        {
            yield return new WaitForSeconds(brickRevealDuration);
        }
    }

    private IEnumerator RevealBrickRoutine(Transform brick)
    {
        if (brick == null)
        {
            yield break;
        }

        Vector3 originalLocalPosition = brick.localPosition;
        Vector3 originalLocalScale = brick.localScale;

        Vector3 startLocalPosition =
            originalLocalPosition + Vector3.down * startYOffset;

        Vector3 startLocalScale =
            originalLocalScale * startScaleMultiplier;

        Vector3 overshootLocalScale =
            originalLocalScale * overshootScaleMultiplier;

        brick.localPosition = startLocalPosition;
        brick.localScale = startLocalScale;
        brick.gameObject.SetActive(true);

        float totalDuration =
            Mathf.Max(0.01f, brickRevealDuration);

        float riseDuration = totalDuration * 0.75f;
        float settleDuration = totalDuration * 0.25f;

        float elapsed = 0f;

        /*
         * 1단계:
         * 아래에서 원래 위치로 올라오면서
         * 시작 크기에서 105% 크기까지 커집니다.
         */
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                riseDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / riseDuration);

            float eased =
                1f - Mathf.Pow(1f - normalized, 3f);

            brick.localPosition =
                Vector3.Lerp(
                    startLocalPosition,
                    originalLocalPosition,
                    eased);

            brick.localScale =
                Vector3.Lerp(
                    startLocalScale,
                    overshootLocalScale,
                    eased);

            yield return null;
        }

        brick.localPosition = originalLocalPosition;
        brick.localScale = overshootLocalScale;

        elapsed = 0f;

        /*
         * 2단계:
         * 105%에서 원래 크기인 100%로 살짝 줄어듭니다.
         */
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                settleDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / settleDuration);

            brick.localScale =
                Vector3.Lerp(
                    overshootLocalScale,
                    originalLocalScale,
                    normalized);

            yield return null;
        }

        brick.localPosition = originalLocalPosition;
        brick.localScale = originalLocalScale;
    }

    private void ShowRoadImmediately(Transform roadRoot)
    {
        if (roadRoot == null)
        {
            return;
        }

        roadRoot.gameObject.SetActive(true);

        if (roadRoot.childCount == 0)
        {
            return;
        }

        for (int i = 0; i < roadRoot.childCount; i++)
        {
            Transform child = roadRoot.GetChild(i);

            if (child != null)
            {
                child.gameObject.SetActive(true);
            }
        }
    }

    private static void SetRoadActive(
        Transform roadRoot,
        bool active)
    {
        if (roadRoot == null)
        {
            return;
        }

        roadRoot.gameObject.SetActive(active);
    }

    private Transform[] GetExtraRoadObjects(Transform roadRoot)
    {
        if (roadRoot == road1) return extraRoadObjectsForRoad1;
        if (roadRoot == road2) return extraRoadObjectsForRoad2;
        if (roadRoot == road3) return extraRoadObjectsForRoad3;
        if (roadRoot == road4) return extraRoadObjectsForRoad4;
        return null;
    }

    private static void SetExtraRoadObjectsActive(
        Transform[] roadObjects,
        bool active)
    {
        if (roadObjects == null) return;

        for (int i = 0; i < roadObjects.Length; i++)
        {
            Transform roadObject = roadObjects[i];
            if (roadObject != null)
                roadObject.gameObject.SetActive(active);
        }
    }

    private void RestartRoadRoutine(
        ref Coroutine currentRoutine,
        IEnumerator newRoutine)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }

        currentRoutine = StartCoroutine(newRoutine);
    }

    private void StopAllRevealRoutines()
    {
        if (road0Routine != null)
        {
            StopCoroutine(road0Routine);
            road0Routine = null;
        }

        if (road1Routine != null)
        {
            StopCoroutine(road1Routine);
            road1Routine = null;
        }

        if (road2Routine != null)
        {
            StopCoroutine(road2Routine);
            road2Routine = null;
        }

        if (road3Routine != null)
        {
            StopCoroutine(road3Routine);
            road3Routine = null;
        }

        if (road4Routine != null)
        {
            StopCoroutine(road4Routine);
            road4Routine = null;
        }
    }

    private void OnValidate()
    {
        startYOffset =
            Mathf.Max(0f, startYOffset);

        startScaleMultiplier =
            Mathf.Clamp(
                startScaleMultiplier,
                0.01f,
                1f);

        overshootScaleMultiplier =
            Mathf.Max(
                1f,
                overshootScaleMultiplier);

        brickRevealDuration =
            Mathf.Max(
                0.01f,
                brickRevealDuration);

        delayBetweenBricks =
            Mathf.Max(
                0f,
                delayBetweenBricks);
    }
}
