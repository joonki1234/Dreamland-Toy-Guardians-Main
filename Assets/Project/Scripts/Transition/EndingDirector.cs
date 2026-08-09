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

    [Tooltip("보스 정화 이후 32~35번 엔딩 대사를 직접 말할 3D 장난감 친구")]
    [SerializeField]
    private ToyFriendController toyFriend;

    [SerializeField, Min(0f)]
    private float toyFriendStoryTransitionDuration = 0.35f;

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
        "너희 덕분에 장난감들도 다시 원래 모습으로 돌아갈 수 있을 거야. 정말 고마워!";

    [TextArea(2, 4)]
    [SerializeField]
    private string secondMessage =
        "이제 너희도 현실로 돌아갈 시간이야.";

    [TextArea(2, 4)]
    [SerializeField]
    private string thirdMessage =
        "현실로 돌아가더라도, 각자의 꿈을 지키기 위해 계속 노력해 줘.";

    [TextArea(2, 4)]
    [SerializeField]
    private string fourthMessage =
        "꿈을 포기하지 않는다면, 언젠가 그 꿈에 꼭 닿을 수 있을 거야!";

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
    private float secondDialogueDuration = 2.8f;

    [Min(0f)]
    [SerializeField]
    private float thirdDialogueDuration = 3.8f;

    [Min(0f)]
    [SerializeField]
    private float fourthDialogueDuration = 3.8f;

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

    public void AbortAndResetForTest()
    {
        StopEndingRoutine();
        completionEventRaised = false;
        currentState = EndingState.Idle;
        missionUI?.ClearPersistentText();
        missionUI?.SetObjective(string.Empty);
        missionUI?.SetProgress(string.Empty);
    }

    private void Awake()
    {
        ApplyStoryDialogueRevision();
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

        if (toyFriend == null)
        {
            toyFriend =
                UnityEngine.Object.FindAnyObjectByType<ToyFriendController>();
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

        if (toyFriend != null)
        {
            yield return toyFriend.ShowForStory(
                toyFriendStoryTransitionDuration);
        }

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
            float duration = Mathf.Max(0.1f, firstDialogueDuration);
            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(firstMessage, duration, true);
            }
            else
            {
                missionUI?.ShowDialogue(
                    speaker,
                    firstMessage,
                    duration);
            }

            if (firstDialogueDuration > 0f)
            {
                yield return new WaitForSeconds(firstDialogueDuration);
            }
        }

        if (!string.IsNullOrWhiteSpace(secondMessage))
        {
            float duration = Mathf.Max(0.1f, secondDialogueDuration);
            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(secondMessage, duration, false);
            }
            else
            {
                missionUI?.ShowDialogue(
                    speaker,
                    secondMessage,
                    duration);
            }

            if (secondDialogueDuration > 0f)
            {
                yield return new WaitForSeconds(secondDialogueDuration);
            }
        }

        if (!string.IsNullOrWhiteSpace(thirdMessage))
        {
            float duration = Mathf.Max(0.1f, thirdDialogueDuration);
            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(thirdMessage, duration, false);
            }
            else
            {
                missionUI?.ShowDialogue(
                    speaker,
                    thirdMessage,
                    duration);
            }

            if (thirdDialogueDuration > 0f)
            {
                yield return new WaitForSeconds(thirdDialogueDuration);
            }
        }

        if (!string.IsNullOrWhiteSpace(fourthMessage))
        {
            float duration = Mathf.Max(0.1f, fourthDialogueDuration);
            missionUI?.HideTransientMessages();
            if (toyFriend != null)
            {
                toyFriend.Speak(fourthMessage, duration, true);
            }
            else
            {
                missionUI?.ShowDialogue(
                    speaker,
                    fourthMessage,
                    duration);
            }

            if (fourthDialogueDuration > 0f)
            {
                yield return new WaitForSeconds(fourthDialogueDuration);
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

    private void ApplyStoryDialogueRevision()
    {
        firstMessage =
            "너희 덕분에 장난감들도 다시 원래 모습으로 돌아갈 수 있을 거야. 정말 고마워!";
        secondMessage = "이제 너희도 현실로 돌아갈 시간이야.";
        thirdMessage =
            "현실로 돌아가더라도, 각자의 꿈을 지키기 위해 계속 노력해 줘.";
        fourthMessage =
            "꿈을 포기하지 않는다면, 언젠가 그 꿈에 꼭 닿을 수 있을 거야!";
    }

    private void OnValidate()
    {
        openingBannerDuration = Mathf.Max(0f, openingBannerDuration);
        toyFriendStoryTransitionDuration =
            Mathf.Max(0f, toyFriendStoryTransitionDuration);
        firstDialogueDuration = Mathf.Max(0f, firstDialogueDuration);
        secondDialogueDuration = Mathf.Max(0f, secondDialogueDuration);
        thirdDialogueDuration = Mathf.Max(0f, thirdDialogueDuration);
        fourthDialogueDuration = Mathf.Max(0f, fourthDialogueDuration);
        finalBannerDuration = Mathf.Max(0f, finalBannerDuration);
    }
}
