using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnRise : MonoBehaviour
    {
        private Coroutine routine;

        public void Begin(
            Vector3 finalPosition,
            float depth,
            float duration,
            EnemyCoreMover mover,
            FloorRiftMarker rift,
            bool enableMoverAfterRise = true)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(RiseRoutine(
                finalPosition,
                Mathf.Max(0f, depth),
                Mathf.Max(0.05f, duration),
                mover,
                rift,
                enableMoverAfterRise));
        }

        private IEnumerator RiseRoutine(
            Vector3 finalPosition,
            float depth,
            float duration,
            EnemyCoreMover mover,
            FloorRiftMarker rift,
            bool enableMoverAfterRise)
        {
            if (mover != null)
            {
                mover.enabled = false;
            }

            rift?.Play();

            Vector3 targetScale = transform.localScale;
            Vector3 startPosition = finalPosition - Vector3.up * depth;
            Vector3 startScale = targetScale * 0.72f;

            transform.position = startPosition;
            transform.localScale = startScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, ratio);
                transform.position = Vector3.LerpUnclamped(startPosition, finalPosition, eased);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
                yield return null;
            }

            transform.position = finalPosition;
            transform.localScale = targetScale;

            if (mover != null)
            {
                mover.enabled = enableMoverAfterRise;
            }

            routine = null;
            Destroy(this);
        }
    }
}
