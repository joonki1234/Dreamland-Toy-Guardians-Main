using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemyWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.15f, 0f);
        [SerializeField, Min(0.0001f)] private float worldScale = 0.0105f;
        [SerializeField] private bool alwaysVisible;
        [SerializeField] private bool autoPlaceAboveRenderer = false;
        [SerializeField, Min(0f)] private float extraHeight = 0.15f;

        private EnemyHealth health;
        private Camera targetCamera;
        private GameObject barRoot;
        private RectTransform fillRect;
        private const float FullFillWidth = 154f;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            BuildBar();
        }

        private void OnEnable()
        {
            health ??= GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.HealthChanged += HandleHealthChanged;
                health.HitRegistered += HandleHit;
                health.Died += HandleDied;
            }
        }

        private void Start()
        {
            targetCamera = Camera.main;
            UpdatePlacement();
            UpdateBar(health != null ? health.NormalizedHealth : 1f);

            if (barRoot != null)
            {
                barRoot.SetActive(alwaysVisible);
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= HandleHealthChanged;
                health.HitRegistered -= HandleHit;
                health.Died -= HandleDied;
            }
        }

        private void LateUpdate()
        {
            if (barRoot == null)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                Vector3 awayFromCamera = barRoot.transform.position - targetCamera.transform.position;
                if (awayFromCamera.sqrMagnitude > 0.0001f)
                {
                    barRoot.transform.rotation = Quaternion.LookRotation(awayFromCamera.normalized, Vector3.up);
                }
            }
        }

        public void Hide()
        {
            if (barRoot != null)
            {
                barRoot.SetActive(false);
            }
        }

        private void BuildBar()
        {
            if (barRoot != null)
            {
                return;
            }

            barRoot = new GameObject("WorldHealthBar", typeof(RectTransform), typeof(Canvas));
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = localOffset;
            barRoot.transform.localScale = Vector3.one * worldScale;

            RectTransform rootRect = barRoot.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(160f, 24f);

            Canvas canvas = barRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            GameObject background = CreateImage("Background", rootRect, new Color(0.03f, 0.03f, 0.05f, 0.9f));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            Stretch(backgroundRect, Vector2.zero, Vector2.zero);

            GameObject fill = CreateImage("Fill", rootRect, new Color(0.3f, 0.95f, 0.8f, 1f));
            fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(3f, 0f);
            fillRect.sizeDelta = new Vector2(FullFillWidth, 18f);
        }

        private void UpdatePlacement()
        {
            if (barRoot == null)
            {
                return;
            }

            Vector3 offset = localOffset;
            if (autoPlaceAboveRenderer)
            {
                Renderer[] targetRenderers = GetComponentsInChildren<Renderer>(true);
                float highestWorldY = transform.position.y + localOffset.y;

                foreach (Renderer targetRenderer in targetRenderers)
                {
                    if (targetRenderer == null || !targetRenderer.enabled)
                    {
                        continue;
                    }

                    highestWorldY = Mathf.Max(highestWorldY, targetRenderer.bounds.max.y);
                }

                offset.y = Mathf.Max(localOffset.y, highestWorldY - transform.position.y + extraHeight);
            }

            barRoot.transform.localPosition = offset;
            barRoot.transform.localScale = Vector3.one * worldScale;
        }

        private static GameObject CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return imageObject;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void HandleHealthChanged(EnemyHealth _, float current, float maximum)
        {
            UpdateBar(maximum <= 0f ? 0f : current / maximum);
        }

        private void HandleHit(EnemyHealth _, DamageInfo __)
        {
            if (barRoot != null)
            {
                barRoot.SetActive(true);
            }
        }

        private void HandleDied(EnemyHealth _, DamageInfo __)
        {
            Hide();
        }

        private void UpdateBar(float ratio)
        {
            if (fillRect == null)
            {
                return;
            }

            Vector2 size = fillRect.sizeDelta;
            size.x = FullFillWidth * Mathf.Clamp01(ratio);
            fillRect.sizeDelta = size;
        }
    }
}
