using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class MissionBannerUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera uiCamera;

        [Header("Custom Banner (Optional)")]
        [Tooltip("씬에 배치한 미션 배너의 루트 오브젝트")]
        [SerializeField] private GameObject customBannerRoot;

        [Tooltip("커스텀 배너의 제목 TMP 텍스트")]
        [SerializeField] private TMP_Text customBannerTitle;

        [Tooltip("커스텀 배너의 설명 TMP 텍스트")]
        [SerializeField] private TMP_Text customBannerSubtitle;

        [Header("Timing")]
        [SerializeField, Min(0.2f)] private float defaultBannerDuration = 2f;

        private Canvas canvas;

        // 커스텀 배너가 연결되지 않았을 때만 사용하는 기존 임시 배너
        private GameObject fallbackBannerPanel;
        private Text fallbackBannerTitle;
        private Text fallbackBannerSubtitle;

        private Text dialogueText;
        private Text objectiveText;
        private Text progressText;
        private Text roleText;
        private Coroutine bannerRoutine;
        private Coroutine dialogueRoutine;
        private Coroutine synergyRoutine;
        private static Font runtimeFont;

        private bool HasCustomBanner =>
            customBannerRoot != null &&
            customBannerTitle != null &&
            customBannerSubtitle != null;

        private void Awake()
        {
            EnsureUI();

            if (customBannerRoot != null)
            {
                customBannerRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            DreamGameEvents.SynergyTriggered += HandleSynergy;
        }

        private void OnDisable()
        {
            DreamGameEvents.SynergyTriggered -= HandleSynergy;
        }

        public void Configure(Camera targetCamera)
        {
            uiCamera = targetCamera;

            if (Application.isPlaying)
            {
                EnsureUI();
                ApplyCamera();
            }
        }

        public void ShowBanner(string title, string subtitle = "", float duration = -1f)
        {
            EnsureUI();

            if (bannerRoutine != null)
            {
                StopCoroutine(bannerRoutine);
            }

            bannerRoutine = StartCoroutine(BannerRoutine(
                title,
                subtitle,
                duration > 0f ? duration : defaultBannerDuration));
        }

        public void ShowDialogue(string speaker, string message, float duration = 3f)
        {
            EnsureUI();

            if (dialogueRoutine != null)
            {
                StopCoroutine(dialogueRoutine);
            }

            string prefix = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker + "\n";
            dialogueRoutine = StartCoroutine(DialogueRoutine(prefix + message, duration));
        }

        public void SetObjective(string message)
        {
            EnsureUI();
            objectiveText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : "MISSION  " + message;
            objectiveText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        public void SetProgress(string message)
        {
            EnsureUI();
            progressText.text = message ?? string.Empty;
            progressText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        public void SetRole(PlayerRole role)
        {
            EnsureUI();
            roleText.text = "현재 직업: " + DreamGameText.GetRoleName(role) + "  [1 경찰 / 2 소방관 / 3 천문학자 / 4 건축가]";
        }

        public void ClearPersistentText()
        {
            SetObjective(string.Empty);
            SetProgress(string.Empty);
        }

        private IEnumerator BannerRoutine(string title, string subtitle, float duration)
        {
            if (HasCustomBanner)
            {
                customBannerTitle.text = title ?? string.Empty;
                customBannerSubtitle.text = subtitle ?? string.Empty;
                customBannerRoot.SetActive(true);

                yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

                customBannerRoot.SetActive(false);
            }
            else
            {
                fallbackBannerTitle.text = title ?? string.Empty;
                fallbackBannerSubtitle.text = subtitle ?? string.Empty;
                fallbackBannerPanel.SetActive(true);

                yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

                fallbackBannerPanel.SetActive(false);
            }

            bannerRoutine = null;
        }

        private IEnumerator DialogueRoutine(string message, float duration)
        {
            dialogueText.text = message;
            dialogueText.gameObject.SetActive(true);
            yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
            dialogueText.gameObject.SetActive(false);
            dialogueRoutine = null;
        }

        private void HandleSynergy(SynergyEventData data)
        {
            if (synergyRoutine != null)
            {
                StopCoroutine(synergyRoutine);
            }

            synergyRoutine = StartCoroutine(SynergyRoutine(data));
        }

        private IEnumerator SynergyRoutine(SynergyEventData data)
        {
            string synergyName = DreamGameText.GetSynergyName(data.Kind);
            string pair = DreamGameText.GetRoleName(data.FirstRole) + " + " + DreamGameText.GetRoleName(data.SecondRole);
            progressText.text = "SYNERGY!  " + synergyName + "  ·  " + pair;
            progressText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            progressText.gameObject.SetActive(false);
            synergyRoutine = null;
        }

        private void EnsureUI()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "PrototypeMissionUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            ApplyCamera();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

            // 커스텀 배너가 없을 때만 기존 임시 배너 생성
            if (!HasCustomBanner)
            {
                fallbackBannerPanel = CreatePanel(
                    "BannerPanel",
                    canvasRect,
                    new Vector2(0.5f, 0.58f),
                    new Vector2(720f, 190f),
                    new Color(0.02f, 0.04f, 0.10f, 0.84f));

                RectTransform bannerRect = fallbackBannerPanel.GetComponent<RectTransform>();
                fallbackBannerTitle = CreateText(
                    "BannerTitle",
                    bannerRect,
                    new Vector2(0.5f, 0.64f),
                    new Vector2(680f, 82f),
                    52,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);

                fallbackBannerSubtitle = CreateText(
                    "BannerSubtitle",
                    bannerRect,
                    new Vector2(0.5f, 0.25f),
                    new Vector2(680f, 52f),
                    26,
                    TextAnchor.MiddleCenter,
                    FontStyle.Normal);

                fallbackBannerPanel.SetActive(false);
            }

            dialogueText = CreateText(
                "DialogueText",
                canvasRect,
                new Vector2(0.5f, 0.15f),
                new Vector2(1280f, 150f),
                30,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            dialogueText.color = Color.white;
            AddOutline(dialogueText);
            dialogueText.gameObject.SetActive(false);

            objectiveText = CreateText(
                "ObjectiveText",
                canvasRect,
                new Vector2(0.04f, 0.92f),
                new Vector2(980f, 62f),
                28,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            objectiveText.rectTransform.pivot = new Vector2(0f, 0.5f);
            AddOutline(objectiveText);
            objectiveText.gameObject.SetActive(false);

            progressText = CreateText(
                "ProgressText",
                canvasRect,
                new Vector2(0.5f, 0.82f),
                new Vector2(960f, 62f),
                30,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            AddOutline(progressText);
            progressText.gameObject.SetActive(false);

            roleText = CreateText(
                "RoleText",
                canvasRect,
                new Vector2(0.5f, 0.04f),
                new Vector2(1280f, 46f),
                20,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            roleText.color = new Color(0.85f, 0.95f, 1f, 1f);
            AddOutline(roleText);
            SetRole(PlayerRole.Police);
        }

        private void ApplyCamera()
        {
            if (canvas == null)
            {
                return;
            }

            if (uiCamera == null)
            {
                uiCamera = Camera.main;
            }

            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1.2f;
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Color color)
        {
            GameObject panel = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            FontStyle fontStyle)
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
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;
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
                runtimeFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 32);
            }
            catch
            {
                runtimeFont = null;
            }

            runtimeFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return runtimeFont;
        }

        private static void AddOutline(Text text)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
    }
}
