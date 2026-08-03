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
        private Vector3 visualBaseLocalPosition;
        private Quaternion visualBaseLocalRotation;
        private Vector3 characterBaseLocalScale;
        private static Font runtimeFont;

        public Transform SpawnPoint => spawnPoint;
        public Transform TalkPoint => talkPoint;
        public bool IsMoving { get; private set; }
        public bool IsSpeaking => speakingRoutine != null;

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
        }

        private IEnumerator Start()
        {
            yield return null;

            if (playEntranceOnStart)
            {
                PlayEntrance();
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
                speechBubbleRoot.transform.rotation =
                    playerLookTarget.rotation;
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
            EnsureSpeechBubble();

            speakingRoutine = StartCoroutine(
                SpeakingRoutine(
                    message,
                    Mathf.Max(0.2f, duration),
                    celebratory,
                    voiceClip));
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
        }

        public void SetVisible(bool visible)
        {
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

            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }

            onSpeechFinished?.Invoke();
            speakingRoutine = null;
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
            canvasRect.sizeDelta = new Vector2(640f, 190f);
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
            panel.color = new Color(0.96f, 1f, 0.98f, 0.96f);
            panel.raycastTarget = false;

            GameObject tailObject = new GameObject(
                "BubbleTail",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            tailObject.transform.SetParent(speechBubbleRoot.transform, false);

            RectTransform tailRect = tailObject.GetComponent<RectTransform>();
            tailRect.anchorMin = new Vector2(0.28f, 0f);
            tailRect.anchorMax = new Vector2(0.28f, 0f);
            tailRect.pivot = new Vector2(0.5f, 0.5f);
            tailRect.anchoredPosition = new Vector2(0f, -22f);
            tailRect.sizeDelta = new Vector2(54f, 54f);
            tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image tail = tailObject.GetComponent<Image>();
            tail.color = panel.color;
            tail.raycastTarget = false;

            GameObject textObject = new GameObject(
                "Message",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(speechBubbleRoot.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(38f, 24f);
            textRect.offsetMax = new Vector2(-38f, -24f);

            speechBubbleText = textObject.GetComponent<Text>();
            speechBubbleText.font = GetRuntimeFont();
            speechBubbleText.fontSize = 34;
            speechBubbleText.fontStyle = FontStyle.Bold;
            speechBubbleText.alignment = TextAnchor.MiddleCenter;
            speechBubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            speechBubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            speechBubbleText.color = new Color(0.05f, 0.12f, 0.11f, 1f);
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
