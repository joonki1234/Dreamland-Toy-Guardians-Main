using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비에서 현재 플레이어의 직업 선택과 준비 상태를 관리한다.
///
/// 현재는 네트워크가 없는 PC 테스트용 로컬 방식이다.
/// 추후 네트워크 담당 코드에서 직업 선택, Ready 상태,
/// 현재 접속 인원 값을 동기화할 수 있도록 구성한다.
/// </summary>
public class LobbySelectionController : MonoBehaviour
{
    /// <summary>
    /// 로비에서 선택 가능한 직업 종류.
    /// None은 아직 직업을 선택하지 않은 상태다.
    /// </summary>
    private enum LobbyJob
    {
        None,
        Police,
        Firefighter,
        Chef,
        Builder
    }

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

    [Tooltip(
        "오른쪽 상태 목록에 표시할 현재 플레이어 이름입니다. " +
        "현재는 로컬 테스트용입니다."
    )]
    [SerializeField]
    private string localPlayerName = "플레이어 1";

    [Header("접속 인원 테스트")]
    [Tooltip(
        "현재 에디터에서 테스트할 접속 인원입니다. " +
        "나중에는 네트워크의 실제 접속 인원으로 변경합니다."
    )]
    [SerializeField, Range(1, 8)]
    private int currentPlayerCount = 1;

    [Tooltip("게임에 접속할 수 있는 최대 플레이어 수입니다.")]
    [SerializeField, Range(1, 8)]
    private int maximumPlayerCount = 8;

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

    private LobbyJob selectedJob = LobbyJob.None;
    private bool isReady;

    private void Awake()
    {
        if (policeButton != null)
        {
            policeButton.onClick.AddListener(
                () => SelectJob(LobbyJob.Police)
            );
        }

        if (firefighterButton != null)
        {
            firefighterButton.onClick.AddListener(
                () => SelectJob(LobbyJob.Firefighter)
            );
        }

        if (chefButton != null)
        {
            chefButton.onClick.AddListener(
                () => SelectJob(LobbyJob.Chef)
            );
        }

        if (builderButton != null)
        {
            builderButton.onClick.AddListener(
                () => SelectJob(LobbyJob.Builder)
            );
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(ToggleReady);
        }
    }

    private void Start()
    {
        selectedJob = LobbyJob.None;
        isReady = false;

        maximumPlayerCount = Mathf.Clamp(
            maximumPlayerCount,
            1,
            8
        );

        currentPlayerCount = Mathf.Clamp(
            currentPlayerCount,
            1,
            maximumPlayerCount
        );

        UpdateUI();
    }

    /// <summary>
    /// 플레이어가 누른 직업을 현재 선택 직업으로 저장한다.
    ///
    /// 이미 선택된 직업을 다시 누르면 선택을 취소한다.
    /// Ready 완료 후에는 직업을 변경하거나 취소할 수 없다.
    /// </summary>
    private void SelectJob(LobbyJob job)
    {
        if (isReady)
        {
            return;
        }

        if (selectedJob == job)
        {
            selectedJob = LobbyJob.None;
        }
        else
        {
            selectedJob = job;
        }

        UpdateUI();
    }

    /// <summary>
    /// Ready와 Ready 취소 상태를 전환한다.
    /// 직업을 선택하지 않았다면 Ready할 수 없다.
    /// </summary>
    private void ToggleReady()
    {
        if (selectedJob == LobbyJob.None)
        {
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text =
                    "먼저 직업을 선택해주세요";
            }

            return;
        }

        isReady = !isReady;

