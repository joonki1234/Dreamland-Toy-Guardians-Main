using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    /// <summary>
    /// Dreamland 전투 HUD.
    /// Sci-Fi GUI Skin의 프레임/게이지와 Strategic Warfare UI의 상태 패널을
    /// 조합해 미션, 웨이브, 적 수, 보스 HP, 직업, 대사, 시너지 UI를 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionBannerUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera uiCamera;

        [Header("Legacy Custom Banner (kept for scene compatibility)")]
        [SerializeField] private GameObject customBannerRoot;
        [SerializeField] private TMP_Text customBannerTitle;
        [SerializeField] private TMP_Text customBannerSubtitle;

        [Header("Toy Friend Quick Guide")]
        [SerializeField] private Sprite toyFriendPortrait;
        [SerializeField] private GameObject customGuideRoot;
        [SerializeField] private Image customGuidePortrait;
        [SerializeField] private TMP_Text customGuideSpeaker;
        [SerializeField] private TMP_Text customGuideMessage;

        [Header("Timing")]
        [SerializeField, Min(0.2f)] private float defaultBannerDuration = 2f;

        private Canvas canvas;
        private static Font runtimeFont;

        private GameObject bannerPanel;
        private CanvasGroup bannerGroup;
        private Text bannerTitle;
        private Text bannerSubtitle;

        private GameObject missionPanel;
        private RectTransform missionPanelRect;
        private Text objectiveText;
        private RectTransform objectiveTextRect;

        private GameObject combatPanel;
        private Text combatWaveText;
        private Text combatCountText;
        private Text combatDetailText;

        private GameObject rolePanel;
        private Text roleTitleText;
        private Text roleNameText;

        private GameObject bossPanel;
        private Text bossNameText;
        private Text bossHealthText;
        private RectTransform bossFillRect;
        private Image bossFillImage;

        private GameObject dialoguePanel;
        private Text dialogueText;

        private GameObject guidePanel;
        private CanvasGroup guideGroup;
        private Image guidePortrait;
        private GameObject guidePortraitFallback;
        private Text guidePortraitFallbackText;
        private Text guideSpeaker;
        private Text guideMessage;

        private GameObject synergyPanel;
        private Text synergyTitle;
        private Text synergyMessage;

        private Coroutine bannerRoutine;
        private Coroutine dialogueRoutine;
        private Coroutine guideRoutine;
        private Coroutine synergyRoutine;
        private Coroutine legacyRoleCleanupRoutine;
        private Coroutine storyFocusRestoreRoutine;

        private bool storyFocusActive;
        private bool storyMissionWasActive;
        private bool storyCombatWasActive;
        private bool storyRoleWasActive;
        private bool storyBossWasActive;
        private bool storySynergyWasActive;

        private void Awake()
        {
            // 씬에 남아 있는 과거 커스텀 UI는 중복 표시를 막기 위해 끕니다.
            if (customBannerRoot != null)
            {
                customBannerRoot.SetActive(false);
            }
            if (customGuideRoot != null)
            {
                customGuideRoot.SetActive(false);
            }

            EnsureUI();
            HideLegacyRoleReadouts();
        }

        private void OnEnable()
        {
            DreamGameEvents.SynergyTriggered -= HandleSynergy;
            DreamGameEvents.SynergyTriggered += HandleSynergy;

            HideLegacyRoleReadouts();
            if (legacyRoleCleanupRoutine != null)
            {
                StopCoroutine(legacyRoleCleanupRoutine);
            }
            legacyRoleCleanupRoutine = StartCoroutine(HideLegacyRoleReadoutsRoutine());
        }

        private void OnDisable()
        {
            DreamGameEvents.SynergyTriggered -= HandleSynergy;

            if (legacyRoleCleanupRoutine != null)
            {
                StopCoroutine(legacyRoleCleanupRoutine);
                legacyRoleCleanupRoutine = null;
            }

            if (storyFocusRestoreRoutine != null)
            {
                StopCoroutine(storyFocusRestoreRoutine);
                storyFocusRestoreRoutine = null;
            }
        }

        public void Configure(Camera targetCamera)
        {
            uiCamera = targetCamera;
            EnsureUI();
            ApplyCamera();
        }

        public void ShowBanner(
            string title,
            string subtitle = "",
            float duration = -1f)
        {
            EnsureUI();

            if (bannerRoutine != null)
            {
                StopCoroutine(bannerRoutine);
            }

            bannerRoutine = StartCoroutine(
                BannerRoutine(
                    title,
                    subtitle,
                    duration > 0f ? duration : defaultBannerDuration));
        }

        /// <summary>
        /// 레거시 호환용 API입니다. 현재 대화 표시는 3D 말풍선과
        /// 2D 장난감 친구 통신창 두 종류만 사용하므로 2D 통신창으로 통일합니다.
        /// </summary>
        public void ShowDialogue(
            string speaker,
            string message,
            float duration = 3f)
        {
            ShowQuickGuide(speaker, message, duration);
        }

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
                QuickGuideRoutine(speaker, message, duration));
        }

        public void SetToyFriendPortrait(Sprite portrait)
        {
            toyFriendPortrait = portrait;
            if (guidePortrait != null)
            {
                guidePortrait.sprite = portrait;
                guidePortrait.enabled = portrait != null;
            }

            if (guidePortraitFallback != null)
            {
                guidePortraitFallback.SetActive(portrait == null);
            }
        }

        public void SetObjective(string message)
        {
            EnsureUI();
            bool visible = !string.IsNullOrWhiteSpace(message);
            missionPanel.SetActive(visible);
            objectiveText.text = visible ? message.Trim() : string.Empty;

            if (visible)
            {
                RefreshObjectiveLayout();
            }
        }

        private void RefreshObjectiveLayout()
        {
            if (missionPanelRect == null || objectiveText == null || objectiveTextRect == null)
            {
                return;
            }

            // 한 줄 목표는 기존의 컴팩트한 카드 크기를 유지하고,
            // 자동 줄바꿈으로 두 줄 이상이 되는 목표만 카드 높이를 늘립니다.
            Canvas.ForceUpdateCanvases();
            float preferredHeight = objectiveText.preferredHeight;
            int renderedLineCount = objectiveText.cachedTextGenerator != null
                ? objectiveText.cachedTextGenerator.lineCount
                : 1;
            bool multiline = renderedLineCount > 1 ||
                preferredHeight > 38f ||
                objectiveText.text.Contains("\n");

            missionPanelRect.sizeDelta = new Vector2(
                missionPanelRect.sizeDelta.x,
                multiline ? 146f : 118f);

            objectiveTextRect.sizeDelta = new Vector2(
                objectiveTextRect.sizeDelta.x,
                multiline ? 78f : 54f);

            objectiveTextRect.anchorMin = new Vector2(0f, multiline ? 0.31f : 0.34f);
            objectiveTextRect.anchorMax = objectiveTextRect.anchorMin;
            objectiveTextRect.anchoredPosition = new Vector2(34f, 0f);

            // 두 줄 목표에서는 글자를 억지로 작게 줄이기보다 충분한 세로 공간을 줍니다.
            objectiveText.resizeTextMinSize = multiline ? 19 : 16;
            objectiveText.resizeTextMaxSize = 24;
        }

        /// <summary>
        /// 기존 Stage 1/2 코드와의 호환 API입니다.
        /// 문장에서 공격 단계와 남은 적 수를 추출해 별도 전투 패널에 표시합니다.
        /// </summary>
        public void SetProgress(string message)
        {
            EnsureUI();

            if (string.IsNullOrWhiteSpace(message))
            {
                combatPanel.SetActive(false);
                return;
            }

            string safeMessage = message.Trim();

            if (safeMessage.StartsWith("FINAL BOSS HP"))
            {
                // 구 버전 Director와도 호환되도록 숫자를 읽어 보스 바에 넣습니다.
                int slash = safeMessage.IndexOf('/');
                if (slash > 0)
                {
                    int current = ExtractLastInteger(safeMessage.Substring(0, slash));
                    int maximum = ExtractFirstInteger(safeMessage.Substring(slash + 1));
                    if (current >= 0 && maximum > 0)
                    {
                        SetBossHealth("오염된 선물 상자", current, maximum);
                        return;
                    }
                }
            }

            // 튜토리얼의 "명중 0 / 3" 형태도 전투 상태 패널에서
            // 실제 카운터로 보여줍니다. 기존 코드는 이 문자열을 적 숫자로
            // 해석하지 못해서 화면에는 계속 --/00처럼 보일 수 있었습니다.
            if (safeMessage.StartsWith("명중", System.StringComparison.Ordinal))
            {
                int slash = safeMessage.IndexOf('/');
                if (slash > 0)
                {
                    int current = ExtractLastInteger(safeMessage.Substring(0, slash));
                    int maximum = ExtractFirstInteger(safeMessage.Substring(slash + 1));
                    if (current >= 0 && maximum > 0)
                    {
                        SetTrainingHitStatus(current, maximum);
                        return;
                    }
                }
            }

            int enemyCount = -1;
            if (!TryExtractIntegerAfter(safeMessage, "남은 악몽", out enemyCount))
            {
                TryExtractIntegerAfter(safeMessage, "전장 악몽", out enemyCount);
            }

            string waveLabel = ExtractWaveLabel(safeMessage);
            SetCombatStatus(waveLabel, enemyCount, safeMessage);
        }

        public void SetCombatStatus(
            string waveLabel,
            int enemyCount,
            string detail = null)
        {
            EnsureUI();
            combatPanel.SetActive(true);

            combatWaveText.text = string.IsNullOrWhiteSpace(waveLabel)
                ? "전투 상황"
                : waveLabel.ToUpperInvariant();

            combatCountText.fontSize = 40;
            combatCountText.text = enemyCount >= 0
                ? enemyCount.ToString("00")
                : "--";

            combatDetailText.text = string.IsNullOrWhiteSpace(detail)
                ? (enemyCount >= 0 ? "남은 적" : "전투 진행 중")
                : SimplifyProgressDetail(detail);
        }

        private void SetTrainingHitStatus(int current, int maximum)
        {
            EnsureUI();
            combatPanel.SetActive(true);

            combatWaveText.text = "명중 횟수";
            combatCountText.fontSize = 34;
            combatCountText.text =
                Mathf.Clamp(current, 0, maximum).ToString("00") + "/" +
                Mathf.Max(1, maximum).ToString("00");
            combatDetailText.text = "튜토리얼 적 명중";
        }

        public void SetBossHealth(
            string bossName,
            float current,
            float maximum)
        {
            EnsureUI();

            float safeMax = Mathf.Max(1f, maximum);
            float ratio = Mathf.Clamp01(current / safeMax);

            bossPanel.SetActive(true);
            bossNameText.text = string.IsNullOrWhiteSpace(bossName)
                ? "최종 보스"
                : bossName;
            bossHealthText.text =
                Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(safeMax);

            if (bossFillRect != null)
            {
                bossFillRect.anchorMax = new Vector2(
                    Mathf.Max(0.0001f, ratio),
                    1f);
            }

            if (bossFillImage != null)
            {
                bossFillImage.enabled = ratio > 0f;
                bossFillImage.sprite = DreamlandUiSkin.KenneyBossBarRed;
            }
        }

        public void HideBossHealth()
        {
            if (bossPanel != null)
            {
                bossPanel.SetActive(false);
            }
        }

        public void SetRole(PlayerRole role)
        {
            // 플레이 화면 하단의 "현재 직업" HUD는 사용하지 않습니다.
            // 기존 호출부와의 호환성을 위해 메서드는 유지하되 화면에는 표시하지 않습니다.
            EnsureUI();

            if (rolePanel != null)
            {
                rolePanel.SetActive(false);
            }

            HideLegacyRoleReadouts();
        }

        private IEnumerator HideLegacyRoleReadoutsRoutine()
        {
            // 씬의 과거 UI가 Start/OnEnable에서 한두 프레임 늦게 생성되는 경우까지 제거합니다.
            // 이후 직업 변경 호출 시에도 SetRole에서 다시 정리합니다.
            float elapsed = 0f;
            while (elapsed < 2f)
            {
                HideLegacyRoleReadouts();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            legacyRoleCleanupRoutine = null;
        }

        private static void HideLegacyRoleReadouts()
        {
            Text[] legacyTexts =
                UnityEngine.Object.FindObjectsByType<Text>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (Text textComponent in legacyTexts)
            {
                if (textComponent == null ||
                    string.IsNullOrWhiteSpace(textComponent.text))
                {
                    continue;
                }

                if (IsLegacyRoleReadout(textComponent.text))
                {
                    textComponent.enabled = false;
                }
            }

            TMP_Text[] legacyTmpTexts =
                UnityEngine.Object.FindObjectsByType<TMP_Text>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (TMP_Text textComponent in legacyTmpTexts)
            {
                if (textComponent == null ||
                    string.IsNullOrWhiteSpace(textComponent.text))
                {
                    continue;
                }

                if (IsLegacyRoleReadout(textComponent.text))
                {
                    textComponent.enabled = false;
                }
            }
        }

        private static bool IsLegacyRoleReadout(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Replace(" ", string.Empty);
            return normalized.Contains("현재직업:") ||
                   (normalized.Contains("1경찰") &&
                    normalized.Contains("2소방관") &&
                    normalized.Contains("3요리사") &&
                    normalized.Contains("4건축가"));
        }

        public void ClearPersistentText()
        {
            SetObjective(string.Empty);
            SetProgress(string.Empty);
            HideBossHealth();
        }

        /// <summary>
        /// 3D 장난감 친구가 직접 말할 때 화면용 대화/가이드 패널이
        /// 겹쳐 보이지 않도록 즉시 숨깁니다.
        /// </summary>
        public void HideTransientMessages()
        {
            if (dialogueRoutine != null)
            {
                StopCoroutine(dialogueRoutine);
                dialogueRoutine = null;
            }

            if (guideRoutine != null)
            {
                StopCoroutine(guideRoutine);
                guideRoutine = null;
            }

            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            if (guidePanel != null)
            {
                guidePanel.SetActive(false);
            }
        }


        /// <summary>
        /// 3D 장난감 친구가 중요한 이야기를 할 때 전투 HUD를 잠시 정리합니다.
        /// CoreHealthHUD는 별도 Canvas이므로 계속 표시됩니다.
        /// </summary>
        public void BeginToyFriendStoryFocus()
        {
            EnsureUI();

            if (storyFocusRestoreRoutine != null)
            {
                StopCoroutine(storyFocusRestoreRoutine);
                storyFocusRestoreRoutine = null;
            }

            if (!storyFocusActive)
            {
                storyFocusActive = true;
                storyMissionWasActive = missionPanel != null && missionPanel.activeSelf;
                storyCombatWasActive = combatPanel != null && combatPanel.activeSelf;
                storyRoleWasActive = rolePanel != null && rolePanel.activeSelf;
                storyBossWasActive = bossPanel != null && bossPanel.activeSelf;
                storySynergyWasActive = synergyPanel != null && synergyPanel.activeSelf;
            }

            HideTransientMessages();

            missionPanel?.SetActive(false);
            combatPanel?.SetActive(false);
            rolePanel?.SetActive(false);
            bossPanel?.SetActive(false);
            synergyPanel?.SetActive(false);
        }

        /// <summary>
        /// 연속된 3D 대사 사이에서 HUD가 깜빡이지 않도록 잠시 기다렸다 복원합니다.
        /// 다음 대사가 곧 시작되면 BeginToyFriendStoryFocus가 복원을 취소합니다.
        /// </summary>
        public void EndToyFriendStoryFocus(float restoreDelay = 0.40f)
        {
            if (!storyFocusActive)
            {
                return;
            }

            if (storyFocusRestoreRoutine != null)
            {
                StopCoroutine(storyFocusRestoreRoutine);
            }

            storyFocusRestoreRoutine = StartCoroutine(
                RestoreToyFriendStoryFocusRoutine(Mathf.Max(0f, restoreDelay)));
        }

        private IEnumerator RestoreToyFriendStoryFocusRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (!storyFocusActive)
            {
                storyFocusRestoreRoutine = null;
                yield break;
            }

            missionPanel?.SetActive(storyMissionWasActive);
            combatPanel?.SetActive(storyCombatWasActive);
            rolePanel?.SetActive(storyRoleWasActive);
            bossPanel?.SetActive(storyBossWasActive);
            synergyPanel?.SetActive(storySynergyWasActive);

            storyFocusActive = false;
            storyFocusRestoreRoutine = null;
        }

        private IEnumerator BannerRoutine(
            string title,
            string subtitle,
            float duration)
        {
            bannerTitle.text = title ?? string.Empty;
            bannerSubtitle.text = subtitle ?? string.Empty;
            bannerPanel.SetActive(true);

            RectTransform rect = bannerPanel.GetComponent<RectTransform>();
            float fadeIn = 0.18f;
            float elapsed = 0f;
            bannerGroup.alpha = 0f;
            rect.localScale = Vector3.one * 0.90f;

            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeIn);
                bannerGroup.alpha = t;
                rect.localScale = Vector3.one * Mathf.Lerp(0.90f, 1f, t);
                yield return null;
            }

            float visibleDuration = Mathf.Max(0.1f, duration - 0.36f);
            yield return new WaitForSecondsRealtime(visibleDuration);

            elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeIn);
                bannerGroup.alpha = 1f - t;
                yield return null;
            }

            bannerPanel.SetActive(false);
            bannerGroup.alpha = 1f;
            rect.localScale = Vector3.one;
            bannerRoutine = null;
        }

        private IEnumerator DialogueRoutine(
            string speaker,
            string message,
            float duration)
        {
            string prefix = string.IsNullOrWhiteSpace(speaker)
                ? string.Empty
                : speaker.Trim() + "  //  ";

            dialogueText.text = prefix + (message ?? string.Empty);
            dialoguePanel.SetActive(true);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, duration));
            dialoguePanel.SetActive(false);
            dialogueRoutine = null;
        }

        private IEnumerator QuickGuideRoutine(
            string speaker,
            string message,
            float duration)
        {
            if (toyFriendPortrait == null)
            {
                toyFriendPortrait = Resources.Load<Sprite>(
                    "DreamlandUI/toy_friend_portrait");
            }

            guideSpeaker.text = string.IsNullOrWhiteSpace(speaker)
                ? "장난감 친구"
                : speaker.Trim();
            guideMessage.text = message ?? string.Empty;
            guidePortrait.sprite = toyFriendPortrait;
            guidePortrait.enabled = toyFriendPortrait != null;

            if (guidePortraitFallback != null)
            {
                guidePortraitFallback.SetActive(toyFriendPortrait == null);
            }

            guidePanel.SetActive(true);

            RectTransform rect = guidePanel.GetComponent<RectTransform>();
            if (guideGroup != null)
            {
                guideGroup.alpha = 0f;
            }
            rect.localScale = Vector3.one * 0.94f;

            const float fadeIn = 0.14f;
            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeIn);
                if (guideGroup != null)
                {
                    guideGroup.alpha = t;
                }
                rect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, t);
                yield return null;
            }

            if (guideGroup != null)
            {
                guideGroup.alpha = 1f;
            }
            rect.localScale = Vector3.one;

            float hold = Mathf.Max(0.1f, duration) - fadeIn - 0.12f;
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            const float fadeOut = 0.12f;
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOut);
                if (guideGroup != null)
                {
                    guideGroup.alpha = 1f - t;
                }
                yield return null;
            }

            guidePanel.SetActive(false);
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
            string pair =
                DreamGameText.GetRoleName(data.FirstRole) +
                " + " +
                DreamGameText.GetRoleName(data.SecondRole);

            synergyTitle.text = "SYNERGY ACTIVATED";
            synergyMessage.text = synergyName + "  //  " + pair;
            synergyPanel.SetActive(true);

            yield return new WaitForSecondsRealtime(2f);

            synergyPanel.SetActive(false);
            synergyRoutine = null;
        }

        private void EnsureUI()
        {
            if (canvas != null)
            {
                return;
            }

            if (toyFriendPortrait == null)
            {
                toyFriendPortrait = Resources.Load<Sprite>(
                    "DreamlandUI/toy_friend_portrait");
            }

            GameObject canvasObject = new GameObject(
                "DreamlandSciFiCombatHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 녹화 영상처럼 4:3에 가까운 Game View와 16:9 HMD 출력 모두에서
            // HUD가 지나치게 작아지지 않도록 조금 더 세로 친화적인 기준을 사용합니다.
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.matchWidthOrHeight = 0.58f;

            ApplyCamera();
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            BuildBanner(root);
            BuildMissionPanel(root);
            BuildCombatPanel(root);
            BuildRolePanel(root);
            BuildBossPanel(root);
            BuildGuidePanel(root);
            BuildSynergyPanel(root);

            if (rolePanel != null)
            {
                rolePanel.SetActive(false);
            }
        }

        private void BuildBanner(RectTransform root)
        {
            // Strategic warning 프레임의 원본 비율(약 3.1:1)에 맞춰
            // 세로가 눌리지 않도록 높이를 확보합니다.
            bannerPanel = CreatePanel(
                "MissionStartBanner",
                root,
                new Vector2(0.5f, 0.62f),
                new Vector2(840f, 250f),
                DreamlandUiSkin.StrategicWarningPanel,
                new Color(0.88f, 0.98f, 1f, 0.98f),
                false);

            bannerGroup = bannerPanel.AddComponent<CanvasGroup>();
            RectTransform rect = bannerPanel.GetComponent<RectTransform>();

            bannerTitle = CreateText(
                "Title",
                rect,
                new Vector2(0.5f, 0.61f),
                new Vector2(650f, 70f),
                43,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            bannerTitle.color = new Color(0.78f, 1f, 1f, 1f);

            bannerSubtitle = CreateText(
                "Subtitle",
                rect,
                new Vector2(0.5f, 0.36f),
                new Vector2(660f, 58f),
                27,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            bannerSubtitle.color = Color.white;

            bannerPanel.SetActive(false);
        }

        private void BuildMissionPanel(RectTransform root)
        {
            missionPanel = CreatePanel(
                "MissionObjective",
                root,
                new Vector2(0f, 1f),
                new Vector2(442f, 118f),
                DreamlandUiSkin.KenneyMissionPanel,
                new Color(0.86f, 0.92f, 1f, 0.96f),
                true,
                new Vector2(22f, -18f),
                new Vector2(0f, 1f));

            RectTransform rect = missionPanel.GetComponent<RectTransform>();
            missionPanelRect = rect;

            Text label = CreateText(
                "MissionLabel",
                rect,
                new Vector2(0f, 0.72f),
                new Vector2(260f, 28f),
                18,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(34f, 0f),
                new Vector2(0f, 0.5f));
            label.text = "현재 목표";
            label.color = new Color(0.76f, 0.98f, 0.95f, 1f);

            objectiveText = CreateText(
                "Objective",
                rect,
                new Vector2(0f, 0.34f),
                new Vector2(370f, 54f),
                24,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(34f, 0f),
                new Vector2(0f, 0.5f));
            objectiveText.color = new Color(1f, 0.99f, 1f, 1f);
            objectiveTextRect = objectiveText.GetComponent<RectTransform>();
            missionPanel.SetActive(false);
        }

        private void BuildCombatPanel(RectTransform root)
        {
            combatPanel = CreatePanel(
                "CombatStatus",
                root,
                new Vector2(1f, 1f),
                new Vector2(338f, 112f),
                DreamlandUiSkin.KenneyCounterPanel,
                new Color(0.87f, 0.93f, 1f, 0.96f),
                true,
                new Vector2(-22f, -18f),
                new Vector2(1f, 1f));

            RectTransform rect = combatPanel.GetComponent<RectTransform>();

            combatWaveText = CreateText(
                "Wave",
                rect,
                new Vector2(0f, 0.70f),
                new Vector2(170f, 28f),
                17,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(28f, 0f),
                new Vector2(0f, 0.5f));
            combatWaveText.color = new Color(0.75f, 0.97f, 0.95f, 1f);

            combatDetailText = CreateText(
                "Detail",
                rect,
                new Vector2(0f, 0.32f),
                new Vector2(176f, 36f),
                16,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(28f, 0f),
                new Vector2(0f, 0.5f));
            combatDetailText.color = new Color(0.99f, 0.99f, 1f, 1f);

            combatCountText = CreateText(
                "EnemyCount",
                rect,
                new Vector2(1f, 0.50f),
                new Vector2(122f, 66f),
                40,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Vector2(-18f, 0f),
                new Vector2(1f, 0.5f));
            combatCountText.color = new Color(1f, 0.93f, 0.57f, 1f);

            combatPanel.SetActive(false);
        }

        private void BuildRolePanel(RectTransform root)
        {
            rolePanel = CreatePanel(
                "RoleStatus",
                root,
                new Vector2(0f, 0f),
                new Vector2(334f, 98f),
                DreamlandUiSkin.SciFiWindow,
                new Color(1f, 1f, 1f, 0.94f),
                true,
                new Vector2(28f, 26f),
                Vector2.zero);

            RectTransform rect = rolePanel.GetComponent<RectTransform>();
            GameObject accent = CreatePanel(
                "RoleAccent",
                rect,
                new Vector2(0.5f, 0.80f),
                new Vector2(210f, 8f),
                DreamlandUiSkin.SciFiBarBackground,
                new Color(0.40f, 0.95f, 1f, 0.92f),
                false);
            accent.GetComponent<Image>().type = Image.Type.Sliced;

            roleTitleText = CreateText(
                "RoleLabel",
                rect,
                new Vector2(0f, 0.66f),
                new Vector2(180f, 26f),
                16,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Vector2(30f, 0f),
                new Vector2(0f, 0.5f));
            roleTitleText.color = new Color(0.66f, 0.94f, 1f, 1f);

            roleNameText = CreateText(
                "RoleName",
                rect,
                new Vector2(0f, 0.34f),
                new Vector2(250f, 36f),
                24,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(30f, 0f),
                new Vector2(0f, 0.5f));
        }

        private void BuildBossPanel(RectTransform root)
        {
            bossPanel = CreatePanel(
                "BossHealthHUD",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(860f, 178f),
                DreamlandUiSkin.KenneyBossPanel,
                new Color(0.94f, 0.88f, 1f, 0.97f),
                true,
                new Vector2(0f, -120f),
                new Vector2(0.5f, 1f));

            RectTransform rect = bossPanel.GetComponent<RectTransform>();
            bossNameText = CreateText(
                "BossName",
                rect,
                new Vector2(0f, 0.67f),
                new Vector2(520f, 42f),
                27,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(48f, 0f),
                new Vector2(0f, 0.5f));
            bossNameText.color = new Color(1f, 0.73f, 0.86f, 1f);

            bossHealthText = CreateText(
                "BossHP",
                rect,
                new Vector2(1f, 0.67f),
                new Vector2(190f, 42f),
                25,
                TextAnchor.MiddleRight,
                FontStyle.Bold,
                new Vector2(-42f, 0f),
                new Vector2(1f, 0.5f));

            GameObject barBg = CreatePanel(
                "BossBarBackground",
                rect,
                new Vector2(0.5f, 0.27f),
                new Vector2(710f, 34f),
                DreamlandUiSkin.KenneyBossBarBlue,
                new Color(0.78f, 0.70f, 0.98f, 0.92f),
                true);

            RectTransform bgRect = barBg.GetComponent<RectTransform>();
            GameObject fill = CreatePanel(
                "BossBarFill",
                bgRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                DreamlandUiSkin.KenneyBossBarRed,
                new Color(0.97f, 0.57f, 0.77f, 0.97f),
                true);

            bossFillRect = fill.GetComponent<RectTransform>();
            bossFillRect.anchorMin = Vector2.zero;
            bossFillRect.anchorMax = Vector2.one;
            bossFillRect.pivot = new Vector2(0f, 0.5f);
            bossFillRect.offsetMin = new Vector2(4f, 4f);
            bossFillRect.offsetMax = new Vector2(-4f, -4f);
            bossFillImage = fill.GetComponent<Image>();
            bossHealthText.color = new Color(1f, 0.98f, 1f, 1f);

            bossPanel.SetActive(false);
        }

        private void BuildDialoguePanel(RectTransform root)
        {
            dialoguePanel = CreatePanel(
                "StoryDialogue",
                root,
                new Vector2(0.5f, 0f),
                new Vector2(920f, 196f),
                DreamlandUiSkin.SciFiWindow,
                new Color(1f, 1f, 1f, 0.95f),
                true,
                new Vector2(0f, 22f),
                new Vector2(0.5f, 0f));

            RectTransform rect = dialoguePanel.GetComponent<RectTransform>();
            dialogueText = CreateText(
                "Dialogue",
                rect,
                new Vector2(0.5f, 0.50f),
                new Vector2(780f, 112f),
                29,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            dialogueText.color = new Color(1f, 1f, 1f, 1f);

            dialoguePanel.SetActive(false);
        }

        private void BuildGuidePanel(RectTransform root)
        {
            // 전투 중 짧은 안내 전용: 화면 왼쪽 아래에 작은 2D 통신창만 표시합니다.
            guidePanel = CreatePanel(
                "ToyFriendComms",
                root,
                new Vector2(0f, 0f),
                new Vector2(548f, 158f),
                DreamlandUiSkin.KenneyCounterPanel,
                new Color(1f, 1f, 1f, 0.68f),
                true,
                new Vector2(24f, 26f),
                new Vector2(0f, 0f));

            guideGroup = guidePanel.AddComponent<CanvasGroup>();
            RectTransform rect = guidePanel.GetComponent<RectTransform>();

            GameObject inner = CreatePanel(
                "GuideInner",
                rect,
                new Vector2(0.5f, 0.5f),
                new Vector2(510f, 124f),
                null,
                new Color(0.91f, 0.97f, 1f, 0.92f),
                false);
            inner.transform.SetAsFirstSibling();

            GameObject portraitFrame = CreatePanel(
                "PortraitFrame",
                rect,
                new Vector2(0f, 0.5f),
                new Vector2(104f, 104f),
                DreamlandUiSkin.KenneyMissionPanel,
                new Color(0.76f, 0.92f, 1f, 0.98f),
                true,
                new Vector2(66f, 0f),
                new Vector2(0.5f, 0.5f));

            RectTransform portraitFrameRect = portraitFrame.GetComponent<RectTransform>();
            guidePortrait = CreateImage(
                "Portrait",
                portraitFrameRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(78f, 78f),
                Color.white);
            guidePortrait.preserveAspect = true;
            guidePortrait.sprite = toyFriendPortrait;
            guidePortrait.enabled = toyFriendPortrait != null;

            guidePortraitFallback = new GameObject(
                "PortraitFallback",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            guidePortraitFallback.transform.SetParent(portraitFrameRect, false);
            RectTransform fallbackRect = guidePortraitFallback.GetComponent<RectTransform>();
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;

            guidePortraitFallbackText = CreateText(
                "FallbackLabel",
                fallbackRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(78f, 42f),
                20,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            guidePortraitFallbackText.text = "친구";
            guidePortraitFallbackText.color = new Color(0.34f, 0.64f, 0.78f, 1f);
            guidePortraitFallback.SetActive(toyFriendPortrait == null);

            guideSpeaker = CreateText(
                "Speaker",
                rect,
                new Vector2(0f, 0.76f),
                new Vector2(330f, 28f),
                18,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(132f, 0f),
                new Vector2(0f, 0.5f));
            guideSpeaker.color = new Color(0.27f, 0.66f, 0.64f, 1f);
            Outline speakerOutline = guideSpeaker.GetComponent<Outline>();
            if (speakerOutline != null)
            {
                speakerOutline.enabled = false;
            }

            guideMessage = CreateText(
                "Message",
                rect,
                new Vector2(0f, 0.33f),
                new Vector2(376f, 70f),
                21,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Vector2(132f, 0f),
                new Vector2(0f, 0.5f));
            guideMessage.color = new Color(0.10f, 0.17f, 0.25f, 1f);
            guideMessage.resizeTextMinSize = 17;
            guideMessage.resizeTextMaxSize = 21;
            Outline messageOutline = guideMessage.GetComponent<Outline>();
            if (messageOutline != null)
            {
                messageOutline.enabled = false;
            }

            guidePanel.SetActive(false);
        }

        private void BuildSynergyPanel(RectTransform root)
        {
            synergyPanel = CreatePanel(
                "SynergyAlert",
                root,
                new Vector2(0.5f, 0.36f),
                new Vector2(648f, 176f),
                DreamlandUiSkin.SciFiWindow,
                new Color(1f, 1f, 1f, 0.95f),
                true);

            RectTransform rect = synergyPanel.GetComponent<RectTransform>();
            GameObject accent = CreatePanel(
                "SynergyAccent",
                rect,
                new Vector2(0.5f, 0.82f),
                new Vector2(400f, 8f),
                DreamlandUiSkin.SciFiBarBackground,
                new Color(0.48f, 1f, 0.88f, 0.94f),
                false);
            accent.GetComponent<Image>().type = Image.Type.Sliced;

            Image icon = CreateImage(
                "Lightning",
                rect,
                new Vector2(0f, 0.5f),
                new Vector2(52f, 52f),
                new Color(1f, 0.92f, 0.46f, 1f),
                new Vector2(78f, 0f),
                new Vector2(0.5f, 0.5f));
            icon.sprite = DreamlandUiSkin.SciFiRocket;
            icon.preserveAspect = true;

            synergyTitle = CreateText(
                "SynergyTitle",
                rect,
                new Vector2(0f, 0.62f),
                new Vector2(400f, 38f),
                26,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(128f, 0f),
                new Vector2(0f, 0.5f));
            synergyTitle.color = new Color(0.52f, 1f, 1f, 1f);

            synergyMessage = CreateText(
                "SynergyMessage",
                rect,
                new Vector2(0f, 0.37f),
                new Vector2(400f, 44f),
                22,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(128f, 0f),
                new Vector2(0f, 0.5f));

            synergyPanel.SetActive(false);
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
            Sprite sprite,
            Color color,
            bool sliced,
            Vector2? anchoredPosition = null,
            Vector2? pivot = null)
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
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            rect.sizeDelta = size;

            Image image = panel.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sliced && sprite != null && sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Color color,
            Vector2? anchoredPosition = null,
            Vector2? pivot = null)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
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
            FontStyle fontStyle,
            Vector2? anchoredPosition = null,
            Vector2? pivot = null)
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
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = GetRuntimeFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(14, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

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
                runtimeFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 30);
            }
            catch
            {
                runtimeFont = null;
            }

            runtimeFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return runtimeFont;
        }

        private static bool TryExtractIntegerAfter(
            string text,
            string marker,
            out int value)
        {
            value = -1;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(marker))
            {
                return false;
            }

            int markerIndex = text.IndexOf(marker, System.StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            int index = markerIndex + marker.Length;
            while (index < text.Length && !char.IsDigit(text[index]))
            {
                index++;
            }

            if (index >= text.Length)
            {
                return false;
            }

            int start = index;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            return int.TryParse(text.Substring(start, index - start), out value);
        }

        private static int ExtractFirstInteger(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    continue;
                }

                int start = i;
                while (i < text.Length && char.IsDigit(text[i]))
                {
                    i++;
                }

                return int.TryParse(text.Substring(start, i - start), out int value)
                    ? value
                    : -1;
            }

            return -1;
        }

        private static int ExtractLastInteger(string text)
        {
            int last = -1;
            if (string.IsNullOrEmpty(text))
            {
                return last;
            }

            int index = 0;
            while (index < text.Length)
            {
                if (!char.IsDigit(text[index]))
                {
                    index++;
                    continue;
                }

                int start = index;
                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }

                if (int.TryParse(text.Substring(start, index - start), out int value))
                {
                    last = value;
                }
            }

            return last;
        }

        private static string ExtractWaveLabel(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "전투 상황";
            }

            int separator = message.IndexOf('·');
            string left = separator >= 0
                ? message.Substring(0, separator).Trim()
                : message.Trim();

            if (left.StartsWith("남은 악몽"))
            {
                return "COMBAT";
            }

            if (left.Length > 26)
            {
                left = left.Substring(0, 26);
            }

            return left;
        }

        private static string SimplifyProgressDetail(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            if (message.Contains("남은 악몽") || message.Contains("전장 악몽"))
            {
                return "남은 적";
            }

            return message.Length > 34
                ? message.Substring(0, 34)
                : message;
        }

        private static string GetRoleDisplayName(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Police:
                    return "POLICE  //  경찰";
                case PlayerRole.Firefighter:
                    return "FIREFIGHTER  //  소방관";
                case PlayerRole.Chef:
                    return "CHEF  //  요리사";
                case PlayerRole.Architect:
                    return "BUILDER  //  건축가";
                default:
                    return DreamGameText.GetRoleName(role);
            }
        }
    }
}
