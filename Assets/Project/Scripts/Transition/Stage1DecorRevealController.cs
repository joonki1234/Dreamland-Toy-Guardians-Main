using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DreamGuardians;

/// <summary>
/// Stage1 웨이브가 클리어될 때마다 길 주변 소품(나무/식물/장난감 등)이
/// 작은 것부터 순서대로 솟아오르며 등장하는 연출을 담당합니다.
///
/// DreamWorldRevealController(Stage2/보스전의 마을 침식 연출)와 같은 기법
/// (아래에서 솟아오르며 작게→살짝 크게→원래 크기로 정착)을 재사용하지만,
/// Stage2/보스전의 임팩트를 가리지 않도록 완전히 별도의 가벼운 컴포넌트로
/// 분리했습니다. 05_FINAL_DREAMLAND 루트나 DreamWorldRevealController의
/// 그룹(Part_1~4, fence 등)은 전혀 건드리지 않습니다.
///
/// wave1Decor: 1차 공격 클리어 시 등장
/// wave2Decor: 2차 공격 클리어 시 등장
/// finalDecor: 최종 공격(Stage1 전체) 클리어 시 등장
///
/// 각 그룹의 "직속 자식 오브젝트들"이 각각 하나의 소품으로 취급되어
/// 순서대로 등장합니다. 씬에서 나무/식물/장난감 소품 등을 미리 배치해
/// 이 그룹들 밑에 자식으로 넣어두세요(빈 그룹이면 아무 일도 일어나지
/// 않습니다).
/// </summary>
[DisallowMultipleComponent]
public sealed class Stage1DecorRevealController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Stage1WaveController stage1WaveController;


    [Header("웨이브별 소품 그룹 (직속 자식들이 각각 하나의 소품)")]

    [Tooltip("1차 공격 클리어 시 등장할 소품들의 부모입니다.")]
    [SerializeField]
    private Transform wave1Decor;

    [Tooltip("2차 공격 클리어 시 등장할 소품들의 부모입니다.")]
    [SerializeField]
    private Transform wave2Decor;

    [Tooltip("최종 공격(Stage1 클리어) 시 등장할 소품들의 부모입니다.")]
    [SerializeField]
    private Transform finalDecor;


    [Header("등장 애니메이션")]

    [Tooltip("소품이 원래 위치보다 얼마나 아래에서 시작할지입니다.")]
    [SerializeField, Min(0f)]
    private float startYOffset = 0.6f;

    [Tooltip("소품의 시작 크기입니다. 0.6은 원래 크기의 60%입니다.")]
    [SerializeField, Range(0.01f, 1f)]
    private float startScaleMultiplier = 0.6f;

    [Tooltip("올라오면서 순간적으로 도달하는 최대 크기입니다.")]
    [SerializeField, Min(1f)]
    private float overshootScaleMultiplier = 1.05f;

    [Tooltip("소품 하나가 올라와 정착하는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.01f)]
    private float objectRevealDuration = 0.6f;

    [Tooltip("다음 소품의 등장 연출을 시작할 때까지의 간격입니다.")]
    [SerializeField, Min(0f)]
    private float delayBetweenObjects = 0.12f;

    [Tooltip("게임 시작 시 세 그룹을 모두 숨깁니다.")]
    [SerializeField]
    private bool hideAllGroupsOnStart = true;


    private sealed class TransformState
    {
        public readonly Vector3 LocalPosition;
        public readonly Vector3 LocalScale;

        public TransformState(Vector3 localPosition, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
        }
    }

    private readonly Dictionary<Transform, TransformState> originalStates =
        new Dictionary<Transform, TransformState>();

    private readonly HashSet<Transform> revealedGroups =
        new HashSet<Transform>();


    private void Awake()
    {
        if (stage1WaveController == null)
        {
            stage1WaveController =
                FindAnyObjectByType<Stage1WaveController>();
        }

        CaptureGroupStates(wave1Decor);
        CaptureGroupStates(wave2Decor);
        CaptureGroupStates(finalDecor);

        if (hideAllGroupsOnStart)
        {
            HideAllGroupsImmediately();
        }
    }


    private void OnEnable()
    {
        if (stage1WaveController != null)
        {
            stage1WaveController.WaveGroupCompleted -=
                HandleWaveGroupCompleted;

            stage1WaveController.WaveGroupCompleted +=
                HandleWaveGroupCompleted;
        }
    }


    private void OnDisable()
    {
        if (stage1WaveController != null)
        {
            stage1WaveController.WaveGroupCompleted -=
                HandleWaveGroupCompleted;
        }
    }


    /// <summary>
    /// Stage1WaveController.WaveGroupCompleted(0=1차, 1=2차, 2=최종)에
    /// 맞춰 해당 소품 그룹을 등장시킵니다.
    /// </summary>
    private void HandleWaveGroupCompleted(int groupIndex)
    {
        Transform target = groupIndex switch
        {
            0 => wave1Decor,
            1 => wave2Decor,
            _ => finalDecor,
        };

        if (target != null)
        {
            StartCoroutine(RevealGroupRoutine(target));
        }
    }


    [ContextMenu("테스트 - 1차 공격 소품 등장")]
    public void RevealWave1Decor()
    {
        if (wave1Decor != null)
        {
            StartCoroutine(RevealGroupRoutine(wave1Decor));
        }
    }


    [ContextMenu("테스트 - 2차 공격 소품 등장")]
    public void RevealWave2Decor()
    {
        if (wave2Decor != null)
        {
            StartCoroutine(RevealGroupRoutine(wave2Decor));
        }
    }


    [ContextMenu("테스트 - 최종 공격 소품 등장")]
    public void RevealFinalDecor()
    {
        if (finalDecor != null)
        {
            StartCoroutine(RevealGroupRoutine(finalDecor));
        }
    }


    [ContextMenu("테스트 - 모든 소품 즉시 숨기기")]
    public void HideAllGroupsImmediately()
    {
        HideGroupImmediately(wave1Decor);
        HideGroupImmediately(wave2Decor);
        HideGroupImmediately(finalDecor);

        revealedGroups.Clear();
    }


    private void CaptureGroupStates(Transform groupRoot)
    {
        if (groupRoot == null)
        {
            return;
        }

        for (int i = 0; i < groupRoot.childCount; i++)
        {
            Transform child = groupRoot.GetChild(i);

            if (child == null)
            {
                continue;
            }

            originalStates[child] =
                new TransformState(child.localPosition, child.localScale);
        }
    }


    private static void HideGroupImmediately(Transform groupRoot)
    {
        if (groupRoot == null)
        {
            return;
        }

        for (int i = 0; i < groupRoot.childCount; i++)
        {
            Transform child = groupRoot.GetChild(i);

            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }


    private IEnumerator RevealGroupRoutine(Transform groupRoot)
    {
        if (groupRoot == null || revealedGroups.Contains(groupRoot))
        {
            yield break;
        }

        List<Transform> targets = new List<Transform>();

        for (int i = 0; i < groupRoot.childCount; i++)
        {
            Transform child = groupRoot.GetChild(i);

            if (child != null)
            {
                targets.Add(child);
            }
        }

        if (targets.Count == 0)
        {
            yield break;
        }

        // 작은 소품부터 큰 소품 순서로 등장시킵니다.
        targets.Sort(
            (a, b) => CalculateVisualSize(a).CompareTo(CalculateVisualSize(b)));

        foreach (Transform target in targets)
        {
            StartCoroutine(RevealObjectRoutine(target));

            if (delayBetweenObjects > 0f)
            {
                yield return new WaitForSeconds(delayBetweenObjects);
            }
        }

        if (objectRevealDuration > 0f)
        {
            yield return new WaitForSeconds(objectRevealDuration);
        }

        revealedGroups.Add(groupRoot);

        Debug.Log(
            $"[Stage1DecorReveal] {groupRoot.name} 소품 등장 완료. " +
            $"개수: {targets.Count}",
            this);
    }


    private static float CalculateVisualSize(Transform target)
    {
        if (target == null)
        {
            return 0f;
        }

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            Vector3 scale = target.lossyScale;
            return Mathf.Abs(scale.x * scale.y * scale.z);
        }

        bool boundsCreated = false;
        Bounds combinedBounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!boundsCreated)
            {
                combinedBounds = renderer.bounds;
                boundsCreated = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!boundsCreated)
        {
            return 0f;
        }

        Vector3 size = combinedBounds.size;

        float volume = Mathf.Abs(size.x * size.y * size.z);
        float longestSide = Mathf.Max(size.x, size.y, size.z);

        return volume + longestSide * longestSide;
    }


    /// <summary>
    /// 소품 하나가 아래에서 올라오며 지정한 배율로 정착합니다.
    /// </summary>
    private IEnumerator RevealObjectRoutine(Transform target)
    {
        if (target == null)
        {
            yield break;
        }

        if (!originalStates.TryGetValue(target, out TransformState state))
        {
            state = new TransformState(target.localPosition, target.localScale);
            originalStates[target] = state;
        }

        Vector3 originalPosition = state.LocalPosition;
        Vector3 originalScale = state.LocalScale;

        Vector3 startPosition =
            originalPosition + Vector3.down * startYOffset;

        Vector3 startScale =
            originalScale * startScaleMultiplier;

        Vector3 overshootScale =
            originalScale * overshootScaleMultiplier;

        target.localPosition = startPosition;
        target.localScale = startScale;
        target.gameObject.SetActive(true);

        float duration = Mathf.Max(0.01f, objectRevealDuration);
        float riseDuration = duration * 0.8f;
        float settleDuration = duration * 0.2f;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            target.localPosition =
                Vector3.Lerp(startPosition, originalPosition, eased);

            target.localScale =
                Vector3.Lerp(startScale, overshootScale, eased);

            yield return null;
        }

        target.localPosition = originalPosition;
        target.localScale = overshootScale;

        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);

            target.localScale =
                Vector3.Lerp(overshootScale, originalScale, t);

            yield return null;
        }

        target.localPosition = originalPosition;
        target.localScale = originalScale;
    }


    private void OnValidate()
    {
        startYOffset = Mathf.Max(0f, startYOffset);
        startScaleMultiplier = Mathf.Clamp(startScaleMultiplier, 0.01f, 1f);
        overshootScaleMultiplier = Mathf.Max(1f, overshootScaleMultiplier);
        objectRevealDuration = Mathf.Max(0.01f, objectRevealDuration);
        delayBetweenObjects = Mathf.Max(0f, delayBetweenObjects);
    }
}
