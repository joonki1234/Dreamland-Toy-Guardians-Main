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
                    ? DreamlandUiSkin.SciFiBarRed
                    : DreamlandUiSkin.SciFiBarGreen;
            }

            if (labelText != null)
            {
                labelText.color = ratio <= 0.30f
                    ? new Color(1f, 0.42f, 0.34f, 1f)
                    : new Color(0.54f, 1f, 0.94f, 1f);
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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();

            panel = CreatePanel(
                "CoreStatusPanel",
                root,
                DreamlandUiSkin.StrategicCoreStrip,
                new Vector2(0.5f, 1f),
                new Vector2(500f, 92f),
                new Vector2(0f, -18f),
                new Vector2(0.5f, 1f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();

            labelText = CreateText(
                "CoreLabel",
                panelRect,
                "CORE STATUS",
                new Vector2(0f, 0.70f),
                new Vector2(215f, 28f),
                18,
                TextAnchor.MiddleLeft,
                new Vector2(92f, 0f),
                new Vector2(0f, 0.5f));
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = new Color(0.54f, 1f, 0.94f, 1f);

            healthText = CreateText(
                "CoreHP",
                panelRect,
                "100 / 100",
                new Vector2(1f, 0.70f),
                new Vector2(145f, 30f),
                21,
                TextAnchor.MiddleRight,
                new Vector2(-22f, 0f),
                new Vector2(1f, 0.5f));
            healthText.fontStyle = FontStyle.Bold;

            GameObject barBg = CreatePanel(
                "CoreBarBackground",
                panelRect,
                DreamlandUiSkin.SciFiBarBackground,
                new Vector2(0.57f, 0.30f),
                new Vector2(355f, 24f),
                Vector2.zero,
                new Vector2(0.5f, 0.5f));

            RectTransform bgRect = barBg.GetComponent<RectTransform>();
            GameObject fill = CreatePanel(
                "CoreBarFill",
                bgRect,
                DreamlandUiSkin.SciFiBarGreen,
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
        }

        private void ApplyCamera()
        {
            if (canvas == null)
            {
                return;
            }

            Camera target = Camera.main;
            if (target == null)
            {
                target = Object.FindAnyObjectByType<Camera>();
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
            text.color = Color.white;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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
