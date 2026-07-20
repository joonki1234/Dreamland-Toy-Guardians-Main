using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class Stage1WaveController : MonoBehaviour
    {
        [Serializable]
        public sealed class WaveGroup
        {
            public string label = "공격";
            [Min(1)] public int enemyCount = 3;
            [Min(0f)] public float spawnInterval = 0.5f;
            [Min(0.1f)] public float healthMultiplier = 1f;
            [Min(0f)] public float preDelay = 1f;
            [Min(0f)] public float transitionDelay = 4f;
            [Min(0f)] public float minimumDuration;
        }

        [Header("References")]
        [SerializeField] private DreamEnemySpawner spawner;
        [SerializeField] private MissionBannerUI missionUI;
        [SerializeField] private CoreState core;
        [SerializeField] private TutorialDialogueData dialogueData;

        [Header("Wave 1")]
        [SerializeField] private List<WaveGroup> groups = new List<WaveGroup>();
        [SerializeField, Min(0f)] private float waveStartDelay = 3f;
        [SerializeField, Min(0f)] private float clearSequenceDuration = 3f;
        [SerializeField, Min(0f)] private float targetStageDurationSeconds = 240f;

        private Coroutine waveRoutine;
        private bool failed;

        public bool IsRunning => waveRoutine != null;
        public IReadOnlyList<WaveGroup> Groups => groups;
        public event Action Completed;
        public event Action Failed;

        private void Awake()
        {
            EnsureDefaultGroups();
        }

        private void OnEnable()
        {
            if (core != null)
            {
                core.CoreDestroyed += HandleCoreDestroyed;
            }
        }

        private void OnDisable()
        {
            if (core != null)
            {
                core.CoreDestroyed -= HandleCoreDestroyed;
            }
        }

        public void Configure(DreamEnemySpawner enemySpawner, MissionBannerUI ui, CoreState targetCore)
        {
            if (core != null)
            {
                core.CoreDestroyed -= HandleCoreDestroyed;
            }

            spawner = enemySpawner;
            missionUI = ui;
            core = targetCore;

            if (isActiveAndEnabled && core != null)
            {
                core.CoreDestroyed += HandleCoreDestroyed;
            }

            EnsureDefaultGroups();
        }

        public void SetDialogueData(TutorialDialogueData data)
        {
            dialogueData = data;
        }

        public void SetDefaultGroups()
        {
            groups = new List<WaveGroup>
            {
                new WaveGroup
                {
                    label = "1차 공격",
                    enemyCount = 3,
                    spawnInterval = 2.5f,
                    healthMultiplier = 1f,
                    preDelay = 2f,
                    transitionDelay = 10f,
                    minimumDuration = 0f
                },
                new WaveGroup
                {
                    label = "2차 공격",
                    enemyCount = 3,
                    spawnInterval = 2.5f,
                    healthMultiplier = 1.25f,
                    preDelay = 2f,
                    transitionDelay = 12f,
                    minimumDuration = 0f
                },
                new WaveGroup
                {
                    label = "최종 공격",
                    enemyCount = 6,
                    spawnInterval = 1.8f,
                    healthMultiplier = 1.5f,
                    preDelay = 3f,
                    transitionDelay = 0f,
                    minimumDuration = 0f
                }
            };
        }

        public void ApplyPrototypePacingV5()
        {
            waveStartDelay = 3f;
            clearSequenceDuration = 3f;
            targetStageDurationSeconds = 240f;
            SetDefaultGroups();
        }

        public void StartStage1()
        {
            if (waveRoutine != null)
            {
                return;
            }

            if (spawner == null)
            {
                Debug.LogError("[Dreamland] Stage 1을 시작할 수 없습니다. DreamEnemySpawner가 연결되지 않았습니다.");
                return;
            }

            failed = false;
            waveRoutine = StartCoroutine(RunWaveRoutine());
        }

        private IEnumerator RunWaveRoutine()
        {
            float stageStartedAt = Time.time;
            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.WaveStartTitle : "WAVE 1 START",
                dialogueData != null ? dialogueData.WaveStartSubtitle : "꿈빛 코어를 지켜라",
                2f);
            missionUI?.SetObjective(
                dialogueData != null ? dialogueData.WaveObjective : "Stage 1 · 코어 방어");
            yield return new WaitForSeconds(waveStartDelay);

            for (int index = 0; index < groups.Count; index++)
            {
                if (failed)
                {
                    yield break;
                }

                WaveGroup group = groups[index];
                float groupStartedAt = Time.time;

                missionUI?.ShowBanner(
                    group.label,
                    $"악몽 {group.enemyCount}마리",
                    1.5f);
                missionUI?.SetProgress($"공격 {index + 1} / {groups.Count}");

                if (group.preDelay > 0f)
                {
                    yield return new WaitForSeconds(group.preDelay);
                }

                yield return spawner.SpawnGroup(
                    group.enemyCount,
                    group.spawnInterval,
                    group.healthMultiplier);

                while (!failed && spawner.ActiveEnemyCount > 0)
                {
                    missionUI?.SetProgress(
                        $"공격 {index + 1} / {groups.Count}  ·  남은 악몽 {spawner.ActiveEnemyCount}");
                    yield return null;
                }

                if (failed)
                {
                    yield break;
                }

                float elapsed = Time.time - groupStartedAt;
                if (elapsed < group.minimumDuration)
                {
                    missionUI?.SetProgress("코어 주변의 꿈빛을 안정화하는 중...");
                    yield return new WaitForSeconds(group.minimumDuration - elapsed);
                }

                missionUI?.ShowBanner(group.label + " 정화 완료", string.Empty, 1.4f);

                if (index < groups.Count - 1)
                {
                    int environmentPhase = index + 1;
                    DreamGameEvents.RequestEnvironmentPhase(environmentPhase);

                    TutorialDialogueLine transitionLine = null;
                    if (dialogueData != null)
                    {
                        transitionLine = environmentPhase == 1
                            ? dialogueData.AfterFirstGroupLine
                            : dialogueData.BeforeFinalGroupLine;
                    }

                    if (transitionLine != null && !string.IsNullOrWhiteSpace(transitionLine.Message))
                    {
                        missionUI?.ShowDialogue(
                            transitionLine.Speaker,
                            transitionLine.Message,
                            transitionLine.Duration);
                    }
                    else
                    {
                        missionUI?.ShowDialogue(
                            "장난감 친구",
                            environmentPhase == 1
                                ? "주변의 꿈이 변하고 있어. 다음 공격을 준비해!"
                                : "균열이 더 커졌어. 마지막 공격이 올 거야!",
                            3f);
                    }

                    if (group.transitionDelay > 0f)
                    {
                        yield return new WaitForSeconds(group.transitionDelay);
                    }
                }
            }

            missionUI?.SetProgress(string.Empty);
            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.WaveClearTitle : "WAVE 1 CLEAR",
                dialogueData != null ? dialogueData.WaveClearSubtitle : "꿈빛 코어 방어 성공",
                clearSequenceDuration);

            float stageDuration = Time.time - stageStartedAt;
            Debug.Log(
                $"[Dreamland] Wave 1 완료: {stageDuration:0.0}초 " +
                $"(현재 목표 {targetStageDurationSeconds:0}초 - 실제 플레이 테스트 후 HP와 간격을 조절하세요.)");

            yield return new WaitForSeconds(clearSequenceDuration);
            waveRoutine = null;
            Completed?.Invoke();
        }

        private void HandleCoreDestroyed()
        {
            if (failed)
            {
                return;
            }

            failed = true;

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            missionUI?.SetProgress(string.Empty);
            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.MissionFailedTitle : "MISSION FAILED",
                dialogueData != null ? dialogueData.MissionFailedSubtitle : "꿈빛 코어가 무너졌습니다",
                3f);
            Failed?.Invoke();
        }

        private void EnsureDefaultGroups()
        {
            if (groups == null || groups.Count == 0)
            {
                SetDefaultGroups();
            }
        }

        private void Reset()
        {
            SetDefaultGroups();
        }
    }
}
