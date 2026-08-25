using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DreamGuardians
{
    /// <summary>
    /// 장난감 친구의 이동과 기본 애니메이션을 제어합니다.
    /// SpawnPoint에서 TalkPoint까지 걸어온 뒤 플레이어를 바라봅니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToyFriendController : MonoBehaviour
    {
        private static readonly int BlendHash =
            Animator.StringToHash("Blend");
        private static readonly int NormalHash =
            Animator.StringToHash("normal");
        private static readonly int HappyHash =
            Animator.StringToHash("happy");

        [Header("References")]
        [SerializeField]
        private Animator animator;

        [Tooltip("플레이어의 Main Camera 또는 HMD 카메라를 연결하세요.")]
        [SerializeField]
        private Transform playerLookTarget;

        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private Transform talkPoint;

        [Tooltip("비워두면 Animator가 붙은 Visual 루트를 사용합니다.")]
        [SerializeField]
        private Transform visualRoot;

        [Header("Movement")]
        [SerializeField, Min(0.1f)]
        private float moveSpeed = 1.7f;

        [SerializeField, Min(1f)]
        private float rotationSpeed = 360f;

        [SerializeField, Min(0.01f)]
        private float stoppingDistance = 0.05f;

        [Tooltip("캐릭터가 뒤를 보며 걸으면 180으로 변경하세요.")]
        [SerializeField]
        private float modelForwardOffset;

        [Header("Animation")]
        [SerializeField, Range(0f, 1f)]
        private float idleBlend;

        [SerializeField, Range(0f, 1f)]
        private float walkBlend = 0.25f;

        [SerializeField, Min(0f)]
        private float animationDampTime = 0.12f;

        [Header("Speaking")]
        [Tooltip("말풍선 위치입니다. 비워두면 캐릭터 렌더러 위에 자동 배치합니다.")]
        [SerializeField]
        private Transform bubbleAnchor;

        [SerializeField, Min(0.001f)]
        private float bubbleWorldScale = 0.0035f;

        [SerializeField, Min(0f)]
        private float bubbleHeightPadding = 0.35f;

        [SerializeField, Min(0f)]
        private float speakingBobAmount = 0.025f;

        [SerializeField, Min(0f)]
        private float speakingSwayAngle = 2.5f;

        [SerializeField, Min(0.1f)]
        private float speakingMotionSpeed = 3.2f;

        [Tooltip("대사별 AudioClip이 연결되면 이 소스로 재생합니다.")]
        [SerializeField]
        private AudioSource voiceSource;

        [Tooltip(
            "voiceClip이 따로 없을 때, 동물의숲 스타일로 대사 길이만큼 " +
            "무작위 음절을 중얼거려주는 컴포넌트입니다. 비워두면 사용하지 않습니다.")]
        [SerializeField]
        private AnimaleseVoicePlayer animaleseVoice;

        [SerializeField]
        private UnityEvent onSpeechStarted;

        [SerializeField]
        private UnityEvent onSpeechFinished;

        [Header("Story Presence")]
        [Tooltip("전투 중 숨었다가 스토리 설명을 위해 다시 나타날 때의 기본 연출 시간입니다.")]
        [SerializeField, Min(0f)]
        private float storyPresenceTransitionDuration = 0.35f;

        [Header("Entrance Test")]
        [Tooltip("빛 등장 연출을 사용할 때는 외부 스크립트가 자동으로 해제합니다.")]
        [SerializeField]
        private bool playEntranceOnStart = true;

        [SerializeField]
        private bool hideBeforeEntrance;

        private Coroutine currentRoutine;
        private Coroutine speakingRoutine;
        private Renderer[] cachedRenderers;
        private GameObject speechBubbleRoot;
        private Text speechBubbleText;
        private Text speechBubbleSpeakerText;
        private MissionBannerUI missionUI;
        private MapMusicController mapMusicController;
        private bool dialogueDuckRequested;
        private bool storyFocusRequested;
        private Vector3 visualBaseLocalPosition;
        private Quaternion visualBaseLocalRotation;
        private Vector3 characterBaseLocalScale;
        private static Font runtimeFont;

        // ToyFriendViewHud(화면 좌측 상단 고정 로봇+말풍선 HUD)가 씬에 있는지
        // 한 번만 찾아서 캐싱한다. 있으면 3D 모델을 아예 표시하지 않고
        // 그쪽 HUD만 쓴다 - 3D 위치/회전 계산이 어긋나 카메라 코앞에
        // 뒷통수를 보이며 뜨는 문제를 근본적으로 피하기 위해서다.
        private bool _hasCheckedForHud;
        private bool _hasViewHud;

        public Transform SpawnPoint => spawnPoint;
        public Transform TalkPoint => talkPoint;
        public bool IsMoving { get; private set; }
        public bool IsSpeaking => speakingRoutine != null;
        public bool IsVisible { get; private set; } = true;

        private void Awake()
        {
            if (animator == null)
            {
                animator =
                    GetComponentInChildren<Animator>(true);
            }

            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.transform;
            }

            if (bubbleAnchor == null)
            {
                Transform existingAnchor = transform.Find("WorldBubblePoint");
                bubbleAnchor = existingAnchor;
            }

            if (playerLookTarget == null &&
                Camera.main != null)
            {
                playerLookTarget =
                    Camera.main.transform;
            }

            cachedRenderers =
                GetComponentsInChildren<Renderer>(true);

            if (visualRoot != null)
            {
                visualBaseLocalPosition = visualRoot.localPosition;
                visualBaseLocalRotation = visualRoot.localRotation;
            }

            characterBaseLocalScale = transform.localScale;

            SetBlend(idleBlend, true);

            if (hideBeforeEntrance)
            {
                SetVisible(false);
            }
            else
            {
                IsVisible = true;
            }
        }

        private IEnumerator Start()
        {
            yield return null;

            if (playEntranceOnStart)
            {
                PlayEntrance();
            }
        }

        private void Update()
        {
            // 이동 중이거나 말하는 중에는 각각의 루틴(MoveToRoutine/SpeakingRoutine)이
            // 이미 RotateTowards로 회전을 처리하므로, 가만히 서 있을 때만 여기서
            // 계속 플레이어 쪽을 바라보게 한다. 새 회전 로직 없이 기존
            // RotateTowards/GetTargetRotation을 그대로 재사용한다.
            if (IsMoving || IsSpeaking || !IsVisible)
            {
                return;
            }

            if (playerLookTarget == null && Camera.main != null)
            {
                playerLookTarget = Camera.main.transform;
            }

            if (playerLookTarget != null)
            {
                RotateTowards(playerLookTarget.position);
            }
        }

        private void LateUpdate()
        {
            if (speechBubbleRoot == null ||
                !speechBubbleRoot.activeSelf)
            {
                return;
            }

            if (playerLookTarget == null && Camera.main != null)
            {
                playerLookTarget = Camera.main.transform;
            }

            if (playerLookTarget != null)
            {
                // 카메라 회전값을 그대로 복사하면 말풍선(Canvas)의 앞면이
                // 카메라가 "보고 있는" 방향과 같은 쪽을 향하게 되어
                // 결과적으로 플레이어에게는 말풍선의 뒷면이 보인다.
                // 180도를 더해 앞면이 항상 카메라를 향하도록 한다.
                speechBubbleRoot.transform.rotation =
                    playerLookTarget.rotation *
                    Quaternion.Euler(0f, 180f, 0f);
            }
        }

        /// <summary>
        /// 외부 등장 연출이 자동 시작을 끄거나 켤 때 사용합니다.
        /// </summary>
        public void SetAutomaticEntrance(bool enabled)
        {
            playEntranceOnStart = enabled;
        }

        /// <summary>
        /// playerLookTarget이 비어 있으면 이 스크립트 곳곳에서 Camera.main으로
        /// 대체하는데, 이 프로젝트의 플레이어 카메라는 MainCamera 태그를 쓰지
        /// 않아서 Camera.main이 항상 null이다(멀티플레이에서 남의 카메라를 잘못
        /// 잡는 걸 막으려고 일부러 안 씀). 그래서 말할 때/등장 시 회전, 말풍선
        /// 정면 처리, 평상시 플레이어 응시가 전부 동작하지 않고 있었다.
        /// 로컬 플레이어 카메라가 확정되는 시점(NetworkPlayerMovement.Spawned())에
        /// 여기로 명시적으로 넘겨받는다.
        /// </summary>
        public void SetPlayerLookTarget(Transform target)
        {
            if (target != null)
            {
                playerLookTarget = target;
            }
        }

        /// <summary>
        /// 장난감 친구를 SpawnPoint에 배치하고 표시 여부를 설정합니다.
        /// </summary>
        public void PrepareAtSpawn(bool visible)
        {
            StopCurrentRoutine();
            StopSpeaking();

            if (spawnPoint != null)
            {
                transform.SetPositionAndRotation(
                    spawnPoint.position,
                    spawnPoint.rotation);
            }

            SetBlend(idleBlend, true);
            SetVisible(visible);
            IsMoving = false;
        }

        /// <summary>
        /// SpawnPoint에서 나타나 TalkPoint까지 이동합니다.
        /// </summary>
        public void PlayEntrance()
        {
            if (spawnPoint == null ||
                talkPoint == null)
            {
                Debug.LogWarning(
                    "[ToyFriendController] SpawnPoint 또는 TalkPoint가 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentRoutine();
            currentRoutine =
                StartCoroutine(EntranceRoutine());
        }

        public void MoveTo(Transform destination)
        {
            if (destination == null)
            {
                return;
            }

            StopCurrentRoutine();
            currentRoutine =
                StartCoroutine(
                    MoveToRoutine(destination.position));
        }

        public void LookAtPlayer()
        {
            if (playerLookTarget == null)
            {
                return;
            }

            StopCurrentRoutine();
            currentRoutine =
                StartCoroutine(
                    LookAtRoutine(
                        playerLookTarget.position));
        }

        /// <summary>
        /// 장난감 친구가 플레이어를 바라보며 말풍선과 작은 몸짓으로 말합니다.
        /// voiceClip은 비워둘 수 있으며, 나중에 대사별 음성을 바로 연결할 수 있습니다.
        /// </summary>
        public void Speak(
            string message,
            float duration,
            bool celebratory = false,
            AudioClip voiceClip = null)
        {
            StopSpeaking();

            // 좌측 상단 고정 HUD로 대체된 경우, 3D 캐릭터 머리 위 월드 말풍선은
            // 만들지 않는다 - 안 그러면 안 보이는 3D 모델 위치에 말풍선만 따로
            // 떠 있는 이상한 상태가 될 수 있다.
            if (!HasViewHud())
            {
                EnsureSpeechBubble();
            }

            missionUI ??= Object.FindAnyObjectByType<MissionBannerUI>();
            mapMusicController ??= Object.FindAnyObjectByType<MapMusicController>();
            if (missionUI != null)
            {
                missionUI.HideTransientMessages();
                missionUI.BeginToyFriendStoryFocus();
                storyFocusRequested = true;
            }

            speakingRoutine = StartCoroutine(
                SpeakingRoutine(
                    message,
                    Mathf.Max(0.2f, duration),
                    celebratory,
                    voiceClip));

            if (mapMusicController != null)
            {
                mapMusicController.BeginToyFriendDialogueDuck();
                dialogueDuckRequested = true;
            }
        }

        public void StopSpeaking()
        {
            if (speakingRoutine != null)
            {
                StopCoroutine(speakingRoutine);
                speakingRoutine = null;
            }

            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }

            RestoreVisualPose();

            if (speechBubbleRoot != null)
            {
                speechBubbleRoot.SetActive(false);
            }

            ToyFriendViewHud.Instance?.Hide();

            if (storyFocusRequested)
            {
                missionUI?.EndToyFriendStoryFocus();
                storyFocusRequested = false;
            }

            if (dialogueDuckRequested)
            {
                if (mapMusicController != null)
                {
                    mapMusicController.EndToyFriendDialogueDuck();
                }
                dialogueDuckRequested = false;
            }
        }

        public void SetVisible(bool visible)
        {
            // 3D 캐릭터는 map_3 중앙에서 원래대로 걸어다니며 보이도록 그대로 둔다.
            // (말풍선 내용만 좌측 상단 HUD로 대체한다 - HasViewHud() 관련 로직 참고)
            IsVisible = visible;

            if (cachedRenderers == null)
            {
                cachedRenderers =
                    GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0;
                 i < cachedRenderers.Length;
                 i++)
            {
                Renderer targetRenderer =
                    cachedRenderers[i];

                if (targetRenderer != null)
                {
                    targetRenderer.enabled = visible;
                }
            }

            if (!visible && speechBubbleRoot != null)
            {
                speechBubbleRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 전투 중 숨었던 장난감 친구를 TalkPoint에서 다시 보여줍니다.
        /// Stage 1의 시너지 설명처럼 중요한 3D 스토리 장면에 사용합니다.
        /// </summary>
        public IEnumerator ShowForStory(float duration = -1f)
        {
            StopCurrentRoutine();
            StopSpeaking();

            if (talkPoint != null)
            {
                transform.position = talkPoint.position;
            }

            float safeDuration = duration >= 0f
                ? duration
                : storyPresenceTransitionDuration;

            Vector3 targetScale = characterBaseLocalScale;

            // Stage 2 클리어 → 전조 → 보스 설명 → 엔딩처럼 스토리가
            // 연속될 때 이미 보이는 친구를 매 대사 구간마다 다시 축소/등장시키지 않습니다.
            if (IsVisible)
            {
                transform.localScale = targetScale;
                FacePlayerImmediately();
                yield break;
            }

            Vector3 startScale = targetScale * 0.05f;

            transform.localScale = startScale;
            SetVisible(true);

            if (safeDuration <= 0f)
            {
                transform.localScale = targetScale;
                FacePlayerImmediately();
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / safeDuration));

                transform.localScale = Vector3.LerpUnclamped(
                    startScale,
                    targetScale,
                    t);

                FacePlayerImmediately();
                yield return null;
            }

            transform.localScale = targetScale;
            FacePlayerImmediately();
        }

        /// <summary>
        /// 장난감 친구를 전투 화면에서 숨깁니다.
        /// 오브젝트를 비활성화하지 않으므로 이후 3D 설명 때 다시 사용할 수 있습니다.
        /// </summary>
        public IEnumerator HideForCombat(float duration = -1f)
        {
            StopCurrentRoutine();
            StopSpeaking();

            float safeDuration = duration >= 0f
                ? duration
                : storyPresenceTransitionDuration;

            Vector3 startScale = characterBaseLocalScale;
            Vector3 targetScale = startScale * 0.05f;

            transform.localScale = startScale;

            if (safeDuration > 0f)
            {
                float elapsed = 0f;

                while (elapsed < safeDuration)
                {
                    elapsed += Time.deltaTime;

                    float t = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(elapsed / safeDuration));

                    transform.localScale = Vector3.LerpUnclamped(
                        startScale,
                        targetScale,
                        t);

                    yield return null;
                }
            }

            SetVisible(false);
            transform.localScale = characterBaseLocalScale;
        }

        /// <summary>
        /// 연출 없이 즉시 전투용 숨김 상태로 만듭니다.
        /// Stage 1 직접 시작 및 테스트 진입의 안전장치입니다.
        /// </summary>
        public void HideForCombatImmediately()
        {
            StopCurrentRoutine();
            StopSpeaking();
            transform.localScale = characterBaseLocalScale;
            SetVisible(false);
        }

        private void FacePlayerImmediately()
        {
            if (playerLookTarget == null && Camera.main != null)
            {
                playerLookTarget = Camera.main.transform;
            }

            if (playerLookTarget == null)
            {
                return;
            }

            Vector3 lookDirection =
                playerLookTarget.position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up) *
                    Quaternion.Euler(0f, modelForwardOffset, 0f);
            }
        }

        private IEnumerator SpeakingRoutine(
            string message,
            float duration,
            bool celebratory,
            AudioClip voiceClip)
        {
            if (speechBubbleText != null)
            {
                speechBubbleText.text = message ?? string.Empty;
            }

            if (speechBubbleRoot != null)
            {
                speechBubbleRoot.SetActive(true);
            }

            ToyFriendViewHud.Instance?.ShowMessage(message);

            if (animator != null)
            {
                int trigger = celebratory ? HappyHash : NormalHash;

                if (HasAnimatorParameter(trigger, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(trigger);
                }

                SetBlend(idleBlend, true);
            }

            if (voiceClip != null)
            {
                if (voiceSource == null)
                {
                    voiceSource = gameObject.AddComponent<AudioSource>();
                    voiceSource.playOnAwake = false;
                    voiceSource.spatialBlend = 1f;
                    voiceSource.minDistance = 1f;
                    voiceSource.maxDistance = 18f;
                }

                voiceSource.clip = voiceClip;
                voiceSource.Play();
            }
            else if (animaleseVoice != null)
            {
                animaleseVoice.PlayForText(message, 0.055f);
            }

            onSpeechStarted?.Invoke();

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                if (playerLookTarget != null)
                {
                    RotateTowards(playerLookTarget.position);
                }

                if (visualRoot != null)
                {
                    float phase = elapsed * speakingMotionSpeed;
                    float bob = Mathf.Sin(phase * 2f) * speakingBobAmount;
                    float sway = Mathf.Sin(phase) * speakingSwayAngle;

                    visualRoot.localPosition =
                        visualBaseLocalPosition + Vector3.up * bob;
                    visualRoot.localRotation =
                        visualBaseLocalRotation *
                        Quaternion.Euler(0f, 0f, sway);
                }

                yield return null;
            }

            RestoreVisualPose();

            if (animator != null &&
                HasAnimatorParameter(NormalHash, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(NormalHash);
            }

            if (speechBubbleRoot != null)
            {
                speechBubbleRoot.SetActive(false);
            }

            ToyFriendViewHud.Instance?.Hide();

            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }

            if (animaleseVoice != null)
            {
                animaleseVoice.StopBabble();
            }

            if (storyFocusRequested)
            {
                missionUI?.EndToyFriendStoryFocus();
                storyFocusRequested = false;
            }

            if (dialogueDuckRequested)
            {
                if (mapMusicController != null)
                {
                    mapMusicController.EndToyFriendDialogueDuck();
                }
                dialogueDuckRequested = false;
            }

            onSpeechFinished?.Invoke();
            speakingRoutine = null;
        }

        private void OnDisable()
        {
            StopCurrentRoutine();
            StopSpeaking();
            if (animaleseVoice != null)
            {
                animaleseVoice.StopBabble();
            }
        }

        private void EnsureSpeechBubble()
        {
            if (speechBubbleRoot != null)
            {
                return;
            }

            if (bubbleAnchor == null)
            {
                GameObject anchorObject = new GameObject("WorldBubblePoint");
                bubbleAnchor = anchorObject.transform;
                bubbleAnchor.SetParent(transform, false);

                Bounds bounds = CalculateRendererBounds();
                Vector3 worldPosition = bounds.size.sqrMagnitude > 0f
                    ? new Vector3(bounds.center.x, bounds.max.y + bubbleHeightPadding, bounds.center.z)
                    : transform.position + Vector3.up * 2f;

                bubbleAnchor.position = worldPosition;
            }
            else if (Mathf.Abs(bubbleAnchor.localPosition.y) < 0.01f)
            {
                Bounds bounds = CalculateRendererBounds();

                if (bounds.size.sqrMagnitude > 0f)
                {
                    bubbleAnchor.position = new Vector3(
                        bounds.center.x,
                        bounds.max.y + bubbleHeightPadding,
                        bounds.center.z);
                }
            }

            speechBubbleRoot = new GameObject(
                "ToyFriendSpeechBubble",
                typeof(RectTransform),
                typeof(Canvas));
            speechBubbleRoot.transform.SetParent(bubbleAnchor, false);

            Canvas canvas = speechBubbleRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            RectTransform canvasRect = speechBubbleRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(700f, 224f);
            canvasRect.localScale = Vector3.one * bubbleWorldScale;

            GameObject panelObject = new GameObject(
                "BubblePanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panelObject.transform.SetParent(speechBubbleRoot.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panel = panelObject.GetComponent<Image>();
            panel.sprite = DreamlandUiSkin.KenneyMissionPanel;
            panel.type = panel.sprite != null && panel.sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            panel.color = new Color(1f, 1f, 1f, 0.62f);
            panel.raycastTarget = false;

            GameObject innerObject = new GameObject(
                "BubbleInner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            innerObject.transform.SetParent(speechBubbleRoot.transform, false);
            RectTransform innerRect = innerObject.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(18f, 18f);
            innerRect.offsetMax = new Vector2(-18f, -18f);
            Image inner = innerObject.GetComponent<Image>();
            inner.color = new Color(0.91f, 0.97f, 1f, 0.94f);
            inner.raycastTarget = false;

            GameObject accentObject = new GameObject(
                "BubbleAccent",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            accentObject.transform.SetParent(speechBubbleRoot.transform, false);
            RectTransform accentRect = accentObject.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0.5f, 1f);
            accentRect.anchorMax = new Vector2(0.5f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = new Vector2(0f, -19f);
            accentRect.sizeDelta = new Vector2(470f, 8f);
            Image accent = accentObject.GetComponent<Image>();
            accent.sprite = DreamlandUiSkin.KenneyCoreBarBlue;
            accent.type = accent.sprite != null && accent.sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            accent.color = new Color(0.54f, 0.90f, 0.90f, 0.95f);
            accent.raycastTarget = false;

            GameObject speakerObject = new GameObject(
                "Speaker",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            speakerObject.transform.SetParent(speechBubbleRoot.transform, false);
            RectTransform speakerRect = speakerObject.GetComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(0f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = new Vector2(48f, -34f);
            speakerRect.sizeDelta = new Vector2(250f, 32f);

            speechBubbleSpeakerText = speakerObject.GetComponent<Text>();
            speechBubbleSpeakerText.font = GetRuntimeFont();
            speechBubbleSpeakerText.fontSize = 24;
            speechBubbleSpeakerText.fontStyle = FontStyle.Bold;
            speechBubbleSpeakerText.alignment = TextAnchor.MiddleLeft;
            speechBubbleSpeakerText.text = "장난감 친구";
            speechBubbleSpeakerText.color = new Color(0.23f, 0.58f, 0.67f, 1f);
            speechBubbleSpeakerText.raycastTarget = false;

            GameObject tailObject = new GameObject(
                "BubbleTail",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            tailObject.transform.SetParent(speechBubbleRoot.transform, false);

            RectTransform tailRect = tailObject.GetComponent<RectTransform>();
            tailRect.anchorMin = new Vector2(0.30f, 0f);
            tailRect.anchorMax = new Vector2(0.30f, 0f);
            tailRect.pivot = new Vector2(0.5f, 0.5f);
            tailRect.anchoredPosition = new Vector2(0f, -20f);
            tailRect.sizeDelta = new Vector2(48f, 48f);
            tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image tail = tailObject.GetComponent<Image>();
            tail.color = new Color(0.74f, 0.90f, 1f, 0.98f);
            tail.raycastTarget = false;
            Outline tailOutline = tailObject.AddComponent<Outline>();
            tailOutline.effectColor = new Color(0.38f, 0.72f, 0.80f, 0.80f);
            tailOutline.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject(
                "Message",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(speechBubbleRoot.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(48f, 30f);
            textRect.offsetMax = new Vector2(-48f, -70f);

            speechBubbleText = textObject.GetComponent<Text>();
            speechBubbleText.font = GetRuntimeFont();
            speechBubbleText.fontSize = 32;
            speechBubbleText.fontStyle = FontStyle.Bold;
            speechBubbleText.alignment = TextAnchor.MiddleCenter;
            speechBubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            speechBubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            speechBubbleText.resizeTextForBestFit = true;
            speechBubbleText.resizeTextMinSize = 24;
            speechBubbleText.resizeTextMaxSize = 32;
            speechBubbleText.color = new Color(0.10f, 0.18f, 0.27f, 1f);
            speechBubbleText.raycastTarget = false;

            speechBubbleRoot.SetActive(false);
        }

        private Bounds CalculateRendererBounds()
        {
            if (cachedRenderers == null)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            Bounds bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer targetRenderer = cachedRenderers[i];

                if (targetRenderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }

            return bounds;
        }

        /// <summary>
        /// 씬에 ToyFriendViewHud(좌측 상단 고정 로봇+말풍선 HUD)가 있는지
        /// 한 번만 검사해서 캐싱한다.
        /// </summary>
        private bool HasViewHud()
        {
            if (!_hasCheckedForHud)
            {
                _hasCheckedForHud = true;
                _hasViewHud = ToyFriendViewHud.Instance != null ||
                    FindAnyObjectByType<ToyFriendViewHud>() != null;
            }

            return _hasViewHud;
        }

        private bool HasAnimatorParameter(
            int parameterHash,
            AnimatorControllerParameterType parameterType)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == parameterHash &&
                    parameters[i].type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreVisualPose()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localRotation = visualBaseLocalRotation;
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

        private IEnumerator EntranceRoutine()
        {
            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation);

            SetVisible(true);
            SetBlend(idleBlend, true);

            yield return MoveToRoutine(
                talkPoint.position);

            if (playerLookTarget != null)
            {
                yield return LookAtRoutine(
                    playerLookTarget.position);
            }

            SetBlend(idleBlend);
            currentRoutine = null;
        }

        private IEnumerator MoveToRoutine(
            Vector3 destination)
        {
            IsMoving = true;
            SetBlend(walkBlend);

            while (HorizontalDistance(
                       transform.position,
                       destination) >
                   stoppingDistance)
            {
                RotateTowards(destination);

                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        destination,
                        moveSpeed *
                        Time.deltaTime);

                yield return null;
            }

            transform.position = destination;
            SetBlend(idleBlend);
            IsMoving = false;
        }

        private IEnumerator LookAtRoutine(
            Vector3 targetPosition)
        {
            Quaternion targetRotation =
                GetTargetRotation(targetPosition);

            while (Quaternion.Angle(
                       transform.rotation,
                       targetRotation) >
                   0.5f)
            {
                transform.rotation =
                    Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed *
                        Time.deltaTime);

                yield return null;
            }

            transform.rotation = targetRotation;
        }

        private void RotateTowards(
            Vector3 targetPosition)
        {
            Quaternion targetRotation =
                GetTargetRotation(targetPosition);

            transform.rotation =
                Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime);
        }

        private Quaternion GetTargetRotation(
            Vector3 targetPosition)
        {
            Vector3 direction =
                targetPosition -
                transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude <
                0.0001f)
            {
                return transform.rotation;
            }

            return
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up) *
                Quaternion.Euler(
                    0f,
                    modelForwardOffset,
                    0f);
        }

        private void SetBlend(
            float value,
            bool immediate = false)
        {
            if (animator == null)
            {
                return;
            }

            if (immediate)
            {
                animator.SetFloat(
                    BlendHash,
                    value);
            }
            else
            {
                animator.SetFloat(
                    BlendHash,
                    value,
                    animationDampTime,
                    Time.deltaTime);
            }
        }

        private void StopCurrentRoutine()
        {
            if (currentRoutine == null)
            {
                return;
            }

            StopCoroutine(currentRoutine);
            currentRoutine = null;
            IsMoving = false;
        }

        private static float HorizontalDistance(
            Vector3 first,
            Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;

            return Vector3.Distance(
                first,
                second);
        }
    }
}
