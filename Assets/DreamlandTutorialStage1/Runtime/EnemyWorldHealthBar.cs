using UnityEngine;
using UnityEngine.UI;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemyWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private string healthBarResourcePath = "UI/EnemyHealthBar_Pixel";
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.35f, 0f);
        [SerializeField, Min(0.0001f)] private float worldScale = 0.01f;
        [SerializeField] private bool alwaysVisible;
        [SerializeField] private bool autoPlaceAboveRenderer = true;
        [SerializeField, Min(0f)] private float extraHeight = 0.35f;

        private EnemyHealth health;
        private Camera targetCamera;
        private GameObject barRoot;
        private Slider slider;

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

        /// <summary>
        /// 실제 EnemyHealth 데미지 파이프라인을 거치지 않고 체력바를 직접 채웁니다.
        /// 튜토리얼 명중 연습처럼 몬스터가 무적 상태(damageEnabled=false)라
        /// HealthChanged가 발생하지 않을 때, 명중 횟수에 맞춰 체력바만 시각적으로
        /// 줄여 보여주는 용도입니다. 이후 RestoreFullHealth()/실제 데미지가 다시
        /// HealthChanged를 발생시키면 정상적으로 그쪽 값이 이어받습니다.
        /// </summary>
        public void SetManualRatio(float ratio)
        {
            if (barRoot != null)
            {
                barRoot.SetActive(true);
            }

            UpdateBar(ratio);
        }

        private void BuildBar()
        {
            if (barRoot != null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(healthBarResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemyWorldHealthBar] Resources에서 체력바 프리팹을 찾지 못했습니다: {healthBarResourcePath}");
                return;
            }

            barRoot = Instantiate(prefab, transform);
            barRoot.name = "WorldHealthBar";
            barRoot.transform.localPosition = localOffset;
            barRoot.transform.localRotation = Quaternion.identity;
            barRoot.transform.localScale = Vector3.one * worldScale;

            slider = barRoot.GetComponent<Slider>();
            if (slider == null)
            {
                slider = barRoot.GetComponentInChildren<Slider>(true);
            }

            if (slider != null)
            {
                slider.interactable = false;
                slider.transition = Selectable.Transition.None;
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
                slider.value = 1f;
            }

            Canvas canvas = barRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = barRoot.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            GraphicRaycaster raycaster = barRoot.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                Destroy(raycaster);
            }

            Graphic[] graphics = barRoot.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in graphics)
            {
                graphic.raycastTarget = false;
            }
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

        private void HandleHealthChanged(EnemyHealth _, float current, float maximum)
        {
            UpdateBar(maximum <= 0f ? 0f : current / maximum);
        }

        private void HandleHit(EnemyHealth _, DamageInfo __)
        {
            if (barRoot != null)
            {
                UpdatePlacement();
                barRoot.SetActive(true);
            }
        }

        private void HandleDied(EnemyHealth _, DamageInfo __)
        {
            Hide();
        }

        private void UpdateBar(float ratio)
        {
            if (slider == null)
            {
                return;
            }

            slider.value = Mathf.Clamp01(ratio);
        }
    }
}