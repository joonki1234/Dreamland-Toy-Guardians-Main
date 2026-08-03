using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DreamGuardians
{
    /// <summary>
    /// 튜토리얼 안에서 8명이 네 직업을 두 명씩 선택하는 단계입니다.
    ///
    /// 현재 프로토타입에서는 한 화면에서 PLAYER 1~8을 차례로 선택합니다.
    /// 멀티플레이가 연결되면 각 클라이언트가 SubmitSelection(playerId, role)을
    /// 호출하는 방식으로 같은 규칙을 그대로 사용할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoleSelectionController : MonoBehaviour
    {
        private static readonly PlayerRole[] SelectableRoles =
        {
            PlayerRole.Police,
            PlayerRole.Firefighter,
            PlayerRole.Chef,
            PlayerRole.Architect
        };

        [Header("Selection Rule")]
        [SerializeField, Min(1)] private int playerCount = 8;
        [SerializeField, Min(1)] private int maxPlayersPerRole = 2;
        [SerializeField] private string localPlayerId = "PLAYER_1";
        [SerializeField, Min(0f)] private float completionHoldDuration = 1.2f;

        [Header("Optional References")]
        [SerializeField] private PrototypeRayWeapon localWeapon;
        [SerializeField] private GameObject customSelectionRoot;

        private readonly Dictionary<string, PlayerRole> selections =
            new Dictionary<string, PlayerRole>();
        private readonly List<string> playerIds = new List<string>();
        private readonly Dictionary<PlayerRole, Button> roleButtons =
            new Dictionary<PlayerRole, Button>();
        private readonly Dictionary<PlayerRole, Text> roleButtonTexts =
            new Dictionary<PlayerRole, Text>();

        private GameObject runtimeRoot;
        private Text seatListText;
        private Text currentPlayerText;
        private Text statusText;
        private int currentSeatIndex;
        private static Font runtimeFont;

        public bool IsShowing { get; private set; }
        public bool IsComplete { get; private set; }
        public IReadOnlyDictionary<string, PlayerRole> Selections => selections;

        public event Action SelectionCompleted;

        private GameObject ActiveRoot =>
            customSelectionRoot != null ? customSelectionRoot : runtimeRoot;

        private void Awake()
        {
            playerCount = Mathf.Max(1, playerCount);
            maxPlayersPerRole = Mathf.Max(1, maxPlayersPerRole);
            BuildPrototypePlayerList();

            if (localWeapon == null)
            {
                localWeapon = FindAnyObjectByType<PrototypeRayWeapon>();
            }

            if (customSelectionRoot != null)
            {
                customSelectionRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsShowing || Keyboard.current == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard.digit1Key.wasPressedThisFrame ||
                keyboard.numpad1Key.wasPressedThisFrame)
            {
                SelectForCurrentSeat(PlayerRole.Police);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame ||
                     keyboard.numpad2Key.wasPressedThisFrame)
            {
                SelectForCurrentSeat(PlayerRole.Firefighter);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame ||
                     keyboard.numpad3Key.wasPressedThisFrame)
            {
                SelectForCurrentSeat(PlayerRole.Chef);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame ||
                     keyboard.numpad4Key.wasPressedThisFrame)
            {
                SelectForCurrentSeat(PlayerRole.Architect);
            }
        }

        public IEnumerator ShowAndWait()
        {
            Show();

            while (!IsComplete)
            {
                yield return null;
            }

            if (completionHoldDuration > 0f)
            {
                yield return new WaitForSeconds(completionHoldDuration);
            }

            Hide();
        }

        public void Show()
        {
            EnsureRuntimeUI();
            IsShowing = true;

            if (ActiveRoot != null)
            {
                ActiveRoot.SetActive(true);
            }

            RefreshUI();
        }

        public void Hide()
        {
            IsShowing = false;

            if (ActiveRoot != null)
            {
                ActiveRoot.SetActive(false);
            }
        }

        public void ResetSelection()
        {
            selections.Clear();
            currentSeatIndex = 0;
            IsComplete = false;
            RefreshUI();
        }

        /// <summary>
        /// 실제 멀티플레이어의 목록이 준비되었을 때 호출합니다.
        /// playerIds의 개수는 현재 설정된 8명과 같아야 합니다.
        /// </summary>
        public bool ConfigurePlayers(IReadOnlyList<string> networkPlayerIds)
        {
            if (networkPlayerIds == null || networkPlayerIds.Count != playerCount)
            {
                Debug.LogWarning(
                    $"[RoleSelection] 플레이어 목록은 정확히 {playerCount}명이어야 합니다.",
                    this);
                return false;
            }

            playerIds.Clear();

            for (int i = 0; i < networkPlayerIds.Count; i++)
            {
                string id = networkPlayerIds[i];

                if (string.IsNullOrWhiteSpace(id) || playerIds.Contains(id))
                {
                    Debug.LogWarning(
                        "[RoleSelection] 비어 있거나 중복된 플레이어 ID가 있습니다.",
                        this);
                    BuildPrototypePlayerList();
                    return false;
                }

                playerIds.Add(id);
            }

            ResetSelection();
            return true;
        }

        /// <summary>
        /// 네트워크 플레이어 한 명의 선택을 등록합니다.
        /// 이미 선택한 플레이어는 자리가 남는 다른 직업으로 변경할 수 있습니다.
        /// </summary>
        public bool SubmitSelection(string playerId, PlayerRole role)
        {
            if (IsComplete ||
                string.IsNullOrWhiteSpace(playerId) ||
                !playerIds.Contains(playerId) ||
                Array.IndexOf(SelectableRoles, role) < 0)
            {
                return false;
            }

            selections.TryGetValue(playerId, out PlayerRole previousRole);

            if (previousRole == role)
            {
                return true;
            }

            if (GetRoleCount(role) >= maxPlayersPerRole)
            {
                SetStatus($"{DreamGameText.GetRoleName(role)} 자리는 이미 가득 찼어!");
                return false;
            }

            selections[playerId] = role;

            if (playerId == localPlayerId && localWeapon != null)
            {
                localWeapon.SetPlayerIdentity(playerId, role);
            }

            MoveToNextUnassignedSeat();
            RefreshUI();
            CheckCompletion();
            return true;
        }

        public void SelectForCurrentSeat(PlayerRole role)
        {
            if (currentSeatIndex < 0 || currentSeatIndex >= playerIds.Count)
            {
                return;
            }

            SubmitSelection(playerIds[currentSeatIndex], role);
        }

        private void CheckCompletion()
        {
            if (selections.Count != playerCount)
            {
                return;
            }

            for (int i = 0; i < SelectableRoles.Length; i++)
            {
                if (GetRoleCount(SelectableRoles[i]) != maxPlayersPerRole)
                {
                    return;
                }
            }

            IsComplete = true;
            SetStatus("직업 선택 완료 · 모든 수호대원이 준비됐어!");

            foreach (Button button in roleButtons.Values)
            {
                button.interactable = false;
            }

            SelectionCompleted?.Invoke();
        }

        private void MoveToNextUnassignedSeat()
        {
            for (int offset = 1; offset <= playerIds.Count; offset++)
            {
                int index = (currentSeatIndex + offset) % playerIds.Count;

                if (!selections.ContainsKey(playerIds[index]))
                {
                    currentSeatIndex = index;
                    return;
                }
            }
        }

        private int GetRoleCount(PlayerRole role)
        {
            int count = 0;

            foreach (PlayerRole selectedRole in selections.Values)
            {
                if (selectedRole == role)
                {
                    count++;
                }
            }

            return count;
        }

        private void BuildPrototypePlayerList()
        {
            playerIds.Clear();

            for (int i = 0; i < playerCount; i++)
            {
                playerIds.Add($"PLAYER_{i + 1}");
            }

            if (!playerIds.Contains(localPlayerId))
            {
                localPlayerId = playerIds[0];
            }

            ResetSelection();
        }

        private void RefreshUI()
        {
            if (seatListText != null)
            {
                System.Text.StringBuilder builder = new System.Text.StringBuilder();

                for (int i = 0; i < playerIds.Count; i++)
                {
                    string id = playerIds[i];
                    string marker = i == currentSeatIndex && !IsComplete ? "▶ " : "   ";
                    string roleName = selections.TryGetValue(id, out PlayerRole role)
                        ? DreamGameText.GetRoleName(role)
                        : "선택 대기";

                    builder.AppendLine($"{marker}플레이어 {i + 1}  ·  {roleName}");
                }

                seatListText.text = builder.ToString();
            }

            if (currentPlayerText != null)
            {
                currentPlayerText.text = IsComplete
                    ? "8 / 8 선택 완료"
                    : $"플레이어 {currentSeatIndex + 1}의 직업을 선택하세요";
            }

            foreach (PlayerRole role in SelectableRoles)
            {
                int count = GetRoleCount(role);

                if (roleButtonTexts.TryGetValue(role, out Text label))
                {
                    label.text =
                        $"{GetRoleNumber(role)}  {DreamGameText.GetRoleName(role)}\n" +
                        $"{GetRoleDescription(role)}\n{count} / {maxPlayersPerRole}";
                }

                if (roleButtons.TryGetValue(role, out Button button))
                {
                    button.interactable = !IsComplete && count < maxPlayersPerRole;
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private void EnsureRuntimeUI()
        {
            if (customSelectionRoot != null || runtimeRoot != null)
            {
                return;
            }

            EnsureEventSystem();

            GameObject canvasObject = new GameObject(
                "RoleSelectionCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            Camera targetCamera = ResolveUICamera();
            canvas.renderMode = targetCamera != null
                ? RenderMode.ScreenSpaceCamera
                : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 2000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            runtimeRoot = CreatePanel(
                "RoleSelectionRoot",
                canvasObject.transform,
                new Color(0.015f, 0.035f, 0.07f, 0.96f));

            CreateText(
                "Title",
                runtimeRoot.transform,
                "꿈나라 수호대 · 직업 선택",
                52,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.89f),
                new Vector2(1500f, 90f));

            CreateText(
                "Rule",
                runtimeRoot.transform,
                "총 8명 · 직업별 2명   |   버튼 또는 숫자 1~4로 선택",
                26,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.82f),
                new Vector2(1500f, 55f));

            seatListText = CreateText(
                "SeatList",
                runtimeRoot.transform,
                string.Empty,
                28,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                new Vector2(0.2f, 0.51f),
                new Vector2(520f, 500f));

            currentPlayerText = CreateText(
                "CurrentPlayer",
                runtimeRoot.transform,
                string.Empty,
                31,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.65f, 0.72f),
                new Vector2(980f, 60f));

            for (int i = 0; i < SelectableRoles.Length; i++)
            {
                PlayerRole capturedRole = SelectableRoles[i];
                float x = i % 2 == 0 ? 0.52f : 0.78f;
                float y = i < 2 ? 0.56f : 0.34f;
                Button button = CreateRoleButton(
                    runtimeRoot.transform,
                    capturedRole,
                    new Vector2(x, y));

                button.onClick.AddListener(() => SelectForCurrentSeat(capturedRole));
                roleButtons[capturedRole] = button;
                roleButtonTexts[capturedRole] = button.GetComponentInChildren<Text>();
            }

            statusText = CreateText(
                "Status",
                runtimeRoot.transform,
                "플레이어 1부터 차례로 직업을 선택해 줘.",
                26,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.65f, 0.16f),
                new Vector2(1100f, 70f));
            statusText.color = new Color(0.65f, 1f, 0.88f, 1f);

            runtimeRoot.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
        }

        private static Camera ResolveUICamera()
        {
            if (Camera.main != null)
            {
                return Camera.main;
            }

            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsSortMode.None);

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
            Color color)
        {
            GameObject panel = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Button CreateRoleButton(
            Transform parent,
            PlayerRole role,
            Vector2 anchor)
        {
            GameObject buttonObject = new GameObject(
                DreamGameText.GetRoleName(role) + "Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(430f, 180f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = GetRoleColor(role);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(image.color, Color.black, 0.15f);
            colors.disabledColor = new Color(0.22f, 0.24f, 0.28f, 0.65f);
            button.colors = colors;

            Text label = CreateText(
                "Label",
                buttonObject.transform,
                DreamGameText.GetRoleName(role),
                26,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f),
                new Vector2(400f, 160f));
            label.raycastTarget = false;
            return button;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 anchor,
            Vector2 size)
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
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.text = value;
            return text;
        }

        private static int GetRoleNumber(PlayerRole role)
        {
            return Array.IndexOf(SelectableRoles, role) + 1;
        }

        private static string GetRoleDescription(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Police => "위험 탐지와 제압",
                PlayerRole.Firefighter => "긴급 대응과 보호",
                PlayerRole.Chef => "회복과 전투 지원",
                PlayerRole.Architect => "길과 방어 구축",
                _ => string.Empty
            };
        }

        private static Color GetRoleColor(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Police => new Color(0.16f, 0.37f, 0.66f, 0.95f),
                PlayerRole.Firefighter => new Color(0.72f, 0.24f, 0.18f, 0.95f),
                PlayerRole.Chef => new Color(0.28f, 0.57f, 0.32f, 0.95f),
                PlayerRole.Architect => new Color(0.65f, 0.48f, 0.16f, 0.95f),
                _ => new Color(0.3f, 0.3f, 0.3f, 0.95f)
            };
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

        private void OnValidate()
        {
            playerCount = Mathf.Max(1, playerCount);
            maxPlayersPerRole = Mathf.Max(1, maxPlayersPerRole);
            completionHoldDuration = Mathf.Max(0f, completionHoldDuration);
        }
    }
}
