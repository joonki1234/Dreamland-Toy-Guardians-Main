using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [Serializable]
    public sealed class TutorialDialogueLine
    {
        [SerializeField] private string speaker = "장난감 친구";
        [SerializeField, TextArea(2, 5)] private string message = "대사를 입력하세요.";
        [SerializeField, Min(0.2f)] private float duration = 3f;

        public string Speaker => speaker;
        public string Message => message;
        public float Duration => Mathf.Max(0.2f, duration);

        public TutorialDialogueLine()
        {
        }

        public TutorialDialogueLine(string lineSpeaker, string lineMessage, float lineDuration)
        {
            speaker = lineSpeaker;
            message = lineMessage;
            duration = lineDuration;
        }
    }

    [CreateAssetMenu(
        fileName = "TutorialDialogueData",
        menuName = "Dreamland/튜토리얼 대사 데이터")]
    public sealed class TutorialDialogueData : ScriptableObject
    {
        [Header("미션 시작 배너")]
        [SerializeField] private string missionStartTitle = "MISSION START";
        [SerializeField] private string missionStartSubtitle = "꿈빛 무기 훈련";
        [SerializeField, Min(0.2f)] private float missionStartDuration = 2f;

        [Header("튜토리얼 시작 대사 - 위에서부터 순서대로 재생")]
        [SerializeField] private List<TutorialDialogueLine> introLines = new List<TutorialDialogueLine>();

        [Header("튜토리얼 적 등장 직전")]
        [SerializeField] private TutorialDialogueLine enemyAppearsLine = new TutorialDialogueLine();

        [Header("명중 연습")]
        [SerializeField] private string shootingObjective = "튜토리얼 몬스터를 명중";
        [SerializeField] private TutorialDialogueLine shootingInstructionLine = new TutorialDialogueLine();

        [Header("직업 시너지 연습")]
        [SerializeField] private string synergyObjective = "두 가지 직업 시너지를 발동";
        [SerializeField] private TutorialDialogueLine synergyInstructionLine = new TutorialDialogueLine();

        [Header("튜토리얼 적 정화")]
        [SerializeField] private string purificationObjective = "모두 함께 악몽을 정화";
        [SerializeField] private string purificationProgress = "몬스터 HP를 0으로 만드세요";
        [SerializeField] private TutorialDialogueLine purificationInstructionLine = new TutorialDialogueLine();

        [Header("튜토리얼 완료")]
        [SerializeField] private string tutorialClearTitle = "TUTORIAL CLEAR";
        [SerializeField] private string tutorialClearSubtitle = "꿈빛 에너지가 코어로 돌아왔습니다";
        [SerializeField, Min(0.2f)] private float tutorialClearDuration = 2f;
        [SerializeField] private TutorialDialogueLine tutorialClearLine = new TutorialDialogueLine();

        [Header("Wave 1 시작")]
        [SerializeField] private string waveStartTitle = "WAVE 1 START";
        [SerializeField] private string waveStartSubtitle = "꿈빛 코어를 지켜라";
        [SerializeField] private string waveObjective = "Stage 1 · 코어 방어";

        [Header("Wave 사이 안내 대사")]
        [SerializeField] private TutorialDialogueLine afterFirstGroupLine = new TutorialDialogueLine();
        [SerializeField] private TutorialDialogueLine beforeFinalGroupLine = new TutorialDialogueLine();

        [Header("Wave 1 완료")]
        [SerializeField] private string waveClearTitle = "WAVE 1 CLEAR";
        [SerializeField] private string waveClearSubtitle = "꿈빛 코어 방어 성공";

        [Header("코어 업그레이드")]
        [SerializeField] private string coreUpgradeTitle = "CORE UPGRADE";
        [SerializeField] private string coreUpgradeSubtitle = "꿈빛 코어가 무기를 강화합니다";
        [SerializeField] private TutorialDialogueLine coreUpgradeLine = new TutorialDialogueLine();

        [Header("실패")]
        [SerializeField] private string missionFailedTitle = "MISSION FAILED";
        [SerializeField] private string missionFailedSubtitle = "꿈빛 코어가 무너졌습니다";

        public string MissionStartTitle => missionStartTitle;
        public string MissionStartSubtitle => missionStartSubtitle;
        public float MissionStartDuration => Mathf.Max(0.2f, missionStartDuration);
        public IReadOnlyList<TutorialDialogueLine> IntroLines => introLines;
        public TutorialDialogueLine EnemyAppearsLine => enemyAppearsLine;
        public string ShootingObjective => shootingObjective;
        public TutorialDialogueLine ShootingInstructionLine => shootingInstructionLine;
        public string SynergyObjective => synergyObjective;
        public TutorialDialogueLine SynergyInstructionLine => synergyInstructionLine;
        public string PurificationObjective => purificationObjective;
        public string PurificationProgress => purificationProgress;
        public TutorialDialogueLine PurificationInstructionLine => purificationInstructionLine;
        public string TutorialClearTitle => tutorialClearTitle;
        public string TutorialClearSubtitle => tutorialClearSubtitle;
        public float TutorialClearDuration => Mathf.Max(0.2f, tutorialClearDuration);
        public TutorialDialogueLine TutorialClearLine => tutorialClearLine;
        public string WaveStartTitle => waveStartTitle;
        public string WaveStartSubtitle => waveStartSubtitle;
        public string WaveObjective => waveObjective;
        public TutorialDialogueLine AfterFirstGroupLine => afterFirstGroupLine;
        public TutorialDialogueLine BeforeFinalGroupLine => beforeFinalGroupLine;
        public string WaveClearTitle => waveClearTitle;
        public string WaveClearSubtitle => waveClearSubtitle;
        public string CoreUpgradeTitle => coreUpgradeTitle;
        public string CoreUpgradeSubtitle => coreUpgradeSubtitle;
        public TutorialDialogueLine CoreUpgradeLine => coreUpgradeLine;
        public string MissionFailedTitle => missionFailedTitle;
        public string MissionFailedSubtitle => missionFailedSubtitle;

        public void ResetToPrototypeDefaults()
        {
            missionStartTitle = "MISSION START";
            missionStartSubtitle = "꿈빛 무기 훈련";
            missionStartDuration = 2f;

            introLines = new List<TutorialDialogueLine>
            {
                new TutorialDialogueLine(
                    "장난감 친구",
                    "꿈나라 수호대에 온 걸 환영해!",
                    2.5f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "저 빛나는 곳이 우리가 지켜야 할 꿈빛 코어야.",
                    3f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "손에 든 꿈빛 무기로 나타나는 악몽을 맞혀보자!",
                    3f)
            };

            enemyAppearsLine = new TutorialDialogueLine(
                "장난감 친구",
                "정면 성 쪽을 봐! 바닥의 작은 균열에서 연습용 악몽이 나타날 거야.",
                3f);

            shootingObjective = "튜토리얼 몬스터를 명중";
            shootingInstructionLine = new TutorialDialogueLine(
                "장난감 친구",
                "조준한 뒤 악몽을 세 번 맞혀봐!",
                2.5f);

            synergyObjective = "두 가지 직업 시너지를 발동";
            synergyInstructionLine = new TutorialDialogueLine(
                "장난감 친구",
                "잘했어! 이제 경찰과 소방관, 천문학자와 건축가의 꿈빛을 각각 이어보자!",
                4f);

            purificationObjective = "모두 함께 악몽을 정화";
            purificationProgress = "몬스터 HP를 0으로 만드세요";
            purificationInstructionLine = new TutorialDialogueLine(
                "장난감 친구",
                "좋아! 이제 모두 함께 악몽을 완전히 정화해보자!",
                3f);

            tutorialClearTitle = "TUTORIAL CLEAR";
            tutorialClearSubtitle = "꿈빛 에너지가 코어로 돌아왔습니다";
            tutorialClearDuration = 2f;
            tutorialClearLine = new TutorialDialogueLine(
                "장난감 친구",
                "완벽해! 이제 몰려오는 악몽들로부터 꿈빛 코어를 지켜줘!",
                3f);

            waveStartTitle = "WAVE 1 START";
            waveStartSubtitle = "꿈빛 코어를 지켜라";
            waveObjective = "Stage 1 · 코어 방어";

            afterFirstGroupLine = new TutorialDialogueLine(
                "장난감 친구",
                "주변의 꿈이 변하고 있어. 다음 공격을 준비해!",
                3f);
            beforeFinalGroupLine = new TutorialDialogueLine(
                "장난감 친구",
                "균열이 더 커졌어. 마지막 공격이 올 거야!",
                3f);

            waveClearTitle = "WAVE 1 CLEAR";
            waveClearSubtitle = "꿈빛 코어 방어 성공";

            coreUpgradeTitle = "CORE UPGRADE";
            coreUpgradeSubtitle = "꿈빛 코어가 무기를 강화합니다";
            coreUpgradeLine = new TutorialDialogueLine(
                "장난감 친구",
                "코어가 되찾은 꿈빛으로 무기를 강화하고 있어!",
                3f);

            missionFailedTitle = "MISSION FAILED";
            missionFailedSubtitle = "꿈빛 코어가 무너졌습니다";
        }

        private void OnValidate()
        {
            missionStartDuration = Mathf.Max(0.2f, missionStartDuration);
            tutorialClearDuration = Mathf.Max(0.2f, tutorialClearDuration);
            if (introLines == null)
            {
                introLines = new List<TutorialDialogueLine>();
            }
        }
    }
}
