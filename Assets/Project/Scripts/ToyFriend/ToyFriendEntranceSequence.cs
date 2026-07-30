using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace DreamGuardians
{
    /// <summary>
    /// 코어에서 작은 빛이 출발해 SpawnPoint로 이동하고,
    /// 도착 순간 장난감 친구로 변한 뒤 TalkPoint까지 걷게 합니다.
    ///
    /// Orb Prefab을 비워두면 발광 구체, Point Light,
    /// Trail Renderer를 런타임에 자동 생성합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToyFriendEntranceSequence : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ToyFriendController toyFriend;

        [Tooltip("코어에서 빛이 나오는 위치입니다.")]
        [SerializeField]
        private Transform coreLightSpawnPoint;

        [Tooltip("비워두면 ToyFriendController의 SpawnPoint를 사용합니다.")]
        [SerializeField]
        private Transform arrivalPoint;

        [Tooltip("선택 사항입니다. 비워두면 코드가 빛 구슬을 자동 생성합니다.")]
        [SerializeField]
        private GameObject orbPrefab;

        [Header("Timing")]
        [SerializeField, Min(0f)]
        private float startDelay = 0.8f;

        [SerializeField, Min(0.1f)]
        private float travelDuration = 2.2f;

        [SerializeField, Min(0f)]
        private float arrivalFlashDuration = 0.25f;

        [Header("Flight Shape")]
        [SerializeField, Min(0f)]
        private float arcHeight = 1.6f;

        [SerializeField, Min(0f)]
        private float sideWobble = 0.18f;

        [SerializeField, Min(0f)]
        private float wobbleFrequency = 2.5f;

        [Header("Generated Orb")]
        [SerializeField]
        private Color orbColor =
            new Color(0.62f, 1f, 0.92f, 1f);

        [SerializeField, Min(0.01f)]
        private float orbScale = 0.18f;

        [SerializeField, Min(0f)]
        private float lightIntensity = 3.5f;

        [SerializeField, Min(0.1f)]
        private float lightRange = 2.5f;

        [SerializeField, Min(0.01f)]
        private float trailTime = 0.45f;

        [Header("Start")]
        [SerializeField]
        private bool playOnStart = true;

        [SerializeField]
        private bool playOnlyOnce = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onOrbStarted;

        [SerializeField]
        private UnityEvent onFriendAppeared;

        private Coroutine sequenceRoutine;
        private bool hasPlayed;

        private void Awake()
        {
            if (toyFriend == null)
            {
                toyFriend =
                    FindFirstObjectByType<ToyFriendController>();
            }

            if (toyFriend != null)
            {
                // 기존 ToyFriendController가 동시에 자동 등장하지 않도록 막습니다.
                toyFriend.SetAutomaticEntrance(false);
                toyFriend.PrepareAtSpawn(false);
            }
        }

        private IEnumerator Start()
        {
            yield return null;

            if (playOnStart)
            {
                PlaySequence();
            }
        }

        [ContextMenu("Play Entrance Sequence")]
        public void PlaySequence()
        {
            if (playOnlyOnce &&
                hasPlayed)
            {
                return;
            }

            if (!ValidateReferences())
            {
                return;
            }

            StopSequence();

            sequenceRoutine =
                StartCoroutine(
                    SequenceRoutine());
        }

        [ContextMenu("Reset Sequence")]
        public void ResetSequence()
        {
            StopSequence();
            hasPlayed = false;

            if (toyFriend != null)
            {
                toyFriend.SetAutomaticEntrance(false);
                toyFriend.PrepareAtSpawn(false);
            }
        }

        public void StopSequence()
        {
            if (sequenceRoutine == null)
            {
                return;
            }

            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        private bool ValidateReferences()
        {
            if (toyFriend == null)
            {
                Debug.LogWarning(
                    "[ToyFriendEntranceSequence] Toy Friend가 연결되지 않았습니다.",
                    this);

                return false;
            }

            if (coreLightSpawnPoint == null)
            {
                Debug.LogWarning(
                    "[ToyFriendEntranceSequence] Core Light Spawn Point가 연결되지 않았습니다.",
                    this);

                return false;
            }

            if (GetArrivalPoint() == null)
            {
                Debug.LogWarning(
                    "[ToyFriendEntranceSequence] Arrival Point 또는 ToyFriend SpawnPoint가 없습니다.",
                    this);

                return false;
            }

            return true;
        }

        private IEnumerator SequenceRoutine()
        {
            hasPlayed = true;
            toyFriend.SetAutomaticEntrance(false);
            toyFriend.PrepareAtSpawn(false);

            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(
                    startDelay);
            }

            Transform target =
                GetArrivalPoint();

            GameObject orb =
                CreateOrb();

            orb.transform.position =
                coreLightSpawnPoint.position;

            onOrbStarted?.Invoke();

            Vector3 startPosition =
                coreLightSpawnPoint.position;

            Vector3 endPosition =
                target.position;

            Vector3 controlPosition =
                (startPosition + endPosition) *
                0.5f +
                Vector3.up *
                arcHeight;

            Vector3 travelDirection =
                endPosition -
                startPosition;

            Vector3 sideDirection =
                Vector3.Cross(
                    travelDirection.normalized,
                    Vector3.up);

            if (sideDirection.sqrMagnitude <
                0.001f)
            {
                sideDirection =
                    Vector3.right;
            }

            float elapsed = 0f;

            while (elapsed <
                   travelDuration)
            {
                elapsed += Time.deltaTime;

                float normalizedTime =
                    Mathf.Clamp01(
                        elapsed /
                        travelDuration);

                float easedTime =
                    normalizedTime *
                    normalizedTime *
                    (3f -
                     2f *
                     normalizedTime);

                Vector3 basePosition =
                    QuadraticBezier(
                        startPosition,
                        controlPosition,
                        endPosition,
                        easedTime);

                float wobble =
                    Mathf.Sin(
                        normalizedTime *
                        Mathf.PI *
                        2f *
                        wobbleFrequency) *
                    sideWobble *
                    Mathf.Sin(
                        normalizedTime *
                        Mathf.PI);

                orb.transform.position =
                    basePosition +
                    sideDirection *
                    wobble;

                float pulse =
                    1f +
                    Mathf.Sin(
                        elapsed *
                        10f) *
                    0.12f;

                orb.transform.localScale =
                    Vector3.one *
                    orbScale *
                    pulse;

                yield return null;
            }

            orb.transform.position =
                endPosition;

            yield return ArrivalFlash(
                orb);

            toyFriend.PrepareAtSpawn(true);
            onFriendAppeared?.Invoke();

            Destroy(orb);

            // 친구가 SpawnPoint에서 TalkPoint까지 걷도록 기존 로직을 실행합니다.
            toyFriend.PlayEntrance();

            sequenceRoutine = null;
        }

        private IEnumerator ArrivalFlash(
            GameObject orb)
        {
            if (arrivalFlashDuration <=
                0f)
            {
                yield break;
            }

            float elapsed = 0f;
            Vector3 startScale =
                Vector3.one *
                orbScale;

            while (elapsed <
                   arrivalFlashDuration)
            {
                elapsed += Time.deltaTime;

                float normalizedTime =
                    Mathf.Clamp01(
                        elapsed /
                        arrivalFlashDuration);

                float flashScale =
                    Mathf.Lerp(
                        1f,
                        3.2f,
                        normalizedTime);

                orb.transform.localScale =
                    startScale *
                    flashScale;

                Renderer targetRenderer =
                    orb.GetComponentInChildren<Renderer>();

                if (targetRenderer != null &&
                    targetRenderer.material != null)
                {
                    Color color =
                        orbColor;

                    color.a =
                        1f -
                        normalizedTime;

                    SetMaterialColor(
                        targetRenderer.material,
                        color);
                }

                yield return null;
            }
        }

        private Transform GetArrivalPoint()
        {
            if (arrivalPoint != null)
            {
                return arrivalPoint;
            }

            return toyFriend != null
                ? toyFriend.SpawnPoint
                : null;
        }

        private GameObject CreateOrb()
        {
            if (orbPrefab != null)
            {
                return Instantiate(
                    orbPrefab,
                    coreLightSpawnPoint.position,
                    Quaternion.identity);
            }

            GameObject root =
                new GameObject(
                    "ToyFriend_CoreLightOrb_Runtime");

            GameObject sphere =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);

            sphere.name =
                "GlowSphere";

            sphere.transform.SetParent(
                root.transform,
                false);

            sphere.transform.localScale =
                Vector3.one *
                orbScale;

            Collider sphereCollider =
                sphere.GetComponent<Collider>();

            if (sphereCollider != null)
            {
                Destroy(sphereCollider);
            }

            Material glowMaterial =
                CreateGlowMaterial(
                    orbColor);

            Renderer sphereRenderer =
                sphere.GetComponent<Renderer>();

            if (sphereRenderer != null)
            {
                sphereRenderer.material =
                    glowMaterial;
            }

            Light pointLight =
                root.AddComponent<Light>();

            pointLight.type =
                LightType.Point;

            pointLight.color =
                orbColor;

            pointLight.intensity =
                lightIntensity;

            pointLight.range =
                lightRange;

            TrailRenderer trail =
                root.AddComponent<TrailRenderer>();

            trail.time =
                trailTime;

            trail.startWidth =
                orbScale *
                0.75f;

            trail.endWidth = 0f;
            trail.minVertexDistance = 0.02f;
            trail.numCornerVertices = 4;
            trail.numCapVertices = 4;
            trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            Material trailMaterial =
                CreateGlowMaterial(
                    orbColor);

            trail.material =
                trailMaterial;

            Gradient gradient =
                new Gradient();

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        Color.white,
                        0f),
                    new GradientColorKey(
                        orbColor,
                        1f)
                },
                new[]
                {
                    new GradientAlphaKey(
                        0.9f,
                        0f),
                    new GradientAlphaKey(
                        0f,
                        1f)
                });

            trail.colorGradient =
                gradient;

            return root;
        }

        private static Material CreateGlowMaterial(
            Color color)
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Sprites/Default");
            }

            Material material =
                new Material(shader);

            SetMaterialColor(
                material,
                color);

            if (material.HasProperty(
                    "_Surface"))
            {
                material.SetFloat(
                    "_Surface",
                    1f);
            }

            if (material.HasProperty(
                    "_Blend"))
            {
                material.SetFloat(
                    "_Blend",
                    0f);
            }

            if (material.HasProperty(
                    "_SrcBlend"))
            {
                material.SetFloat(
                    "_SrcBlend",
                    (float)
                    UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty(
                    "_DstBlend"))
            {
                material.SetFloat(
                    "_DstBlend",
                    (float)
                    UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty(
                    "_ZWrite"))
            {
                material.SetFloat(
                    "_ZWrite",
                    0f);
            }

            material.renderQueue = 3000;

            return material;
        }

        private static void SetMaterialColor(
            Material material,
            Color color)
        {
            if (material.HasProperty(
                    "_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    color);
            }

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    color);
            }
        }

        private static Vector3 QuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float time)
        {
            float inverseTime =
                1f -
                time;

            return
                inverseTime *
                inverseTime *
                start +
                2f *
                inverseTime *
                time *
                control +
                time *
                time *
                end;
        }
    }
}
