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

    /// <summary>
    /// 씬에 이 컴포넌트를 수동으로 배치하지 않아도 항상 존재하도록 자동 생성한다.
    /// 예전에는 "빈 오브젝트 만들고 이 스크립트를 붙여달라"고 에디터 작업을
    /// 안내했는데, 실제로 씬에 추가되지 않은 채로 남아있어서 코어가 0이
    /// 돼도(DreamlandGameFlowController.OnStateChanged가 GameOver로 바뀌어도)
    /// 그걸 들을 컴포넌트 자체가 존재하지 않아 게임오버 화면이 계속 안 떴다.
    ///
    /// [RuntimeInitializeOnLoadMethod]는 앱이 켜질 때(=Lobby/StartScene)
    /// 딱 한 번만 실행되고, 이후 SceneManager.LoadScene()으로 실제 게임플레이
    /// 맵(Dreamland_map_3)으로 넘어갈 때는 다시 실행되지 않는다 - 그래서
    /// 그 방식 대신, 게임플레이 맵에만 있는 DreamlandGameFlowController.Awake()
    /// 에서 이 메서드를 직접 호출한다(그 컴포넌트는 씬이 로드될 때마다
    /// 항상 Awake()가 실행된다).
    /// </summary>
    public static void EnsureInstanceExists()
    {
        if (FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject autoObject = new GameObject("GameOverUI (Auto)");
        autoObject.AddComponent<GameOverUI>();

        Debug.Log(
            "[GameOverUI] 씬에 배치되어 있지 않아 자동으로 생성했습니다.");
    }

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

    // 이 컴포넌트는 항상 코드로만 생성되므로(EnsureInstanceExists 참고) Inspector에서
    // titleFont를 미리 연결해 둘 방법이 없다. 그래서 FireHoseController가 SFX를
    // Resources.Load로 불러오는 것과 같은 패턴으로, Assets/Resources/Fonts에
    // 미리 복사해 둔 TMP Font Asset(Righteous - 둥글고 두꺼운 디스플레이 폰트)을
    // 코드에서 직접 불러온다. titleFont를 수동으로 연결해 두면 그 값이 우선한다.
    private static TMP_FontAsset cachedTitleFont;

    private TMP_FontAsset ResolveTitleFont()
    {
        if (titleFont != null)
        {
            return titleFont;
        }

        if (cachedTitleFont == null)
        {
            cachedTitleFont = Resources.Load<TMP_FontAsset>("Fonts/GameOverTitle SDF");
        }

        return cachedTitleFont;
    }

    private void BuildTitleText(Transform parent)
    {
        TMP_FontAsset resolvedFont = ResolveTitleFont();
        Vector2 anchoredPosition = new Vector2(0f, 70f);
        Vector2 sizeDelta = new Vector2(1400f, 320f);

        // 메인 글자 뒤에 어두운 색 복제본을 대각선으로 여러 겹 쌓아서 두꺼운
        // "압출(extrusion)" 느낌을 만든다. 형제 오브젝트는 먼저 추가된 것이
        // 아래쪽(뒤)에 그려지므로, 이 레이어들을 메인 텍스트보다 먼저 만든다.
        BuildTitleExtrusionLayers(parent, anchoredPosition, sizeDelta, resolvedFont);

        GameObject textObject = new GameObject("GameOverText");
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = sizeDelta;

        TextMeshProUGUI titleText = textObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "GAME OVER";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 130f;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 60f;
        titleText.fontSizeMax = 150f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;

        if (resolvedFont != null)
        {
            titleText.font = resolvedFont;
        }

        // 흰색 채우기 + 검정 테두리. TMP는 outlineWidth/outlineColor를 설정하면
        // 알아서 머티리얼 인스턴스를 만들어 적용해준다(원본 폰트 애셋 공유 머티리얼은
        // 건드리지 않는다).
        titleText.outlineWidth = 0.3f;
        titleText.outlineColor = Color.black;

        // TMP Underlay로 글자 자체에 부드럽게 번지는 그림자를 추가해 입체감을
        // 더한다(뒤에 쌓은 압출 레이어와는 별개로, 안티에일리어싱된 부드러운
        // 그림자를 준다). 기본 TMP SDF 셰이더는 UNDERLAY_ON 키워드만 켜주면
        // 바로 지원한다.
        Material titleMaterial = titleText.fontMaterial;
        titleMaterial.EnableKeyword("UNDERLAY_ON");
        titleMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
        titleMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
        titleMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
        titleMaterial.SetFloat("_UnderlayDilate", 0.35f);
        titleMaterial.SetFloat("_UnderlaySoftness", 0.4f);

        // UI Shadow 컴포넌트도 같이 둬서 화면 배경이 밝을 때도 잘 읽히게 한다.
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(4f, -4f);
    }

    /// <summary>
    /// GAME OVER 글자 뒤에 어두운 색으로 살짝씩 어긋난 복제본을 여러 겹
    /// 쌓아서, 실제 3D 지오메트리 없이도 두께가 있는 압출(extrusion) 글자처럼
    /// 보이게 한다.
    /// </summary>
    private static void BuildTitleExtrusionLayers(
        Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, TMP_FontAsset font)
    {
        const int layerCount = 6;
        Color extrusionColor = new Color(0.07f, 0.05f, 0.05f, 1f);

        for (int i = layerCount; i >= 1; i--)
        {
            GameObject layerObject = new GameObject("GameOverText_Extrusion_" + i);
            layerObject.transform.SetParent(parent, false);

            RectTransform layerRect = layerObject.AddComponent<RectTransform>();
            layerRect.anchorMin = new Vector2(0.5f, 0.5f);
            layerRect.anchorMax = new Vector2(0.5f, 0.5f);
            layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.anchoredPosition = anchoredPosition + new Vector2(i * 2.2f, -i * 2.2f);
            layerRect.sizeDelta = sizeDelta;

            TextMeshProUGUI layerText = layerObject.AddComponent<TextMeshProUGUI>();
            layerText.text = "GAME OVER";
            layerText.alignment = TextAlignmentOptions.Center;
            layerText.fontSize = 130f;
            layerText.enableAutoSizing = true;
            layerText.fontSizeMin = 60f;
            layerText.fontSizeMax = 150f;
            layerText.fontStyle = FontStyles.Bold;
            layerText.color = extrusionColor;
            layerText.raycastTarget = false;

            if (font != null)
            {
                layerText.font = font;
            }
        }
    }

    private void BuildRetryButton(Transform parent)
    {
        TMP_FontAsset resolvedFont = ResolveTitleFont();
        Vector2 anchoredPosition = new Vector2(0f, -100f);
        Vector2 sizeDelta = new Vector2(340f, 96f);

        // 버튼 뒤에 어두운 그림자 사각형을 살짝 아래-오른쪽으로 겹쳐서,
        // 버튼이 패널 위에 떠 있는 것처럼 입체감을 준다.
        GameObject shadowObject = new GameObject("RetryButtonShadow");
        shadowObject.transform.SetParent(parent, false);

        RectTransform shadowRect = shadowObject.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.anchoredPosition = anchoredPosition + new Vector2(6f, -6f);
        shadowRect.sizeDelta = sizeDelta;

        Image shadowImage = shadowObject.AddComponent<Image>();
        shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
        shadowImage.raycastTarget = false;

        GameObject buttonObject = new GameObject("RetryButton");
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = sizeDelta;

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

        // 버튼 자체에도 옅은 그림자를 줘서 패널 위에 얹혀 있는 느낌을 더한다.
        Shadow buttonShadow = buttonObject.AddComponent<Shadow>();
        buttonShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        buttonShadow.effectDistance = new Vector2(2f, -2f);

        // 위쪽은 밝게, 아래쪽은 어둡게 얇은 띠를 깔아 베벨(bevel) 느낌을 준다 -
        // 실제 3D 형상 없이 UI Image만으로 눌린 버튼 같은 입체감을 흉내낸다.
        BuildButtonBevelStrip(buttonObject.transform, atTop: true, color: new Color(1f, 1f, 1f, 0.55f));
        BuildButtonBevelStrip(buttonObject.transform, atTop: false, color: new Color(0f, 0f, 0f, 0.35f));

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

        if (resolvedFont != null)
        {
            label.font = resolvedFont;
        }
    }

    /// <summary>
    /// 버튼 위쪽/아래쪽 가장자리에 얇고 반투명한 띠를 깔아 베벨(bevel)처럼
    /// 보이게 하는 보조 오브젝트를 만든다. Raycast Target을 꺼 둬서 버튼
    /// 클릭 판정에는 영향을 주지 않는다(EventSystem은 자식이 맞아도 부모의
    /// Button까지 자동으로 찾아 올라가므로 클릭 자체는 항상 정상 동작한다).
    /// </summary>
    private static void BuildButtonBevelStrip(Transform buttonTransform, bool atTop, Color color)
    {
        GameObject stripObject = new GameObject(atTop ? "BevelHighlight" : "BevelShade");
        stripObject.transform.SetParent(buttonTransform, false);

        RectTransform stripRect = stripObject.AddComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
        stripRect.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
        stripRect.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
        stripRect.sizeDelta = new Vector2(0f, 8f);
        stripRect.anchoredPosition = Vector2.zero;

        Image stripImage = stripObject.AddComponent<Image>();
        stripImage.color = color;
        stripImage.raycastTarget = false;
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
