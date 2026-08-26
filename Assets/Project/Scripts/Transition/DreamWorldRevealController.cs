using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꿈나라 마을과 주변 사물이 현실 공간을 침식하듯
/// 아래에서 순서대로 올라오는 연출을 담당합니다.
///
/// 각 Part의 직속 자식을 하나의 오브젝트로 취급하고,
/// Renderer 크기를 계산해 작은 오브젝트부터 큰 오브젝트 순서로
/// 자동 정렬하여 등장시킵니다.
///
/// 진행 구성:
/// Stage 2 첫 공격  : Part_1 → Part_2
/// Stage 2 두 번째 : Part_3 → Part_4
/// Stage 2 최종    : fence → FinalProps
/// 완전 VR 전환    : Tree_Border → CastleScene → DreamBackground
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamWorldRevealController : MonoBehaviour
{
    [Header("Root Reference")]

    [Tooltip(
        "05_FINAL_DREAMLAND 부모를 연결합니다. " +
        "연출을 실행할 때 부모가 꺼져 있으면 자동으로 켭니다.")]
    [SerializeField]
    private GameObject finalDreamlandRoot;


    [Header("Village Parts")]

    [Tooltip("왼쪽 위 버섯 마을 Part_1")]
    [SerializeField]
    private Transform part1;

    [Tooltip("오른쪽 위 놀이 마을 Part_2")]
    [SerializeField]
    private Transform part2;

    [Tooltip("쿠키와 겨울 장식 구역 Part_3")]
    [SerializeField]
    private Transform part3;

    [Tooltip("체스판과 흰색 체스말 구역 Part_4")]
    [SerializeField]
    private Transform part4;


    [Header("Final Groups")]

    [Tooltip("중앙 십자 형태의 빨간 울타리")]
    [SerializeField]
    private Transform fence;

    [Tooltip("최종 장식 소품 그룹")]
    [SerializeField]
    private Transform finalProps;

    [Tooltip("맵 외곽의 나무 그룹")]
    [SerializeField]
    private Transform treeBorder;

    [Tooltip("중앙 성 그룹")]
    [SerializeField]
    private Transform castleScene;

    [Tooltip("완전한 꿈나라 배경")]
    [SerializeField]
    private Transform dreamBackground;


    [Header("Object Reveal Animation")]

    [Tooltip("사물이 원래 위치보다 얼마나 아래에서 시작할지 설정합니다.")]
    [Min(0f)]
    [SerializeField]
    private float startYOffset = 0.8f;

    [Tooltip(
        "사물의 시작 크기입니다. " +
        "0.6은 원래 크기의 60%입니다.")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float startScaleMultiplier = 0.6f;

    [Tooltip(
        "올라오면서 순간적으로 도달하는 최대 크기입니다. " +
        "1.03은 원래 크기의 103%입니다.")]
    [Min(1f)]
    [SerializeField]
    private float overshootScaleMultiplier = 1.03f;

    [Tooltip("사물 하나가 올라와 정착하는 데 걸리는 시간입니다.")]
    [Min(0.01f)]
    [SerializeField]
    private float objectRevealDuration = 0.7f;

    [Tooltip("다음 사물의 등장 연출을 시작할 때까지의 간격입니다.")]
    [Min(0f)]
    [SerializeField]
    private float delayBetweenObjects = 0.12f;

    [Tooltip("한 마을이 끝난 뒤 다음 마을을 시작하기 전 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField]
    private float delayBetweenGroups = 0.7f;


    [Header("Background")]

    [Tooltip(
        "DreamBackground는 크기가 매우 큰 배경일 가능성이 높으므로 " +
        "아래에서 올리지 않고 즉시 표시합니다.")]
    [SerializeField]
    private bool showBackgroundImmediately = true;


    [Header("Start State")]

    [Tooltip(
        "게임 시작 시 Part_1~4와 최종 그룹들을 자동으로 숨깁니다. " +
        "05_FINAL_DREAMLAND 부모는 활성화하되 자식만 숨깁니다.")]
    [SerializeField]
    private bool hideAllGroupsOnStart = true;


    private readonly Dictionary<Transform, TransformState>
        originalStates =
            new Dictionary<Transform, TransformState>();

    private readonly HashSet<Transform>
        revealedGroups =
            new HashSet<Transform>();

    private readonly HashSet<Transform>
        revealingGroups =
            new HashSet<Transform>();

    private readonly Dictionary<Transform, Coroutine>
        groupRevealRoutines =
            new Dictionary<Transform, Coroutine>();

    private Coroutine sequenceRoutine;


    /// <summary>
    /// 오브젝트의 원래 로컬 위치와 크기를 저장합니다.
    /// </summary>
    private sealed class TransformState
    {
        public Vector3 LocalPosition;
        public Vector3 LocalScale;

        public TransformState(
            Vector3 localPosition,
            Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
        }
    }


    private void Awake()
    {
        ResolveReferences();
        CaptureAllOriginalStates();
    }


    private void Start()
    {
        if (hideAllGroupsOnStart)
        {
            HideAllGroupsImmediately();
        }
    }


    /// <summary>
    /// Inspector 참조가 비어 있을 경우
    /// 씬에서 오브젝트 이름으로 자동 탐색합니다.
    /// </summary>
    private void ResolveReferences()
    {
        finalDreamlandRoot ??=
            FindSceneObject("05_FINAL_DREAMLAND");

        part1 ??=
            FindSceneTransform("Part_1");

        part2 ??=
            FindSceneTransform("Part_2");

        part3 ??=
            FindSceneTransform("Part_3");

        part4 ??=
            FindSceneTransform("Part_4");

        fence ??=
            FindSceneTransform("fence");

        finalProps ??=
            FindSceneTransform("FinalProps");

        treeBorder ??=
            FindSceneTransform("Tree_Border");

        castleScene ??=
            FindSceneTransform("CastleScene");

        dreamBackground ??=
            FindSceneTransform("DreamBackground");
    }


    /// <summary>
    /// 모든 그룹과 직속 자식의 원래 Transform 값을 저장합니다.
    /// </summary>
    private void CaptureAllOriginalStates()
    {
        CaptureGroupStates(part1);
        CaptureGroupStates(part2);
        CaptureGroupStates(part3);
        CaptureGroupStates(part4);

        CaptureGroupStates(fence);
        CaptureGroupStates(finalProps);
        CaptureGroupStates(treeBorder);
        CaptureGroupStates(castleScene);
        CaptureGroupStates(dreamBackground);
    }


    private void CaptureGroupStates(
        Transform groupRoot)
    {
        if (groupRoot == null)
        {
            return;
        }

        CaptureTransformState(groupRoot);

        for (int i = 0;
             i < groupRoot.childCount;
             i++)
        {
            CaptureTransformState(
                groupRoot.GetChild(i));
        }
    }


    private void CaptureTransformState(
        Transform target)
    {
        if (target == null ||
            originalStates.ContainsKey(target))
        {
            return;
        }

        originalStates.Add(
            target,
            new TransformState(
                target.localPosition,
                target.localScale));
    }


    /// <summary>
    /// 모든 꿈나라 마을과 최종 그룹을 즉시 숨깁니다.
    ///
    /// 05_FINAL_DREAMLAND 부모는 켜두지만,
    /// 실제 자식 그룹들은 모두 꺼지므로
    /// 시작 화면에는 아무 사물도 보이지 않습니다.
    /// </summary>
    [ContextMenu("테스트 - 모든 주변 사물 숨기기")]
    public void HideAllGroupsImmediately()
    {
        StopCurrentSequence();
        RestoreAllOriginalStates();

        EnsureFinalDreamlandRootActive();

        SetGroupActive(part1, false);
        SetGroupActive(part2, false);
        SetGroupActive(part3, false);
        SetGroupActive(part4, false);

        SetGroupActive(fence, false);
        SetGroupActive(finalProps, false);
        SetGroupActive(treeBorder, false);
        SetGroupActive(castleScene, false);
        SetGroupActive(dreamBackground, false);

        revealedGroups.Clear();

        Debug.Log(
            "[DreamWorldReveal] 모든 주변 꿈나라 사물을 숨겼습니다.",
            this);
    }


    /// <summary>
    /// 모든 그룹을 애니메이션 없이 즉시 표시합니다.
    /// 현재 배치를 확인할 때 사용합니다.
    /// </summary>
    [ContextMenu("테스트 - 모든 주변 사물 즉시 표시")]
    public void ShowAllGroupsImmediately()
    {
        StopCurrentSequence();
        RestoreAllOriginalStates();

        EnsureFinalDreamlandRootActive();

        ShowGroupImmediately(part1);
        ShowGroupImmediately(part2);
        ShowGroupImmediately(part3);
        ShowGroupImmediately(part4);

        ShowGroupImmediately(fence);
        ShowGroupImmediately(finalProps);
        ShowGroupImmediately(treeBorder);
        ShowGroupImmediately(castleScene);
        ShowGroupImmediately(dreamBackground);

        MarkAllGroupsRevealed();

        Debug.Log(
            "[DreamWorldReveal] 모든 주변 꿈나라 사물을 즉시 표시했습니다.",
            this);
    }


    // =========================================================
    // 개별 그룹 테스트
    // =========================================================

    [ContextMenu("테스트 - Part 1 버섯 마을 등장")]
    public void RevealPart1()
    {
        StartSingleGroupReveal(part1);
    }


    [ContextMenu("테스트 - Part 2 놀이 마을 등장")]
    public void RevealPart2()
    {
        StartSingleGroupReveal(part2);
    }


    [ContextMenu("테스트 - Part 3 쿠키/겨울 장식 구역 등장")]
    public void RevealPart3()
    {
        StartSingleGroupReveal(part3);
    }


    [ContextMenu("테스트 - Part 4 체스 구역 등장")]
    public void RevealPart4()
    {
        StartSingleGroupReveal(part4);
    }


    [ContextMenu("테스트 - Fence 등장")]
    public void RevealFence()
    {
        StartSingleGroupReveal(fence);
    }


    [ContextMenu("테스트 - Final Props 등장")]
    public void RevealFinalProps()
    {
        StartSingleGroupReveal(finalProps);
    }


    [ContextMenu("테스트 - Tree Border 등장")]
    public void RevealTreeBorder()
    {
        StartSingleGroupReveal(treeBorder);
    }


    [ContextMenu("테스트 - Castle Scene 등장")]
    public void RevealCastleScene()
    {
        StartSingleGroupReveal(castleScene);
    }


    [ContextMenu("테스트 - Dream Background 등장")]
    public void RevealDreamBackground()
    {
        EnsureFinalDreamlandRootActive();

        if (dreamBackground == null)
        {
            LogMissingGroup("DreamBackground");
            return;
        }

        if (showBackgroundImmediately)
        {
            ShowGroupImmediately(dreamBackground);
            revealedGroups.Add(dreamBackground);

            Debug.Log(
                "[DreamWorldReveal] DreamBackground를 즉시 표시했습니다.",
                this);

            return;
        }

        StartSingleGroupReveal(dreamBackground);
    }


    // =========================================================
    // 진행 단계별 시퀀스
    // =========================================================

    /// <summary>
    /// Stage 2 첫 번째 공격:
    /// Part_1 버섯 마을 → Part_2 놀이 마을
    /// </summary>
    [ContextMenu("테스트 - Stage 2 첫 침식")]
    public void RevealStage2FirstPhase()
    {
        RestartSequence(
            RevealTwoGroupsRoutine(
                part1,
                part2));
    }


    /// <summary>
    /// Stage 2 두 번째 공격:
    /// Part_3 체스 마을 → Part_4 눈 마을
    /// </summary>
    [ContextMenu("테스트 - Stage 2 두 번째 침식")]
    public void RevealStage2SecondPhase()
    {
        RestartSequence(
            RevealTwoGroupsRoutine(
                part3,
                part4));
    }


    /// <summary>
    /// Stage 2 최종 공격:
    /// 중앙 울타리 → 최종 소품
    /// </summary>
    [ContextMenu("테스트 - Stage 2 최종 침식")]
    public void RevealStage2FinalPhase()
    {
        RestartSequence(
            RevealTwoGroupsRoutine(
                fence,
                finalProps));
    }


    /// <summary>
    /// 완전 VR 전환:
    /// 외곽 나무 → 성 → 배경
    /// </summary>
    [ContextMenu("테스트 - 완전 VR 주변 사물 등장")]
    public void RevealFullVRPhase()
    {
        RestartSequence(
            RevealFullVRRoutine());
    }


    /// <summary>
    /// 모든 침식 단계를 처음부터 순서대로 확인합니다.
    /// 실제 게임 연결용이 아닌 전체 테스트용입니다.
    /// </summary>
    [ContextMenu("테스트 - 전체 침식 연출")]
    public void RevealAllInSequence()
    {
        RestartSequence(
            RevealAllRoutine());
    }


    private void StartSingleGroupReveal(
        Transform groupRoot)
    {
        EnsureFinalDreamlandRootActive();

        if (groupRoot == null)
        {
            Debug.LogWarning(
                "[DreamWorldReveal] 등장시킬 그룹이 연결되지 않았습니다.",
                this);

            return;
        }

        if (revealedGroups.Contains(groupRoot))
        {
            Debug.Log(
                $"[DreamWorldReveal] {groupRoot.name}은 이미 등장했습니다.",
                this);

            return;
        }

        if (revealingGroups.Contains(groupRoot))
        {
            Debug.Log(
                $"[DreamWorldReveal] {groupRoot.name}은 이미 등장 중입니다.",
                this);

            return;
        }

        Coroutine routine =
            StartCoroutine(
                RevealGroupRoutine(groupRoot));

        groupRevealRoutines[groupRoot] = routine;
    }


    private IEnumerator RevealTwoGroupsRoutine(
        Transform firstGroup,
        Transform secondGroup)
    {
        EnsureFinalDreamlandRootActive();

        yield return RevealGroupRoutine(
            firstGroup);

        if (delayBetweenGroups > 0f)
        {
            yield return new WaitForSeconds(
                delayBetweenGroups);
        }

        yield return RevealGroupRoutine(
            secondGroup);

        sequenceRoutine = null;
    }


    private IEnumerator RevealFullVRRoutine()
    {
        EnsureFinalDreamlandRootActive();

        yield return RevealGroupRoutine(
            treeBorder);

        if (delayBetweenGroups > 0f)
        {
            yield return new WaitForSeconds(
                delayBetweenGroups);
        }

        yield return RevealGroupRoutine(
            castleScene);

        if (delayBetweenGroups > 0f)
        {
            yield return new WaitForSeconds(
                delayBetweenGroups);
        }

        if (showBackgroundImmediately)
        {
            ShowGroupImmediately(
                dreamBackground);

            if (dreamBackground != null)
            {
                revealedGroups.Add(
                    dreamBackground);
            }
        }
        else
        {
            yield return RevealGroupRoutine(
                dreamBackground);
        }

        sequenceRoutine = null;
    }


    private IEnumerator RevealAllRoutine()
    {
        EnsureFinalDreamlandRootActive();

        yield return RevealGroupRoutine(part1);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(part2);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(part3);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(part4);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(fence);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(finalProps);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(treeBorder);
        yield return WaitBetweenGroups();

        yield return RevealGroupRoutine(castleScene);
        yield return WaitBetweenGroups();

        if (showBackgroundImmediately)
        {
            ShowGroupImmediately(
                dreamBackground);

            if (dreamBackground != null)
            {
                revealedGroups.Add(
                    dreamBackground);
            }
        }
        else
        {
            yield return RevealGroupRoutine(
                dreamBackground);
        }

        sequenceRoutine = null;
    }


    private IEnumerator WaitBetweenGroups()
    {
        if (delayBetweenGroups > 0f)
        {
            yield return new WaitForSeconds(
                delayBetweenGroups);
        }
    }


    /// <summary>
    /// 그룹의 직속 자식들을 크기가 작은 순서대로 정렬한 뒤
    /// 아래에서 올라오는 연출을 실행합니다.
    /// </summary>
    private IEnumerator RevealGroupRoutine(
        Transform groupRoot)
    {
        if (groupRoot == null)
        {
            yield break;
        }

        if (revealedGroups.Contains(groupRoot) ||
            revealingGroups.Contains(groupRoot))
        {
            yield break;
        }

        EnsureFinalDreamlandRootActive();

        List<Transform> targets =
            GetDirectRevealTargets(groupRoot);

        if (targets.Count == 0)
        {
            yield break;
        }

        revealingGroups.Add(groupRoot);

        /*
         * 크기가 작은 오브젝트부터 큰 오브젝트 순서로 정렬합니다.
         */
        targets.Sort(
            CompareByVisualSize);

        /*
         * 부모는 켜두고 실제 등장 대상만 먼저 숨깁니다.
         */
        groupRoot.gameObject.SetActive(true);

        foreach (Transform target in targets)
        {
            RestoreOriginalState(target);
            target.gameObject.SetActive(false);
        }

        /*
         * 오브젝트 연출은 서로 조금씩 겹치도록 시작합니다.
         */
        foreach (Transform target in targets)
        {
            StartCoroutine(
                RevealObjectRoutine(target));

            if (delayBetweenObjects > 0f)
            {
                yield return new WaitForSeconds(
                    delayBetweenObjects);
            }
        }

        /*
         * 마지막 오브젝트가 완전히 정착할 때까지 기다립니다.
         */
        if (objectRevealDuration > 0f)
        {
            yield return new WaitForSeconds(
                objectRevealDuration);
        }

        revealedGroups.Add(groupRoot);
        revealingGroups.Remove(groupRoot);
        groupRevealRoutines.Remove(groupRoot);

        Debug.Log(
            $"[DreamWorldReveal] {groupRoot.name} 등장 완료. " +
            $"오브젝트 수: {targets.Count}",
            this);
    }


    /// <summary>
    /// 직속 자식이 있으면 자식들을 반환하고,
    /// 자식이 없는 경우 그룹 오브젝트 자체를 반환합니다.
    /// </summary>
    private List<Transform> GetDirectRevealTargets(
        Transform groupRoot)
    {
        List<Transform> targets =
            new List<Transform>();

        if (groupRoot == null)
        {
            return targets;
        }

        if (groupRoot.childCount == 0)
        {
            CaptureTransformState(groupRoot);
            targets.Add(groupRoot);
            return targets;
        }

        for (int i = 0;
             i < groupRoot.childCount;
             i++)
        {
            Transform child =
                groupRoot.GetChild(i);

            if (child == null)
            {
                continue;
            }

            CaptureTransformState(child);
            targets.Add(child);
        }

        return targets;
    }


    /// <summary>
    /// Renderer의 전체 Bounds 크기를 이용해
    /// 두 오브젝트의 시각적 크기를 비교합니다.
    /// </summary>
    private int CompareByVisualSize(
        Transform first,
        Transform second)
    {
        float firstSize =
            CalculateVisualSize(first);

        float secondSize =
            CalculateVisualSize(second);

        return firstSize.CompareTo(
            secondSize);
    }


    private float CalculateVisualSize(
        Transform target)
    {
        if (target == null)
        {
            return 0f;
        }

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(
                true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            /*
             * Renderer가 없는 빈 부모라면
             * Transform Scale을 대신 사용합니다.
             */
            Vector3 scale =
                target.lossyScale;

            return Mathf.Abs(
                scale.x *
                scale.y *
                scale.z);
        }

        bool boundsCreated = false;
        Bounds combinedBounds =
            new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!boundsCreated)
            {
                combinedBounds =
                    renderer.bounds;

                boundsCreated = true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    renderer.bounds);
            }
        }

        if (!boundsCreated)
        {
            return 0f;
        }

        Vector3 size =
            combinedBounds.size;

        /*
         * 부피만 사용하면 체스판처럼 낮고 넓은 물체가
         * 너무 작게 계산될 수 있으므로,
         * 부피와 가장 긴 변의 길이를 함께 반영합니다.
         */
        float volume =
            Mathf.Abs(
                size.x *
                size.y *
                size.z);

        float longestSide =
            Mathf.Max(
                size.x,
                size.y,
                size.z);

        return volume +
               longestSide * longestSide;
    }


    /// <summary>
    /// 오브젝트 하나가 아래에서 올라오며
    /// 60% → 103% → 100% 크기로 정착합니다.
    /// </summary>
    private IEnumerator RevealObjectRoutine(
        Transform target)
    {
        if (target == null)
        {
            yield break;
        }

        CaptureTransformState(target);

        TransformState state =
            originalStates[target];

        Vector3 originalPosition =
            state.LocalPosition;

        Vector3 originalScale =
            state.LocalScale;

        Vector3 startPosition =
            originalPosition +
            Vector3.down *
            startYOffset;

        Vector3 startScale =
            originalScale *
            startScaleMultiplier;

        Vector3 overshootScale =
            originalScale *
            overshootScaleMultiplier;


        target.localPosition =
            startPosition;

        target.localScale =
            startScale;

        target.gameObject.SetActive(true);


        float duration =
            Mathf.Max(
                0.01f,
                objectRevealDuration);

        float riseDuration =
            duration * 0.8f;

        float settleDuration =
            duration * 0.2f;

        float elapsed = 0f;


        /*
         * 아래에서 올라오면서 103%까지 커집니다.
         */
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                riseDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed /
                        riseDuration);

            float eased =
                1f -
                Mathf.Pow(
                    1f - normalized,
                    3f);

            target.localPosition =
                Vector3.Lerp(
                    startPosition,
                    originalPosition,
                    eased);

            target.localScale =
                Vector3.Lerp(
                    startScale,
                    overshootScale,
                    eased);

            yield return null;
        }


        target.localPosition =
            originalPosition;

        target.localScale =
            overshootScale;

        elapsed = 0f;


        /*
         * 103%에서 원래 크기인 100%로 정착합니다.
         */
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                settleDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed /
                        settleDuration);

            target.localScale =
                Vector3.Lerp(
                    overshootScale,
                    originalScale,
                    normalized);

            yield return null;
        }


        target.localPosition =
            originalPosition;

        target.localScale =
            originalScale;
    }


    private void EnsureFinalDreamlandRootActive()
    {
        if (finalDreamlandRoot != null &&
            !finalDreamlandRoot.activeSelf)
        {
            finalDreamlandRoot.SetActive(true);
        }
    }


    private void ShowGroupImmediately(
        Transform groupRoot)
    {
        if (groupRoot == null)
        {
            return;
        }

        RestoreOriginalState(groupRoot);
        groupRoot.gameObject.SetActive(true);

        for (int i = 0;
             i < groupRoot.childCount;
             i++)
        {
            Transform child =
                groupRoot.GetChild(i);

            RestoreOriginalState(child);

            if (child != null)
            {
                child.gameObject.SetActive(true);
            }
        }
    }


    private void SetGroupActive(
        Transform groupRoot,
        bool active)
    {
        if (groupRoot == null)
        {
            return;
        }

        groupRoot.gameObject.SetActive(
            active);
    }


    private void RestoreAllOriginalStates()
    {
        foreach (
            KeyValuePair<Transform, TransformState> pair
            in originalStates)
        {
            Transform target =
                pair.Key;

            TransformState state =
                pair.Value;

            if (target == null ||
                state == null)
            {
                continue;
            }

            target.localPosition =
                state.LocalPosition;

            target.localScale =
                state.LocalScale;
        }
    }


    private void RestoreOriginalState(
        Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (!originalStates.TryGetValue(
                target,
                out TransformState state))
        {
            CaptureTransformState(target);

            if (!originalStates.TryGetValue(
                    target,
                    out state))
            {
                return;
            }
        }

        target.localPosition =
            state.LocalPosition;

        target.localScale =
            state.LocalScale;
    }


    private void MarkAllGroupsRevealed()
    {
        AddRevealedGroup(part1);
        AddRevealedGroup(part2);
        AddRevealedGroup(part3);
        AddRevealedGroup(part4);

        AddRevealedGroup(fence);
        AddRevealedGroup(finalProps);
        AddRevealedGroup(treeBorder);
        AddRevealedGroup(castleScene);
        AddRevealedGroup(dreamBackground);
    }


    private void AddRevealedGroup(
        Transform groupRoot)
    {
        if (groupRoot != null)
        {
            revealedGroups.Add(
                groupRoot);
        }
    }


    private void RestartSequence(
        IEnumerator newSequence)
    {
        StopCurrentSequence();

        sequenceRoutine =
            StartCoroutine(
                newSequence);
    }


    private void StopCurrentSequence()
    {
        /*
         * 개별 오브젝트 연출 코루틴까지 모두 중지합니다.
         */
        StopAllCoroutines();

        sequenceRoutine = null;
        groupRevealRoutines.Clear();
        revealingGroups.Clear();
    }


    private void LogMissingGroup(
        string groupName)
    {
        Debug.LogWarning(
            $"[DreamWorldReveal] {groupName}이 연결되지 않았습니다.",
            this);
    }


    private static GameObject FindSceneObject(
        string objectName)
    {
        Transform transform =
            FindSceneTransform(
                objectName);

        return transform != null
            ? transform.gameObject
            : null;
    }


    private static Transform FindSceneTransform(
        string objectName)
    {
        Transform[] transforms =
            UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null &&
                candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }


    private void OnDisable()
    {
        StopCurrentSequence();
    }


    private void OnValidate()
    {
        startYOffset =
            Mathf.Max(
                0f,
                startYOffset);

        startScaleMultiplier =
            Mathf.Clamp(
                startScaleMultiplier,
                0.01f,
                1f);

        overshootScaleMultiplier =
            Mathf.Max(
                1f,
                overshootScaleMultiplier);

        objectRevealDuration =
            Mathf.Max(
                0.01f,
                objectRevealDuration);

        delayBetweenObjects =
            Mathf.Max(
                0f,
                delayBetweenObjects);

        delayBetweenGroups =
            Mathf.Max(
                0f,
                delayBetweenGroups);
    }
}
