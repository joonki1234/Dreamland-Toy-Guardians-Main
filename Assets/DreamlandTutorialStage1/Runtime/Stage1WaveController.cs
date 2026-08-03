using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// Stage 1의 전투 웨이브를 관리합니다.
    ///
    /// 최종 진행:
    ///
    /// Stage 1 시작
    /// → 준비 단계 0 요청
    /// → Portal A
    /// → Road_1
    /// → 준비 완료 대기
    ///
    /// 1차 공격
    /// → 준비 단계 1 요청
    /// → Portal B
    /// → Road_2
    /// → 준비 완료
    /// → 적 스폰
    ///
    /// 2차 공격
    /// → 준비 단계 2 요청
    /// → Portal C
    /// → Road_3
    /// → 준비 완료
    /// → 적 스폰
    ///
    /// 최종 공격
    /// → 준비 단계 3 요청
    /// → Portal D
    /// → Road_4
    /// → 준비 완료
    /// → 적 스폰
    /// </summary>
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

            [Tooltip(
                "포탈과 길 준비가 완료된 뒤 " +
                "첫 적을 생성하기 전 추가 대기 시간")]
            [Min(0f)]
            public float preDelay = 1f;

            [Tooltip("다음 웨이브 준비로 넘어가기까지의 시간")]
            [Min(0f)]
            public float transitionDelay = 4f;

            [Tooltip("이전 버전 호환용 값")]
            [Min(0f)]
            public float minimumDuration;
        }


        [Header("References")]

        [SerializeField]
        private DreamEnemySpawner spawner;

        [SerializeField]
        private MissionBannerUI missionUI;

        [SerializeField]
        private CoreState core;

        [SerializeField]
        private TutorialDialogueData dialogueData;

        [SerializeField]
        private ToyFriendController toyFriend;


        [Header("Wave 1")]

        [SerializeField]
        private List<WaveGroup> groups =
            new List<WaveGroup>();

        [Tooltip(
            "Portal A와 Road_1 준비가 끝난 뒤 " +
            "1차 공격 준비를 시작하기 전 대기 시간")]
        [SerializeField, Min(0f)]
        private float waveStartDelay = 3f;

        [SerializeField, Min(0f)]
        private float clearSequenceDuration = 3f;

        [SerializeField, Min(0f)]
        private float targetStageDurationSeconds = 240f;


        [Header("환경 준비 안전 설정")]

        [Tooltip(
            "EnemyPortalStageController가 준비 완료 응답을 보내지 못했을 때 " +
            "게임이 영원히 멈추지 않도록 하는 최대 대기 시간")]
        [SerializeField, Min(1f)]
        private float preparationTimeout = 10f;


        private Coroutine waveRoutine;

        private int runningSpawnRoutineCount;

        private bool allWaveSpawnsCompleted;
        private bool combatCompleted;
        private bool failed;

        /*
         * 현재 포탈과 길 준비 완료를 기다리고 있는 단계입니다.
         *
         * -1 = 준비를 기다리지 않는 상태
         *  0 = Portal A + Road_1
         *  1 = Portal B + Road_2
         *  2 = Portal C + Road_3
         *  3 = Portal D + Road_4
         */
        private int waitingPreparationStep = -1;

        private bool preparationCompleted;


        public bool IsRunning =>
            waveRoutine != null;

        public IReadOnlyList<WaveGroup> Groups =>
            groups;


        /// <summary>
        /// Stage 1 전체 시작 신호입니다.
        /// 기존 다른 코드와의 호환을 위해 유지합니다.
        /// </summary>
        public event Action Started;


        /// <summary>
        /// 포탈과 길을 준비해야 할 때 발생합니다.
        ///
        /// step 0:
        /// Portal A + Road_1
        ///
        /// step 1:
        /// Portal B + Road_2
        ///
        /// step 2:
        /// Portal C + Road_3
        ///
        /// step 3:
        /// Portal D + Road_4
        /// </summary>
        public event Action<int> EnvironmentPreparationRequested;

        /// <summary>0부터 시작하는 공격 그룹 인덱스입니다.</summary>
        public event Action<int> WaveGroupCompleted;

        public event Action SynergyUnlocked;


        public event Action Completed;
        public event Action Failed;


        private void Awake()
        {
            EnsureDefaultGroups();

            if (toyFriend == null)
            {
                toyFriend = FindAnyObjectByType<ToyFriendController>();
            }
        }


        private void OnEnable()
        {
            if (core != null)
            {
                core.CoreDestroyed -=
                    HandleCoreDestroyed;

                core.CoreDestroyed +=
                    HandleCoreDestroyed;
            }
        }


        private void OnDisable()
        {
            if (core != null)
            {
                core.CoreDestroyed -=
                    HandleCoreDestroyed;
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
                core.CoreDestroyed -=
                    HandleCoreDestroyed;
            }

            spawner = enemySpawner;
            missionUI = ui;
            core = targetCore;

            if (isActiveAndEnabled &&
                core != null)
            {
                core.CoreDestroyed -=
                    HandleCoreDestroyed;

                core.CoreDestroyed +=
                    HandleCoreDestroyed;
            }

            EnsureDefaultGroups();
        }


        public void SetDialogueData(
            TutorialDialogueData data)
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
                    preDelay = 0.5f,
                    transitionDelay = 10f,
                    minimumDuration = 0f
                },

                new WaveGroup
                {
                    label = "2차 공격",
                    enemyCount = 3,
                    spawnInterval = 2.5f,
                    healthMultiplier = 1.25f,
                    preDelay = 0.5f,
                    transitionDelay = 12f,
                    minimumDuration = 0f
                },

                new WaveGroup
                {
                    label = "최종 공격",
                    enemyCount = 6,
                    spawnInterval = 1.8f,
                    healthMultiplier = 1.5f,
                    preDelay = 0.5f,
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
            preparationTimeout = 10f;

            SetDefaultGroups();
        }


        public void StartStage1()
        {
            if (waveRoutine != null)
            {
                Debug.LogWarning(
                    "[Dreamland] Stage 1이 이미 진행 중이므로 " +
                    "중복 시작 요청을 무시합니다.",
                    this);

                return;
            }

            if (spawner == null)
            {
                Debug.LogError(
                    "[Dreamland] Stage 1을 시작할 수 없습니다. " +
                    "DreamEnemySpawner가 연결되지 않았습니다.",
                    this);

                return;
            }

            EnsureDefaultGroups();

            failed = false;
            combatCompleted = false;
            allWaveSpawnsCompleted = false;
            runningSpawnRoutineCount = 0;

            waitingPreparationStep = -1;
            preparationCompleted = false;
            RoleSynergyProgression.Lock();

            /*
             * 기존 Stage 1 시작 이벤트입니다.
             * 여기서는 포탈과 길을 직접 실행하지 않습니다.
             */
            Started?.Invoke();

            waveRoutine =
                StartCoroutine(
                    RunWaveRoutine());
        }


        private IEnumerator RunWaveRoutine()
        {
            float stageStartedAt =
                Time.time;


            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.WaveStartTitle
                    : "WAVE 1 START",

                dialogueData != null
                    ? dialogueData.WaveStartSubtitle
                    : "꿈빛 코어를 지켜라",

                2f);


            missionUI?.SetObjective(
                dialogueData != null
                    ? dialogueData.WaveObjective
                    : "Stage 1 · 등장한 모든 악몽을 정화하라");


            /*
             * Stage 1 시작 침식:
             *
             * Portal A
             * → Road_1
             *
             * 아직 적은 생성하지 않습니다.
             */
            yield return WaitForEnvironmentPreparation(
                0,
                "Portal A와 Road_1");


            if (failed)
            {
                yield break;
            }


            /*
             * 첫 길이 생성된 화면을 잠시 보여준 뒤
             * 1차 공격 준비로 넘어갑니다.
             */
            if (waveStartDelay > 0f)
            {
                yield return new WaitForSeconds(
                    waveStartDelay);
            }


            /*
             * 현재 Stage 1 공격 그룹은 세 개입니다.
             *
             * index 0 → 준비 단계 1 → Portal B + Road_2
             * index 1 → 준비 단계 2 → Portal C + Road_3
             * index 2 → 준비 단계 3 → Portal D + Road_4
             */
            for (int index = 0;
                 index < groups.Count;
                 index++)
            {
                if (failed)
                {
                    yield break;
                }


                WaveGroup group =
                    groups[index];

                if (group == null)
                {
                    Debug.LogWarning(
                        $"[Dreamland] Stage 1 공격 그룹 " +
                        $"{index + 1}이 비어 있어 건너뜁니다.",
                        this);

                    continue;
                }


                missionUI?.ShowBanner(
                    group.label,
                    $"악몽 {group.enemyCount}마리 출현",
                    1.5f);


                missionUI?.SetProgress(
                    $"공격 {index + 1} / {groups.Count}" +
                    $"  ·  전장 악몽 {spawner.ActiveEnemyCount}");


                int preparationStep =
                    index + 1;


                /*
                 * 반드시 포탈과 길 준비가 끝날 때까지 기다립니다.
                 */
                yield return WaitForEnvironmentPreparation(
                    preparationStep,
                    $"공격 {index + 1} 포탈과 길");


                if (failed)
                {
                    yield break;
                }


                /*
                 * 길 생성이 끝난 뒤 적이 바로 튀어나오지 않도록
                 * 짧게 추가 대기합니다.
                 */
                if (group.preDelay > 0f)
                {
                    yield return new WaitForSeconds(
                        group.preDelay);
                }


                if (failed)
                {
                    yield break;
                }


                /*
                 * 포탈 → 길 → 적 순서 중
                 * 마지막인 적 스폰을 여기에서 시작합니다.
                 */
                runningSpawnRoutineCount++;
                Debug.Log(
                    $"[Dreamland] Stage 1 {group.label} 스폰 시작. " +
                    $"포탈과 길 준비 완료 후 " +
                    $"총 {group.enemyCount}마리를 생성합니다. " +
                    $"스폰 간격 {group.spawnInterval:0.0}초.",
                    this);

                yield return SpawnWaveGroup(group);


                /*
                 * 이번 공격으로 등장한 적을 모두 정화해야
                 * 해당 공격이 종료되고 다음 단계로 넘어갑니다.
                 */
                yield return WaitForWaveClear(index);


                if (failed)
                {
                    yield break;
                }


                WaveGroupCompleted?.Invoke(index);


                float occupiedTransitionTime = 0f;

                /*
                 * index 1 = Stage 1의 두 번째 공격 종료.
                 * 이 순간부터 모든 새 Enemy의 직업 시너지가 활성화됩니다.
                 */
                if (index == 1)
                {
                    occupiedTransitionTime =
                        GetSynergyUnlockSequenceDuration();

                    yield return PlaySynergyUnlockSequence();
                }


                if (index >= groups.Count - 1)
                {
                    continue;
                }


                float remainingTransitionDelay =
                    Mathf.Max(
                        0f,
                        group.transitionDelay - occupiedTransitionTime);

                if (remainingTransitionDelay > 0f)
                {
                    yield return WaitForNextWaveDelay(
                        remainingTransitionDelay,
                        index);
                }


                if (failed)
                {
                    yield break;
                }


                /*
                 * 기존 대사와 다른 환경 시스템의 호환을 위해
                 * 예전 환경 변화 이벤트는 유지합니다.
                 *
                 * EnemyPortalStageController는 이 이벤트로
                 * 포탈과 길을 실행하지 않습니다.
                 */
                int environmentPhase =
                    index + 1;

                DreamGameEvents.RequestEnvironmentPhase(
                    environmentPhase);


                TutorialDialogueLine transitionLine =
                    null;


                if (dialogueData != null)
                {
                    transitionLine =
                        environmentPhase == 1
                            ? dialogueData.AfterFirstGroupLine
                            : dialogueData.BeforeFinalGroupLine;
                }


                if (transitionLine != null &&
                    !string.IsNullOrWhiteSpace(
                        transitionLine.Message))
                {
                    missionUI?.ShowQuickGuide(
                        transitionLine.Speaker,
                        transitionLine.Message,
                        transitionLine.Duration);
                }
                else
                {
                    missionUI?.ShowQuickGuide(
                        "장난감 친구",

                        environmentPhase == 1
                            ? "새로운 균열이 열리고 있어! 코어를 지켜!"
                            : "마지막 균열이 열렸어. 끝까지 버텨!",

                        3f);
                }
            }


            /*
             * 모든 웨이브의 적 생성이 끝날 때까지 기다립니다.
             */
            while (!failed &&
                   runningSpawnRoutineCount > 0)
            {
                missionUI?.SetProgress(
                    $"모든 공격 전개 중  ·  " +
                    $"전장 악몽 {spawner.ActiveEnemyCount}");

                yield return null;
            }


            if (failed)
            {
                yield break;
            }


            allWaveSpawnsCompleted = true;


            Debug.Log(
                "[Dreamland] Stage 1의 모든 웨이브 스폰이 완료됐습니다.",
                this);


            missionUI?.ShowBanner(
                "FINAL PHASE",
                "등장한 악몽을 모두 정화하라",
                1.5f);


            while (!failed &&
                   spawner.ActiveEnemyCount > 0)
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


            missionUI?.SetProgress(
                string.Empty);


            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.WaveClearTitle
                    : "WAVE 1 CLEAR",

                dialogueData != null
                    ? dialogueData.WaveClearSubtitle
                    : "꿈빛 코어 방어 성공",

                clearSequenceDuration);


            float stageDuration =
                Time.time - stageStartedAt;


            Debug.Log(
                $"[Dreamland] Stage 1 전투 완료: " +
                $"{stageDuration:0.0}초 " +
                $"(목표 {targetStageDurationSeconds:0}초)",
                this);


            if (clearSequenceDuration > 0f)
            {
                yield return new WaitForSeconds(
                    clearSequenceDuration);
            }


            waveRoutine = null;

            Completed?.Invoke();
        }


        /// <summary>
        /// EnemyPortalStageController에 포탈과 길 준비를 요청하고,
        /// 준비 완료 응답이 올 때까지 기다립니다.
        /// </summary>
        private IEnumerator WaitForEnvironmentPreparation(
            int preparationStep,
            string preparationLabel)
        {
            waitingPreparationStep =
                preparationStep;

            preparationCompleted =
                false;


            Debug.Log(
                $"[Dreamland] 환경 준비 단계 {preparationStep} 요청: " +
                preparationLabel,
                this);


            EnvironmentPreparationRequested?.Invoke(
                preparationStep);


            /*
             * 이벤트를 구독한 컨트롤러가 전혀 없으면
             * 게임을 멈추지 않고 경고 후 진행합니다.
             */
            if (EnvironmentPreparationRequested == null)
            {
                Debug.LogWarning(
                    "[Dreamland] 환경 준비 이벤트를 받는 컨트롤러가 없습니다. " +
                    "포탈과 길 연출 없이 진행합니다.",
                    this);

                preparationCompleted = true;
            }


            float elapsed = 0f;

            float safeTimeout =
                Mathf.Max(
                    1f,
                    preparationTimeout);


            while (!failed &&
                   !preparationCompleted &&
                   elapsed < safeTimeout)
            {
                elapsed +=
                    Time.deltaTime;

                yield return null;
            }


            if (failed)
            {
                yield break;
            }


            if (!preparationCompleted)
            {
                Debug.LogWarning(
                    $"[Dreamland] 환경 준비 단계 {preparationStep}의 " +
                    $"완료 응답이 {safeTimeout:0.0}초 동안 없어 " +
                    "안전 장치로 다음 진행을 시작합니다.",
                    this);
            }
            else
            {
                Debug.Log(
                    $"[Dreamland] 환경 준비 단계 {preparationStep} 완료: " +
                    preparationLabel,
                    this);
            }


            waitingPreparationStep = -1;
            preparationCompleted = false;
        }


        /// <summary>
        /// EnemyPortalStageController가
        /// 포탈과 길 연출을 완료한 뒤 호출합니다.
        /// </summary>
        public void NotifyEnvironmentPreparationCompleted(
            int preparationStep)
        {
            if (waitingPreparationStep !=
                preparationStep)
            {
                Debug.LogWarning(
                    $"[Dreamland] 현재 기다리는 준비 단계는 " +
                    $"{waitingPreparationStep}인데 " +
                    $"{preparationStep} 완료 신호를 받았습니다. " +
                    "해당 신호를 무시합니다.",
                    this);

                return;
            }


            preparationCompleted =
                true;
        }


        private IEnumerator SpawnWaveGroup(
            WaveGroup group)
        {
            yield return spawner.SpawnGroup(
                group.enemyCount,
                group.spawnInterval,
                group.healthMultiplier);


            runningSpawnRoutineCount =
                Mathf.Max(
                    0,
                    runningSpawnRoutineCount - 1);


            Debug.Log(
                $"[Dreamland] {group.label}의 모든 적 생성 완료. " +
                $"진행 중인 스폰 코루틴: {runningSpawnRoutineCount}",
                this);
        }


        private IEnumerator WaitForWaveClear(int groupIndex)
        {
            while (!failed && spawner.ActiveEnemyCount > 0)
            {
                missionUI?.SetProgress(
                    $"공격 {groupIndex + 1} / {groups.Count}" +
                    $"  ·  남은 악몽 {spawner.ActiveEnemyCount}");

                yield return null;
            }
        }


        private IEnumerator PlaySynergyUnlockSequence()
        {
            RoleSynergyProgression.Unlock();
            SynergyUnlocked?.Invoke();

            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.SynergyUnlockTitle
                    : "SYNERGY UNLOCK",
                dialogueData != null
                    ? dialogueData.SynergyUnlockSubtitle
                    : "직업의 힘이 서로 연결됩니다",
                2.2f);

            yield return new WaitForSeconds(1.2f);

            if (dialogueData != null &&
                dialogueData.SynergyUnlockLines != null &&
                dialogueData.SynergyUnlockLines.Count > 0)
            {
                for (int i = 0;
                     i < dialogueData.SynergyUnlockLines.Count;
                     i++)
                {
                    TutorialDialogueLine line =
                        dialogueData.SynergyUnlockLines[i];

                    if (line == null ||
                        string.IsNullOrWhiteSpace(line.Message))
                    {
                        continue;
                    }

                    float playbackDuration =
                        line.VoiceClip != null
                            ? Mathf.Max(line.Duration, line.VoiceClip.length)
                            : line.Duration;

                    missionUI?.ShowDialogue(
                        line.Speaker,
                        line.Message,
                        playbackDuration);

                    toyFriend?.Speak(
                        line.Message,
                        playbackDuration,
                        i == 0,
                        line.VoiceClip);

                    yield return new WaitForSeconds(playbackDuration);
                }
            }
            else
            {
                const string fallbackMessage =
                    "코어에 꿈빛이 충분히 모였어! 이제 동료의 직업 능력을 이어 시너지를 발동해 봐!";

                missionUI?.ShowDialogue(
                    "장난감 친구",
                    fallbackMessage,
                    4.5f);
                toyFriend?.Speak(
                    fallbackMessage,
                    4.5f,
                    true);

                yield return new WaitForSeconds(4.5f);
            }
        }


        private float GetSynergyUnlockSequenceDuration()
        {
            float duration = 1.2f;

            if (dialogueData == null ||
                dialogueData.SynergyUnlockLines == null ||
                dialogueData.SynergyUnlockLines.Count == 0)
            {
                return duration + 4.5f;
            }

            for (int i = 0;
                 i < dialogueData.SynergyUnlockLines.Count;
                 i++)
            {
                TutorialDialogueLine line =
                    dialogueData.SynergyUnlockLines[i];

                if (line != null &&
                    !string.IsNullOrWhiteSpace(line.Message))
                {
                    duration += line.VoiceClip != null
                        ? Mathf.Max(line.Duration, line.VoiceClip.length)
                        : line.Duration;
                }
            }

            return duration;
        }


        private IEnumerator WaitForNextWaveDelay(
            float duration,
            int currentGroupIndex)
        {
            float elapsed = 0f;

            float safeDuration =
                Mathf.Max(
                    0f,
                    duration);


            while (!failed &&
                   elapsed < safeDuration)
            {
                missionUI?.SetProgress(
                    $"공격 {currentGroupIndex + 1} / {groups.Count}" +
                    $"  ·  전장 악몽 {spawner.ActiveEnemyCount}");

                elapsed +=
                    Time.deltaTime;

                yield return null;
            }
        }


        private bool TryMarkCombatCompleted()
        {
            if (failed ||
                combatCompleted)
            {
                return false;
            }


            if (!allWaveSpawnsCompleted)
            {
                Debug.LogWarning(
                    "[Dreamland] 모든 웨이브 스폰이 끝나기 전에 " +
                    "Stage 1 완료가 요청되어 무시했습니다.",
                    this);

                return false;
            }


            if (spawner.ActiveEnemyCount > 0)
            {
                Debug.LogWarning(
                    "[Dreamland] 적이 남아 있는 상태에서 " +
                    "Stage 1 완료가 요청되어 무시했습니다.",
                    this);

                return false;
            }


            combatCompleted = true;

            return true;
        }


        private void HandleCoreDestroyed()
        {
            if (waveRoutine == null ||
                failed ||
                combatCompleted)
            {
                return;
            }


            failed = true;

            StopAllCoroutines();
            toyFriend?.StopSpeaking();

            waveRoutine = null;
            runningSpawnRoutineCount = 0;
            allWaveSpawnsCompleted = false;

            waitingPreparationStep = -1;
            preparationCompleted = false;


            missionUI?.SetProgress(
                string.Empty);


            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.MissionFailedTitle
                    : "MISSION FAILED",

                dialogueData != null
                    ? dialogueData.MissionFailedSubtitle
                    : "꿈빛 코어가 무너졌습니다",

                3f);


            Debug.Log(
                "[Dreamland] 코어가 파괴되어 " +
                "Stage 1 진행을 중단했습니다.",
                this);


            Failed?.Invoke();
        }


        /// <summary>
        /// Stage 2 직접 테스트 전에
        /// Stage 1 웨이브와 스폰 코루틴을 모두 중단합니다.
        /// </summary>
        public void StopForStage2Test()
        {
            StopAllCoroutines();
            toyFriend?.StopSpeaking();

            ResetRuntimeState();
            RoleSynergyProgression.Unlock();

            missionUI?.SetProgress(
                string.Empty);


            Debug.Log(
                "[Dreamland] Stage 2 테스트를 위해 " +
                "Stage 1 웨이브를 중단했습니다.",
                this);
        }


        private void ResetRuntimeState()
        {
            waveRoutine = null;
            runningSpawnRoutineCount = 0;
            allWaveSpawnsCompleted = false;
            combatCompleted = false;
            failed = false;

            waitingPreparationStep = -1;
            preparationCompleted = false;
        }


        private void EnsureDefaultGroups()
        {
            if (groups == null ||
                groups.Count == 0)
            {
                SetDefaultGroups();
            }
        }


        private void Reset()
        {
            SetDefaultGroups();
        }


        private void OnValidate()
        {
            waveStartDelay =
                Mathf.Max(
                    0f,
                    waveStartDelay);

            clearSequenceDuration =
                Mathf.Max(
                    0f,
                    clearSequenceDuration);

            targetStageDurationSeconds =
                Mathf.Max(
                    0f,
                    targetStageDurationSeconds);

            preparationTimeout =
                Mathf.Max(
                    1f,
                    preparationTimeout);
        }
    }
}
