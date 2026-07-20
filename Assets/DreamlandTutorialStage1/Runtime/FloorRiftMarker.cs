using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class FloorRiftMarker : MonoBehaviour
    {
        [SerializeField] private Color riftColor = new Color(0.65f, 0.08f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float visibleDuration = 1.8f;
        [SerializeField, Min(0.1f)] private float openDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float closeDuration = 0.45f;
        [SerializeField] private Vector3 visualScale = new Vector3(1.6f, 1f, 1.05f);

        private Transform visualRoot;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private Coroutine pulseRoutine;

        private void Awake()
        {
            EnsureVisual();
            SetVisualScale(0f);
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }
        }

        public void Play()
        {
            EnsureVisual();
            if (visualRoot == null)
            {
                return;
            }

            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
            }

            pulseRoutine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            visualRoot.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / openDuration);
                SetVisualScale(Mathf.SmoothStep(0f, 1f, ratio));
                yield return null;
            }

            SetVisualScale(1f);
            yield return new WaitForSeconds(visibleDuration);

            elapsed = 0f;
            while (elapsed < closeDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / closeDuration);
                SetVisualScale(1f - Mathf.SmoothStep(0f, 1f, ratio));
                yield return null;
            }

            SetVisualScale(0f);
            visualRoot.gameObject.SetActive(false);
            pulseRoutine = null;
        }

        private void EnsureVisual()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("FloorRiftVisual");
            if (existing != null)
            {
                visualRoot = existing;
                renderers.Clear();
                renderers.AddRange(visualRoot.GetComponentsInChildren<Renderer>(true));
                return;
            }

            GameObject root = new GameObject("FloorRiftVisual");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * 0.025f;
            visualRoot = root.transform;

            Material material = CreateRiftMaterial();
            CreateCrackSegment("Main Crack", new Vector3(0f, 0f, 0f), 0f, new Vector3(1.45f, 0.035f, 0.10f), material);
            CreateCrackSegment("Branch A", new Vector3(-0.35f, 0f, 0.16f), 28f, new Vector3(0.75f, 0.03f, 0.08f), material);
            CreateCrackSegment("Branch B", new Vector3(0.25f, 0f, -0.18f), -34f, new Vector3(0.85f, 0.03f, 0.08f), material);
            CreateCrackSegment("Branch C", new Vector3(0.52f, 0f, 0.18f), 52f, new Vector3(0.55f, 0.025f, 0.07f), material);
            CreateCrackSegment("Branch D", new Vector3(-0.58f, 0f, -0.10f), -48f, new Vector3(0.48f, 0.025f, 0.07f), material);
        }

        private void CreateCrackSegment(
            string segmentName,
            Vector3 localPosition,
            float yaw,
            Vector3 localScale,
            Material material)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = segmentName;
            segment.transform.SetParent(visualRoot, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            segment.transform.localScale = localScale;

            Collider segmentCollider = segment.GetComponent<Collider>();
            if (segmentCollider != null)
            {
                Destroy(segmentCollider);
            }

            Renderer targetRenderer = segment.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = material;
                targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                targetRenderer.receiveShadows = false;
                renderers.Add(targetRenderer);
            }
        }

        private Material CreateRiftMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            shader ??= Shader.Find("Unlit/Color");
            shader ??= Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "FloorRift_Runtime",
                color = riftColor
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", riftColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", riftColor * 3f);
            }

            return material;
        }

        private void SetVisualScale(float ratio)
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localScale = Vector3.Scale(visualScale, Vector3.one * Mathf.Clamp01(ratio));
        }
    }
}
