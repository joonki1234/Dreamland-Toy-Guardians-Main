using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비에서 현재 플레이어의 직업 선택과 준비 상태를 관리한다.
///
/// 이제는 네트워크 연동 버전이다. 실제 선택/준비 값은 로컬 플레이어의
/// LobbyPlayerState(네트워크 오브젝트)에 저장되고, 화면에는 그 값을 그대로
/// 반영한다. 오른쪽 플레이어 목록도 실제로 접속한 모든 플레이어를 보여준다.
///
/// 전원이 준비를 마치면 자동으로 카운트다운을 시작하고,
/// 다 되면 RoomManager.LoadGameplayScene()을 호출해 Dreamland_map_3로 이동한다.
/// </summary>
public class LobbySelectionController : MonoBehaviour
{
    [Header("네트워크 연결")]
    [Tooltip("접속/스폰을 담당하는 RoomManager를 연결합니다.")]
    [SerializeField]
    private RoomManager roomManager;

    [Header("직업 선택 버튼")]
    [SerializeField]
    private Button policeButton;

    [SerializeField]
    private Button firefighterButton;

    [SerializeField]
    private Button chefButton;

    [SerializeField]
    private Button builderButton;

    [Header("준비 버튼")]
    [SerializeField]
    private Button readyButton;

    [Header("상태 표시 글자")]
    [SerializeField]
    private TMP_Text selectedJobText;

    // Toyfriend 말풍선에 표시되는 직업 설명
    [SerializeField]
    private TMP_Text jobDescriptionText;

    [SerializeField]
    private TMP_Text lobbyStatusText;

    [SerializeField]
    private TMP_Text readyButtonText;

    [SerializeField]
    private TMP_Text connectedPlayerText;

    [Header("장난감 친구 Ready 반응")]
    [SerializeField]
    private Image toyfriendPortrait;

    [SerializeField]
    private Sprite readyPortraitSprite;

    [Header("장난감 친구 목소리")]
    [Tooltip(
        "직업 설명 문구가 바뀔 때 재생할 동물의숲 스타일 중얼거림 보이스입니다. " +
        "map_3(게임플레이 맵)의 장난감 친구와 같은 목소리로 통일하려면 " +
        "AnimaleseVoicePlayer의 voiceName/pitchRange/syllableInterval 설정도 동일하게 맞추세요."
    )]
    [SerializeField]
    private AnimaleseVoicePlayer robotVoice;

    [Tooltip("문구가 바뀔 때 중얼거림을 재생할 시간(초)입니다.")]
    [SerializeField, Min(0.1f)]
    private float robotVoiceDuration = 1.4f;

    private string lastSpokenDescription;

    [Header("플레이어 상태 목록 연결")]
    [Tooltip(
        "오른쪽 플레이어 상태 목록을 관리하는 " +
        "LobbyPlayerStatusUI를 연결합니다."
    )]
    [SerializeField]
    private LobbyPlayerStatusUI playerStatusUI;

    [Header("난이도 선택")]
    [Tooltip("난이도를 한 단계 낮추는(하 방향) 화살표 버튼")]
    [SerializeField]
    private Button difficultyLeftArrowButton;

    [Tooltip("난이도를 한 단계 높이는(상 방향) 화살표 버튼")]
    [SerializeField]
    private Button difficultyRightArrowButton;

    [Tooltip("현재 난이도(하/중/상)를 보여줄 텍스트")]
    [SerializeField]
    private TMP_Text difficultyText;

    [Header("PC/VR 플레이 모드 선택 (개인별)")]
    [Tooltip("플레이 모드를 VR 쪽으로 넘기는(왼쪽) 화살표 버튼")]
    [SerializeField]
    private Button playModeLeftArrowButton;

    [Tooltip("플레이 모드를 컴퓨터 쪽으로 넘기는(오른쪽) 화살표 버튼")]
    [SerializeField]
    private Button playModeRightArrowButton;

    [Tooltip("현재 플레이 모드(컴퓨터/VR)를 보여줄 텍스트")]
    [SerializeField]
    private TMP_Text playModeText;

    [Header("인원 설정")]
    [Tooltip("게임에 접속할 수 있는 최대 플레이어 수입니다.")]
    [SerializeField, Range(1, 8)]
    private int maximumPlayerCount = 8;

    [Header("전원 준비 완료 후 카운트다운")]
    [Tooltip("모든 플레이어가 준비를 마치면 이 시간(초) 후 자동으로 맵으로 이동합니다.")]
    [SerializeField, Min(1f)]
    private float readyCountdownSeconds = 5f;

