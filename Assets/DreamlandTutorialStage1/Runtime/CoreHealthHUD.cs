using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    /// <summary>
    /// 코어 체력을 화면 상단 중앙에 고정해서 표시합니다.
    /// XR 카메라의 위치/회전에 영향을 받지 않도록 루트 Screen Space Camera Canvas를 사용하고,
    /// 체력바는 Image.fillAmount 대신 RectTransform 폭을 직접 줄여 확실하게 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreState))]
    public sealed class CoreHealthHUD : MonoBehaviour
    {
        private static Font runtimeFont;

        private CoreState core;
        private Canvas canvas;
        private GameObject canvasObject;
        private Text healthText;
        private Image healthFill;
        private RectTransform healthFillRect;

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
            if (core == null)
            {
                return;
            }

            UpdateDisplay(core.CurrentHealth, core.MaxHealth);
        }

        private void UpdateDisplay(float current, float maximum)
        {
            EnsureUI();

            float safeMaximum = Mathf.Max(1f, maximum);
            float normalized = Mathf.Clamp01(current / safeMaximum);

            if (healthText != null)
            {
                healthText.text =
                    $"CORE HP  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(safeMaximum)}";
            }

            // Filled Image는 Sprite/Shader 조합에 따라 fillAmount가 시각적으로
            // 반영되지 않는 경우가 있어 RectTransform의 우측 앵커를 직접 줄입니다.
            if (healthFillRect != null)
            {
                healthFillRect.anchorMax =
                    new Vector2(Mathf.Max(0.0001f, normalized), 1f);
            }

            if (healthFill != null)
            {
                healthFill.enabled = normalized > 0.0001f;
            }
        }

        private void EnsureUI()
        {
            if (canvas != null)
            {
                return;
            }

            canvasObject = new GameObject(
                "CoreHealthHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            // 월드 오브젝트인 코어의 자식으로 두지 않습니다.
            // 이렇게 해야 XR 카메라를 위/아래로 움직여도 HUD 위치가 변하지 않습니다.
            canvasObject.transform.SetParent(null, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = 1500;
            canvas.planeDistance = 0.6f;
            ApplyCamera();

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

            GameObject panel = CreateImageObject(
                "CoreHealthPanel",
                canvasRect,
                new Color(0.02f, 0.035f, 0.07f, 0.84f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -28f);
            panelRect.sizeDelta = new Vector2(460f, 76f);

            healthText = CreateText(
                "CoreHealthText",
                panelRect,
                new Vector2(0.5f, 0.68f),
                new Vector2(420f, 38f),
                27);

            GameObject barBackground = CreateImageObject(
                "HealthBarBackground",
                panelRect,
                new Color(0.10f, 0.10f, 0.13f, 0.95f));

            RectTransform barBackgroundRect =
                barBackground.GetComponent<RectTransform>();

            barBackgroundRect.anchorMin = new Vector2(0.5f, 0.28f);
            barBackgroundRect.anchorMax = new Vector2(0.5f, 0.28f);
            barBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
            barBackgroundRect.anchoredPosition = Vector2.zero;
            barBackgroundRect.sizeDelta = new Vector2(402f, 14f);

            GameObject fillObject = CreateImageObject(
                "HealthBarFill",
                barBackgroundRect,
                new Color(0.20f, 0.95f, 0.58f, 1f));

            healthFillRect = fillObject.GetComponent<RectTransform>();
            healthFillRect.anchorMin = Vector2.zero;
            healthFillRect.anchorMax = Vector2.one;
            healthFillRect.pivot = new Vector2(0f, 0.5f);
            healthFillRect.offsetMin = new Vector2(2f, 2f);
            healthFillRect.offsetMax = new Vector2(-2f, -2f);

            healthFill = fillObject.GetComponent<Image>();
            healthFill.type = Image.Type.Simple;
            healthFill.preserveAspect = false;
        }

        private void ApplyCamera()
        {
            if (canvas == null)
            {
                return;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }

            if (targetCamera != null && canvas.worldCamera != targetCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = targetCamera;
                canvas.planeDistance = 0.6f;
            }
        }


        private static GameObject CreateImageObject(
            string objectName,
            Transform parent,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return imageObject;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            int fontSize)
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
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = GetRuntimeFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static Font GetRuntimeFont()
        {
            if (runtimeFont != null)
            {
                return runtimeFont;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (runtimeFont == null)
            {
                runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return runtimeFont;
        }
    }
}
