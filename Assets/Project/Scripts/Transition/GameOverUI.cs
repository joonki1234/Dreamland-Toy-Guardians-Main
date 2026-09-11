using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 시 화면 정중앙에 "GAME OVER" 문구와 RETRY 버튼을 띄우는 오버레이입니다.
///
/// DreamlandGameFlowController.OnStateChanged를 구독해서 GameFlowState.GameOver로
/// 바뀌는 순간 자동으로 표시됩니다. 필요한 UI(Canvas/Text/Button)를 전부 이
/// 스크립트가 코드에서 직접 만들기 때문에, 씬에 빈 GameObject 하나를 만들고
/// 이 스크립트만 붙이면(그리고 원한다면 Game Flow Controller를 Inspector에
/// 연결하면) 별도 프리팹/캔버스 세팅 없이 바로 동작합니다.
///
/// RenderMode.ScreenSpaceOverlay를 쓰기 때문에 이 프로젝트의 다른 HUD들이
/// 겪었던 "Camera.main이 항상 null이라 화면에 안 그려짐" 문제와 아예
/// 무관합니다(카메라 참조가 필요 없는 렌더 모드).
/// </summary>
[DisallowMultipleComponent]
public sealed class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField]
    private DreamlandGameFlowController gameFlowController;

    [Tooltip("RETRY를 누르면 이동할 씬 이름입니다.")]
    [SerializeField]
    private string retrySceneName = "StartScene";

    [Tooltip("비워두면 TextMeshPro 기본 폰트를 사용합니다. 예쁜 커스텀 폰트가 " +
        "있다면 TMP Font Asset을 만들어 여기에 연결하세요.")]
    [SerializeField]
    private TMP_FontAsset titleFont;

    private GameObject rootPanel;
    private bool retryRequested;

    private void Awake()
    {
        if (gameFlowController == null)
        {
            // 기본 FindAnyObjectByType<T>()는 비활성 오브젝트를 못 찾는다 -
            // 이 프로젝트에서 여러 번 발목을 잡았던 것과 동일한 함정이라
            // 처음부터 Include로 찾는다.
            gameFlowController =
                FindAnyObjectByType<DreamlandGameFlowController>(
                    FindObjectsInactive.Include);
        }

        BuildUI();
        rootPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
            gameFlowController.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(
        DreamlandGameFlowController.GameFlowState newState)
    {
        if (newState == DreamlandGameFlowController.GameFlowState.GameOver)
        {
            Show();
        }
    }

    /// <summary>
    /// 외부(테스트 버튼 등)에서 강제로 게임 오버 화면을 띄우고 싶을 때 사용합니다.
    /// </summary>
    [ContextMenu("테스트 - 게임 오버 화면 표시")]
    public void Show()
    {
        if (rootPanel == null)
        {
            BuildUI();
        }

        retryRequested = false;
        rootPanel.SetActive(true);
    }

    public void Hide()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    // =====================================================================
    // UI 생성
    // =====================================================================

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("GameOverCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 다른 HUD/배너보다 항상 위에 그려지도록 정렬 순서를 크게 잡는다.
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        rootPanel = new GameObject("Panel");
        rootPanel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = rootPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 살짝 어둡게 깔아서 GAME OVER 문구가 더 또렷하게 보이도록 한다.
        Image dim = rootPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        BuildTitleText(rootPanel.transform);
        BuildRetryButton(rootPanel.transform);
    }

    private void BuildTitleText(Transform parent)
    {
        GameObject textObject = new GameObject("GameOverText");
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 70f);
        textRect.sizeDelta = new Vector2(1400f, 320f);

        TextMeshProUGUI titleText = textObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "GAME OVER";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 130f;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 60f;
        titleText.fontSizeMax = 150f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;

        if (titleFont != null)
        {
            titleText.font = titleFont;
        }

        // 흰색 채우기 + 검정 테두리. TMP는 outlineWidth/outlineColor를 설정하면
        // 알아서 머티리얼 인스턴스를 만들어 적용해준다(원본 폰트 애셋 공유 머티리얼은
        // 건드리지 않는다).
        titleText.outlineWidth = 0.25f;
        titleText.outlineColor = Color.black;

        // 살짝 그림자를 더해 어떤 배경 위에서도 잘 읽히게 한다.
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(4f, -4f);
    }

    private void BuildRetryButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("RetryButton");
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -100f);
        buttonRect.sizeDelta = new Vector2(340f, 96f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.45f, 1f);
        colors.pressedColor = new Color(0.85f, 0.68f, 0.28f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(HandleRetryClicked);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "RETRY";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 40f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.12f, 0.12f, 0.16f, 1f);

        if (titleFont != null)
        {
            label.font = titleFont;
        }
    }

    // =====================================================================
    // Retry
    // =====================================================================

    private void HandleRetryClicked()
    {
        if (retryRequested)
        {
            return;
        }

        retryRequested = true;
        StartCoroutine(RetryRoutine());
    }

    /// <summary>
    /// Fusion 세션에 연결돼 있다면 씬을 넘기기 전에 먼저 정리해서, StartScene으로
    /// 돌아간 뒤 새로 접속할 때 이전 세션이 남아있는 상태로 꼬이지 않게 한다.
    /// </summary>
    private IEnumerator RetryRoutine()
    {
        RoomManager roomManager =
            FindAnyObjectByType<RoomManager>(
                FindObjectsInactive.Include);

        NetworkRunner runner =
            roomManager != null ? roomManager.Runner : null;

        if (runner != null && runner.IsRunning)
        {
            var shutdownTask = runner.Shutdown();

            while (!shutdownTask.IsCompleted)
            {
                yield return null;
            }
        }

        SceneManager.LoadScene(retrySceneName);
    }
}