    [Header("직업 버튼 색상")]
    [Tooltip("아직 선택되지 않은 직업 버튼의 기본 색상입니다.")]
    [SerializeField]
    private Color normalButtonColor =
        new Color32(235, 235, 235, 255);

    [Tooltip("현재 선택한 직업 버튼에 적용할 강조 색상입니다.")]
    [SerializeField]
    private Color selectedButtonColor =
        new Color32(170, 170, 170, 255);

    [Tooltip("마우스 또는 VR 레이가 버튼 위에 있을 때의 색상입니다.")]
    [SerializeField]
    private Color highlightedButtonColor =
        new Color32(250, 250, 250, 255);

    [Tooltip("버튼을 누르고 있는 순간의 색상입니다.")]
    [SerializeField]
    private Color pressedButtonColor =
        new Color32(145, 145, 145, 255);

    [Tooltip("Ready 완료 후 선택되지 않은 버튼에 적용할 색상입니다.")]
    [SerializeField]
    private Color disabledButtonColor =
        new Color32(190, 190, 190, 140);

    private bool isCountdownActive;
    private float countdownRemaining;
    private Sprite defaultPortraitSprite;
    private ToyFriendDialogueHUD sharedDialogueHud;

    private void Awake()
    {
        sharedDialogueHud = ToyFriendDialogueHUD.GetOrCreate();
        if (sharedDialogueHud != null)
        {
            jobDescriptionText = sharedDialogueHud.DialogueText;
            toyfriendPortrait = sharedDialogueHud.Portrait;
        }

        if (toyfriendPortrait != null)
        {
            defaultPortraitSprite =
                toyfriendPortrait.overrideSprite != null
                    ? toyfriendPortrait.overrideSprite
                    : toyfriendPortrait.sprite;
        }

        if (policeButton != null)
        {
            policeButton.onClick.AddListener(
                () => SelectJob(PlayerJob.Police)
            );
        }

        if (firefighterButton != null)
        {
            firefighterButton.onClick.AddListener(
                () => SelectJob(PlayerJob.Firefighter)
            );
        }

        if (chefButton != null)
        {
            chefButton.onClick.AddListener(
                () => SelectJob(PlayerJob.Chef)
            );
        }

        if (builderButton != null)
        {
            builderButton.onClick.AddListener(
                () => SelectJob(PlayerJob.Builder)
            );
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(ToggleReady);
        }

        if (difficultyLeftArrowButton != null)
        {
            difficultyLeftArrowButton.onClick.AddListener(() => StepDifficulty(-1));
        }

        if (difficultyRightArrowButton != null)
        {
            difficultyRightArrowButton.onClick.AddListener(() => StepDifficulty(1));
        }

        if (playModeLeftArrowButton != null)
        {
            playModeLeftArrowButton.onClick.AddListener(() => StepPlayMode(-1));
        }

        if (playModeRightArrowButton != null)
        {
            playModeRightArrowButton.onClick.AddListener(() => StepPlayMode(1));
        }
    }

    private void Start()
    {
        maximumPlayerCount = Mathf.Clamp(maximumPlayerCount, 1, 8);
        UpdateConnectedPlayerCount();
        UpdateUI();
    }

    private void Update()
    {
        // 접속 전에도 인원수는 0으로 갱신한다.
        if (roomManager == null || roomManager.Runner == null)
        {
            UpdateConnectedPlayerCount();
            return;
        }

        UpdateUI();
        UpdatePlayerStatusList();
        UpdateReadyCountdown();
    }

    /// <summary>
    /// 로컬 플레이어의 네트워크 로비 상태를 가져온다.
    /// 아직 스폰되지 않았다면 null을 반환한다.
    /// </summary>
    private LobbyPlayerState GetLocalState()
    {
        if (roomManager == null || roomManager.Runner == null)
        {
            return null;
        }

        var runner = roomManager.Runner;
        var playerObject = runner.GetPlayerObject(runner.LocalPlayer);

        return playerObject != null
            ? playerObject.GetComponent<LobbyPlayerState>()
            : null;
    }

    /// <summary>
    /// 플레이어가 누른 직업을 로컬 플레이어의 네트워크 상태에 저장한다.
    ///
    /// 이미 선택된 직업을 다시 누르면 선택을 취소한다.
    /// Ready 완료 후에는 직업을 변경하거나 취소할 수 없다.
    /// </summary>
    private void SelectJob(PlayerJob job)
    {
        var state = GetLocalState();

        if (state == null || state.IsReady)
        {
            return;
        }

        if (state.HasSelectedJob && state.SelectedJob == job)
        {
            state.ClearJob();
        }
        else
        {
            state.SetJob(job);
        }
    }

