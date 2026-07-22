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
        [SerializeField] private DreamEnemySpawner spawner;
        [SerializeField] private Stage1WaveController stage1;
        [SerializeField] private MissionBannerUI missionUI;
        [SerializeField] private Transform tutorialSpawnPoint;
        [SerializeField] private TutorialDialogueData dialogueData;

        [Header("Tutorial")]
        [SerializeField, Min(1)] private int requiredHitsPerPlayer = 3;
        [SerializeField, Min(1)] private int expectedPlayerCount = 1;
        [SerializeField] private bool requireBothSynergiesBeforePurification;
        [SerializeField] private bool autoStart = true;
        [SerializeField, Min(0f)] private float firstSpawnDelay = 0.8f;
        [SerializeField, Min(0f)] private float waveStartDelay = 2f;

        private readonly Dictionary<string, int> hitCountsByPlayer = new Dictionary<string, int>();
        private EnemyHealth tutorialEnemy;
        private Coroutine flowRoutine;
        private bool emergencySuppressionCompleted;
        private bool starlightBlueprintCompleted;
        private Coroutine stage1CompletionRoutine;
        private bool stage1CompletionEventRaised;

        public TutorialStage1State State { get; private set; } = TutorialStage1State.Idle;
        public int RequiredHitsPerPlayer => requiredHitsPerPlayer;
        public int ExpectedPlayerCount => expectedPlayerCount;
        public TutorialDialogueData DialogueData => dialogueData;

        /// <summary>
        /// Stage 1 전투 완료 후 코어/무기 강화와 완료 연출까지 모두 끝났을 때 발생합니다.
        /// 전체 진행 컨트롤러는 Stage1WaveController.Completed가 아니라 이 이벤트를 구독합니다.
        /// </summary>
        public event Action Stage1Completed;

        private void OnEnable()
        {
            DreamGameEvents.EnemyHit += HandleEnemyHit;
            DreamGameEvents.EnergyAbsorbed += HandleEnergyAbsorbed;
            DreamGameEvents.SynergyTriggered += HandleSynergyTriggered;

            if (stage1 != null)
            {
                stage1.Completed += HandleStage1Completed;
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
            DreamGameEvents.EnemyHit -= HandleEnemyHit;
            DreamGameEvents.EnergyAbsorbed -= HandleEnergyAbsorbed;
            DreamGameEvents.SynergyTriggered -= HandleSynergyTriggered;

            if (stage1 != null)
            {
                stage1.Completed -= HandleStage1Completed;
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
                stage1.Completed -= HandleStage1Completed;
            }

            spawner = enemySpawner;
            stage1 = waveController;
            missionUI = ui;
            tutorialSpawnPoint = spawnPoint;
            expectedPlayerCount = Mathf.Max(1, playerCount);

            if (isActiveAndEnabled && stage1 != null)
            {
                stage1.Completed += HandleStage1Completed;
            }
        }

        public void SetDialogueData(TutorialDialogueData data)
        {
            dialogueData = data;
        }

        public void ApplyStoryDefaultsV8()
        {
            firstSpawnDelay = 0.8f;
            waveStartDelay = 2f;
        }

        public void SetExpectedPlayerCount(int playerCount)
        {
            expectedPlayerCount = Mathf.Max(1, playerCount);
            RefreshShootingProgress();
        }

        public void Begin()
        {
            if (flowRoutine != null || State != TutorialStage1State.Idle)
            {
                return;
            }

            if (spawner == null || stage1 == null)
            {
                Debug.LogError("[Dreamland] 튜토리얼을 시작할 수 없습니다. Spawner 또는 Stage1이 연결되지 않았습니다.");
                return;
            }

            stage1CompletionEventRaised = false;
            flowRoutine = StartCoroutine(BeginRoutine());
        }

        private IEnumerator BeginRoutine()
        {
            State = TutorialStage1State.Intro;

            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.MissionStartTitle : "MISSION START",
                dialogueData != null ? dialogueData.MissionStartSubtitle : "꿈빛 무기 훈련",
                dialogueData != null ? dialogueData.MissionStartDuration : 2f);

            yield return new WaitForSeconds(
                dialogueData != null ? dialogueData.MissionStartDuration : 2f);

            if (dialogueData != null && dialogueData.IntroLines != null)
            {
                foreach (TutorialDialogueLine line in dialogueData.IntroLines)
                {
                    yield return PlayDialogueLine(line);
                }

                yield return PlayDialogueLine(dialogueData.EnemyAppearsLine);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "꿈나라 수호대에 온 걸 환영해! 손에 든 꿈빛 무기로 악몽을 맞혀보자.",
                    3f);
                yield return new WaitForSeconds(3f);
            }

            if (firstSpawnDelay > 0f)
            {
                yield return new WaitForSeconds(firstSpawnDelay);
            }

            PlaceTutorialSpawnInFrontOfCamera();
            tutorialEnemy = spawner.SpawnTutorialEnemy(tutorialSpawnPoint);
            if (tutorialEnemy == null)
            {
                Debug.LogError("[Dreamland] 튜토리얼 몬스터 생성에 실패했습니다.");
                flowRoutine = null;
                yield break;
            }

            tutorialEnemy.SetDamageEnabled(false);
            State = TutorialStage1State.ShootingPractice;

            string objective = dialogueData != null
                ? dialogueData.ShootingObjective
                : "튜토리얼 몬스터를 명중";
            missionUI?.SetObjective($"{objective} ({requiredHitsPerPlayer}회)");
            RefreshShootingProgress();

            if (dialogueData != null)
            {
                ShowDialogueLine(dialogueData.ShootingInstructionLine);
            }

            flowRoutine = null;
        }

        private IEnumerator PlayDialogueLine(TutorialDialogueLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Message))
            {
                yield break;
            }

            missionUI?.ShowDialogue(line.Speaker, line.Message, line.Duration);
            yield return new WaitForSeconds(line.Duration);
        }

        private void ShowDialogueLine(TutorialDialogueLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Message))
            {
                return;
            }

            missionUI?.ShowDialogue(line.Speaker, line.Message, line.Duration);
        }

        private void PlaceTutorialSpawnInFrontOfCamera()
        {
            PrototypeRayWeapon weapon = UnityEngine.Object.FindAnyObjectByType<PrototypeRayWeapon>();
            Camera camera = weapon != null ? weapon.AimCamera : null;

            if (camera == null)
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (Camera candidate in cameras)
                {
                    if (candidate != null && candidate.isActiveAndEnabled)
                    {
                        camera = candidate;
                        break;
                    }
                }
            }

            if (camera == null || tutorialSpawnPoint == null)
            {
                return;
            }

            Transform cameraTransform = camera.transform;
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 desiredPosition = cameraTransform.position + forward * 9f;
            Vector3 groundProbeOrigin = desiredPosition + Vector3.up * 10f;

            if (Physics.Raycast(
                groundProbeOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                30f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                desiredPosition.y = groundHit.point.y + 0.05f;
            }
            else
            {
                desiredPosition.y = cameraTransform.position.y - 1.55f;
            }

            tutorialSpawnPoint.position = desiredPosition;

            Vector3 lookDirection = cameraTransform.position - tutorialSpawnPoint.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                tutorialSpawnPoint.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        private void HandleEnemyHit(EnemyHealth enemy, DamageInfo info)
        {
            if (enemy != tutorialEnemy || State != TutorialStage1State.ShootingPractice)
            {
                return;
            }

            string playerId = string.IsNullOrWhiteSpace(info.playerId) ? "LOCAL" : info.playerId;
            hitCountsByPlayer.TryGetValue(playerId, out int currentCount);
            hitCountsByPlayer[playerId] = Mathf.Min(requiredHitsPerPlayer, currentCount + 1);
            RefreshShootingProgress();

            if (GetCompletedPlayerCount() < expectedPlayerCount)
            {
                return;
            }

            if (requireBothSynergiesBeforePurification)
            {
                State = TutorialStage1State.SynergyPractice;
                missionUI?.SetObjective(
                    dialogueData != null
                        ? dialogueData.SynergyObjective
                        : "두 가지 직업 시너지를 발동");
                RefreshSynergyProgress();

                if (dialogueData != null)
                {
                    ShowDialogueLine(dialogueData.SynergyInstructionLine);
                }
                else
                {
                    missionUI?.ShowDialogue(
                        "장난감 친구",
                        "잘했어! 이제 경찰과 소방관, 천문학자와 건축가의 꿈빛을 각각 이어보자!",
                        4f);
                }

                if (emergencySuppressionCompleted && starlightBlueprintCompleted)
                {
                    EnablePurificationPhase();
                }
            }
            else
            {
                EnablePurificationPhase();
            }
        }

        private void HandleSynergyTriggered(SynergyEventData data)
        {
            if (data.Enemy != tutorialEnemy)
            {
                return;
            }

            if (data.Kind == SynergyKind.EmergencySuppression)
            {
                emergencySuppressionCompleted = true;
            }
            else if (data.Kind == SynergyKind.StarlightBlueprint)
            {
                starlightBlueprintCompleted = true;
            }

            if (State != TutorialStage1State.SynergyPractice)
            {
                return;
            }

            RefreshSynergyProgress();
            if (emergencySuppressionCompleted && starlightBlueprintCompleted)
            {
                EnablePurificationPhase();
            }
        }

        private void EnablePurificationPhase()
        {
            if (tutorialEnemy == null || tutorialEnemy.IsDead)
            {
                return;
            }

            State = TutorialStage1State.PurifyTutorialEnemy;
            tutorialEnemy.RestoreFullHealth();
            tutorialEnemy.SetDamageEnabled(true);
            missionUI?.SetObjective(
                dialogueData != null
                    ? dialogueData.PurificationObjective
                    : "모두 함께 악몽을 정화");
            missionUI?.SetProgress(
                dialogueData != null
                    ? dialogueData.PurificationProgress
                    : "몬스터 HP를 0으로 만드세요");

            if (dialogueData != null)
            {
                ShowDialogueLine(dialogueData.PurificationInstructionLine);
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "좋아! 이제 모두 함께 악몽을 완전히 정화해보자!",
                    3f);
            }
        }

        private void HandleEnergyAbsorbed(EnemyPurification purification, float _)
        {
            if (purification == null || purification.Health != tutorialEnemy)
            {
                return;
            }

            if (State != TutorialStage1State.PurifyTutorialEnemy)
            {
                return;
            }

            State = TutorialStage1State.TutorialClear;
            StartCoroutine(TransitionToWaveRoutine());
        }

        private IEnumerator TransitionToWaveRoutine()
        {
            missionUI?.SetObjective(string.Empty);
            missionUI?.SetProgress(string.Empty);
            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.TutorialClearTitle : "TUTORIAL CLEAR",
                dialogueData != null
                    ? dialogueData.TutorialClearSubtitle
                    : "꿈빛 에너지가 코어로 돌아왔습니다",
                dialogueData != null ? dialogueData.TutorialClearDuration : 2f);

            float transitionDelay = waveStartDelay;
            if (dialogueData != null)
            {
                ShowDialogueLine(dialogueData.TutorialClearLine);
                if (dialogueData.TutorialClearLine != null)
                {
                    transitionDelay = Mathf.Max(transitionDelay, dialogueData.TutorialClearLine.Duration);
                }
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "완벽해! 이제 몰려오는 악몽들로부터 꿈빛 코어를 지켜줘!",
                    3f);
                transitionDelay = Mathf.Max(transitionDelay, 3f);
            }

            yield return new WaitForSeconds(transitionDelay);
            State = TutorialStage1State.Wave1;
            stage1.StartStage1();
        }

        private void HandleStage1Completed()
        {
            if (State != TutorialStage1State.Wave1)
            {
                return;
            }

            if (stage1CompletionRoutine != null || stage1CompletionEventRaised)
            {
                return;
            }

            stage1CompletionRoutine = StartCoroutine(CompleteStage1Routine());
        }

        private IEnumerator CompleteStage1Routine()
        {
            State = TutorialStage1State.Complete;

            const float fallbackDuration = 3f;
            float completionDuration = fallbackDuration;

            missionUI?.ShowBanner(
                dialogueData != null ? dialogueData.CoreUpgradeTitle : "CORE UPGRADE",
                dialogueData != null
                    ? dialogueData.CoreUpgradeSubtitle
                    : "꿈빛 코어가 무기를 강화합니다",
                fallbackDuration);

            if (dialogueData != null)
            {
                ShowDialogueLine(dialogueData.CoreUpgradeLine);

                if (dialogueData.CoreUpgradeLine != null)
                {
                    completionDuration = Mathf.Max(
                        completionDuration,
                        dialogueData.CoreUpgradeLine.Duration);
                }
            }
            else
            {
                missionUI?.ShowDialogue(
                    "장난감 친구",
                    "코어가 되찾은 꿈빛으로 무기를 강화하고 있어!",
                    fallbackDuration);
            }

            // Stage 1의 마무리 연출에 포함되는 내부 요청입니다.
            DreamGameEvents.RequestWeaponUpgrade();

            if (completionDuration > 0f)
            {
                yield return new WaitForSeconds(completionDuration);
            }

            stage1CompletionRoutine = null;

            if (stage1CompletionEventRaised)
            {
                yield break;
            }

            stage1CompletionEventRaised = true;
            Debug.Log(
                "[Dreamland] TutorialStage1Director의 Stage1Completed 이벤트를 발생시킵니다.");
            Stage1Completed?.Invoke();
        }

        private void RefreshShootingProgress()
        {
            int completedPlayers = GetCompletedPlayerCount();
            int localHits = hitCountsByPlayer.TryGetValue("LOCAL", out int count) ? count : 0;

            missionUI?.SetProgress(
                expectedPlayerCount <= 1
                    ? $"명중 {localHits} / {requiredHitsPerPlayer}"
                    : $"훈련 완료 {completedPlayers} / {expectedPlayerCount}");
        }

        private void RefreshSynergyProgress()
        {
            string first = emergencySuppressionCompleted ? "완료" : "대기";
            string second = starlightBlueprintCompleted ? "완료" : "대기";
            missionUI?.SetProgress($"긴급 진압: {first}  ·  별빛 설계: {second}");
        }

        private int GetCompletedPlayerCount()
        {
            int completed = 0;
            foreach (int hitCount in hitCountsByPlayer.Values)
            {
                if (hitCount >= requiredHitsPerPlayer)
                {
                    completed++;
                }
            }

            return completed;
        }

        private void OnValidate()
        {
            requiredHitsPerPlayer = Mathf.Max(1, requiredHitsPerPlayer);
            expectedPlayerCount = Mathf.Max(1, expectedPlayerCount);
            firstSpawnDelay = Mathf.Max(0f, firstSpawnDelay);
            waveStartDelay = Mathf.Max(0f, waveStartDelay);
        }
    }
}
