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

            [Min(1)]
            public int enemyCount = 3;

            [Min(0f)]
            public float spawnInterval = 0.5f;

            [Min(0.1f)]
            public float healthMultiplier = 1f;

            [Tooltip("웨이브 안내가 표시된 뒤 첫 적이 생성되기까지의 시간입니다.")]
            [Min(0f)]
            public float preDelay = 1f;

            [Tooltip(
                "현재 웨이브의 분산 스폰을 시작한 뒤 다음 웨이브 준비로 넘어가기까지의 시간입니다. " +
                "현재 웨이브의 적이 남아 있어도 이 시간이 지나면 다음 웨이브가 진행됩니다.")]
            [Min(0f)]
            public float transitionDelay = 4f;

            [Tooltip("이전 버전과의 직렬화 호환을 위해 남겨 둔 값입니다. 현재 진행 방식에서는 사용하지 않습니다.")]
            [Min(0f)]
            public float minimumDuration;
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

        [Tooltip("강제 제한시간이 아니라 플레이 테스트 결과와 비교하기 위한 밸런싱 목표 시간입니다.")]
        [SerializeField, Min(0f)] private float targetStageDurationSeconds = 240f;

        private Coroutine waveRoutine;
        private int runningSpawnRoutineCount;
        private bool allWaveSpawnsCompleted;
        private bool combatCompleted;
        private bool failed;

        public bool IsRunning => waveRoutine != null;
        public IReadOnlyList<WaveGroup> Groups => groups;

        /// <summary>
        /// 마지막 웨이브의 모든 적 생성이 끝나고,
        /// 현재 등장한 모든 적의 정화까지 완료됐을 때 발생합니다.
        /// 현재 TutorialStage1Director와의 호환을 위해 이름을 유지합니다.
        /// </summary>
        public event Action Completed;

        // Stage 1이 실제로 시작되는 순간 발생
        public event Action Started;

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

            if (waveRoutine != null)
            {
                StopAllCoroutines();
                ResetRuntimeState();
            }
        }

        public void Configure(
            DreamEnemySpawner enemySpawner,
            MissionBannerUI ui,
            CoreState targetCore)
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
                Debug.LogWarning(
                    "[Dreamland] Stage 1이 이미 진행 중이므로 중복 시작 요청을 무시합니다.");
                return;
            }

            if (spawner == null)
            {
                Debug.LogError(
                    "[Dreamland] Stage 1을 시작할 수 없습니다. " +
                    "DreamEnemySpawner가 연결되지 않았습니다.");
                return;
            }

            EnsureDefaultGroups();

            // Stage 1 시작을 다른 연출 시스템에 알린다.
            Started?.Invoke();

            failed = false;
            combatCompleted = false;
            allWaveSpawnsCompleted = false;
            runningSpawnRoutineCount = 0;

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
                dialogueData != null
                    ? dialogueData.WaveObjective
                    : "Stage 1 · 등장한 모든 악몽을 정화하라");

            if (waveStartDelay > 0f)
            {
                yield return new WaitForSeconds(waveStartDelay);
            }

            for (int index = 0; index < groups.Count; index++)
            {
                if (failed)
                {
                    yield break;
                }

                WaveGroup group = groups[index];
                if (group == null)
                {
                    Debug.LogWarning(
                        $"[Dreamland] Stage 1 공격 그룹 {index + 1}이 비어 있어 건너뜁니다.");
                    continue;
                }

                missionUI?.ShowBanner(
                    group.label,
                    $"악몽 {group.enemyCount}마리 출현",
                    1.5f);

                missionUI?.SetProgress(
                    $"공격 {index + 1} / {groups.Count}  ·  전장 악몽 {spawner.ActiveEnemyCount}");

                if (group.preDelay > 0f)
                {
                    yield return new WaitForSeconds(group.preDelay);
                }

                if (failed)
                {
                    yield break;
                }

                // SpawnGroup이 끝날 때까지 기다리지 않는다.
                // 각 웨이브는 별도 코루틴에서 일정 간격으로 분산 스폰된다.
                runningSpawnRoutineCount++;
                StartCoroutine(SpawnWaveGroup(group));

                Debug.Log(
                    $"[Dreamland] Stage 1 {group.label} 스폰 시작. " +
                    $"총 {group.enemyCount}마리, 스폰 간격 {group.spawnInterval:0.0}초.");

                if (index >= groups.Count - 1)
                {
                    continue;
                }

                // 적 전멸 여부와 관계없이 설정된 시간이 지나면 다음 웨이브 준비로 넘어간다.
                if (group.transitionDelay > 0f)
                {
                    yield return WaitForNextWaveDelay(group.transitionDelay, index);
                }

                if (failed)
                {
                    yield break;
                }

                int environmentPhase = index + 1;
                DreamGameEvents.RequestEnvironmentPhase(environmentPhase);

                TutorialDialogueLine transitionLine = null;
                if (dialogueData != null)
                {
                    transitionLine = environmentPhase == 1
                        ? dialogueData.AfterFirstGroupLine
                        : dialogueData.BeforeFinalGroupLine;
                }

                if (transitionLine != null &&
                    !string.IsNullOrWhiteSpace(transitionLine.Message))
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
                            ? "적이 남아 있어도 다음 공격이 시작돼! 코어를 지켜!"
                            : "균열이 더 커졌어. 마지막 공격이 몰려온다!",
                        3f);
                }
            }

            // 마지막 웨이브를 시작한 시점과 모든 적의 실제 생성 완료 시점은 다르다.
            // 겹쳐 실행 중인 모든 SpawnGroup 코루틴이 끝날 때까지 기다린다.
            while (!failed && runningSpawnRoutineCount > 0)
            {
                missionUI?.SetProgress(
                    $"모든 공격 전개 중  ·  전장 악몽 {spawner.ActiveEnemyCount}");
                yield return null;
            }

            if (failed)
            {
                yield break;
            }

            allWaveSpawnsCompleted = true;

            Debug.Log(
                "[Dreamland] Stage 1의 모든 웨이브 스폰이 완료됐습니다. " +
                "남은 모든 적이 정화될 때까지 기다립니다.");

            missionUI?.ShowBanner(
                "FINAL PHASE",
                "등장한 악몽을 모두 정화하라",
                1.5f);

            // 강제 종료 타이머는 없다.
            // 마지막 적의 정화 연출까지 끝나 ActiveEnemyCount가 0이 되어야 완료된다.
            while (!failed && spawner.ActiveEnemyCount > 0)
            {
                missionUI?.SetProgress(
                    $"남은 악몽 {spawner.ActiveEnemyCount}");
                yield return null;
            }

            if (failed)
            {
                yield break;
            }

            if (!TryMarkCombatCompleted())
            {
                yield break;
            }

            missionUI?.SetProgress(string.Empty);
            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.WaveClearTitle : "WAVE 1 CLEAR",
                dialogueData != null ? dialogueData.WaveClearSubtitle : "꿈빛 코어 방어 성공",
                clearSequenceDuration);

            float stageDuration = Time.time - stageStartedAt;
            Debug.Log(
                $"[Dreamland] Stage 1 전투 완료: {stageDuration:0.0}초 " +
                $"(밸런싱 목표 {targetStageDurationSeconds:0}초 - 강제 제한시간이 아닙니다.)");

            if (clearSequenceDuration > 0f)
            {
                yield return new WaitForSeconds(clearSequenceDuration);
            }

            waveRoutine = null;
            Completed?.Invoke();
        }

        private IEnumerator SpawnWaveGroup(WaveGroup group)
        {
            yield return spawner.SpawnGroup(
                group.enemyCount,
                group.spawnInterval,
                group.healthMultiplier);

            runningSpawnRoutineCount = Mathf.Max(0, runningSpawnRoutineCount - 1);

            Debug.Log(
                $"[Dreamland] {group.label}의 모든 적 생성 완료. " +
                $"진행 중인 스폰 코루틴: {runningSpawnRoutineCount}");
        }

        private IEnumerator WaitForNextWaveDelay(float duration, int currentGroupIndex)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0f, duration);

            while (!failed && elapsed < safeDuration)
            {
                missionUI?.SetProgress(
                    $"공격 {currentGroupIndex + 1} / {groups.Count}" +
                    $"  ·  전장 악몽 {spawner.ActiveEnemyCount}");

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private bool TryMarkCombatCompleted()
        {
            if (failed || combatCompleted)
            {
                return false;
            }

            if (!allWaveSpawnsCompleted)
            {
                Debug.LogWarning(
                    "[Dreamland] 모든 웨이브 스폰이 끝나기 전에 " +
                    "Stage 1 완료가 요청되어 무시했습니다.");
                return false;
            }

            if (spawner.ActiveEnemyCount > 0)
            {
                Debug.LogWarning(
                    "[Dreamland] 적이 남아 있는 상태에서 " +
                    "Stage 1 완료가 요청되어 무시했습니다.");
                return false;
            }

            combatCompleted = true;
            return true;
        }

        private void HandleCoreDestroyed()
        {
            if (waveRoutine == null || failed || combatCompleted)
            {
                return;
            }

            failed = true;

            // 메인 진행 코루틴뿐 아니라 겹쳐 실행 중인 모든 스폰 코루틴도 중단한다.
            StopAllCoroutines();

            waveRoutine = null;
            runningSpawnRoutineCount = 0;
            allWaveSpawnsCompleted = false;

            missionUI?.SetProgress(string.Empty);
            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.MissionFailedTitle : "MISSION FAILED",
                dialogueData != null ? dialogueData.MissionFailedSubtitle : "꿈빛 코어가 무너졌습니다",
                3f);

            Debug.Log(
                "[Dreamland] 코어가 파괴되어 Stage 1 진행과 남은 스폰을 중단했습니다.");

            Failed?.Invoke();
        }

        private void ResetRuntimeState()
        {
            waveRoutine = null;
            runningSpawnRoutineCount = 0;
            allWaveSpawnsCompleted = false;
            combatCompleted = false;
            failed = false;
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