    /// <summary>
    /// Ready와 Ready 취소 상태를 전환한다.
    /// 직업을 선택하지 않았다면 Ready할 수 없다.
    /// </summary>
    private void ToggleReady()
    {
        var state = GetLocalState();

        if (state == null)
        {
            return;
        }

        if (!state.HasSelectedJob)
        {
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text =
                    "먼저 직업을 선택해주세요";
            }

            return;
        }

        state.SetReady(!state.IsReady);
    }

    /// <summary>
    /// 현재 직업, Ready 상태를 UI에 반영한다.
    /// 카운트다운 중에는 Ready 안내 문구를 건드리지 않는다.
    /// </summary>
    private void UpdateUI()
    {
        var state = GetLocalState();

        bool hasJob =
            state != null &&
            state.HasSelectedJob;

        PlayerJob job =
            hasJob
                ? state.SelectedJob
                : PlayerJob.Police;

        bool ready =
            state != null &&
            state.IsReady;

        UpdateSelectedJobText(
            hasJob,
            job,
            ready
        );

        UpdateJobButtons(
            hasJob,
            job,
            ready
        );

        if (!isCountdownActive)
        {
            UpdateReadyUI(
                hasJob,
                ready
            );
        }

        UpdateDifficultyUI();
        UpdatePlayModeUI(state, ready);
    }

    /// <summary>
    /// 화살표를 눌렀을 때 난이도를 한 단계 옮긴다.
    /// direction은 -1(하 방향) 또는 1(상 방향)이다. 하/상 끝에서는
    /// 그 이상 넘어가지 않고 멈춘다(순환 안 함).
    /// </summary>
    private void StepDifficulty(int direction)
    {
        if (roomManager == null)
        {
            return;
        }

        GameDifficultyState difficultyState =
            roomManager.GetOrFindDifficultyState();

        // difficultyState는 FindAnyObjectByType으로 "찾아지긴" 했지만 Fusion이
        // 아직 [Networked] 값을 읽을 수 있는 상태로 완전히 붙여놓기 전인 짧은
        // 순간이 있다(참가자 접속 직후 실제로 재현된 InvalidOperationException:
        // "Error when accessing GameDifficultyState.CurrentDifficulty..."). 그
        // 틈에 읽으면 예외가 나서 난이도 조작이 그 프레임에서 통째로 끊긴다.
        if (difficultyState == null || !difficultyState.IsReady)
        {
            return;
        }

        int nextValue =
            Mathf.Clamp(
                (int)difficultyState.CurrentDifficulty + direction,
                0,
                2
            );

        difficultyState.RequestSetDifficulty((GameDifficulty)nextValue);
    }

    /// <summary>
    /// 현재 난이도 텍스트와 화살표 버튼의 활성/비활성 상태를 갱신한다.
    /// 전원 준비 완료 카운트다운이 시작되면(곧 맵으로 넘어가면) 더 이상
    /// 바꿀 수 없도록 잠근다 - 방 전체가 공유하는 값이라 그 시점 이후
    /// 바뀌면 다른 플레이어와 혼란스러울 수 있다.
    /// </summary>
    private void UpdateDifficultyUI()
    {
        GameDifficultyState difficultyState =
            roomManager != null
                ? roomManager.GetOrFindDifficultyState()
                : null;

        // FindAnyObjectByType으로는 찾아졌지만 Fusion이 아직 [Networked] 값을
        // 읽을 준비가 안 된 상태일 수 있다(실제로 재현된
        // InvalidOperationException 참고 - GameDifficultyState.IsReady 참조).
        // 이 프레임에는 "연결 중" 텍스트를 보여주고 다음 프레임에 다시 시도한다.
        bool difficultyReady =
            difficultyState != null && difficultyState.IsReady;

        GameDifficulty difficulty =
            difficultyReady
                ? difficultyState.CurrentDifficulty
                : GameDifficulty.Medium;

        if (difficultyText != null)
        {
            difficultyText.text =
                difficultyReady
                    ? $"난이도: {GetDifficultyName(difficulty)}"
                    : "난이도: 중 (연결 중...)";
        }

        bool locked = isCountdownActive || !difficultyReady;

        if (difficultyLeftArrowButton != null)
        {
            difficultyLeftArrowButton.interactable =
                !locked && difficulty != GameDifficulty.Easy;
        }

        if (difficultyRightArrowButton != null)
        {
            difficultyRightArrowButton.interactable =
                !locked && difficulty != GameDifficulty.Hard;
        }
    }

    /// <summary>
    /// 난이도 값을 화면에 표시할 한글로 변환한다.
    /// </summary>
    private string GetDifficultyName(GameDifficulty difficulty)
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return "하";

            case GameDifficulty.Hard:
                return "상";

            default:
                return "중";
        }
    }

    /// <summary>
    /// 화살표를 눌렀을 때 내 플레이 모드(컴퓨터/VR)를 전환한다.
    /// 방 전체가 공유하는 난이도와 달리 로컬 플레이어 개인의 LobbyPlayerState에 저장된다.
    /// PlayMode는 VR=0, PC=1이라 direction -1(왼쪽)은 VR 방향, 1(오른쪽)은 컴퓨터 방향이다.
    /// 양 끝에서는 순환하지 않고 멈춘다.
    /// </summary>
    private void StepPlayMode(int direction)
    {
        var state = GetLocalState();

        if (state == null || state.IsReady)
        {
            return;
        }

        int nextValue =
            Mathf.Clamp(
                (int)state.SelectedPlayMode + direction,
                0,
                1
            );

        state.SetPlayMode((PlayMode)nextValue);
    }

    /// <summary>
    /// 현재 플레이 모드 텍스트와 화살표 버튼의 활성/비활성 상태를 갱신한다.
    /// Ready 완료 후에는 더 이상 바꿀 수 없도록 잠근다.
    /// </summary>
    private void UpdatePlayModeUI(LobbyPlayerState state, bool ready)
    {
        PlayMode playMode =
            state != null
                ? state.SelectedPlayMode
                : PlayMode.VR;

        if (playModeText != null)
        {
            playModeText.text =
                state != null
                    ? $"모드: {GetPlayModeName(playMode)}"
                    : "모드: VR (연결 중...)";
        }

        bool locked = ready || state == null;

        if (playModeLeftArrowButton != null)
        {
            playModeLeftArrowButton.interactable =
                !locked && playMode != PlayMode.VR;
        }

        if (playModeRightArrowButton != null)
        {
            playModeRightArrowButton.interactable =
                !locked && playMode != PlayMode.PC;
        }
    }

    /// <summary>
    /// 플레이 모드 값을 화면에 표시할 한글로 변환한다.
    /// </summary>
    private string GetPlayModeName(PlayMode mode)
    {
        return mode == PlayMode.PC ? "컴퓨터" : "VR";
    }

    /// <summary>
    /// 현재 선택한 직업 이름과 Toyfriend의 직업 설명을 표시한다.
    /// </summary>
    private void UpdateSelectedJobText(
        bool hasJob,
        PlayerJob job,
        bool ready
    )
    {
        // 기존 선택 직업 표시
        if (selectedJobText != null)
        {
            selectedJobText.text =
                $"선택한 직업: {(hasJob ? GetJobName(job) : "없음")}";
        }

        // 공통 HUD 이전에는 JobDescriptionPanel이 비활성인 동안 별도 Text가
        // 배경 설명에 영향을 줄 수 없었습니다. 같은 진행 경계를 그대로 복원합니다.
        if (!LobbyContactController.IsJobDialoguePhase)
        {
            return;
        }

        // 새로 추가한 Toyfriend 말풍선 설명
        if (jobDescriptionText != null)
        {
            string description;

            if (ready)
            {
                description = "좋은 선택이야~!";
            }
            else if (hasJob)
            {
                description = GetJobDescription(job);
            }
            else
            {
                description = "궁금한 직업을 선택해봐!";
            }

            jobDescriptionText.text = description;

            // Update()에서 매 프레임 호출되므로, 문구가 실제로 바뀌었을 때만
            // 중얼거림 보이스를 재생한다 (매 프레임 재생되는 것을 방지).
            if (robotVoice != null &&
                jobDescriptionText.gameObject.activeInHierarchy &&
                description != lastSpokenDescription)
            {
                lastSpokenDescription = description;
                robotVoice.PlayForText(description, 0.055f);
            }
        }

        if (toyfriendPortrait != null)
        {
            Sprite targetPortraitSprite =
                ready && readyPortraitSprite != null
                    ? readyPortraitSprite
                    : defaultPortraitSprite;

            if (toyfriendPortrait.overrideSprite !=
                targetPortraitSprite)
            {
                toyfriendPortrait.overrideSprite =
                    targetPortraitSprite;
            }
        }
    }

    /// <summary>
    /// Ready 버튼과 안내 문구를 현재 상태에 맞게 변경한다.
    /// </summary>
    private void UpdateReadyUI(
        bool hasJob,
        bool ready
    )
    {
        if (!hasJob)
        {
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text =
                    "직업을 선택해주세요";
            }

            if (readyButton != null)
            {
                readyButton.interactable = false;
            }
        }
        else if (!ready)
        {
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text =
                    "준비 버튼을 눌러주세요";
            }

            if (readyButton != null)
            {
                readyButton.interactable = true;
            }
        }
        else
        {
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text =
                    "준비 완료 - 다른 플레이어를 기다리는 중...";
            }

            if (readyButton != null)
            {
                readyButton.interactable = true;
            }
        }

        if (readyButtonText != null)
        {
            readyButtonText.text =
                ready
                    ? "준비 취소"
                    : "준비";
        }
    }

    /// <summary>
    /// 직업 버튼의 활성화 상태와 색상을 갱신한다.
    /// </summary>
    private void UpdateJobButtons(
        bool hasJob,
        PlayerJob selectedJob,
        bool isReady
    )
    {
        ApplyJobButtonState(
            policeButton,
            PlayerJob.Police,
            hasJob,
            selectedJob,
            isReady
        );

        ApplyJobButtonState(
            firefighterButton,
            PlayerJob.Firefighter,
            hasJob,
            selectedJob,
            isReady
        );

        ApplyJobButtonState(
            chefButton,
            PlayerJob.Chef,
            hasJob,
            selectedJob,
            isReady
        );

        ApplyJobButtonState(
            builderButton,
            PlayerJob.Builder,
            hasJob,
            selectedJob,
            isReady
        );
    }

    /// <summary>
    /// 직업 버튼 하나에 선택 여부와 Ready 상태를 적용한다.
    /// </summary>
    private void ApplyJobButtonState(
        Button button,
        PlayerJob buttonJob,
        bool hasJob,
        PlayerJob selectedJob,
        bool isReady
    )
    {
        if (button == null)
        {
            return;
        }

        bool isSelected =
            hasJob &&
            selectedJob == buttonJob;

        button.interactable = !isReady;

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            isSelected
                ? selectedButtonColor
                : normalButtonColor;

        colors.highlightedColor =
            isSelected
                ? selectedButtonColor
                : highlightedButtonColor;

        colors.pressedColor =
            pressedButtonColor;

        colors.selectedColor =
            isSelected
                ? selectedButtonColor
                : normalButtonColor;

        colors.disabledColor =
            isSelected
                ? selectedButtonColor
                : disabledButtonColor;

        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;

        button.colors = colors;
    }

    /// <summary>
    /// 실제로 접속한 모든 플레이어의 직업/준비 상태를
    /// 오른쪽 플레이어 상태 목록에 반영한다.
    /// </summary>
    private void UpdatePlayerStatusList()
    {
        UpdateConnectedPlayerCount();

        if (playerStatusUI == null)
        {
            return;
        }

        var runner =
            roomManager.Runner;

        int index = 0;

        foreach (var player in runner.ActivePlayers)
        {
            var playerObject =
                runner.GetPlayerObject(player);

            var state =
                playerObject != null
                    ? playerObject.GetComponent<LobbyPlayerState>()
                    : null;

            string jobName =
                state != null &&
                state.HasSelectedJob
                    ? GetJobName(state.SelectedJob)
                    : "직업 없음";

            bool ready =
                state != null &&
                state.IsReady;

            playerStatusUI.SetPlayerStatus(
                index,
                $"플레이어 {index + 1}",
                jobName,
                ready,
                true
            );

            index++;
        }

        for (
            int i = index;
            i < maximumPlayerCount && i < 8;
            i++
        )
        {
            playerStatusUI.RemovePlayerStatus(i);
        }

    }

    private void UpdateConnectedPlayerCount()
    {
        if (connectedPlayerText == null)
        {
            return;
        }

        if (roomManager == null ||
            roomManager.Runner == null ||
            !roomManager.Runner.IsRunning)
        {
            connectedPlayerText.text = $"0/{maximumPlayerCount}";
            return;
        }

        int count = 0;

        foreach (var player in roomManager.Runner.ActivePlayers)
        {
            count++;
        }

        connectedPlayerText.text = $"{count}/{maximumPlayerCount}";
    }

    /// <summary>
    /// 접속한 모든 플레이어가 준비를 마쳤는지 확인하고,
    /// 그렇다면 카운트다운을 진행하다가 시간이 다 되면 맵으로 이동한다.
    /// 누군가 준비를 취소하거나 나가면 카운트다운을 취소한다.
    /// </summary>
    private void UpdateReadyCountdown()
    {
        bool allReady =
            AreAllPlayersReady(
                out int playerCount
            );

        if (
            allReady &&
            !isCountdownActive
        )
        {
            isCountdownActive = true;
            countdownRemaining =
                readyCountdownSeconds;
        }
        else if (
            !allReady &&
            isCountdownActive
        )
        {
            isCountdownActive = false;
            countdownRemaining = readyCountdownSeconds;
        }

        if (!isCountdownActive)
        {
            return;
        }

        countdownRemaining -=
            Time.deltaTime;

        if (lobbyStatusText != null)
        {
            int secondsLeft =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        countdownRemaining
                    )
                );

            lobbyStatusText.text =
                $"모든 플레이어 준비 완료! {secondsLeft}초 후 맵으로 이동합니다";
        }

        if (countdownRemaining <= 0f)
        {
            isCountdownActive = false;

            roomManager.LoadGameplayScene();
        }
    }

    /// <summary>
    /// 접속한 플레이어가 1명 이상이고,
    /// 전원의 IsReady가 true인지 확인한다.
    /// </summary>
    private bool AreAllPlayersReady(
        out int playerCount
    )
    {
        playerCount = 0;
        int readyCount = 0;

        var runner =
            roomManager.Runner;

        foreach (
            var player in runner.ActivePlayers
        )
        {
            var playerObject =
                runner.GetPlayerObject(player);

            var state =
                playerObject != null
                    ? playerObject.GetComponent<LobbyPlayerState>()
                    : null;

            if (state == null)
            {
                continue;
            }

            playerCount++;

            if (state.IsReady)
            {
                readyCount++;
            }
        }

        return
            playerCount > 0 &&
            playerCount == readyCount;
    }

    /// <summary>
    /// 코드 내부 직업 값을 화면에 표시할 한글로 변환한다.
    /// </summary>
    private string GetJobName(
        PlayerJob job
    )
    {
        switch (job)
        {
            case PlayerJob.Police:
                return "경찰";

            case PlayerJob.Firefighter:
                return "소방관";

            case PlayerJob.Chef:
                return "요리사";

            case PlayerJob.Builder:
                return "건축가";

            default:
                return "없음";
        }
    }

    /// <summary>
    /// Toyfriend가 말풍선에서 보여줄
    /// 직업별 간단한 설명을 반환한다.
    /// </summary>
    private string GetJobDescription(PlayerJob job)
    {
        switch (job)
        {
            case PlayerJob.Police:
                return
                    "테이저건으로 먼 적을\n" +
                    "빠르고 정확하게 공격해!";

            case PlayerJob.Firefighter:
                return
                    "물줄기를 계속 뿜어\n" +
                    "적에게 지속 피해를 줘!";

            case PlayerJob.Chef:
                return
                    "후라이팬으로 여러 요리를\n" +
                    "포물선으로 던져 공격해!";

            case PlayerJob.Builder:
                return
                    "여러 탄환을 퍼뜨려 공격해!\n" +
                    "적과 가까울수록 더 강해!";

            default:
                return "";
        }
    }

    private void OnDestroy()
    {
        if (robotVoice != null)
        {
            robotVoice.StopBabble();
        }

        if (policeButton != null)
        {
            policeButton.onClick.RemoveAllListeners();
        }

        if (firefighterButton != null)
        {
            firefighterButton.onClick.RemoveAllListeners();
        }

        if (chefButton != null)
        {
            chefButton.onClick.RemoveAllListeners();
        }

        if (builderButton != null)
        {
            builderButton.onClick.RemoveAllListeners();

        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
        }

        if (difficultyLeftArrowButton != null)
        {
            difficultyLeftArrowButton.onClick.RemoveAllListeners();
        }

        if (difficultyRightArrowButton != null)
        {
            difficultyRightArrowButton.onClick.RemoveAllListeners();
        }

        if (playModeLeftArrowButton != null)
        {
            playModeLeftArrowButton.onClick.RemoveAllListeners();
        }

        if (playModeRightArrowButton != null)
        {
            playModeRightArrowButton.onClick.RemoveAllListeners();
        }
    }
}
