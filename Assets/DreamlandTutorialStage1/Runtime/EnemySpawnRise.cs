using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnRise : MonoBehaviour
    {
        private Coroutine routine;

        /// <summary>
        /// 적의 등장 연출을 시작한다.
        ///
        /// usePortalDirection이 false면:
        /// 튜토리얼 적처럼 아래에서 위로 등장한다.
        ///
        /// usePortalDirection이 true면:
        /// 전달받은 portalForward 방향을 따라 포탈 밖으로 등장한다.
        /// </summary>
        public void Begin(
            Vector3 finalPosition,
            float depth,
            float duration,
            EnemyCoreMover mover,
            FloorRiftMarker rift,
            bool enableMoverAfterRise = true,
            bool usePortalDirection = false,
            Vector3 portalForward = default)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(
                RiseRoutine(
                    finalPosition,
                    Mathf.Max(0f, depth),
                    Mathf.Max(0.05f, duration),
                    mover,
                    rift,
                    enableMoverAfterRise,
                    usePortalDirection,
                    portalForward)
            );
        }

        private IEnumerator RiseRoutine(
            Vector3 finalPosition,
            float depth,
            float duration,
            EnemyCoreMover mover,
            FloorRiftMarker rift,
            bool enableMoverAfterRise,
            bool usePortalDirection,
            Vector3 portalForward)
        {
            // 등장 연출 중에는 코어 이동을 잠시 멈춘다.
            if (mover != null)
            {
                mover.enabled = false;
            }

            rift?.Play();

            Vector3 targetScale = transform.localScale;
            Vector3 startPosition;

            if (usePortalDirection &&
                portalForward.sqrMagnitude > 0.0001f)
            {
                // 일반 웨이브 적:
                // 포탈 뒤쪽에서 시작하여 포탈 앞 방향으로 나온다.
                Vector3 direction = portalForward.normalized;

                startPosition =
                    finalPosition - direction * depth;
            }
            else
            {
                // 튜토리얼 적:
                // 기존처럼 바닥 아래에서 위로 솟아오른다.
                startPosition =
                    finalPosition - Vector3.up * depth;
            }

            Vector3 startScale = targetScale * 0.72f;

            transform.position = startPosition;
            transform.localScale = startScale;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float ratio = Mathf.Clamp01(
                    elapsed / duration
                );

                float eased = Mathf.SmoothStep(
                    0f,
                    1f,
                    ratio
                );

                transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    finalPosition,
                    eased
                );

                transform.localScale = Vector3.LerpUnclamped(
                    startScale,
                    targetScale,
                    eased
                );

                yield return null;
            }

            transform.position = finalPosition;
            transform.localScale = targetScale;

            // 등장 연출이 끝난 뒤 코어 이동을 다시 시작한다.
            if (mover != null)
            {
                mover.enabled = enableMoverAfterRise;
            }

            routine = null;
            Destroy(this);
        }
    }
}