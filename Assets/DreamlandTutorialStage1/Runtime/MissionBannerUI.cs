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
        private Text objectiveText;

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
        private Image guidePortrait;
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

        public void ShowDialogue(
            string speaker,
            string message,
            float duration = 3f)
        {
            EnsureUI();

            if (dialogueRoutine != null)
            {
                StopCoroutine(dialogueRoutine);
            }

            dialogueRoutine = StartCoroutine(
                DialogueRoutine(speaker, message, duration));
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
        }

        public void SetObjective(string message)
        {
            EnsureUI();
            bool visible = !string.IsNullOrWhiteSpace(message);
            missionPanel.SetActive(visible);
            objectiveText.text = visible ? message.Trim() : string.Empty;
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
                        SetBossHealth("CORRUPTED TOY BOX", current, maximum);
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
                ? "COMBAT STATUS"
                : waveLabel.ToUpperInvariant();

            combatCountText.text = enemyCount >= 0
                ? enemyCount.ToString("00")
                : "--";

            combatDetailText.text = string.IsNullOrWhiteSpace(detail)
                ? (enemyCount >= 0 ? "ENEMIES REMAINING" : "SYSTEM ACTIVE")
                : SimplifyProgressDetail(detail);
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
                ? "FINAL BOSS"
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
                bossFillImage.sprite = ratio <= 0.34f
                    ? DreamlandUiSkin.SciFiBarRed
                    : DreamlandUiSkin.SciFiBarPurple;
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
            guideSpeaker.text = string.IsNullOrWhiteSpace(speaker)
                ? "TOY FRIEND"
                : speaker.Trim();
            guideMessage.text = message ?? string.Empty;
            guidePortrait.sprite = toyFriendPortrait;
            guidePortrait.enabled = toyFriendPortrait != null;
            guidePanel.SetActive(true);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, duration));

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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            ApplyCamera();
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            BuildBanner(root);
            BuildMissionPanel(root);
            BuildCombatPanel(root);
            BuildRolePanel(root);
            BuildBossPanel(root);
            BuildDialoguePanel(root);
            BuildGuidePanel(root);
            BuildSynergyPanel(root);

            if (rolePanel != null)
            {
                rolePanel.SetActive(false);
            }
        }

        private void BuildBanner(RectTransform root)
        {
            bannerPanel = CreatePanel(
                "MissionStartBanner",
                root,
                new Vector2(0.5f, 0.62f),
                new Vector2(850f, 205f),
                DreamlandUiSkin.StrategicWarningPanel,
                new Color(0.76f, 0.95f, 1f, 0.98f),
                true);

            bannerGroup = bannerPanel.AddComponent<CanvasGroup>();
            RectTransform rect = bannerPanel.GetComponent<RectTransform>();

            bannerTitle = CreateText(
                "Title",
                rect,
                new Vector2(0.57f, 0.64f),
                new Vector2(650f, 66f),
                44,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            bannerTitle.color = new Color(0.78f, 1f, 1f, 1f);

            bannerSubtitle = CreateText(
                "Subtitle",
                rect,
                new Vector2(0.57f, 0.33f),
                new Vector2(650f, 56f),
                25,
                TextAnchor.MiddleLeft,
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
                new Vector2(690f, 118f),
                DreamlandUiSkin.SciFiWindow,
                new Color(0.62f, 0.92f, 1f, 0.86f),
                true,
                new Vector2(28f, -25f),
                new Vector2(0f, 1f));

            RectTransform rect = missionPanel.GetComponent<RectTransform>();
            Text label = CreateText(
                "MissionLabel",
                rect,
                new Vector2(0f, 0.74f),
                new Vector2(185f, 34f),
                19,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(36f, 0f),
                new Vector2(0f, 0.5f));
            label.text = "MISSION // OBJECTIVE";
            label.color = new Color(0.43f, 1f, 1f, 1f);

            objectiveText = CreateText(
                "Objective",
                rect,
                new Vector2(0f, 0.38f),
                new Vector2(610f, 56f),
                25,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(36f, 0f),
                new Vector2(0f, 0.5f));
            objectiveText.color = Color.white;
            missionPanel.SetActive(false);
        }

        private void BuildCombatPanel(RectTransform root)
        {
            combatPanel = CreatePanel(
                "CombatStatus",
                root,
                new Vector2(1f, 1f),
                new Vector2(370f, 118f),
                DreamlandUiSkin.StrategicEnemyStrip,
                Color.white,
                true,
                new Vector2(-28f, -27f),
                new Vector2(1f, 1f));

            RectTransform rect = combatPanel.GetComponent<RectTransform>();

            combatWaveText = CreateText(
                "Wave",
                rect,
                new Vector2(0f, 0.70f),
                new Vector2(225f, 30f),
                18,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(82f, 0f),
                new Vector2(0f, 0.5f));
            combatWaveText.color = new Color(0.76f, 0.98f, 1f, 1f);

            combatCountText = CreateText(
                "EnemyCount",
                rect,
                new Vector2(1f, 0.52f),
                new Vector2(94f, 72f),
                46,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Vector2(-24f, 0f),
                new Vector2(1f, 0.5f));
            combatCountText.color = new Color(1f, 0.85f, 0.26f, 1f);

            combatDetailText = CreateText(
                "Detail",
                rect,
                new Vector2(0f, 0.28f),
                new Vector2(225f, 35f),
                16,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Vector2(82f, 0f),
                new Vector2(0f, 0.5f));
            combatDetailText.color = new Color(0.86f, 0.90f, 0.94f, 1f);

            combatPanel.SetActive(false);
        }

        private void BuildRolePanel(RectTransform root)
        {
            rolePanel = CreatePanel(
                "RoleStatus",
                root,
                new Vector2(0f, 0f),
                new Vector2(330f, 92f),
                DreamlandUiSkin.StrategicRoleStrip,
                Color.white,
                true,
                new Vector2(28f, 26f),
                Vector2.zero);

            RectTransform rect = rolePanel.GetComponent<RectTransform>();

            roleTitleText = CreateText(
                "RoleLabel",
                rect,
                new Vector2(0f, 0.68f),
                new Vector2(190f, 26f),
                16,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Vector2(78f, 0f),
                new Vector2(0f, 0.5f));
            roleTitleText.color = new Color(0.66f, 0.94f, 1f, 1f);

            roleNameText = CreateText(
                "RoleName",
                rect,
                new Vector2(0f, 0.34f),
                new Vector2(220f, 34f),
                23,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(78f, 0f),
                new Vector2(0f, 0.5f));
        }

        private void BuildBossPanel(RectTransform root)
        {
            bossPanel = CreatePanel(
                "BossHealthHUD",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(790f, 112f),
                DreamlandUiSkin.SciFiWindow,
                new Color(0.32f, 0.08f, 0.42f, 0.92f),
                true,
                new Vector2(0f, -116f),
                new Vector2(0.5f, 1f));

            RectTransform rect = bossPanel.GetComponent<RectTransform>();
            bossNameText = CreateText(
                "BossName",
                rect,
                new Vector2(0.06f, 0.70f),
                new Vector2(500f, 34f),
                23,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(0f, 0.5f));
            bossNameText.color = new Color(1f, 0.45f, 0.62f, 1f);

            bossHealthText = CreateText(
                "BossHP",
                rect,
                new Vector2(0.94f, 0.70f),
                new Vector2(190f, 34f),
                21,
                TextAnchor.MiddleRight,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(1f, 0.5f));

            GameObject barBg = CreatePanel(
                "BossBarBackground",
                rect,
                new Vector2(0.5f, 0.30f),
                new Vector2(690f, 25f),
                DreamlandUiSkin.SciFiBarBackground,
                Color.white,
                true);

            RectTransform bgRect = barBg.GetComponent<RectTransform>();
            GameObject fill = CreatePanel(
                "BossBarFill",
                bgRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                DreamlandUiSkin.SciFiBarPurple,
                Color.white,
                true);

            bossFillRect = fill.GetComponent<RectTransform>();
            bossFillRect.anchorMin = Vector2.zero;
            bossFillRect.anchorMax = Vector2.one;
            bossFillRect.pivot = new Vector2(0f, 0.5f);
            bossFillRect.offsetMin = new Vector2(5f, 5f);
            bossFillRect.offsetMax = new Vector2(-5f, -5f);
            bossFillImage = fill.GetComponent<Image>();

            bossPanel.SetActive(false);
        }

        private void BuildDialoguePanel(RectTransform root)
        {
            dialoguePanel = CreatePanel(
                "StoryDialogue",
                root,
                new Vector2(0.5f, 0f),
                new Vector2(1080f, 100f),
                DreamlandUiSkin.SciFiWindow,
                new Color(0.38f, 0.70f, 0.88f, 0.86f),
                true,
                new Vector2(0f, 38f),
                new Vector2(0.5f, 0f));

            RectTransform rect = dialoguePanel.GetComponent<RectTransform>();
            dialogueText = CreateText(
                "Dialogue",
                rect,
                new Vector2(0.5f, 0.5f),
                new Vector2(980f, 70f),
                24,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);

            dialoguePanel.SetActive(false);
        }

        private void BuildGuidePanel(RectTransform root)
        {
            guidePanel = CreatePanel(
                "ToyFriendComms",
                root,
                new Vector2(1f, 0.72f),
                new Vector2(525f, 190f),
                DreamlandUiSkin.StrategicMissionPanel,
                new Color(0.78f, 0.96f, 1f, 0.96f),
                true,
                new Vector2(-30f, 0f),
                new Vector2(1f, 0.5f));

            RectTransform rect = guidePanel.GetComponent<RectTransform>();
            guidePortrait = CreateImage(
                "Portrait",
                rect,
                new Vector2(0f, 0.48f),
                new Vector2(112f, 112f),
                Color.white,
                new Vector2(45f, 0f),
                new Vector2(0f, 0.5f));
            guidePortrait.preserveAspect = true;

            guideSpeaker = CreateText(
                "Speaker",
                rect,
                new Vector2(0f, 0.73f),
                new Vector2(300f, 35f),
                20,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(175f, 0f),
                new Vector2(0f, 0.5f));
            guideSpeaker.color = new Color(0.48f, 1f, 1f, 1f);

            guideMessage = CreateText(
                "Message",
                rect,
                new Vector2(0f, 0.43f),
                new Vector2(315f, 82f),
                20,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Vector2(175f, 0f),
                new Vector2(0f, 0.5f));

            guidePanel.SetActive(false);
        }

        private void BuildSynergyPanel(RectTransform root)
        {
            synergyPanel = CreatePanel(
                "SynergyAlert",
                root,
                new Vector2(0.5f, 0.36f),
                new Vector2(660f, 122f),
                DreamlandUiSkin.StrategicWarningPanel,
                new Color(0.58f, 0.95f, 1f, 1f),
                true);

            RectTransform rect = synergyPanel.GetComponent<RectTransform>();
            Image icon = CreateImage(
                "Lightning",
                rect,
                new Vector2(0f, 0.5f),
                new Vector2(58f, 72f),
                Color.white,
                new Vector2(78f, 0f),
                new Vector2(0.5f, 0.5f));
            icon.sprite = DreamlandUiSkin.StrategicLightning;
            icon.preserveAspect = true;

            synergyTitle = CreateText(
                "SynergyTitle",
                rect,
                new Vector2(0f, 0.64f),
                new Vector2(420f, 34f),
                24,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Vector2(150f, 0f),
                new Vector2(0f, 0.5f));
            synergyTitle.color = new Color(0.52f, 1f, 1f, 1f);

            synergyMessage = CreateText(
                "SynergyMessage",
                rect,
                new Vector2(0f, 0.36f),
                new Vector2(420f, 38f),
                19,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Vector2(150f, 0f),
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
            text.color = Color.white;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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
                return "COMBAT STATUS";
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
                return "ENEMIES REMAINING";
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
