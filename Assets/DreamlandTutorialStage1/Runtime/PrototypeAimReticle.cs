using System;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class PrototypeAimReticle : MonoBehaviour
    {
        [Header("Aim")]
        [SerializeField] private Camera aimCamera;
        [SerializeField, Min(0.1f)] private float range = 50f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Appearance")]
        [SerializeField, Min(8f)] private float totalSize = 28f;
        [SerializeField, Min(1f)] private float lineThickness = 3f;
        [SerializeField, Min(0f)] private float centerGap = 5f;
        [SerializeField, Min(1f)] private float centerDotSize = 4f;
        [SerializeField] private Color neutralColor = new Color(0.95f, 1f, 1f, 1f);
        [SerializeField] private Color targetColor = new Color(1f, 0.15f, 0.65f, 1f);
        [SerializeField] private Color hitColor = new Color(0.2f, 1f, 0.55f, 1f);
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;

        private float hitFlashUntil;

        private void Awake()
        {
            useGUILayout = false;
            ResolveCamera();
        }

        private void Update()
        {
            ResolveCamera();
        }

        public void Configure(Camera camera)
        {
            aimCamera = camera;
        }

        public void NotifyShot(bool hitEnemy)
        {
            if (hitEnemy)
            {
                hitFlashUntil = Time.unscaledTime + hitFlashDuration;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUI.depth = -10000;

            Color currentColor = Time.unscaledTime < hitFlashUntil
                ? hitColor
                : IsAimingAtEnemy()
                    ? targetColor
                    : neutralColor;

            float displayScale = Mathf.Clamp(Screen.height / 720f, 0.85f, 1.75f);
            DrawCrosshair(outlineColor, displayScale, 2f * displayScale);
            DrawCrosshair(currentColor, displayScale, 0f);
        }

        private void DrawCrosshair(Color color, float displayScale, float outlineExpansion)
        {
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            float halfSize = totalSize * 0.5f * displayScale;
            float gap = centerGap * displayScale;
            float thickness = lineThickness * displayScale + outlineExpansion;
            float dot = centerDotSize * displayScale + outlineExpansion;

            Color previous = GUI.color;
            GUI.color = color;

            DrawRect(centerX - dot * 0.5f, centerY - dot * 0.5f, dot, dot);

            float horizontalLength = Mathf.Max(1f, halfSize - gap);
            DrawRect(centerX - halfSize - outlineExpansion * 0.5f, centerY - thickness * 0.5f,
                horizontalLength + outlineExpansion, thickness);
            DrawRect(centerX + gap - outlineExpansion * 0.5f, centerY - thickness * 0.5f,
                horizontalLength + outlineExpansion, thickness);

            float verticalLength = Mathf.Max(1f, halfSize - gap);
            DrawRect(centerX - thickness * 0.5f, centerY - halfSize - outlineExpansion * 0.5f,
                thickness, verticalLength + outlineExpansion);
            DrawRect(centerX - thickness * 0.5f, centerY + gap - outlineExpansion * 0.5f,
                thickness, verticalLength + outlineExpansion);

            GUI.color = previous;
        }

        private static void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }

        private void ResolveCamera()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null)
                {
                    aimCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
                }
            }
        }

        private bool IsAimingAtEnemy()
        {
            if (aimCamera == null)
            {
                return false;
            }

            Ray ray = new Ray(aimCamera.transform.position, aimCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null && !enemy.IsDead)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            range = Mathf.Max(0.1f, range);
            totalSize = Mathf.Max(8f, totalSize);
            lineThickness = Mathf.Max(1f, lineThickness);
            centerGap = Mathf.Max(0f, centerGap);
            centerDotSize = Mathf.Max(1f, centerDotSize);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        }
    }
}
