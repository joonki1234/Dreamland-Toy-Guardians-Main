using System;
using System.Collections;
using DreamGuardians;
using UnityEngine;

/// <summary>
/// Stage 2의 UI와 마무리 연출을 담당합니다.
///
/// Stage2WaveController의 전투 진행 이벤트를 받아 시작/웨이브/완료 UI를 표시하고,
/// 전투 완료 연출까지 끝난 뒤 최종 Stage2Completed 이벤트를 발생시킵니다.
/// 전체 진행 컨트롤러는 Stage2WaveController.CombatCompleted가 아니라
/// 이 컴포넌트의 Stage2Completed만 구독합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class Stage2Director : MonoBehaviour
{
    public enum Stage2DirectorState
    {
        Idle,
        Running,
        Completing,
        Completed,
        Failed
    }

    [Header("References")]
    [SerializeField]
    private Stage2WaveController waveController;

    [SerializeField]
    private MissionBannerUI missionUI;

    [Header("Stage 2 시작 UI")]
    [SerializeField]
    private string stageStartTitle = "STAGE 2 START";

    [SerializeField]
    private string stageStartSubtitle = "확장되는 꿈의 균열을 막아라";

    [SerializeField]
    private string stageObjective =
        "Stage 2 · 코어를 지키며 모든 악몽을 정화하라";

    [Min(0.1f)]
    [SerializeField]
    private float stageStartBannerDuration = 2f;

    [Header("웨이브 UI")]
    [SerializeField]
    private string firstWaveTitle = "1차 침식";

    [SerializeField]
    private string secondWaveTitle = "2차 침식";

    [SerializeField]
    private string finalWaveTitle = "FINAL ATTACK";

    [SerializeField]
    private string finalPhaseTitle = "FINAL PHASE";

    [SerializeField]
    private string finalPhaseSubtitle = "남은 악몽을 모두 정화하라";

    [Min(0.1f)]
    [SerializeField]
    private float waveBannerDuration = 1.5f;

    [Min(0.05f)]
    [SerializeField]
    private float progressRefreshInterval = 0.2f;

    [Header("Stage 2 완료 UI")]
    [SerializeField]
    private string clearTitle = "STAGE 2 CLEAR";

    [SerializeField]
    private string clearSubtitle = "꿈의 균열이 약해졌습니다";

    [SerializeField]
    private string clearSpeaker = "장난감 친구";

    [TextArea(2, 4)]
    [SerializeField]
    private string clearMessage =
        "해냈어! 남은 꿈빛이 하나로 모이며 새로운 길을 열고 있어!";

    [Min(0.1f)]
    [SerializeField]
    private float clearBannerDuration = 3f;

    [Min(0.1f)]
    [SerializeField]
    private float clearDialogueDuration = 3f;

    [Header("실패 UI")]
    [SerializeField]
    private string failedTitle = "MISSION FAILED";

    [SerializeField]
    private string failedSubtitle = "꿈빛 코어가 무너졌습니다";

    [Min(0.1f)]
    [SerializeField]
    private float failedBannerDuration = 3f;

    [Header("현재 상태")]
    [SerializeField]
    private Stage2DirectorState currentState = Stage2DirectorState.Idle;

    private Coroutine progressRoutine;
    private Coroutine completionRoutine;
    private bool allSpawnsCompleted;
    private bool completionEventRaised;
    private string currentPhaseLabel = "Stage 2 준비";

    public Stage2DirectorState CurrentState => currentState;

    /// <summary>
    /// Stage 2 전투와 완료 UI/대사까지 모두 끝났을 때 발생합니다.
    /// </summary>
    public event Action Stage2Completed;

    /// <summary>
    /// Stage 2 진행 중 코어가 파괴됐을 때 발생합니다.
    /// </summary>
    public event Action Stage2Failed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopOwnedRoutines();
    }

    private void ResolveReferences()
    {
        if (waveController == null)
        {
            waveController =
                UnityEngine.Object.FindAnyObjectByType<Stage2WaveController>();
        }

        if (missionUI == null)
        {
            missionUI =
                UnityEngine.Object.FindAnyObjectByType<MissionBannerUI>();
        }
    }

    private void SubscribeEvents()
    {
        if (waveController == null)
        {
            Debug.LogWarning(
                "[Stage2Director] Stage2WaveController를 찾지 못했습니다. " +
                "Inspector 참조를 확인하세요.",
                this);
            return;
        }

        waveController.Stage2Started -= HandleStage2Started;
        waveController.Stage2Started += HandleStage2Started;

        waveController.WaveStarted -= HandleWaveStarted;
        waveController.WaveStarted += HandleWaveStarted;

        waveController.AllWaveSpawnsCompleted -= HandleAllWaveSpawnsCompleted;
        waveController.AllWaveSpawnsCompleted += HandleAllWaveSpawnsCompleted;

        waveController.CombatCompleted -= HandleCombatCompleted;
        waveController.CombatCompleted += HandleCombatCompleted;

        waveController.Failed -= HandleFailed;
        waveController.Failed += HandleFailed;
    }

    private void UnsubscribeEvents()
    {
        if (waveController == null)
        {
            return;
        }

        waveController.Stage2Started -= HandleStage2Started;
        waveController.WaveStarted -= HandleWaveStarted;
        waveController.AllWaveSpawnsCompleted -= HandleAllWaveSpawnsCompleted;
        waveController.CombatCompleted -= HandleCombatCompleted;
        waveController.Failed -= HandleFailed;
    }

    private void HandleStage2Started()
    {
        StopOwnedRoutines();

        currentState = Stage2DirectorState.Running;
        allSpawnsCompleted = false;
        completionEventRaised = false;
        currentPhaseLabel = "Stage 2 준비";

        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            stageStartTitle,
            stageStartSubtitle,
            stageStartBannerDuration);
        missionUI?.SetObjective(stageObjective);
        missionUI?.SetProgress("Stage 2 전투 준비");

        progressRoutine = StartCoroutine(UpdateProgressRoutine());

        Debug.Log("[Stage2Director] Stage 2 시작 UI를 표시합니다.", this);
    }

    private void HandleWaveStarted(
        Stage2WaveController.Stage2WavePhase phase,
        int enemyCount)
    {
        if (currentState != Stage2DirectorState.Running)
        {
            return;
        }

        string title;

        switch (phase)
        {
            case Stage2WaveController.Stage2WavePhase.First:
                title = firstWaveTitle;
                currentPhaseLabel = "첫 번째 공격";
                break;

            case Stage2WaveController.Stage2WavePhase.Second:
                title = secondWaveTitle;
                currentPhaseLabel = "두 번째 공격";
                break;

            case Stage2WaveController.Stage2WavePhase.Final:
                title = finalWaveTitle;
                currentPhaseLabel = "최종 공격";
                break;

            default:
                title = "WAVE";
                currentPhaseLabel = "공격 진행";
                break;
        }

        missionUI?.ShowBanner(
            title,
            "악몽 " + enemyCount + "마리 출현",
            waveBannerDuration);

        RefreshProgressText();
    }

    private void HandleAllWaveSpawnsCompleted()
    {
        if (currentState != Stage2DirectorState.Running)
        {
            return;
        }

        allSpawnsCompleted = true;
        currentPhaseLabel = "최종 정화";

        missionUI?.ShowBanner(
            finalPhaseTitle,
            finalPhaseSubtitle,
            waveBannerDuration);

        RefreshProgressText();
    }

    private void HandleCombatCompleted()
    {
        if (currentState != Stage2DirectorState.Running)
        {
            return;
        }

        if (completionRoutine != null || completionEventRaised)
        {
            return;
        }

        completionRoutine = StartCoroutine(CompleteStage2Routine());
    }

    private IEnumerator CompleteStage2Routine()
    {
        currentState = Stage2DirectorState.Completing;

        if (progressRoutine != null)
        {
            StopCoroutine(progressRoutine);
            progressRoutine = null;
        }

        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            clearTitle,
            clearSubtitle,
            clearBannerDuration);

        if (!string.IsNullOrWhiteSpace(clearMessage))
        {
            missionUI?.ShowDialogue(
                clearSpeaker,
                clearMessage,
                clearDialogueDuration);
        }

        float completionDuration = Mathf.Max(
            clearBannerDuration,
            string.IsNullOrWhiteSpace(clearMessage)
                ? 0f
                : clearDialogueDuration);

        if (completionDuration > 0f)
        {
            yield return new WaitForSeconds(completionDuration);
        }

        completionRoutine = null;

        if (completionEventRaised)
        {
            yield break;
        }

        completionEventRaised = true;
        currentState = Stage2DirectorState.Completed;

        Debug.Log(
            "[Stage2Director] Stage 2 마무리 연출이 끝나 " +
            "Stage2Completed 이벤트를 발생시킵니다.",
            this);

        Stage2Completed?.Invoke();
    }

    private void HandleFailed()
    {
        if (currentState == Stage2DirectorState.Completed ||
            currentState == Stage2DirectorState.Failed)
        {
            return;
        }

        StopOwnedRoutines();

        currentState = Stage2DirectorState.Failed;
        missionUI?.ClearPersistentText();
        missionUI?.ShowBanner(
            failedTitle,
            failedSubtitle,
            failedBannerDuration);

        Debug.Log(
            "[Stage2Director] Stage 2 실패 UI를 표시하고 " +
            "Stage2Failed 이벤트를 발생시킵니다.",
            this);

        Stage2Failed?.Invoke();
    }

    private IEnumerator UpdateProgressRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(
            Mathf.Max(0.05f, progressRefreshInterval));

        while (currentState == Stage2DirectorState.Running)
        {
            RefreshProgressText();
            yield return wait;
        }

        progressRoutine = null;
    }

    private void RefreshProgressText()
    {
        if (missionUI == null || waveController == null)
        {
            return;
        }

        int activeEnemyCount = waveController.ActiveEnemyCount;

        if (allSpawnsCompleted)
        {
            missionUI.SetProgress(
                "남은 악몽 " + activeEnemyCount + "마리");
            return;
        }

        missionUI.SetProgress(
            currentPhaseLabel +
            " · 전장 악몽 " + activeEnemyCount + "마리");
    }

    private void StopOwnedRoutines()
    {
        if (progressRoutine != null)
        {
            StopCoroutine(progressRoutine);
            progressRoutine = null;
        }

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }
    }

    private void OnValidate()
    {
        stageStartBannerDuration = Mathf.Max(0.1f, stageStartBannerDuration);
        waveBannerDuration = Mathf.Max(0.1f, waveBannerDuration);
        progressRefreshInterval = Mathf.Max(0.05f, progressRefreshInterval);
        clearBannerDuration = Mathf.Max(0.1f, clearBannerDuration);
        clearDialogueDuration = Mathf.Max(0.1f, clearDialogueDuration);
        failedBannerDuration = Mathf.Max(0.1f, failedBannerDuration);
    }
}
