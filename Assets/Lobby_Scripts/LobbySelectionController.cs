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

    [SerializeField]
    private TMP_Text lobbyStatusText;

    [SerializeField]
    private TMP_Text readyButtonText;

    [SerializeField]
    private TMP_Text connectedPlayerText;

    [Header("플레이어 상태 목록 연결")]
    [Tooltip(
        "오른쪽 플레이어 상태 목록을 관리하는 " +
        "LobbyPlayerStatusUI를 연결합니다."
    )]
    [SerializeField]
    private LobbyPlayerStatusUI playerStatusUI;

    [Header("인원 설정")]
    [Tooltip("게임에 접속할 수 있는 최대 플레이어 수입니다.")]
    [SerializeField, Range(1, 8)]
    private int maximumPlayerCount = 8;

    [Header("전원 준비 완료 후 카운트다운")]
    [Tooltip("모든 플레이어가 준비를 마치면 이 시간(초) 후 자동으로 맵으로 이동합니다.")]
    [SerializeField, Min(1f)]
    private float readyCountdownSeconds = 30f;

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

    private void Awake()
    {
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
    }

    private void Start()
    {
        maximumPlayerCount = Mathf.Clamp(maximumPlayerCount, 1, 8);
        UpdateUI();
    }

    private void Update()
    {
        // 아직 네트워크 접속 전이면 할 일이 없다.
        if (roomManager == null || roomManager.Runner == null) return;

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
        if (roomManager == null || roomManager.Runner == null) return null;

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
    /// 현재 직업, Ready 상태를 UI에 반영한다. (카운트다운 중에는 문구를 건드리지 않는다)
    /// </summary>
    private void UpdateUI()
    {
        var state = GetLocalState();
        bool hasJob = state != null && state.HasSelectedJob;
        PlayerJob job = hasJob ? state.SelectedJob : PlayerJob.Police;
        bool ready = state != null && state.IsReady;

        UpdateSelectedJobText(hasJob, job);
        UpdateJobButtons(hasJob, job, ready);

        if (!isCountdownActive)
        {
            UpdateReadyUI(hasJob, ready);
        }
    }

    /// <summary>
    /// 현재 선택한 직업 이름을 중앙 UI에 표시한다.
    /// </summary>
    private void UpdateSelectedJobText(bool hasJob, PlayerJob job)
    {
        if (selectedJobText == null)
        {
            return;
        }

        selectedJobText.text =
            $"선택한 직업: {(hasJob ? GetJobName(job) : "없음")}";
    }

    /// <summary>
    /// Ready 버튼과 안내 문구를 현재 상태에 맞게 변경한다.
    /// </summary>
    private void UpdateReadyUI(bool hasJob, bool ready)
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
                ready ? "준비 취소" : "준비";
        }
    }

    /// <summary>
    /// 직업 버튼의 활성화 상태와 색상을 갱신한다.
    /// </summary>
    private void UpdateJobButtons(bool hasJob, PlayerJob selectedJob, bool isReady)
    {
        ApplyJobButtonState(policeButton, PlayerJob.Police, hasJob, selectedJob, isReady);
        ApplyJobButtonState(firefighterButton, PlayerJob.Firefighter, hasJob, selectedJob, isReady);
        ApplyJobButtonState(chefButton, PlayerJob.Chef, hasJob, selectedJob, isReady);
        ApplyJobButtonState(builderButton, PlayerJob.Builder, hasJob, selectedJob, isReady);
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

        bool isSelected = hasJob && selectedJob == buttonJob;

        button.interactable = !isReady;

        ColorBlock colors = button.colors;

        colors.normalColor =
            isSelected ? selectedButtonColor : normalButtonColor;

        colors.highlightedColor =
            isSelected ? selectedButtonColor : highlightedButtonColor;

        colors.pressedColor = pressedButtonColor;

        colors.selectedColor =
            isSelected ? selectedButtonColor : normalButtonColor;

        colors.disabledColor =
            isSelected ? selectedButtonColor : disabledButtonColor;

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
        if (playerStatusUI == null) return;

        var runner = roomManager.Runner;
        int index = 0;

        foreach (var player in runner.ActivePlayers)
        {
            var playerObject = runner.GetPlayerObject(player);
            var state = playerObject != null
                ? playerObject.GetComponent<LobbyPlayerState>()
                : null;

            string jobName = state != null && state.HasSelectedJob
                ? GetJobName(state.SelectedJob)
                : "직업 없음";

            bool ready = state != null && state.IsReady;

            playerStatusUI.SetPlayerStatus(
                index,
                $"플레이어 {index + 1}",
                jobName,
                ready,
                true
            );

            index++;
        }

        for (int i = index; i < maximumPlayerCount && i < 8; i++)
        {
            playerStatusUI.RemovePlayerStatus(i);
        }

        if (connectedPlayerText != null)
        {
            connectedPlayerText.text =
                $"연결된 플레이어: {index} / {maximumPlayerCount}";
        }
    }

    /// <summary>
    /// 접속한 모든 플레이어가 준비를 마쳤는지 확인하고,
    /// 그렇다면 카운트다운을 진행하다가 시간이 다 되면 맵으로 이동한다.
    /// 누군가 준비를 취소하거나 나가면 카운트다운을 취소한다.
    /// </summary>
    private void UpdateReadyCountdown()
    {
        bool allReady = AreAllPlayersReady(out int playerCount);

        if (allReady && !isCountdownActive)
        {
            isCountdownActive = true;
            countdownRemaining = readyCountdownSeconds;
        }
        else if (!allReady && isCountdownActive)
        {
            isCountdownActive = false;
        }

        if (!isCountdownActive) return;

        countdownRemaining -= Time.deltaTime;

        if (lobbyStatusText != null)
        {
            int secondsLeft = Mathf.Max(0, Mathf.CeilToInt(countdownRemaining));
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
    /// 접속한 플레이어가 1명 이상이고, 전원의 IsReady가 true인지 확인한다.
    /// </summary>
    private bool AreAllPlayersReady(out int playerCount)
    {
        playerCount = 0;
        int readyCount = 0;

        var runner = roomManager.Runner;

        foreach (var player in runner.ActivePlayers)
        {
            var playerObject = runner.GetPlayerObject(player);
            var state = playerObject != null
                ? playerObject.GetComponent<LobbyPlayerState>()
                : null;

            if (state == null) continue;

            playerCount++;

            if (state.IsReady)
            {
                readyCount++;
            }
        }

        return playerCount > 0 && playerCount == readyCount;
    }

    /// <summary>
    /// 코드 내부 직업 값을 화면에 표시할 한글로 변환한다.
    /// </summary>
    private string GetJobName(PlayerJob job)
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

    private void OnDestroy()
    {
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
    }
}
