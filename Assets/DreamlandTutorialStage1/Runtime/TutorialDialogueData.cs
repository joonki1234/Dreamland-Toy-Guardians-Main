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
        [Tooltip("나중에 음성을 연결할 때 사용할 선택 항목입니다.")]
        [SerializeField] private AudioClip voiceClip;

        public string Speaker => speaker;
        public string Message => message;
        public float Duration => Mathf.Max(0.2f, duration);
        public AudioClip VoiceClip => voiceClip;

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
        [SerializeField] private string missionStartTitle = "TUTORIAL START";
        [SerializeField] private string missionStartSubtitle = "오염된 장난감을 정화해보세요";
        [SerializeField, Min(0.2f)] private float missionStartDuration = 2f;

        [Header("튜토리얼 시작 대사 - 위에서부터 순서대로 재생")]
        [SerializeField] private List<TutorialDialogueLine> introLines = new List<TutorialDialogueLine>();

        [Header("직업 소개 및 8인 선택")]
        [SerializeField] private List<TutorialDialogueLine> roleIntroductionLines =
            new List<TutorialDialogueLine>();
        [SerializeField] private TutorialDialogueLine roleSelectionPromptLine =
            new TutorialDialogueLine();
        [SerializeField] private TutorialDialogueLine roleSelectionCompleteLine =
            new TutorialDialogueLine();

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

        [Header("2차 공격 종료 - 시너지 해금")]
        [SerializeField] private string synergyUnlockTitle = "SYNERGY UNLOCK";
        [SerializeField] private string synergyUnlockSubtitle = "직업의 힘이 서로 연결됩니다";
        [SerializeField] private List<TutorialDialogueLine> synergyUnlockLines =
            new List<TutorialDialogueLine>();

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
        public IReadOnlyList<TutorialDialogueLine> RoleIntroductionLines =>
            roleIntroductionLines;
        public TutorialDialogueLine RoleSelectionPromptLine =>
            roleSelectionPromptLine;
        public TutorialDialogueLine RoleSelectionCompleteLine =>
            roleSelectionCompleteLine;
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
        public string SynergyUnlockTitle => synergyUnlockTitle;
        public string SynergyUnlockSubtitle => synergyUnlockSubtitle;
        public IReadOnlyList<TutorialDialogueLine> SynergyUnlockLines =>
            synergyUnlockLines;
        public string WaveClearTitle => waveClearTitle;
        public string WaveClearSubtitle => waveClearSubtitle;
        public string CoreUpgradeTitle => coreUpgradeTitle;
        public string CoreUpgradeSubtitle => coreUpgradeSubtitle;
        public TutorialDialogueLine CoreUpgradeLine => coreUpgradeLine;
        public string MissionFailedTitle => missionFailedTitle;
        public string MissionFailedSubtitle => missionFailedSubtitle;

        public void ResetToPrototypeDefaults()
        {
            missionStartTitle = "TUTORIAL START";
            missionStartSubtitle = "오염된 장난감을 정화해보세요";
            missionStartDuration = 2f;

            introLines = new List<TutorialDialogueLine>
            {
                new TutorialDialogueLine(
                    "장난감 친구",
                    "다행이다... 드디어 현실과 연결됐어.",
                    2.6f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "너희가 현실에서 온 아이들이구나. 부탁이 있어.",
                    3.2f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "우리 꿈나라가 악몽 바이러스에 오염되고 있어.",
                    3f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "오염된 장난감들이 꿈빛 코어를 빼앗아 포탈을 닫으려 해.",
                    3.5f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "포탈이 닫히면 꿈나라는 현실과 영원히 단절돼.",
                    3.2f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "나와 함께 코어를 지켜줄래?",
                    2.6f)
            };

            roleIntroductionLines = new List<TutorialDialogueLine>
            {
                new TutorialDialogueLine(
                    "장난감 친구",
                    "너희 여덟 명은 두 명씩 한 팀이 되어 네 가지 직업을 맡게 될 거야.",
                    4f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "경찰은 위험을 빠르게 찾아 막고, 소방관은 위급한 순간에 모두를 지켜줘.",
                    4.5f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "요리사는 동료가 계속 싸울 수 있게 돕고, 건축가는 안전한 길과 방어를 만들어.",
                    4.5f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "지금은 각 직업이 무엇을 잘하는지만 기억해 둬. 힘을 연결하는 방법은 전투 중에 알려줄게!",
                    4.5f)
            };

            roleSelectionPromptLine = new TutorialDialogueLine(
                "장난감 친구",
                "이제 원하는 직업을 골라 줘. 한 직업에는 정확히 두 명씩 들어가야 해!",
                3.8f);

            roleSelectionCompleteLine = new TutorialDialogueLine(
                "장난감 친구",
                "좋아, 여덟 명 모두 준비됐어! 각자의 역할을 기억하고 서로 가까이 움직여 줘.",
                3.8f);

            enemyAppearsLine = new TutorialDialogueLine(
                "장난감 친구",
                "고마워. 먼저 오염된 장난감을 정화하는 방법을 알려줄게.",
                3f);

            shootingObjective = "튜토리얼 몬스터를 명중";
            shootingInstructionLine = new TutorialDialogueLine(
                "장난감 친구",
                "앞에 나타난 장난감을 조준해서 세 번 공격해 봐!",
                2.5f);

            synergyObjective = "두 가지 직업 시너지를 발동";
            synergyInstructionLine = new TutorialDialogueLine(
                "장난감 친구",
                "직업 시너지는 아직 잠겨 있어. 2차 공격을 막아내면 코어가 힘을 열어 줄 거야!",
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
                "좋았어! 이제 오염된 장난감을 정화하면서 꿈빛 코어를 끝까지 지켜줘!",
                3.5f);

            waveStartTitle = "WAVE 1 START";
            waveStartSubtitle = "꿈빛 코어를 지켜라";
            waveObjective = "Stage 1 · 코어 방어";

            afterFirstGroupLine = new TutorialDialogueLine(
                "장난감 친구",
                "악몽 바이러스가 현실을 침식하기 시작했어. 다음 공격을 준비해!",
                3.5f);
            beforeFinalGroupLine = new TutorialDialogueLine(
                "장난감 친구",
                "균열이 더 커졌어. 악몽이 빠르게 퍼지고 있어. 조금만 더 버텨!",
                3.5f);

            synergyUnlockTitle = "SYNERGY UNLOCK";
            synergyUnlockSubtitle = "직업의 힘이 서로 연결됩니다";
            synergyUnlockLines = new List<TutorialDialogueLine>
            {
                new TutorialDialogueLine(
                    "장난감 친구",
                    "코어에 꿈빛이 충분히 모였어! 이제 서로 다른 직업의 힘을 연결할 수 있어.",
                    4.2f),
                new TutorialDialogueLine(
                    "장난감 친구",
                    "동료의 행동에 맞춰 직업 능력을 이어 써 봐. 혼자일 때보다 훨씬 강한 효과가 나타날 거야!",
                    4.5f)
            };

            waveClearTitle = "WAVE 1 CLEAR";
            waveClearSubtitle = "꿈빛 코어 방어 성공";

            coreUpgradeTitle = "CORE UPGRADE";
            coreUpgradeSubtitle = "꿈빛 코어가 무기를 강화합니다";
            coreUpgradeLine = new TutorialDialogueLine(
                "장난감 친구",
                "해냈어! 코어가 되찾은 꿈빛으로 무기를 강화하고 있어!",
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

            roleIntroductionLines ??= new List<TutorialDialogueLine>();
            synergyUnlockLines ??= new List<TutorialDialogueLine>();
        }
    }
}
