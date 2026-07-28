using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 아군 포탈과 코어의 최초 등장 연출을 담당합니다.
///
/// 진행 순서:
/// 1. 아군 포탈이 아래에서 올라옵니다.
/// 2. 잠시 기다립니다.
/// 3. 코어가 포탈 안에서 위로 올라옵니다.
/// 4. 코어 등장 완료 후 Road_0을 등장시킵니다.
/// 5. 모든 등장 연출 완료 신호를 보냅니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AllyPortalCoreRevealController : MonoBehaviour
{
    [Header("References")]

    [Tooltip("02_GAMEPLAY 아래의 AllyPortal 오브젝트를 연결합니다.")]
    [SerializeField]
    private Transform allyPortal;

    [Tooltip("02_GAMEPLAY 아래의 Core 오브젝트를 연결합니다.")]
    [SerializeField]
    private Transform core;

    [Tooltip("DreamRoadRevealController 오브젝트를 연결합니다.")]
    [SerializeField]
    private DreamRoadRevealController roadRevealController;


    [Header("Portal Reveal")]

    [Tooltip("포탈이 원래 위치보다 얼마나 아래에서 시작할지 설정합니다.")]
    [Min(0f)]
    [SerializeField]
    private float portalStartYOffset = 0.6f;

    [Tooltip("포탈의 시작 크기입니다. 0.6은 원래 크기의 60%입니다.")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float portalStartScaleMultiplier = 0.6f;

    [Tooltip("포탈이 등장하는 데 걸리는 시간입니다.")]
    [Min(0.01f)]
    [SerializeField]
    private float portalRevealDuration = 0.7f;


    [Header("Core Reveal")]

    [Tooltip("코어가 원래 위치보다 얼마나 아래에서 시작할지 설정합니다.")]
    [Min(0f)]
    [SerializeField]
    private float coreStartYOffset = 2f;

    [Tooltip("코어의 시작 크기입니다. 0.4는 원래 크기의 40%입니다.")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float coreStartScaleMultiplier = 0.4f;

    [Tooltip("코어가 올라오면서 순간적으로 커지는 크기입니다.")]
    [Min(1f)]
    [SerializeField]
    private float coreOvershootScaleMultiplier = 1.08f;

    [Tooltip("코어가 등장하는 데 걸리는 시간입니다.")]
    [Min(0.01f)]
    [SerializeField]
    private float coreRevealDuration = 1.2f;


    [Header("Timing")]

    [Tooltip("포탈 등장 완료 후 코어가 나오기 전까지의 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField]
    private float delayBeforeCore = 0.25f;

    [Tooltip("코어 등장 완료 후 Road_0이 나오기 전까지의 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField]
    private float delayBeforeRoad0 = 0.2f;

    [Tooltip(
        "Road_0 실행 후 전체 등장 완료로 처리하기 전까지 기다리는 시간입니다. " +
        "Road_0 벽돌 등장 시간이 0.5초라면 0.55초 정도가 적당합니다.")]
    [Min(0f)]
    [SerializeField]
    private float road0CompletionHold = 0.55f;


    [Header("Start State")]

    [Tooltip("게임 시작 시 포탈과 코어를 자동으로 숨깁니다.")]
    [SerializeField]
    private bool hideOnStart = true;

    [Tooltip(
        "게임 시작과 동시에 등장 연출을 자동 실행합니다. " +
        "TutorialStage1Director와 연결할 때는 체크하지 않습니다.")]
    [SerializeField]
    private bool playAutomaticallyOnStart = false;


    private Vector3 portalOriginalLocalPosition;
    private Vector3 portalOriginalLocalScale;

    private Vector3 coreOriginalLocalPosition;
    private Vector3 coreOriginalLocalScale;

    private Coroutine revealRoutine;
    private bool originalTransformCaptured;


    /// <summary>
    /// 현재 등장 연출이 진행 중인지 반환합니다.
    /// TutorialStage1Director가 이 값을 확인하여
    /// 연출이 끝날 때까지 기다립니다.
    /// </summary>
    public bool IsRevealing =>
        revealRoutine != null;


    /// <summary>
    /// 포탈, 코어, Road_0 등장 연출이
    /// 정상적으로 완료됐는지 반환합니다.
    /// </summary>
    public bool HasCompleted { get; private set; }


    /// <summary>
    /// 포탈, 코어, Road_0 등장 연출이
    /// 모두 끝났을 때 발생합니다.
    /// </summary>
    public event Action RevealCompleted;


    private void Awake()
    {
        ResolveReferences();
        CaptureOriginalTransforms();
    }


    private void Start()
    {
        if (hideOnStart)
        {
            HideImmediately();
        }

        if (playAutomaticallyOnStart)
        {
            PlayReveal();
        }
    }


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
    /// 포탈과 코어의 원래 위치 및 크기를 저장합니다.
    /// </summary>
    private void CaptureOriginalTransforms()
    {
        if (originalTransformCaptured)
        {
            return;
        }

        if (allyPortal != null)
        {
            portalOriginalLocalPosition =
                allyPortal.localPosition;

            portalOriginalLocalScale =
                allyPortal.localScale;
        }

        if (core != null)
        {
            coreOriginalLocalPosition =
                core.localPosition;

            coreOriginalLocalScale =
                core.localScale;
        }

        originalTransformCaptured = true;
    }


    /// <summary>
    /// 포탈과 코어를 즉시 숨기고
    /// 등장 완료 상태를 초기화합니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈과 코어 숨기기")]
    public void HideImmediately()
    {
        StopCurrentReveal();

        CaptureOriginalTransforms();
        RestoreOriginalTransforms();

        HasCompleted = false;

        if (allyPortal != null)
        {
            allyPortal.gameObject.SetActive(false);
        }

        if (core != null)
        {
            core.gameObject.SetActive(false);
        }

        Debug.Log(
            "[AllyPortalCoreReveal] 포탈과 코어를 숨겼습니다.",
            this);
    }


    /// <summary>
    /// 포탈과 코어를 애니메이션 없이 즉시 표시합니다.
    ///
    /// 이 메서드는 배치 확인용이며,
    /// Road_0은 별도로 표시하지 않습니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈과 코어 즉시 표시")]
    public void ShowImmediately()
    {
        StopCurrentReveal();

        CaptureOriginalTransforms();
        RestoreOriginalTransforms();

        if (allyPortal != null)
        {
            allyPortal.gameObject.SetActive(true);
        }

        if (core != null)
        {
            core.gameObject.SetActive(true);
        }

        HasCompleted = true;

        Debug.Log(
            "[AllyPortalCoreReveal] 포탈과 코어를 즉시 표시했습니다.",
            this);
    }


    /// <summary>
    /// 포탈 → 코어 → Road_0 순서의
    /// 등장 연출을 실행합니다.
    /// </summary>
    [ContextMenu("테스트 - 포탈과 코어 등장")]
    public void PlayReveal()
    {
        if (allyPortal == null ||
            core == null)
        {
            Debug.LogWarning(
                "[AllyPortalCoreReveal] " +
                "AllyPortal 또는 Core가 연결되지 않았습니다.",
                this);

            return;
        }

        /*
         * 이미 연출 중이라면 중복 실행하지 않습니다.
         */
        if (revealRoutine != null)
        {
            Debug.LogWarning(
                "[AllyPortalCoreReveal] " +
                "등장 연출이 이미 진행 중입니다.",
                this);

            return;
        }

        /*
         * 이미 완료된 상태라면 또 실행하지 않습니다.
         */
        if (HasCompleted)
        {
            Debug.Log(
                "[AllyPortalCoreReveal] " +
                "등장 연출이 이미 완료되어 중복 실행을 무시합니다.",
                this);

            return;
        }

        CaptureOriginalTransforms();
        RestoreOriginalTransforms();

        HasCompleted = false;

        revealRoutine =
            StartCoroutine(
                RevealSequenceRoutine());
    }


    private IEnumerator RevealSequenceRoutine()
    {
        /*
         * 연출 시작 전에 기존 상태를 숨깁니다.
         */
        allyPortal.gameObject.SetActive(false);
        core.gameObject.SetActive(false);


        /*
         * 1단계: 아군 포탈 등장
         */
        yield return RevealPortalRoutine();


        /*
         * 2단계: 코어 등장 전 잠깐 대기
         */
        if (delayBeforeCore > 0f)
        {
            yield return new WaitForSeconds(
                delayBeforeCore);
        }


        /*
         * 3단계: 코어 등장
         */
        yield return RevealCoreRoutine();


        /*
         * 4단계: Road_0 등장 전 잠깐 대기
         */
        if (delayBeforeRoad0 > 0f)
        {
            yield return new WaitForSeconds(
                delayBeforeRoad0);
        }


        /*
         * 5단계: 중앙 길 Road_0 등장
         */
        if (roadRevealController != null)
        {
            roadRevealController.RevealRoad0();
        }
        else
        {
            Debug.LogWarning(
                "[AllyPortalCoreReveal] " +
                "Road Reveal Controller가 없어 " +
                "Road_0을 등장시키지 못했습니다.",
                this);
        }


        /*
         * Road_0이 아래에서 올라오는 연출이
         * 끝날 때까지 기다립니다.
         */
        if (road0CompletionHold > 0f)
        {
            yield return new WaitForSeconds(
                road0CompletionHold);
        }


        revealRoutine = null;
        HasCompleted = true;

        Debug.Log(
            "[AllyPortalCoreReveal] " +
            "아군 포탈, 코어, Road_0 등장 완료.",
            this);

        RevealCompleted?.Invoke();
    }


    private IEnumerator RevealPortalRoutine()
    {
        Vector3 startPosition =
            portalOriginalLocalPosition +
            Vector3.down *
            portalStartYOffset;

        Vector3 startScale =
            portalOriginalLocalScale *
            portalStartScaleMultiplier;

        allyPortal.localPosition =
            startPosition;

        allyPortal.localScale =
            startScale;

        allyPortal.gameObject.SetActive(true);

        float duration =
            Mathf.Max(
                0.01f,
                portalRevealDuration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                Mathf.Clamp01(
                    elapsed / duration);

            float eased =
                1f -
                Mathf.Pow(
                    1f - normalized,
                    3f);

            allyPortal.localPosition =
                Vector3.Lerp(
                    startPosition,
                    portalOriginalLocalPosition,
                    eased);

            allyPortal.localScale =
                Vector3.Lerp(
                    startScale,
                    portalOriginalLocalScale,
                    eased);

            yield return null;
        }

        allyPortal.localPosition =
            portalOriginalLocalPosition;

        allyPortal.localScale =
            portalOriginalLocalScale;
    }


    private IEnumerator RevealCoreRoutine()
    {
        Vector3 startPosition =
            coreOriginalLocalPosition +
            Vector3.down *
            coreStartYOffset;

        Vector3 startScale =
            coreOriginalLocalScale *
            coreStartScaleMultiplier;

        Vector3 overshootScale =
            coreOriginalLocalScale *
            coreOvershootScaleMultiplier;

        core.localPosition =
            startPosition;

        core.localScale =
            startScale;

        core.gameObject.SetActive(true);

        float duration =
            Mathf.Max(
                0.01f,
                coreRevealDuration);

        float riseDuration =
            duration * 0.8f;

        float settleDuration =
            duration * 0.2f;

        float elapsed = 0f;


        /*
         * 아래에서 올라오면서
         * 원래 크기보다 살짝 크게 확장됩니다.
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

            core.localPosition =
                Vector3.Lerp(
                    startPosition,
                    coreOriginalLocalPosition,
                    eased);

            core.localScale =
                Vector3.Lerp(
                    startScale,
                    overshootScale,
                    eased);

            yield return null;
        }

        core.localPosition =
            coreOriginalLocalPosition;

        core.localScale =
            overshootScale;

        elapsed = 0f;


        /*
         * 커졌던 코어가 원래 크기로 정착합니다.
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

            core.localScale =
                Vector3.Lerp(
                    overshootScale,
                    coreOriginalLocalScale,
                    normalized);

            yield return null;
        }

        core.localPosition =
            coreOriginalLocalPosition;

        core.localScale =
            coreOriginalLocalScale;
    }


    private void RestoreOriginalTransforms()
    {
        if (allyPortal != null)
        {
            allyPortal.localPosition =
                portalOriginalLocalPosition;

            allyPortal.localScale =
                portalOriginalLocalScale;
        }

        if (core != null)
        {
            core.localPosition =
                coreOriginalLocalPosition;

            core.localScale =
                coreOriginalLocalScale;
        }
    }


    private void StopCurrentReveal()
    {
        if (revealRoutine == null)
        {
            return;
        }

        StopCoroutine(
            revealRoutine);

        revealRoutine = null;
        HasCompleted = false;

        RestoreOriginalTransforms();
    }


    private void OnDisable()
    {
        StopCurrentReveal();
    }


    private void OnValidate()
    {
        portalStartYOffset =
            Mathf.Max(
                0f,
                portalStartYOffset);

        portalStartScaleMultiplier =
            Mathf.Clamp(
                portalStartScaleMultiplier,
                0.01f,
                1f);

        portalRevealDuration =
            Mathf.Max(
                0.01f,
                portalRevealDuration);

        coreStartYOffset =
            Mathf.Max(
                0f,
                coreStartYOffset);

        coreStartScaleMultiplier =
            Mathf.Clamp(
                coreStartScaleMultiplier,
                0.01f,
                1f);

        coreOvershootScaleMultiplier =
            Mathf.Max(
                1f,
                coreOvershootScaleMultiplier);

        coreRevealDuration =
            Mathf.Max(
                0.01f,
                coreRevealDuration);

        delayBeforeCore =
            Mathf.Max(
                0f,
                delayBeforeCore);

        delayBeforeRoad0 =
            Mathf.Max(
                0f,
                delayBeforeRoad0);

        road0CompletionHold =
            Mathf.Max(
                0f,
                road0CompletionHold);
    }
}