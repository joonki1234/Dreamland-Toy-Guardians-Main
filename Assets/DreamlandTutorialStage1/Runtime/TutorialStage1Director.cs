using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class TutorialStage1Director : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private DreamEnemySpawner spawner;

        [SerializeField]
        private Stage1WaveController stage1;

        [SerializeField]
        private MissionBannerUI missionUI;

        [SerializeField]
        private Transform tutorialSpawnPoint;

        [SerializeField]
        private TutorialDialogueData dialogueData;

        [Tooltip(
            "아군 포탈 → 코어 → Road_0 등장 연출을 " +
            "관리하는 컨트롤러입니다.")]
        [SerializeField]
        private AllyPortalCoreRevealController
            allyPortalCoreRevealController;


        [Header("Tutorial")]

        [SerializeField, Min(1)]
        private int requiredHitsPerPlayer = 3;

        [SerializeField, Min(1)]
        private int expectedPlayerCount = 1;

        [SerializeField]
        private bool requireBothSynergiesBeforePurification;

        [SerializeField]
        private bool autoStart = true;

        [Tooltip("튜토리얼 안내가 끝난 뒤 첫 적이 나오기까지의 시간")]
        [SerializeField, Min(0f)]
        private float firstSpawnDelay = 0.8f;

        [Tooltip("튜토리얼 완료 후 Stage 1 시작 전 대기 시간")]
        [SerializeField, Min(0f)]
        private float waveStartDelay = 2f;


        private readonly Dictionary<string, int>
            hitCountsByPlayer =
                new Dictionary<string, int>();


        private EnemyHealth tutorialEnemy;

        private Coroutine flowRoutine;
        private Coroutine transitionToWaveRoutine;
        private Coroutine stage1CompletionRoutine;

        private bool emergencySuppressionCompleted;
        private bool starlightBlueprintCompleted;
        private bool stage1CompletionEventRaised;


        public TutorialStage1State State
        {
            get;
            private set;
        } = TutorialStage1State.Idle;


        public int RequiredHitsPerPlayer =>
            requiredHitsPerPlayer;

        public int ExpectedPlayerCount =>
            expectedPlayerCount;

        public TutorialDialogueData DialogueData =>
            dialogueData;


        public event Action Stage1Completed;


        private void Awake()
        {
            ResolveReferences();
        }


        private void OnEnable()
        {
            DreamGameEvents.EnemyHit +=
                HandleEnemyHit;

            DreamGameEvents.EnergyAbsorbed +=
                HandleEnergyAbsorbed;

            DreamGameEvents.SynergyTriggered +=
                HandleSynergyTriggered;

            if (stage1 != null)
            {
                stage1.Completed +=
                    HandleStage1Completed;
            }
        }


        private void Start()
        {
            if (autoStart)
            {
                Begin();
            }
        }


        private void OnDisable()
        {
            DreamGameEvents.EnemyHit -=
                HandleEnemyHit;

            DreamGameEvents.EnergyAbsorbed -=
                HandleEnergyAbsorbed;

            DreamGameEvents.SynergyTriggered -=
                HandleSynergyTriggered;

            if (stage1 != null)
            {
                stage1.Completed -=
                    HandleStage1Completed;
            }
        }


        /// <summary>
        /// Inspector 참조가 비어 있으면
        /// 씬에서 자동으로 찾습니다.
        /// </summary>
        private void ResolveReferences()
        {
            if (allyPortalCoreRevealController == null)
            {
                allyPortalCoreRevealController =
                    UnityEngine.Object.FindAnyObjectByType
                        <AllyPortalCoreRevealController>();
            }
        }


        public void Configure(
            DreamEnemySpawner enemySpawner,
            Stage1WaveController waveController,
            MissionBannerUI ui,
            Transform spawnPoint,
            int playerCount = 1)
        {
            if (stage1 != null)
            {
                stage1.Completed -=
                    HandleStage1Completed;
            }

            spawner = enemySpawner;
            stage1 = waveController;
            missionUI = ui;
            tutorialSpawnPoint = spawnPoint;

            expectedPlayerCount =
                Mathf.Max(
                    1,
                    playerCount);

            if (isActiveAndEnabled &&
                stage1 != null)
            {
                stage1.Completed +=
                    HandleStage1Completed;
            }
        }


        public void SetDialogueData(
            TutorialDialogueData data)
        {
            dialogueData = data;
        }


        public void ApplyStoryDefaultsV8()
        {
            firstSpawnDelay = 0.8f;
            waveStartDelay = 2f;
        }


        public void SetExpectedPlayerCount(
            int playerCount)
        {
            expectedPlayerCount =
                Mathf.Max(
                    1,
                    playerCount);

            RefreshShootingProgress();
        }


        public void Begin()
        {
            if (flowRoutine != null ||
                State != TutorialStage1State.Idle)
            {
                return;
            }

            if (spawner == null ||
                stage1 == null)
            {
                Debug.LogError(
                    "[Dreamland] 튜토리얼을 시작할 수 없습니다. " +
                    "Spawner 또는 Stage1이 연결되지 않았습니다.",
                    this);

                return;
            }

            ResolveReferences();

            stage1CompletionEventRaised = false;

            flowRoutine =
                StartCoroutine(
                    BeginRoutine());
        }


        /// <summary>
        /// 튜토리얼 시작 흐름입니다.
        ///
        /// 진행 순서:
        /// 1. MISSION START 배너
        /// 2. 아군 포탈 등장
        /// 3. 코어 등장
        /// 4. Road_0 등장
        /// 5. 튜토리얼 대사
        /// 6. 튜토리얼 적 등장
        /// </summary>
        private IEnumerator BeginRoutine()
        {
            State =
                TutorialStage1State.Intro;


            /*
             * 1단계: 미션 시작 배너
             */
            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.MissionStartTitle
                    : "MISSION START",

                dialogueData != null
                    ? dialogueData.MissionStartSubtitle
                    : "꿈빛 무기 훈련",

                dialogueData != null
                    ? dialogueData.MissionStartDuration
                    : 2f);


            float missionStartDuration =
                dialogueData != null
                    ? dialogueData.MissionStartDuration
                    : 2f;

            if (missionStartDuration > 0f)
            {
                yield return new WaitForSeconds(
                    missionStartDuration);
            }


            /*
             * 2단계:
             * 아군 포탈 → 코어 → Road_0 등장
             */
            if (allyPortalCoreRevealController != null)
            {
                allyPortalCoreRevealController.PlayReveal();

                /*
                 * 전체 등장 연출이 끝날 때까지
                 * 다음 튜토리얼 대사를 시작하지 않습니다.
                 */
                while (
                    allyPortalCoreRevealController
                        .IsRevealing)
                {
                    yield return null;
                }

                if (!allyPortalCoreRevealController
                        .HasCompleted)
                {
                    Debug.LogWarning(
                        "[Dreamland] 아군 포탈과 코어 등장 연출이 " +
                        "완료되지 않았습니다.",
                        this);
                }
            }
            else
            {
                Debug.LogWarning(
                    "[Dreamland] AllyPortalCoreRevealController가 " +
                    "연결되지 않아 아군 포탈과 코어 등장 연출을 " +
                    "건너뜁니다.",
                    this);
            }


            /*
             * 3단계: 튜토리얼 설명 대사
             */
            if (dialogueData != null &&
                dialogueData.IntroLines != null)
            {
                foreach (
                    TutorialDialogueLine line
                    in dialogueData.IntroLines)
                {
                    yield return
                        PlayDialogueLine(line);
                }

                yield return
                    PlayDialogueLine(
                        dialogueData.EnemyAppearsLine);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "꿈나라 수호대에 온 걸 환영해! " +
                    "손에 든 꿈빛 무기로 악몽을 맞혀보자.",
                    3f);

                yield return
                    new WaitForSeconds(3f);
            }


            /*
             * 4단계: 튜토리얼 적 등장 전 대기
             */
            if (firstSpawnDelay > 0f)
            {
                yield return
                    new WaitForSeconds(
                        firstSpawnDelay);
            }


            PlaceTutorialSpawnInFrontOfCamera();


            /*
             * 5단계: 튜토리얼 적 생성
             */
            tutorialEnemy =
                spawner.SpawnTutorialEnemy(
                    tutorialSpawnPoint);

            if (tutorialEnemy == null)
            {
                Debug.LogError(
                    "[Dreamland] 튜토리얼 몬스터 생성에 실패했습니다.",
                    this);

                flowRoutine = null;
                yield break;
            }

            tutorialEnemy.SetDamageEnabled(false);

            State =
                TutorialStage1State.ShootingPractice;


            string objective =
                dialogueData != null
                    ? dialogueData.ShootingObjective
                    : "튜토리얼 몬스터를 명중";

            missionUI?.SetObjective(
                $"{objective} " +
                $"({requiredHitsPerPlayer}회)");

            RefreshShootingProgress();


            if (dialogueData != null)
            {
                ShowDialogueLine(
                    dialogueData
                        .ShootingInstructionLine);
            }

            flowRoutine = null;
        }


        private IEnumerator PlayDialogueLine(
            TutorialDialogueLine line)
        {
            if (line == null ||
                string.IsNullOrWhiteSpace(
                    line.Message))
            {
                yield break;
            }

            missionUI?.ShowDialogue(
                line.Speaker,
                line.Message,
                line.Duration);

            yield return
                new WaitForSeconds(
                    line.Duration);
        }


        private void ShowDialogueLine(
            TutorialDialogueLine line)
        {
            if (line == null ||
                string.IsNullOrWhiteSpace(
                    line.Message))
            {
                return;
            }

            missionUI?.ShowDialogue(
                line.Speaker,
                line.Message,
                line.Duration);
        }


        private void PlaceTutorialSpawnInFrontOfCamera()
        {
            PrototypeRayWeapon weapon =
                UnityEngine.Object
                    .FindAnyObjectByType
                        <PrototypeRayWeapon>();

            Camera camera =
                weapon != null
                    ? weapon.AimCamera
                    : null;


            if (camera == null)
            {
                Camera[] cameras =
                    UnityEngine.Object
                        .FindObjectsByType<Camera>(
                            FindObjectsSortMode.None);

                foreach (
                    Camera candidate
                    in cameras)
                {
                    if (candidate != null &&
                        candidate.isActiveAndEnabled)
                    {
                        camera = candidate;
                        break;
                    }
                }
            }


            if (camera == null ||
                tutorialSpawnPoint == null)
            {
                return;
            }


            Transform cameraTransform =
                camera.transform;

            Vector3 forward =
                cameraTransform.forward;

            forward.y = 0f;


            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward =
                    Vector3.forward;
            }

            forward.Normalize();


            Vector3 desiredPosition =
                cameraTransform.position +
                forward * 9f;

            Vector3 groundProbeOrigin =
                desiredPosition +
                Vector3.up * 10f;


            if (Physics.Raycast(
                groundProbeOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                30f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                desiredPosition.y =
                    groundHit.point.y +
                    0.05f;
            }
            else
            {
                desiredPosition.y =
                    cameraTransform.position.y -
                    1.55f;
            }


            tutorialSpawnPoint.position =
                desiredPosition;


            Vector3 lookDirection =
                cameraTransform.position -
                tutorialSpawnPoint.position;

            lookDirection.y = 0f;


            if (lookDirection.sqrMagnitude >
                0.0001f)
            {
                tutorialSpawnPoint.rotation =
                    Quaternion.LookRotation(
                        lookDirection.normalized,
                        Vector3.up);
            }
        }


        private void HandleEnemyHit(
            EnemyHealth enemy,
            DamageInfo info)
        {
            if (enemy != tutorialEnemy ||
                State !=
                TutorialStage1State.ShootingPractice)
            {
                return;
            }


            string playerId =
                string.IsNullOrWhiteSpace(
                    info.playerId)
                    ? "LOCAL"
                    : info.playerId;


            hitCountsByPlayer.TryGetValue(
                playerId,
                out int currentCount);


            hitCountsByPlayer[playerId] =
                Mathf.Min(
                    requiredHitsPerPlayer,
                    currentCount + 1);


            RefreshShootingProgress();


            if (GetCompletedPlayerCount() <
                expectedPlayerCount)
            {
                return;
            }


            if (requireBothSynergiesBeforePurification)
            {
                State =
                    TutorialStage1State
                        .SynergyPractice;


                missionUI?.SetObjective(
                    dialogueData != null
                        ? dialogueData
                            .SynergyObjective
                        : "두 가지 직업 시너지를 발동");


                RefreshSynergyProgress();


                if (dialogueData != null)
                {
                    ShowDialogueLine(
                        dialogueData
                            .SynergyInstructionLine);
                }
                else
                {
                    missionUI?.ShowDialogue(
                        "장난감 친구",
                        "잘했어! 이제 두 가지 " +
                        "꿈빛 시너지를 발동해보자!",
                        4f);
                }


                if (emergencySuppressionCompleted &&
                    starlightBlueprintCompleted)
                {
                    EnablePurificationPhase();
                }
            }
            else
            {
                EnablePurificationPhase();
            }
        }


        private void HandleSynergyTriggered(
            SynergyEventData data)
        {
            if (data.Enemy != tutorialEnemy)
            {
                return;
            }


            if (data.Kind ==
                SynergyKind.EmergencySuppression)
            {
                emergencySuppressionCompleted =
                    true;
            }
            else if (
                data.Kind ==
                SynergyKind.StarlightBlueprint)
            {
                starlightBlueprintCompleted =
                    true;
            }


            if (State !=
                TutorialStage1State.SynergyPractice)
            {
                return;
            }


            RefreshSynergyProgress();


            if (emergencySuppressionCompleted &&
                starlightBlueprintCompleted)
            {
                EnablePurificationPhase();
            }
        }


        private void EnablePurificationPhase()
        {
            if (tutorialEnemy == null ||
                tutorialEnemy.IsDead)
            {
                return;
            }


            State =
                TutorialStage1State
                    .PurifyTutorialEnemy;


            tutorialEnemy.RestoreFullHealth();
            tutorialEnemy.SetDamageEnabled(true);


            missionUI?.SetObjective(
                dialogueData != null
                    ? dialogueData
                        .PurificationObjective
                    : "모두 함께 악몽을 정화");


            missionUI?.SetProgress(
                dialogueData != null
                    ? dialogueData
                        .PurificationProgress
                    : "몬스터 HP를 0으로 만드세요");


            if (dialogueData != null)
            {
                ShowDialogueLine(
                    dialogueData
                        .PurificationInstructionLine);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "좋아! 이제 악몽을 " +
                    "완전히 정화해보자!",
                    3f);
            }
        }


        private void HandleEnergyAbsorbed(
            EnemyPurification purification,
            float _)
        {
            if (purification == null ||
                purification.Health != tutorialEnemy)
            {
                return;
            }


            if (State !=
                TutorialStage1State
                    .PurifyTutorialEnemy)
            {
                return;
            }


            State =
                TutorialStage1State
                    .TutorialClear;


            if (transitionToWaveRoutine == null)
            {
                transitionToWaveRoutine =
                    StartCoroutine(
                        TransitionToWaveRoutine());
            }
        }


        private IEnumerator TransitionToWaveRoutine()
        {
            missionUI?.SetObjective(
                string.Empty);

            missionUI?.SetProgress(
                string.Empty);


            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData
                        .TutorialClearTitle
                    : "TUTORIAL CLEAR",

                dialogueData != null
                    ? dialogueData
                        .TutorialClearSubtitle
                    : "꿈빛 에너지가 코어로 돌아왔습니다",

                dialogueData != null
                    ? dialogueData
                        .TutorialClearDuration
                    : 2f);


            float transitionDelay =
                waveStartDelay;


            if (dialogueData != null)
            {
                ShowDialogueLine(
                    dialogueData
                        .TutorialClearLine);

                if (dialogueData
                        .TutorialClearLine != null)
                {
                    transitionDelay =
                        Mathf.Max(
                            transitionDelay,
                            dialogueData
                                .TutorialClearLine
                                .Duration);
                }
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "완벽해! 이제 꿈빛 코어를 지켜줘!",
                    3f);

                transitionDelay =
                    Mathf.Max(
                        transitionDelay,
                        3f);
            }


            yield return
                new WaitForSeconds(
                    transitionDelay);


            transitionToWaveRoutine = null;

            State =
                TutorialStage1State.Wave1;


            /*
             * Stage1WaveController의 Started 이벤트가
             * 발생하면서 포탈 A와 Road_1이 등장합니다.
             */
            stage1.StartStage1();
        }


        private void HandleStage1Completed()
        {
            if (State !=
                TutorialStage1State.Wave1)
            {
                return;
            }


            if (stage1CompletionRoutine != null ||
                stage1CompletionEventRaised)
            {
                return;
            }


            stage1CompletionRoutine =
                StartCoroutine(
                    CompleteStage1Routine());
        }


        private IEnumerator CompleteStage1Routine()
        {
            State =
                TutorialStage1State.Complete;


            const float fallbackDuration = 3f;

            float completionDuration =
                fallbackDuration;


            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.CoreUpgradeTitle
                    : "CORE UPGRADE",

                dialogueData != null
                    ? dialogueData.CoreUpgradeSubtitle
                    : "꿈빛 코어가 무기를 강화합니다",

                fallbackDuration);


            if (dialogueData != null)
            {
                ShowDialogueLine(
                    dialogueData.CoreUpgradeLine);

                if (dialogueData.CoreUpgradeLine != null)
                {
                    completionDuration =
                        Mathf.Max(
                            completionDuration,
                            dialogueData
                                .CoreUpgradeLine
                                .Duration);
                }
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "코어가 되찾은 꿈빛으로 " +
                    "무기를 강화하고 있어!",
                    fallbackDuration);
            }


            DreamGameEvents.RequestWeaponUpgrade();


            if (completionDuration > 0f)
            {
                yield return
                    new WaitForSeconds(
                        completionDuration);
            }


            stage1CompletionRoutine = null;


            if (stage1CompletionEventRaised)
            {
                yield break;
            }


            stage1CompletionEventRaised = true;


            Debug.Log(
                "[Dreamland] TutorialStage1Director의 " +
                "Stage1Completed 이벤트를 발생시킵니다.",
                this);


            Stage1Completed?.Invoke();
        }


        /// <summary>
        /// 튜토리얼만 건너뛰고 Stage 1부터 시작합니다.
        ///
        /// 튜토리얼 스킵 시에도 포탈과 코어 등장 연출을
        /// 자동으로 시작합니다.
        /// </summary>
        public void SkipTutorialAndStartStage1()
        {
            if (stage1 == null)
            {
                Debug.LogError(
                    "[Dreamland] Stage1WaveController가 연결되지 않아 " +
                    "튜토리얼을 스킵할 수 없습니다.",
                    this);

                return;
            }


            StopDirectorCoroutines();
            RemoveTutorialEnemy();


            hitCountsByPlayer.Clear();

            emergencySuppressionCompleted = false;
            starlightBlueprintCompleted = false;
            stage1CompletionEventRaised = false;


            missionUI?.SetObjective(
                string.Empty);

            missionUI?.SetProgress(
                string.Empty);


            /*
             * 튜토리얼을 건너뛰더라도
             * 포탈, 코어, Road_0은 등장시킵니다.
             */
            if (allyPortalCoreRevealController != null &&
                !allyPortalCoreRevealController.HasCompleted &&
                !allyPortalCoreRevealController.IsRevealing)
            {
                allyPortalCoreRevealController.PlayReveal();
            }


            State =
                TutorialStage1State.Wave1;


            stage1.StartStage1();


            Debug.Log(
                "[Dreamland] 튜토리얼을 스킵하고 " +
                "Stage 1을 시작했습니다.",
                this);
        }


        /// <summary>
        /// Stage 2 직접 테스트 전에
        /// 튜토리얼과 Stage 1 연결 연출을 중단합니다.
        /// </summary>
        public void StopForStage2Test()
        {
            StopDirectorCoroutines();
            RemoveTutorialEnemy();


            hitCountsByPlayer.Clear();

            emergencySuppressionCompleted = false;
            starlightBlueprintCompleted = false;
            stage1CompletionEventRaised = false;


            State =
                TutorialStage1State.Idle;


            missionUI?.SetObjective(
                string.Empty);

            missionUI?.SetProgress(
                string.Empty);


            Debug.Log(
                "[Dreamland] Stage 2 테스트를 위해 " +
                "튜토리얼 진행을 중단했습니다.",
                this);
        }


        private void StopDirectorCoroutines()
        {
            StopAllCoroutines();

            flowRoutine = null;
            transitionToWaveRoutine = null;
            stage1CompletionRoutine = null;
        }


        private void RemoveTutorialEnemy()
        {
            if (tutorialEnemy == null)
            {
                return;
            }


            if (spawner != null)
            {
                spawner.DespawnEnemyImmediately(
                    tutorialEnemy);
            }
            else
            {
                Destroy(
                    tutorialEnemy.gameObject);
            }


            tutorialEnemy = null;
        }


        private void RefreshShootingProgress()
        {
            int completedPlayers =
                GetCompletedPlayerCount();


            int localHits =
                hitCountsByPlayer.TryGetValue(
                    "LOCAL",
                    out int count)
                    ? count
                    : 0;


            missionUI?.SetProgress(
                expectedPlayerCount <= 1
                    ? $"명중 {localHits} / " +
                      $"{requiredHitsPerPlayer}"
                    : $"훈련 완료 {completedPlayers} / " +
                      $"{expectedPlayerCount}");
        }


        private void RefreshSynergyProgress()
        {
            string first =
                emergencySuppressionCompleted
                    ? "완료"
                    : "대기";


            string second =
                starlightBlueprintCompleted
                    ? "완료"
                    : "대기";


            missionUI?.SetProgress(
                $"긴급 진압: {first}  ·  " +
                $"별빛 설계: {second}");
        }


        private int GetCompletedPlayerCount()
        {
            int completed = 0;


            foreach (
                int hitCount
                in hitCountsByPlayer.Values)
            {
                if (hitCount >=
                    requiredHitsPerPlayer)
                {
                    completed++;
                }
            }


            return completed;
        }


        private void OnValidate()
        {
            requiredHitsPerPlayer =
                Mathf.Max(
                    1,
                    requiredHitsPerPlayer);

            expectedPlayerCount =
                Mathf.Max(
                    1,
                    expectedPlayerCount);

            firstSpawnDelay =
                Mathf.Max(
                    0f,
                    firstSpawnDelay);

            waveStartDelay =
                Mathf.Max(
                    0f,
                    waveStartDelay);
        }
    }
}