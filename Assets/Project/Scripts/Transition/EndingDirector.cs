using System;
using System.Collections;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// 최종 보스 처치 이후의 엔딩 UI와 대사를 담당하고,
/// 모든 엔딩 연출이 끝난 뒤 EndingCompleted 이벤트를 발생시킵니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingDirector : MonoBehaviour
{
    public enum EndingState
    {
        Idle,
        Running,
        Completed
    }

    [Header("References")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [SerializeField]
    private MissionBannerUI missionUI;

    [Header("Ending UI")]
    [SerializeField]
    private string endingTitle = "DREAM RESTORED";

    [SerializeField]
    private string endingSubtitle = "꿈나라에 다시 빛이 돌아왔습니다";

    [SerializeField]
    private string speaker = "장난감 친구";

    [TextArea(2, 4)]
    [SerializeField]
    private string firstMessage =
        "모두의 용기 덕분에 악몽이 사라졌어. 정말 고마워!";

    [TextArea(2, 4)]
    [SerializeField]
    private string secondMessage =
        "현실로 돌아가더라도 오늘의 꿈과 힘을 잊지 말아 줘.";

    [SerializeField]
    private string finalTitle = "THE END";

    [SerializeField]
    private string finalSubtitle = "Dream Guardians";

    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float openingBannerDuration = 3f;

    [Min(0f)]
    [SerializeField]
    private float firstDialogueDuration = 3f;

    [Min(0f)]
    [SerializeField]
    private float secondDialogueDuration = 3f;

    [Min(0f)]
    [SerializeField]
    private float finalBannerDuration = 4f;

    [Header("Runtime")]
    [SerializeField]
    private EndingState currentState = EndingState.Idle;

    private Coroutine endingRoutine;
    private bool completionEventRaised;

    public EndingState CurrentState => currentState;
    public event Action EndingCompleted;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
            gameFlowController.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }

        StopEndingRoutine();
    }

    private void ResolveReferences()
    {
        if (gameFlowController == null)
        {
            gameFlowController =
                UnityEngine.Object.FindAnyObjectByType<DreamlandGameFlowController>();
        }

        if (missionUI == null)
        {
            missionUI =
                UnityEngine.Object.FindAnyObjectByType<MissionBannerUI>();
        }
    }

    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        if (newState == DreamlandGameFlowController.GameFlowState.Ending)
        {
            BeginEnding();
        }
        else if (newState == DreamlandGameFlowController.GameFlowState.GameOver)
        {
            StopEndingRoutine();
        }
    }

    public void BeginEnding()
    {
        if (endingRoutine != null ||
            currentState == EndingState.Running)
        {
            return;
        }

        completionEventRaised = false;
        endingRoutine = StartCoroutine(EndingRoutine());
    }

    private IEnumerator EndingRoutine()
    {
        currentState = EndingState.Running;
        missionUI?.ClearPersistentText();

        missionUI?.ShowBanner(
            endingTitle,
            endingSubtitle,
            Mathf.Max(0.1f, openingBannerDuration));

        if (openingBannerDuration > 0f)
        {
            yield return new WaitForSeconds(openingBannerDuration);
        }

        if (!string.IsNullOrWhiteSpace(firstMessage))
        {
            missionUI?.ShowDialogue(
                speaker,
                firstMessage,
                Mathf.Max(0.1f, firstDialogueDuration));

            if (firstDialogueDuration > 0f)
            {
                yield return new WaitForSeconds(firstDialogueDuration);
            }
        }

        if (!string.IsNullOrWhiteSpace(secondMessage))
        {
            missionUI?.ShowDialogue(
                speaker,
                secondMessage,
                Mathf.Max(0.1f, secondDialogueDuration));

            if (secondDialogueDuration > 0f)
            {
                yield return new WaitForSeconds(secondDialogueDuration);
            }
        }

        missionUI?.ShowBanner(
            finalTitle,
            finalSubtitle,
            Mathf.Max(0.1f, finalBannerDuration));

        if (finalBannerDuration > 0f)
        {
            yield return new WaitForSeconds(finalBannerDuration);
        }

        endingRoutine = null;

        if (completionEventRaised)
        {
            yield break;
        }

        completionEventRaised = true;
        currentState = EndingState.Completed;

        Debug.Log(
            "[Ending] 엔딩 연출 완료. EndingCompleted 이벤트를 발생시킵니다.",
            this);

        EndingCompleted?.Invoke();
    }

    private void StopEndingRoutine()
    {
        if (endingRoutine == null)
        {
            return;
        }

        StopCoroutine(endingRoutine);
        endingRoutine = null;
    }

    private void OnValidate()
    {
        openingBannerDuration = Mathf.Max(0f, openingBannerDuration);
        firstDialogueDuration = Mathf.Max(0f, firstDialogueDuration);
        secondDialogueDuration = Mathf.Max(0f, secondDialogueDuration);
        finalBannerDuration = Mathf.Max(0f, finalBannerDuration);
    }
}
