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

        [Header("Toy Friend Quick Guide (Optional)")]
        [Tooltip("카카오톡형 안내창에 사용할 장난감 친구 얼굴 이미지")]
        [SerializeField] private Sprite toyFriendPortrait;

        [Tooltip("직접 만든 2D 안내창의 루트 오브젝트")]
        [SerializeField] private GameObject customGuideRoot;

        [SerializeField] private Image customGuidePortrait;
        [SerializeField] private TMP_Text customGuideSpeaker;
        [SerializeField] private TMP_Text customGuideMessage;

        [Header("Timing")]
        [SerializeField, Min(0.2f)] private float defaultBannerDuration = 2f;

        private Canvas canvas;

        // 커스텀 배너가 연결되지 않았을 때만 사용하는 기존 임시 배너
        private GameObject fallbackBannerPanel;
        private Text fallbackBannerTitle;
        private Text fallbackBannerSubtitle;

        private Text dialogueText;
        private GameObject fallbackGuidePanel;
        private Image fallbackGuidePortrait;
        private Text fallbackGuidePortraitLabel;
        private Text fallbackGuideSpeaker;
        private Text fallbackGuideMessage;
        private Text objectiveText;
        private Text progressText;
        private Text roleText;
        private Coroutine bannerRoutine;
        private Coroutine dialogueRoutine;
        private Coroutine guideRoutine;
        private Coroutine synergyRoutine;
        private static Font runtimeFont;

        private bool HasCustomBanner =>
            customBannerRoot != null &&
            customBannerTitle != null &&
            customBannerSubtitle != null;

        private bool HasCustomGuide =>
            customGuideRoot != null &&
            customGuideSpeaker != null &&
            customGuideMessage != null;

        private void Awake()
        {
            EnsureUI();

            if (customBannerRoot != null)
            {
                customBannerRoot.SetActive(false);
            }

            if (customGuideRoot != null)
            {
                customGuideRoot.SetActive(false);
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

        /// <summary>
        /// 전투를 가리지 않는 짧은 2D 프로필 안내를 표시합니다.
        /// 커스텀 UI를 연결하지 않으면 우측 상단에 임시 대화창을 자동 생성합니다.
        /// </summary>
        public void ShowQuickGuide(
            string speaker,
            string message,
            float duration = 3f)
        {
            EnsureUI();

            if (guideRoutine != null)
            {
                StopCoroutine(guideRoutine);
            }

            guideRoutine = StartCoroutine(
                QuickGuideRoutine(
                    speaker,
                    message,
                    duration));
        }

        public void SetToyFriendPortrait(Sprite portrait)
        {
            toyFriendPortrait = portrait;

            if (customGuidePortrait != null)
            {
                customGuidePortrait.sprite = portrait;
                customGuidePortrait.enabled = portrait != null;
            }

            if (fallbackGuidePortrait != null)
            {
                fallbackGuidePortrait.sprite = portrait;
                fallbackGuidePortrait.color =
                    portrait != null
                        ? Color.white
                        : new Color(0.64f, 0.92f, 0.83f, 1f);
            }

            if (fallbackGuidePortraitLabel != null)
            {
                fallbackGuidePortraitLabel.gameObject.SetActive(
                    portrait == null);
            }
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
            roleText.text = "현재 직업: " + DreamGameText.GetRoleName(role) + "  [1 경찰 / 2 소방관 / 3 요리사 / 4 건축가]";
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

        private IEnumerator QuickGuideRoutine(
            string speaker,
            string message,
            float duration)
        {
            string safeSpeaker =
                string.IsNullOrWhiteSpace(speaker)
                    ? "장난감 친구"
                    : speaker;

            string safeMessage = message ?? string.Empty;

            if (HasCustomGuide)
            {
                customGuideSpeaker.text = safeSpeaker;
                customGuideMessage.text = safeMessage;

                if (customGuidePortrait != null)
                {
                    customGuidePortrait.sprite = toyFriendPortrait;
                    customGuidePortrait.enabled = toyFriendPortrait != null;
                }

                customGuideRoot.SetActive(true);
            }
            else
            {
                fallbackGuideSpeaker.text = safeSpeaker;
                fallbackGuideMessage.text = safeMessage;
                fallbackGuidePortrait.sprite = toyFriendPortrait;
                fallbackGuidePortrait.color =
                    toyFriendPortrait != null
                        ? Color.white
                        : new Color(0.64f, 0.92f, 0.83f, 1f);
                fallbackGuidePortraitLabel.gameObject.SetActive(
                    toyFriendPortrait == null);
                fallbackGuidePanel.SetActive(true);
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.1f, duration));

            if (HasCustomGuide)
            {
                customGuideRoot.SetActive(false);
            }
            else if (fallbackGuidePanel != null)
            {
                fallbackGuidePanel.SetActive(false);
            }

            guideRoutine = null;
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

            fallbackGuidePanel = CreatePanel(
                "ToyFriendQuickGuide",
                canvasRect,
                new Vector2(0.76f, 0.68f),
                new Vector2(760f, 170f),
                new Color(0.05f, 0.08f, 0.12f, 0.88f));

            RectTransform guideRect =
                fallbackGuidePanel.GetComponent<RectTransform>();

            fallbackGuidePortrait = CreateImage(
                "Portrait",
                guideRect,
                new Vector2(0.11f, 0.5f),
                new Vector2(118f, 118f),
                new Color(0.64f, 0.92f, 0.83f, 1f));
            fallbackGuidePortrait.sprite = toyFriendPortrait;
            fallbackGuidePortrait.preserveAspect = true;

            fallbackGuidePortraitLabel = CreateText(
                "PortraitPlaceholder",
                guideRect,
                new Vector2(0.11f, 0.5f),
                new Vector2(100f, 42f),
                22,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            fallbackGuidePortraitLabel.text = "친구";
            fallbackGuidePortraitLabel.color =
                new Color(0.05f, 0.16f, 0.14f, 1f);
            fallbackGuidePortraitLabel.gameObject.SetActive(
                toyFriendPortrait == null);

            fallbackGuideSpeaker = CreateText(
                "Speaker",
                guideRect,
                new Vector2(0.29f, 0.72f),
                new Vector2(500f, 38f),
                23,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            fallbackGuideSpeaker.rectTransform.pivot =
                new Vector2(0f, 0.5f);
            fallbackGuideSpeaker.color =
                new Color(0.72f, 1f, 0.89f, 1f);

            fallbackGuideMessage = CreateText(
                "Message",
                guideRect,
                new Vector2(0.29f, 0.39f),
                new Vector2(500f, 88f),
                25,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            fallbackGuideMessage.rectTransform.pivot =
                new Vector2(0f, 0.5f);

            fallbackGuidePanel.SetActive(false);

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

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect =
                imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
