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

        [Tooltip("8명이 직업을 두 명씩 선택하는 튜토리얼 단계입니다.")]
        [SerializeField]
        private RoleSelectionController roleSelection;

        [Tooltip("3D 대사 말풍선과 말하는 몸짓을 재생할 장난감 친구입니다.")]
        [SerializeField]
        private ToyFriendController toyFriend;

        [Tooltip(
            "아군 포탈 → 코어 → Road_0 등장 연출을 " +
            "관리하는 컨트롤러입니다.")]
        [SerializeField]
        private AllyPortalCoreRevealController
            allyPortalCoreRevealController;

        [Tooltip(
            "코어에서 빛이 나와 장난감 친구로 변하는 등장 연출입니다.")]
        [SerializeField]
        private ToyFriendEntranceSequence
            toyFriendEntranceSequence;


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
        private Coroutine postShootingStoryRoutine;

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

            if (toyFriendEntranceSequence == null)
            {
                toyFriendEntranceSequence =
                    UnityEngine.Object.FindAnyObjectByType
                        <ToyFriendEntranceSequence>();
            }

            if (toyFriend == null)
            {
                toyFriend = UnityEngine.Object.FindAnyObjectByType
                    <ToyFriendController>();
            }

            // 코어보다 먼저 등장하지 않도록 튜토리얼 진행이 시작 시점을 관리합니다.
            toyFriendEntranceSequence?.SetAutomaticStart(false);
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
            RoleSynergyProgression.Lock();

            flowRoutine =
                StartCoroutine(
                    BeginRoutine());
        }


        /// <summary>
        /// 튜토리얼 시작 흐름입니다.
        ///
        /// 진행 순서:
        /// 1. 아군 포탈 → 코어 → Road_0 등장
        /// 2. 장난감 친구 등장과 이동
        /// 3. 장난감 친구의 3D 스토리 설명
        /// 4. 튜토리얼 시작 배너
        /// 5. 튜토리얼 적 등장과 2D 행동 안내
        /// </summary>
        private IEnumerator BeginRoutine()
        {
            State =
                TutorialStage1State.Intro;


            /*
             * 1단계:
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
             * 2단계:
             * 코어에서 나온 빛이 장난감 친구로 변하고,
             * 대화 위치까지 걸어온 뒤 플레이어를 바라봅니다.
             */
            if (toyFriendEntranceSequence != null)
            {
                toyFriendEntranceSequence.PlaySequence();

                while (toyFriendEntranceSequence.IsPlaying)
                {
                    yield return null;
                }

                if (!toyFriendEntranceSequence.HasCompleted)
                {
                    Debug.LogWarning(
                        "[Dreamland] 장난감 친구 등장 연출이 " +
                        "완료되지 않았습니다.",
                        this);
                }
            }
            else
            {
                Debug.LogWarning(
                    "[Dreamland] ToyFriendEntranceSequence가 없어 " +
                    "장난감 친구 등장 연출을 건너뜁니다.",
                    this);
            }


            /*
             * 3단계: 3D 장난감 친구의 스토리 설명
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

            }
            else
            {
                yield return PlayDialogueLine(
                    new TutorialDialogueLine(
                        "장난감 친구",
                        "우리 꿈나라가 악몽 바이러스에 오염되고 있어. 나와 함께 꿈빛 코어를 지켜줄래?",
                        3f));
            }


            /*
             * 4단계:
             * 직업 설명/직업 선택은 튜토리얼 이전에 별도로 진행합니다.
             * 여기서는 관련 UI와 대사를 모두 건너뛰고 곧바로 전투 학습으로 넘어갑니다.
             */
            State = TutorialStage1State.RoleSelection;
            roleSelection?.Hide();

            if (dialogueData != null)
            {
                yield return PlayDialogueLine(
                    dialogueData.EnemyAppearsLine);
            }

            State = TutorialStage1State.Intro;


            /*
             * 5단계: 설명과 직업 선택이 끝난 뒤 튜토리얼 시작 배너
             */
            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.MissionStartTitle
                    : "TUTORIAL START",

                dialogueData != null
                    ? dialogueData.MissionStartSubtitle
                    : "오염된 장난감을 정화해보세요",

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
             * 5단계: 튜토리얼 적 등장 전 대기
             */
            if (firstSpawnDelay > 0f)
            {
                yield return
                    new WaitForSeconds(
                        firstSpawnDelay);
            }


            PlaceTutorialSpawnInFrontOfCamera();


            /*
             * 6단계: 튜토리얼 적 생성
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
                ShowGuideLine(
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

            float playbackDuration =
                line.VoiceClip != null
                    ? Mathf.Max(line.Duration, line.VoiceClip.length)
                    : line.Duration;

            missionUI?.HideTransientMessages();

            if (toyFriend != null)
            {
                toyFriend.Speak(
                    line.Message,
                    playbackDuration,
                    false,
                    line.VoiceClip);
            }
            else
            {
                missionUI?.ShowDialogue(
                    line.Speaker,
                    line.Message,
                    playbackDuration);
            }

            yield return
                new WaitForSeconds(
                    playbackDuration);
        }


        private void ShowGuideLine(
            TutorialDialogueLine line)
        {
            if (line == null ||
                string.IsNullOrWhiteSpace(
                    line.Message))
            {
                return;
            }

            missionUI?.ShowQuickGuide(
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


            // 싱글 플레이 튜토리얼에서는 무기/네트워크 계층이 어떤 playerId를
            // 보내더라도 하나의 LOCAL 카운터로 합칩니다. 이렇게 해야 화면의
            // 0/3 표시와 실제 완료 판정이 항상 같은 값을 사용합니다.
            string playerId =
                expectedPlayerCount <= 1
                    ? "LOCAL"
                    : (string.IsNullOrWhiteSpace(info.playerId)
                        ? "LOCAL"
                        : info.playerId);


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
                    ShowGuideLine(
                        dialogueData
                            .SynergyInstructionLine);
                }
                else
                {
                    missionUI?.ShowQuickGuide(
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
                if (postShootingStoryRoutine == null)
                {
                    // 3회 명중 직후의 핵심 튜토리얼 대사는 2D 안내만 띄우지 않고
                    // 3D 장난감 친구가 다시 나타나 직접 설명합니다.
                    State = TutorialStage1State.PurifyTutorialEnemy;
                    postShootingStoryRoutine =
                        StartCoroutine(PlayPostShootingStoryRoutine());
                }
            }
        }


        private IEnumerator PlayPostShootingStoryRoutine()
        {
            missionUI?.SetObjective(string.Empty);
            missionUI?.SetProgress(string.Empty);
            missionUI?.HideTransientMessages();

            if (toyFriend != null)
            {
                yield return toyFriend.ShowForStory();
            }

            TutorialDialogueLine line =
                dialogueData != null
                    ? dialogueData.PurificationInstructionLine
                    : null;

            yield return PlayToyFriendOnlyLine(
                line,
                "좋아! 공격이 제대로 들어갔어. 이제 끝까지 공격해서 완전히 정화해!",
                3f,
                true);

            if (toyFriend != null)
            {
                yield return toyFriend.HideForCombat();
            }

            postShootingStoryRoutine = null;
            // 위에서 이미 3D 친구가 정화 방법을 설명했으므로 같은 대사를
            // 오른쪽 2D 가이드 패널에 다시 띄우지 않습니다.
            EnablePurificationPhase(false);
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
                SynergyKind.ChefArchitectCombo)
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


        private void EnablePurificationPhase(bool showInstructionGuide = true)
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


            if (showInstructionGuide)
            {
                if (dialogueData != null)
                {
                    ShowGuideLine(
                        dialogueData
                            .PurificationInstructionLine);
                }
                else
                {
                    missionUI?.ShowQuickGuide(
                        "장난감 친구",
                        "좋아! 이제 악몽을 " +
                        "완전히 정화해보자!",
                        3f);
                }
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
            missionUI?.SetObjective(string.Empty);
            missionUI?.SetProgress(string.Empty);
            missionUI?.HideTransientMessages();

            float clearBannerDuration =
                dialogueData != null
                    ? dialogueData.TutorialClearDuration
                    : 2f;

            missionUI?.ShowBanner(
                dialogueData != null
                    ? dialogueData.TutorialClearTitle
                    : "TUTORIAL CLEAR",

                dialogueData != null
                    ? dialogueData.TutorialClearSubtitle
                    : "꿈빛 에너지가 코어로 돌아왔습니다",

                clearBannerDuration);

            if (clearBannerDuration > 0f)
            {
                yield return new WaitForSeconds(clearBannerDuration);
            }

            // 적 정화 직후의 두 대사는 2D 가이드 패널이 아니라
            // 3D 장난감 친구가 다시 등장해 직접 말합니다.
            if (toyFriend != null)
            {
                yield return toyFriend.ShowForStory();
            }

            TutorialDialogueLine clearLine =
                dialogueData != null
                    ? dialogueData.TutorialClearLine
                    : null;

            yield return PlayToyFriendOnlyLine(
                clearLine,
                "잘했어! 이런 식으로 오염된 장난감을 정화하면서 코어를 지키면 돼.",
                3f,
                true);

            TutorialDialogueLine stage1Line =
                dialogueData != null
                    ? dialogueData.Stage1StartLine
                    : null;

            yield return PlayToyFriendOnlyLine(
                stage1Line,
                "준비됐지? 이제 진짜 공격이 시작될 거야. 코어를 끝까지 지켜줘!",
                3.2f,
                false);

            if (toyFriend != null)
            {
                yield return toyFriend.HideForCombat();
            }

            if (waveStartDelay > 0f)
            {
                yield return new WaitForSeconds(waveStartDelay);
            }

            transitionToWaveRoutine = null;
            State = TutorialStage1State.Wave1;

            // Stage1WaveController의 Started 이벤트가 발생하면서
            // 포탈 A와 Road_1이 등장합니다.
            stage1.StartStage1();
        }


        private IEnumerator PlayToyFriendOnlyLine(
            TutorialDialogueLine line,
            string fallbackMessage,
            float fallbackDuration,
            bool celebratory)
        {
            string speaker =
                line != null && !string.IsNullOrWhiteSpace(line.Speaker)
                    ? line.Speaker
                    : "장난감 친구";

            string message =
                line != null && !string.IsNullOrWhiteSpace(line.Message)
                    ? line.Message
                    : fallbackMessage;

            float duration =
                line != null
                    ? Mathf.Max(
                        0.2f,
                        line.VoiceClip != null
                            ? Mathf.Max(line.Duration, line.VoiceClip.length)
                            : line.Duration)
                    : Mathf.Max(0.2f, fallbackDuration);

            missionUI?.HideTransientMessages();

            if (toyFriend != null)
            {
                toyFriend.Speak(
                    message,
                    duration,
                    celebratory,
                    line != null ? line.VoiceClip : null);
            }
            else
            {
                // 3D 친구가 씬에서 누락된 경우에만 2D 대화창으로 폴백합니다.
                missionUI?.ShowDialogue(
                    speaker,
                    message,
                    duration);
            }

            yield return new WaitForSeconds(duration);
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
                ShowGuideLine(
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
                missionUI?.ShowQuickGuide(
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
            RoleSynergyProgression.Unlock();


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

            roleSelection?.Hide();
            toyFriend?.StopSpeaking();

            flowRoutine = null;
            transitionToWaveRoutine = null;
            stage1CompletionRoutine = null;
            postShootingStoryRoutine = null;
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


            int localHits = 0;

            if (hitCountsByPlayer.TryGetValue(
                    "LOCAL",
                    out int count))
            {
                localHits = count;
            }
            else if (expectedPlayerCount <= 1)
            {
                foreach (int value in hitCountsByPlayer.Values)
                {
                    localHits = Mathf.Max(localHits, value);
                }
            }


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
                $"협동 제작: {second}");
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
