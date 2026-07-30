using System.Collections;
using UnityEngine;

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

        [Header("Entrance Test")]
        [Tooltip("빛 등장 연출을 사용할 때는 외부 스크립트가 자동으로 해제합니다.")]
        [SerializeField]
        private bool playEntranceOnStart = true;

        [SerializeField]
        private bool hideBeforeEntrance;

        private Coroutine currentRoutine;
        private Renderer[] cachedRenderers;

        public Transform SpawnPoint => spawnPoint;
        public Transform TalkPoint => talkPoint;
        public bool IsMoving { get; private set; }

        private void Awake()
        {
            if (animator == null)
            {
                animator =
                    GetComponentInChildren<Animator>(true);
            }

            if (playerLookTarget == null &&
                Camera.main != null)
            {
                playerLookTarget =
                    Camera.main.transform;
            }

            cachedRenderers =
                GetComponentsInChildren<Renderer>(true);

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
