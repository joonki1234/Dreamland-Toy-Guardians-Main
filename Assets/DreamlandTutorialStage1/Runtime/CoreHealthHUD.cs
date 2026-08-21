using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    /// <summary>
    /// 코어 체력을 화면 상단 중앙에 고정 표시하는 Sci-Fi HUD입니다.
    /// 로봇 대화창(ToyFriendMapHud)/로비 JobSelectPanel과 같은 Sci-Fi UI 유리 패널 +
    /// 스타트/로비씬과 같은 TextMeshPro 폰트를 사용해 게임 전체와 통일된 느낌을 줍니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreState))]
    public sealed class CoreHealthHUD : MonoBehaviour
    {
        [Tooltip("코어 체력 숫자(예: 100 / 100)에 쓰는 폰트입니다. Assets/Fonts/HSJiptokki-Black SDF를 지정하세요.")]
        [SerializeField] private TMP_FontAsset displayFont;
        [Tooltip("\"코어 상태\" 라벨에 쓰는 폰트입니다. Assets/Fonts/HS두꺼비체 SDF를 지정하세요.")]
        [SerializeField] private TMP_FontAsset bodyFont;
        [Tooltip(
            "로봇 대화창/로비 JobSelectPanel과 같은 반투명 네온 유리 패널 원본입니다. " +
            "Sci-Fi UI 아틀라스의 \"window\" 서브스프라이트(guid 56d84991286850f428b4e7df0cca7380, " +
            "fileID 21300000)를 직접 지정하세요.")]
        [SerializeField] private Sprite glassPanel;

        private CoreState core;
        private Canvas canvas;
        private GameObject canvasObject;
        private GameObject panel;
        private TextMeshProUGUI healthText;
        private TextMeshProUGUI labelText;
        private RectTransform healthFillRect;
        private Image healthFillImage;

        private Camera explicitCamera;
        private bool cameraExplicitlySet;

        private void Awake()
        {
            core = GetComponent<CoreState>();
            EnsureUI();
        }

        private void OnEnable()
        {
            core ??= GetComponent<CoreState>();

            if (core != null)
            {
                core.HealthChanged -= HandleHealthChanged;
                core.HealthChanged += HandleHealthChanged;
            }

            EnsureUI();
            if (canvasObject != null)
            {
                canvasObject.SetActive(true);
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (core != null)
            {
                core.HealthChanged -= HandleHealthChanged;
            }

            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            ApplyCamera();
        }

        private void OnDestroy()
        {
            if (canvasObject != null)
            {
                Destroy(canvasObject);
            }
        }

        /// <summary>
        /// 멀티플레이 환경에서는 씬에 카메라가 여러 개(다른 플레이어 것 포함) 있을 수 있고,
        /// 이 프로젝트의 플레이어 카메라는 MainCamera 태그도 쓰지 않아 Camera.main이
        /// 항상 null이다. 스폰 시점에 "내" 카메라가 확정되면 이 메서드로 명시적으로
        /// 넘겨받아 그 카메라만 계속 사용한다.
        /// </summary>
        public void SetCamera(Camera camera)
        {
            explicitCamera = camera;
            cameraExplicitlySet = camera != null;
            ApplyCamera();
        }

        /// <summary>
        /// CoreHealthHUD는 CoreState.Awake()에서 AddComponent로 붙기 때문에 씬 파일에서
        /// 직접 폰트/패널 필드를 연결할 방법이 없다. AddComponent가 이 컴포넌트의
        /// Awake()(→ EnsureUI())를 즉시 실행시키므로, 여기서는 이미 만들어진 UI에
        /// 테마를 다시 적용한다.
        /// </summary>
        public void Configure(
            TMP_FontAsset newDisplayFont,
            TMP_FontAsset newBodyFont,
            Sprite newGlassPanel)
        {
            displayFont = newDisplayFont;
            bodyFont = newBodyFont;
            glassPanel = newGlassPanel;

            EnsureUI();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (labelText != null && bodyFont != null)
            {
                labelText.font = bodyFont;
            }

            if (healthText != null && displayFont != null)
            {
                healthText.font = displayFont;
            }

            if (panel != null && glassPanel != null)
            {
                Image panelImage = panel.GetComponent<Image>();
                ApplySlicedSprite(panelImage, glassPanel, panel.GetComponent<RectTransform>().sizeDelta);
            }
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            UpdateDisplay(current, maximum);
        }

        private void Refresh()
        {
            if (core != null)
            {
                UpdateDisplay(core.CurrentHealth, core.MaxHealth);
            }
        }

        private void UpdateDisplay(float current, float maximum)
        {
            EnsureUI();

            float safeMax = Mathf.Max(1f, maximum);
            float ratio = Mathf.Clamp01(current / safeMax);

            healthText.text =
                Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(safeMax);

            if (healthFillRect != null)
            {
                healthFillRect.anchorMax = new Vector2(
                    Mathf.Max(0.0001f, ratio),
                    1f);
            }

            if (healthFillImage != null)
            {
                healthFillImage.enabled = ratio > 0f;
                healthFillImage.sprite = ratio <= 0.30f
                    ? DreamlandUiSkin.KenneyCoreBarRed
                    : DreamlandUiSkin.KenneyCoreBarGreen;
                healthFillImage.color = ratio <= 0.30f
                    ? new Color(1f, 0.54f, 0.53f, 0.98f)
                    : new Color(0.62f, 0.95f, 0.86f, 0.96f);
            }

            if (labelText != null)
            {
                labelText.color = ratio <= 0.30f
                    ? new Color(1f, 0.72f, 0.72f, 1f)
                    : new Color(0.76f, 0.98f, 0.97f, 1f);
            }
        }

        private void EnsureUI()
        {
            if (canvas != null)
            {
                return;
            }

            canvasObject = new GameObject(
                "CoreSciFiHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(null, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = 1500;
            canvas.planeDistance = 0.65f;
            ApplyCamera();

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.matchWidthOrHeight = 0.58f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();

            // 로봇 대화창/로비와 같은 유리 패널을 실제로 보이는 배경으로 사용합니다
            // (예전에는 이 패널이 투명한 레이아웃 껍데기였고 실제로 보이는 건 아래 바뿐이었습니다).
            panel = CreatePanel(
                "CoreStatusPanel",
                root,
                glassPanel != null ? glassPanel : DreamlandUiSkin.SciFiWindow,
                new Vector2(0.5f, 1f),
                new Vector2(560f, 118f),
                new Vector2(0f, -10f),
                new Vector2(0.5f, 1f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();

            labelText = CreateTmpText(
                "CoreLabel",
                panelRect,
                "코어 상태",
                new Vector2(0f, 0.72f),
                new Vector2(250f, 34f),
                22,
                TextAlignmentOptions.MidlineLeft,
                bodyFont,
                new Vector2(46f, 0f),
                new Vector2(0f, 0.5f));
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(0.80f, 0.96f, 0.94f, 1f);

            healthText = CreateTmpText(
                "CoreHP",
                panelRect,
                "100 / 100",
                new Vector2(1f, 0.72f),
                new Vector2(172f, 36f),
                26,
                TextAlignmentOptions.MidlineRight,
                displayFont,
                new Vector2(-34f, 0f),
                new Vector2(1f, 0.5f));
            healthText.fontStyle = FontStyles.Bold;
            healthText.color = new Color(0.97f, 0.99f, 1f, 1f);

            GameObject barBg = CreatePanel(
                "CoreBarBackground",
                panelRect,
                DreamlandUiSkin.KenneyCoreBarBlue,
                new Vector2(0.5f, 0.28f),
                new Vector2(430f, 24f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f));

            Image bgImage = barBg.GetComponent<Image>();
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.70f, 0.82f, 0.96f, 0.84f);

            RectTransform bgRect = barBg.GetComponent<RectTransform>();
            GameObject fill = CreatePanel(
                "CoreBarFill",
                bgRect,
                DreamlandUiSkin.KenneyCoreBarGreen,
                new Vector2(0f, 0f),
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 0.5f));

            healthFillRect = fill.GetComponent<RectTransform>();
            healthFillRect.anchorMin = Vector2.zero;
            healthFillRect.anchorMax = Vector2.one;
            healthFillRect.pivot = new Vector2(0f, 0.5f);
            healthFillRect.offsetMin = new Vector2(5f, 5f);
            healthFillRect.offsetMax = new Vector2(-5f, -5f);
            healthFillImage = fill.GetComponent<Image>();
            healthFillImage.type = Image.Type.Sliced;
            healthFillImage.color = new Color(0.62f, 0.95f, 0.86f, 0.96f);
        }

        private void ApplyCamera()
        {
            if (canvas == null)
            {
                return;
            }

            Camera target = explicitCamera;
            if (!cameraExplicitlySet)
            {
                target = Camera.main;
                if (target == null)
                {
                    target = Object.FindAnyObjectByType<Camera>();
                }
            }

            if (target != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = target;
                canvas.planeDistance = 0.65f;
            }
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition,
            Vector2 pivot)
        {
            GameObject panelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            ApplySlicedSprite(image, sprite, size);

            return panelObject;
        }

        /// <summary>
        /// 9-slice 보더가 패널의 실제 표시 크기보다 크면 위/아래(또는 좌/우) 보더끼리
        /// 겹쳐서 패널이 반으로 갈라진 것처럼 깨져 보인다(MissionBannerUI에서 겪었던
        /// 것과 같은 문제). 짧은 변의 40%를 넘으면 pixelsPerUnitMultiplier로 자동 축소한다.
        /// </summary>
        private static void ApplySlicedSprite(Image image, Sprite sprite, Vector2 size)
        {
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;

            if (image.type != Image.Type.Sliced)
            {
                return;
            }

            Vector4 border = sprite.border;
            float largestBorder = Mathf.Max(
                border.x, border.y, border.z, border.w);
            float shortestSide = Mathf.Min(size.x, size.y);
            float safeBorder = shortestSide * 0.4f;

            if (largestBorder > safeBorder && safeBorder > 0f)
            {
                image.pixelsPerUnitMultiplier = largestBorder / safeBorder;
            }
        }

        private static TextMeshProUGUI CreateTmpText(
            string objectName,
            Transform parent,
            string value,
            Vector2 anchor,
            Vector2 size,
            int fontSize,
            TextAlignmentOptions alignment,
            TMP_FontAsset font,
            Vector2 anchoredPosition,
            Vector2 pivot)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(15, fontSize - 7);
            text.fontSizeMax = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }
}
