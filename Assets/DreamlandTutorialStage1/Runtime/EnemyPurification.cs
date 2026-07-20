using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemyPurification : MonoBehaviour
    {
        [Header("Purification")]
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float orbTravelDuration = 1.2f;
        [SerializeField, Min(0f)] private float energyReward = 10f;
        [SerializeField, Min(0f)] private float riseDistance = 0.6f;
        [SerializeField] private Color dreamLightColor = new Color(0.35f, 0.95f, 1f, 1f);
        [SerializeField] private CoreState targetCore;

        private EnemyHealth health;
        private Renderer[] renderers;
        private bool started;

        public EnemyHealth Health => health;
        public event Action<EnemyPurification> Completed;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            health ??= GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        public void Configure(CoreState core, float reward)
        {
            targetCore = core;
            energyReward = Mathf.Max(0f, reward);
        }

        private void HandleDied(EnemyHealth _, DamageInfo __)
        {
            if (!started)
            {
                StartCoroutine(PurifyRoutine());
            }
        }

        private IEnumerator PurifyRoutine()
        {
            started = true;

            EnemyCoreMover mover = GetComponent<EnemyCoreMover>();
            if (mover != null)
            {
                mover.enabled = false;
            }

            EnemyWorldHealthBar healthBar = GetComponent<EnemyWorldHealthBar>();
            healthBar?.Hide();

            foreach (Collider enemyCollider in GetComponentsInChildren<Collider>(true))
            {
                enemyCollider.enabled = false;
            }

            renderers ??= GetComponentsInChildren<Renderer>(true);
            List<Material> materials = PrepareMaterialsForFade(renderers);
            Vector3 startPosition = transform.position;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                transform.position = startPosition + Vector3.up * (riseDistance * t);
                SetMaterialsAlpha(materials, 1f - t);
                yield return null;
            }

            foreach (Renderer enemyRenderer in renderers)
            {
                if (enemyRenderer != null)
                {
                    enemyRenderer.enabled = false;
                }
            }

            yield return MoveDreamEnergyToCore();

            Completed?.Invoke(this);
            Destroy(gameObject);
        }

        private IEnumerator MoveDreamEnergyToCore()
        {
            if (targetCore == null)
            {
                yield break;
            }

            Vector3 start = transform.position + Vector3.up * 0.25f;
            Vector3 end = targetCore.EnergyTarget.position;
            Vector3 control = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 2f;

            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "DreamLightEnergy";
            orb.transform.position = start;
            orb.transform.localScale = Vector3.one * 0.22f;

            Collider orbCollider = orb.GetComponent<Collider>();
            if (orbCollider != null)
            {
                Destroy(orbCollider);
            }

            Material orbMaterial = CreateOrbMaterial();
            Renderer orbRenderer = orb.GetComponent<Renderer>();
            if (orbRenderer != null && orbMaterial != null)
            {
                orbRenderer.material = orbMaterial;
            }

            AddTrail(orb, orbMaterial);

            float elapsed = 0f;
            while (elapsed < orbTravelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / orbTravelDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                orb.transform.position = QuadraticBezier(start, control, end, eased);
                orb.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.08f, eased);
                yield return null;
            }

            targetCore.AddEnergy(energyReward);
            DreamGameEvents.RaiseEnergyAbsorbed(this, energyReward);
            Destroy(orb);
        }

        private Material CreateOrbMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            shader ??= Shader.Find("Sprites/Default");
            shader ??= Shader.Find("Standard");

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "DreamLightEnergy_Runtime",
                color = dreamLightColor
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", dreamLightColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", dreamLightColor * 2f);
            }

            return material;
        }

        private static void AddTrail(GameObject orb, Material material)
        {
            TrailRenderer trail = orb.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.03f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            trail.autodestruct = false;
            trail.material = material;
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * a + 2f * inverse * t * b + t * t * c;
        }

        private static List<Material> PrepareMaterialsForFade(Renderer[] targetRenderers)
        {
            List<Material> materials = new List<Material>();

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] rendererMaterials = targetRenderer.materials;
                foreach (Material material in rendererMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    ConfigureTransparent(material);
                    materials.Add(material);
                }
            }

            return materials;
        }

        private static void ConfigureTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetMaterialsAlpha(IEnumerable<Material> materials, float alpha)
        {
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color baseColor = material.GetColor("_BaseColor");
                    baseColor.a = alpha;
                    material.SetColor("_BaseColor", baseColor);
                }

                if (material.HasProperty("_Color"))
                {
                    Color color = material.GetColor("_Color");
                    color.a = alpha;
                    material.SetColor("_Color", color);
                }
            }
        }

        private void OnValidate()
        {
            fadeDuration = Mathf.Max(0.05f, fadeDuration);
            orbTravelDuration = Mathf.Max(0.05f, orbTravelDuration);
            energyReward = Mathf.Max(0f, energyReward);
        }
    }
}