        UpdateUI();
    }

    /// <summary>
    /// 현재 직업, Ready 상태, 접속 인원을 UI에 반영한다.
    /// </summary>
    private void UpdateUI()
    {
        UpdateSelectedJobText();
        UpdateReadyUI();
        UpdateJobButtons();
        UpdateConnectedPlayerText();
        UpdateLocalPlayerStatus();
    }

    /// <summary>
    /// 현재 선택한 직업 이름을 중앙 UI에 표시한다.
    /// </summary>
    private void UpdateSelectedJobText()
    {
        if (selectedJobText == null)
        {
            return;
        }

        selectedJobText.text =
            $"선택한 직업: {GetJobName(selectedJob)}";
    }

    /// <summary>
    /// Ready 버튼과 안내 문구를 현재 상태에 맞게 변경한다.
    /// </summary>
    private void UpdateReadyUI()
    {
        if (selectedJob == LobbyJob.None)
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
        else if (!isReady)
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
                    "준비 완료 - 운영자의 시작을 기다리는 중...";
            }

            if (readyButton != null)
            {
                readyButton.interactable = true;
            }
        }

        if (readyButtonText != null)
        {
            readyButtonText.text =
                isReady ? "준비 취소" : "준비";
        }
    }

    /// <summary>
    /// 직업 버튼의 활성화 상태와 색상을 갱신한다.
    /// </summary>
    private void UpdateJobButtons()
    {
        ApplyJobButtonState(
            policeButton,
            LobbyJob.Police
        );

        ApplyJobButtonState(
            firefighterButton,
            LobbyJob.Firefighter
        );

        ApplyJobButtonState(
            chefButton,
            LobbyJob.Chef
        );

        ApplyJobButtonState(
            builderButton,
            LobbyJob.Builder
        );
    }

    /// <summary>
    /// 직업 버튼 하나에 선택 여부와 Ready 상태를 적용한다.
    /// </summary>
    private void ApplyJobButtonState(
        Button button,
        LobbyJob buttonJob
    )
    {
        if (button == null)
        {
            return;
        }

        bool isSelected =
            selectedJob == buttonJob;

        button.interactable = !isReady;

        ColorBlock colors = button.colors;

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
    /// 현재 접속 인원과 최대 인원을 표시한다.
    /// </summary>
    private void UpdateConnectedPlayerText()
    {
        if (connectedPlayerText == null)
        {
            return;
        }

        connectedPlayerText.text =
            $"연결된 플레이어: " +
            $"{currentPlayerCount} / {maximumPlayerCount}";
    }

    /// <summary>
    /// 현재 로컬 플레이어의 직업과 Ready 상태를
    /// 오른쪽 첫 번째 플레이어 상태 줄에 반영한다.
    ///
    /// 최종 네트워크 버전에서는 네트워크 담당 코드가
    /// 각 플레이어의 상태를 직접 전달하게 된다.
    /// </summary>
    private void UpdateLocalPlayerStatus()
    {
        if (playerStatusUI == null)
        {
            return;
        }

        playerStatusUI.SetPlayerStatus(
            0,
            localPlayerName,
            GetStatusJobName(selectedJob),
            isReady,
            true
        );
    }

    /// <summary>
    /// 네트워크 연결 후 실제 접속 인원을 전달할 때 사용할 함수다.
    /// </summary>
    public void SetConnectedPlayerCount(int playerCount)
    {
        currentPlayerCount = Mathf.Clamp(
            playerCount,
            1,
            maximumPlayerCount
        );

        UpdateConnectedPlayerText();
    }

    /// <summary>
    /// 현재 플레이어가 Ready 상태인지 반환한다.
    /// </summary>
    public bool IsReady()
    {
        return isReady;
    }

    /// <summary>
    /// 현재 선택한 직업의 영문 이름을 반환한다.
    /// 아직 선택하지 않았다면 None을 반환한다.
    /// </summary>
    public string GetSelectedJob()
    {
        return selectedJob.ToString();
    }

    /// <summary>
    /// 코드 내부 직업 값을 중앙 UI에 표시할 한글로 변환한다.
    /// </summary>
    private string GetJobName(LobbyJob job)
    {
        switch (job)
        {
            case LobbyJob.Police:
                return "경찰";

            case LobbyJob.Firefighter:
                return "소방관";

            case LobbyJob.Chef:
                return "요리사";

            case LobbyJob.Builder:
                return "건축가";

            default:
                return "없음";
        }
    }

    /// <summary>
    /// 오른쪽 플레이어 상태 목록에 표시할 직업 이름을 반환한다.
    /// 아직 선택하지 않은 경우에는 '직업 없음'으로 표시한다.
    /// </summary>
    private string GetStatusJobName(LobbyJob job)
    {
        if (job == LobbyJob.None)
        {
            return "직업 없음";
        }

        return GetJobName(job);
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