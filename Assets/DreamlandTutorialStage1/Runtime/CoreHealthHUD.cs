using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    /// <summary>
    /// 코어 체력을 화면 상단 중앙에 고정 표시하는 Sci-Fi HUD입니다.
    /// Strategic Warfare의 상태 패널 + Sci-Fi GUI Skin 게이지를 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreState))]
    public sealed class CoreHealthHUD : MonoBehaviour
    {
        private static Font runtimeFont;

        private CoreState core;
        private Canvas canvas;
        private GameObject canvasObject;
        private GameObject panel;
        private Text healthText;
        private Text labelText;
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

            panel = CreatePanel(
                "CoreStatusPanel",
                root,
                DreamlandUiSkin.KenneyCoreBarBlue,
                new Vector2(0.5f, 1f),
                new Vector2(560f, 118f),
                new Vector2(0f, -10f),
                new Vector2(0.5f, 1f));

            // The shell is only a layout root. The visible Kenney bar is built below.
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, 0f);

            RectTransform panelRect = panel.GetComponent<RectTransform>();

            labelText = CreateText(
                "CoreLabel",
                panelRect,
                "코어 상태",
                new Vector2(0f, 0.72f),
                new Vector2(250f, 34f),
                22,
                TextAnchor.MiddleLeft,
                new Vector2(46f, 0f),
                new Vector2(0f, 0.5f));
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = new Color(0.76f, 0.98f, 0.97f, 1f);

            healthText = CreateText(
                "CoreHP",
                panelRect,
                "100 / 100",
                new Vector2(1f, 0.72f),
                new Vector2(172f, 36f),
                26,
                TextAnchor.MiddleRight,
                new Vector2(-34f, 0f),
                new Vector2(1f, 0.5f));
            healthText.fontStyle = FontStyle.Bold;
            healthText.color = new Color(1f, 0.99f, 1f, 1f);

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
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;

            return panelObject;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            Vector2 anchor,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 pivot)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = GetRuntimeFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(15, fontSize - 7);
            text.resizeTextMaxSize = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Font GetRuntimeFont()
        {
            if (runtimeFont != null)
            {
                return runtimeFont;
            }

            string[] preferredFonts =
            {
                "Malgun Gothic",
                "Noto Sans CJK KR",
                "Noto Sans KR",
                "Apple SD Gothic Neo",
                "Arial"
            };

            try
            {
                runtimeFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 28);
            }
            catch
            {
                runtimeFont = null;
            }

            runtimeFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return runtimeFont;
        }
    }
}
