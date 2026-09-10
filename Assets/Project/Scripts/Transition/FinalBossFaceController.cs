using System.Collections.Generic;
using DreamGuardians;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local presentation only. Never writes combat or network state.</summary>
[DisallowMultipleComponent]
public sealed class FinalBossFaceController : MonoBehaviour
{
    public enum Expression
    {
        Idle = 0, BattlePhase2 = 1, BattlePhase3 = 2,
        SummonPrepare = 3, SummonActivate = 4, SummonEnd = 5,
        EnergyCharge = 6, EnergyFire = 7, Mock = 8, Hit = 9,
        Rage = 10, FinalRage = 11
    }

    [SerializeField] private Texture2D faceAtlas;
    [SerializeField] private Material faceMaterial;
    [SerializeField] private Vector2Int atlasGrid = new Vector2Int(4, 3);
    [SerializeField, Range(0.01f, 0.49f)] private float lowHpThreshold = 0.2f;
    [Tooltip("Enable only after assigning the approved 4x3 artwork and checking eye regions.")]
    [SerializeField] private bool approvedAtlasAssigned;
    [Tooltip("Model-local rotation. GiftBox's -90 degree pitch requires +90 here.")]
    [SerializeField] private Vector3 faceLocalEuler = new Vector3(90f, 0f, 0f);
    [Tooltip("Offset in face-frame bounds fractions: right, up, outward.")]
    [SerializeField] private Vector3 faceOffset = new Vector3(0f, -0.08f, 0f);
    [Tooltip("Width / height as fractions of model bounds in the face frame.")]
    [SerializeField] private Vector2 faceSize = new Vector2(0.8f, 0.65f);
    [Tooltip("Outward separation as a fraction of model depth.")]
    [SerializeField, Min(0f)] private float surfaceOffset = 0.015f;

    private EnemyHealth health;
    private DreamEnemySpawner networkPresentation;
    private int presentationEventRevision;
    private bool readingNetworkPresentation;
    private float networkPresentationAge;
    private int lastNetworkPresentationRevision = -1;

    public void BindNetworkPresentation(DreamEnemySpawner source)
    {
        networkPresentation = source;
        lastNetworkPresentationRevision = -1;
        readingNetworkPresentation = false;
    }

    public void ReadAuthoritativePresentation(out Expression expression, out bool visible, out int revision)
    {
        expression = ResolveExpression();
        visible = isActiveAndEnabled && cleanseProgress < 0f;
        revision = expression == Expression.Hit ? presentationEventRevision : 0;
    }
    private float lastHealth;
    private GameObject faceObject;
    private MeshRenderer faceRenderer;
    private Mesh faceMesh;
    private Material runtimeMaterial;
    private Bounds faceBounds;
    private bool hasBounds;
    private Expression? action;
    private float hitUntil, mockUntil, fireUntil, summonEndAt, summonUntil;
    private float chargeStarted, chargeDuration = 1f;
    private Expression baseExpression = Expression.Idle;
    private Vector2 summonLook;
    private Vector3[] restVertices, animatedVertices;
    private Vector2[] cellUvs;
    private readonly float[][] eyeWeights = new float[12][];
    private readonly Rect[,] eyeBounds = new Rect[12, 2];

    // The supplied square sheet has outer padding, not three equal-height cells.
    // Equal 1/3 slicing cuts the top of expression 12 into expression 8.
    // Equal square windows preserve the original silhouettes and scale.
    private static Rect GetAtlasRect(int index)
    {
        return new Rect((index % 4) * 0.25f, 0.67f - (index / 4) * 0.28f, 0.25f, 0.25f);
    }

    private void BuildEyeMasks()
    {
        for (int expression = 0; expression < 12; expression++)
        {
            eyeWeights[expression] = new float[cellUvs.Length];
            if (!faceAtlas.isReadable) continue;
            Rect crop = GetAtlasRect(expression);
            int x0 = Mathf.RoundToInt(crop.x * faceAtlas.width);
            int y0 = Mathf.RoundToInt(crop.y * faceAtlas.height);
            int width = Mathf.FloorToInt(crop.width * faceAtlas.width);
            int height = Mathf.FloorToInt(crop.height * faceAtlas.height);
            Color[] pixels = faceAtlas.GetPixels(x0, y0, width, height);
            int[] labels = new int[pixels.Length];
            var components = new List<List<int>>();
            var queue = new Queue<int>();
            for (int p = 0; p < pixels.Length; p++)
            {
                if (labels[p] != 0 || pixels[p].grayscale < 0.5f) continue;
                var component = new List<int>();
                components.Add(component);
                int label = components.Count;
                labels[p] = label;
                queue.Enqueue(p);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    int px = current % width, py = current / width;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = px + dx, ny = py + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int n = ny * width + nx;
                        if (labels[n] != 0 || pixels[n].grayscale < 0.5f) continue;
                        labels[n] = label;
                        queue.Enqueue(n);
                    }
                }
            }
            components.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (components.Count < 3)
            {
                Debug.LogWarning("Boss face cell has fewer than three connected features; eye motion disabled.", this);
                continue;
            }
            // Of the three largest features, the lowest centroid is the mouth.
            var features = components.GetRange(0, 3);
            features.Sort((a, b) => PixelCenter(a, width).y.CompareTo(PixelCenter(b, width).y));
            List<int> mouth = features[0];
            features.RemoveAt(0);
            features.Sort((a, b) => PixelCenter(a, width).x.CompareTo(PixelCenter(b, width).x));
            int[] featureLabels = new int[pixels.Length];
            foreach (int p in mouth) featureLabels[p] = 3;
            for (int eye = 0; eye < 2; eye++)
            {
                foreach (int p in features[eye]) featureLabels[p] = eye + 1;
                // Fill enclosed pupil holes in the motion mask only. The source
                // pixels (including black rings and white pupils) remain untouched.
                for (int y = 0; y < height; y++)
                {
                    int min = width, max = -1;
                    for (int x = 0; x < width; x++)
                        if (featureLabels[y * width + x] == eye + 1)
                        { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
                    for (int x = min; x <= max; x++)
                        if (featureLabels[y * width + x] != 3)
                            featureLabels[y * width + x] = eye + 1;
                }
                Vector2 center = PixelCenter(features[eye], width);
                eyeBounds[expression, eye] = new Rect(center.x / width, center.y / height, 0f, 0f);
            }
            for (int v = 0; v < cellUvs.Length; v++)
            {
                int px = Mathf.RoundToInt(cellUvs[v].x * width);
                int py = Mathf.RoundToInt(cellUvs[v].y * height);
                int desiredEye = cellUvs[v].x < 0.5f ? 1 : 2;
                float eyeDistance = 1000f, mouthDistance = 1000f;
                const int radius = 14;
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = px + dx, ny = py + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int label = featureLabels[ny * width + nx];
                    float distance = dx * dx + dy * dy;
                    if (label == desiredEye) eyeDistance = Mathf.Min(eyeDistance, distance);
                    if (label == 3) mouthDistance = Mathf.Min(mouthDistance, distance);
                }
                // Keep all triangles touching the mouth fixed, including antialiasing.
                if (mouthDistance <= 64f) continue;
                eyeWeights[expression][v] = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((Mathf.Sqrt(eyeDistance) - 5f) / 8f));
            }
        }
    }

    private static Vector2 PixelCenter(List<int> pixels, int width)
    {
        Vector2 sum = Vector2.zero;
        foreach (int pixel in pixels) sum += new Vector2(pixel % width, pixel / width);
        return sum / pixels.Count;
    }
    private float deathTime = -1f;
    private float cleanseProgress = -1f;
    public Expression CurrentExpression { get; private set; }

#if UNITY_EDITOR
    [Header("Face Preview — Editor Play Mode Only")]
    [SerializeField] private bool editorFacePreview;
    [SerializeField, Range(1, 12)] private int editorExpressionNumber = 1;
    [Tooltip("Off: inspect the original expression. On: replay its eye animation.")]
    [SerializeField] private bool editorAnimateEyes = true;
    [SerializeField, Min(0.1f)] private float editorChargeSeconds = 2f;
    private int editorLastExpression;
    private bool editorWasPreviewing;
    private float editorPreviewStarted;

    [ContextMenu("Face Preview/Show Selected (Play Mode)")]
    private void EditorShowSelected()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        editorFacePreview = true;
        editorExpressionNumber = Mathf.Clamp(editorExpressionNumber, 1, 12);
        editorLastExpression = editorExpressionNumber;
        editorWasPreviewing = true;
        editorPreviewStarted = Time.time;
        previousEyeExpression = (Expression)(-1);
    }

    [ContextMenu("Face Preview/Next Expression (1–12)")]
    private void EditorNextExpression()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        editorExpressionNumber = editorFacePreview ? editorExpressionNumber % 12 + 1 : 1;
        EditorShowSelected();
    }

    [ContextMenu("Face Preview/Previous Expression")]
    private void EditorPreviousExpression()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        editorExpressionNumber = editorFacePreview ? (editorExpressionNumber + 10) % 12 + 1 : 12;
        EditorShowSelected();
    }

    [ContextMenu("Face Preview/Replay Selected Eye Animation")]
    private void EditorReplayEyes()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        editorAnimateEyes = true;
        EditorShowSelected();
    }

    [ContextMenu("Face Preview/Stop — Return To Live State")]
    private void EditorStopPreview()
    {
        if (!Application.isPlaying) return;
        editorFacePreview = editorWasPreviewing = false;
        previousEyeExpression = (Expression)(-1);
    }

    private void EditorUpdatePreview()
    {
        if (!Application.isPlaying) return;
        if (editorFacePreview)
        {
            editorExpressionNumber = Mathf.Clamp(editorExpressionNumber, 1, 12);
            if (!editorWasPreviewing || editorLastExpression != editorExpressionNumber)
                EditorShowSelected();
            CurrentExpression = (Expression)(editorExpressionNumber - 1);
        }
        else if (editorWasPreviewing) EditorStopPreview();
    }
#endif

    // Used by existing model-only scans so this overlay cannot affect hitboxes,
    // grounding, aura bounds or body material instances.
    public static bool IsFaceRenderer(Renderer renderer)
    {
        if (renderer == null) return false;
        var owner = renderer.GetComponentInParent<FinalBossFaceController>();
        return owner != null && owner.faceObject == renderer.gameObject;
    }

    private void Start() { BindHealth(); }
    private void OnEnable() { BindHealth(); }
    private void BindHealth()
    {
        if (health != null) return;
        health = GetComponent<EnemyHealth>();
        if (health == null) return; // Director may add health after Instantiate.
        health.HitRegistered += OnHit;
        lastHealth = health.CurrentHealth;
        health.HealthChanged += OnHealthChanged;
        health.Died += OnDied;
    }
    private void OnDisable()
    {
        if (health != null)
        {
            health.HitRegistered -= OnHit;
            health.HealthChanged -= OnHealthChanged;
            health.Died -= OnDied;
        }
        health = null;
        action = null;
        fireUntil = summonUntil = hitUntil = mockUntil = summonEndAt = 0f;
        deathTime = cleanseProgress = -1f;
        baseExpression = Expression.Idle;
        previousEyeExpression = (Expression)(-1);
        CurrentEyeAnimation = EyeAnimation.Rest;
#if UNITY_EDITOR
        editorFacePreview = editorWasPreviewing = false;
#endif
        if (faceRenderer != null) faceRenderer.enabled = false;
    }
    private void OnHit(EnemyHealth source, DamageInfo damage)
    {
        SetExpression(Expression.Hit);
        networkPresentation?.RequestBossFaceHit();
    }
    private void OnHealthChanged(EnemyHealth source, float current, float maximum)
    {
        // Fusion proxies receive HealthChanged even when HitRegistered is authority-local.
        if (current < lastHealth && current > 0f) SetExpression(Expression.Hit);
        lastHealth = current;
    }
    private void OnDied(EnemyHealth source, DamageInfo damage) { ShowDeath(); }
    public void ShowDeath()
    {
        if (deathTime < 0f) deathTime = Time.time;
    }
    public void SetBattlePhase(int phase)
    {
        baseExpression = phase >= 3 ? Expression.BattlePhase3 :
            phase == 2 ? Expression.BattlePhase2 : Expression.Idle;
    }
    public void SetExpression(Expression expression)
    {
        if ((int)expression < 0 || (int)expression > 11 || !isActiveAndEnabled) return;
        switch (expression)
        {
            case Expression.Hit:
                hitUntil = Time.time + 0.2f;
                presentationEventRevision++;
                break;
            case Expression.Mock: mockUntil = Time.time + 0.65f; break;
            case Expression.EnergyFire:
                if (action == Expression.EnergyCharge) action = null;
                fireUntil = Time.time + 0.4f; break;
            case Expression.SummonEnd:
                summonEndAt = Time.time + 0.2f;
                summonUntil = summonEndAt + 0.8f;
                action = Expression.SummonActivate; break;
            case Expression.Idle:
            case Expression.BattlePhase2:
            case Expression.BattlePhase3:
            case Expression.Rage:
            case Expression.FinalRage: baseExpression = expression; break;
            default:
                action = expression;
                summonUntil = summonEndAt = 0f;
                if (expression == Expression.EnergyCharge) chargeStarted = Time.time;
                break;
        }
    }
    public void BeginSummonPrepare(Vector3 worldTarget)
    {
        Vector3 local = Quaternion.Inverse(Quaternion.Euler(faceLocalEuler)) *
            transform.InverseTransformDirection(worldTarget - transform.position);
        summonLook = Vector2.ClampMagnitude(new Vector2(local.x, local.y), 1f) * 0.006f;
        SetExpression(Expression.SummonPrepare);
    }
    public void BeginEnergyCharge(float duration)
    {
        chargeDuration = Mathf.Max(0.01f, duration);
        SetExpression(Expression.EnergyCharge);
    }
    public void NotifyEnergyFired() { SetExpression(Expression.EnergyFire); }
    public void NotifyAttackSuccess() { SetExpression(Expression.Mock); }
    public void CancelSummon()
    {
        if (action == Expression.SummonPrepare || action == Expression.SummonActivate ||
            action == Expression.SummonEnd) action = null;
        summonUntil = summonEndAt = 0f;
    }
    public void EndAction() { action = null; }
    public void SetCleanseProgress(float progress)
    {
        cleanseProgress = Mathf.Clamp01(progress);
    }

    private void LateUpdate()
    {
        BindHealth();
        if (health != null && health.IsDead) ShowDeath();
        CurrentExpression = ResolveExpression();
        bool showFace = cleanseProgress < 0f;
        bool wasReadingNetwork = readingNetworkPresentation;
        readingNetworkPresentation = networkPresentation != null &&
            networkPresentation.TryReadBossFace(out _, out _);
        if (readingNetworkPresentation && networkPresentation.TryReadBossFace(out var state, out networkPresentationAge))
        {
            CurrentExpression = state.Expression;
            showFace = state.Visible;
            if (!wasReadingNetwork || lastNetworkPresentationRevision != state.Revision)
            {
                previousEyeExpression = (Expression)(-1);
                lastNetworkPresentationRevision = state.Revision;
            }
        }
        else if (wasReadingNetwork) previousEyeExpression = (Expression)(-1);
#if UNITY_EDITOR
        EditorUpdatePreview();
#endif
        if (!approvedAtlasAssigned || !showFace || faceAtlas == null || faceMaterial == null || faceMaterial.shader == null ||
            faceMaterial.shader.name != "DreamGuardians/BossFaceOverlay")
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            return;
        }
        if (faceObject == null) CreateFace();
        if (!hasBounds) return;
        faceRenderer.enabled = true;
        PlaceFace();
        runtimeMaterial.SetTexture("_BaseMap", faceAtlas);
        Rect sample = GetAtlasRect((int)CurrentExpression);
        runtimeMaterial.SetTextureScale("_BaseMap", sample.size);
        runtimeMaterial.SetTextureOffset("_BaseMap", sample.position);
        AnimateEyes();
    }

    private Expression ResolveExpression()
    {
        if (summonUntil > 0f && Time.time >= summonUntil) CancelSummon();
        if (deathTime >= 0f) return Expression.FinalRage;
        if (Time.time < hitUntil) return Expression.Hit;
        if (Time.time < mockUntil) return Expression.Mock;
        if (Time.time < fireUntil) return Expression.EnergyFire;
        if (summonUntil > 0f && Time.time >= summonEndAt) return Expression.SummonEnd;
        if (action.HasValue) return action.Value;
        float hp = health != null ? health.NormalizedHealth : 1f;
        if (hp <= lowHpThreshold || baseExpression == Expression.FinalRage) return Expression.FinalRage;
        if (hp <= 0.5f || baseExpression == Expression.Rage) return Expression.Rage;
        return baseExpression;
    }

    private void CreateFace()
    {
        // Mesh bounds, not world AABBs: stable under boss rotation and squash.
        Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(faceLocalEuler));
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds bounds = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = inverse * transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!hasBounds) { faceBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else faceBounds.Encapsulate(point);
            }
        }
        faceObject = new GameObject("BossFace");
        faceObject.layer = gameObject.layer;
        faceObject.transform.SetParent(transform, false);
        faceMesh = new Mesh { name = "BossFace_Quad", hideFlags = HideFlags.DontSave };
        BuildEyeMesh();
        faceMesh.RecalculateNormals();
        faceMesh.RecalculateBounds();
        faceObject.AddComponent<MeshFilter>().sharedMesh = faceMesh;
        faceRenderer = faceObject.AddComponent<MeshRenderer>();
        faceRenderer.enabled = false;
        faceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        faceRenderer.receiveShadows = false;
        faceRenderer.lightProbeUsage = LightProbeUsage.Off;
        faceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        runtimeMaterial = new Material(faceMaterial) { name = "BossFace_Runtime", hideFlags = HideFlags.DontSave };
        runtimeMaterial.SetFloat("_Surface", 1f);
        runtimeMaterial.SetFloat("_Blend", 0f);
        runtimeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        runtimeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        runtimeMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        runtimeMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        runtimeMaterial.SetFloat("_ZWrite", 0f);
        runtimeMaterial.SetFloat("_Cull", (float)CullMode.Back);
        runtimeMaterial.SetFloat("_AlphaClip", 0f);
        runtimeMaterial.SetColor("_BaseColor", Color.white);
        runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
        runtimeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        runtimeMaterial.DisableKeyword("_ALPHAMODULATE_ON");
        runtimeMaterial.SetOverrideTag("RenderType", "Transparent");
        runtimeMaterial.SetShaderPassEnabled("ShadowCaster", false);
        runtimeMaterial.renderQueue = (int)RenderQueue.Transparent;
        faceRenderer.sharedMaterial = runtimeMaterial;
    }

    private void PlaceFace()
    {
        Quaternion rotation = Quaternion.Euler(faceLocalEuler);
        Vector3 position = faceBounds.center + Vector3.Scale(faceBounds.size, faceOffset);
        position.z += faceBounds.extents.z + faceBounds.size.z * surfaceOffset;
        faceObject.transform.localPosition = rotation * position;
        faceObject.transform.localRotation = rotation;
        float size = Mathf.Min(faceBounds.size.x * faceSize.x, faceBounds.size.y * faceSize.y);
        faceObject.transform.localScale = new Vector3(size, size, 1f);
    }
    private void OnValidate()
    {
        atlasGrid = new Vector2Int(4, 3);
        faceSize = new Vector2(Mathf.Max(0.01f, faceSize.x), Mathf.Max(0.01f, faceSize.y));
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
    }
    private void OnDestroy()
    {
        if (faceObject != null) Destroy(faceObject);
        if (faceMesh != null) Destroy(faceMesh);
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    private void BuildEyeMesh()
    {
        const int steps = 64;
        restVertices = new Vector3[(steps + 1) * (steps + 1)];
        animatedVertices = new Vector3[restVertices.Length];
        cellUvs = new Vector2[restVertices.Length];
        int[] triangles = new int[steps * steps * 6];
        int t = 0;
        for (int y = 0; y <= steps; y++)
        for (int x = 0; x <= steps; x++)
        {
            int i = y * (steps + 1) + x;
            // Viewed from the outward face normal, mesh-local +X points screen-left.
            // Mirror sampling (and its mask coordinates), never the source artwork.
            cellUvs[i] = new Vector2(1f - x / (float)steps, y / (float)steps);
            restVertices[i] = new Vector3(0.5f - cellUvs[i].x, cellUvs[i].y - 0.5f, 0f);
            if (x == steps || y == steps) continue;
            triangles[t++] = i; triangles[t++] = i + 1; triangles[t++] = i + steps + 1;
            triangles[t++] = i + steps + 1; triangles[t++] = i + 1; triangles[t++] = i + steps + 2;
        }
        BuildEyeMasks();
        faceMesh.MarkDynamic();
        faceMesh.vertices = restVertices;
        faceMesh.uv = cellUvs;
        faceMesh.triangles = triangles;
    }

    public enum EyeAnimation
    {
        Rest, IdleSquint, SummonFocus, SummonImpact, SummonSatisfied,
        ChargeSquint, FireImpact, HitJitter, RageTwitch, FinalRageTwitch
    }

    public EyeAnimation CurrentEyeAnimation { get; private set; }
    private Expression previousEyeExpression = (Expression)(-1);
    private float eyeStateStarted, nextTwitch, twitchStarted = -100f;
    // Do not consume Unity's global random stream used by combat/spawning.
    private readonly System.Random eyeRandom = new System.Random();

    private float NextEyeInterval(Expression expression)
    {
        float min = expression == Expression.FinalRage ? 0.8f : expression == Expression.Rage ? 1.5f : 2.5f;
        float max = expression == Expression.FinalRage ? 1.8f : expression == Expression.Rage ? 3f : 5f;
        return Mathf.Lerp(min, max, (float)eyeRandom.NextDouble());
    }

    private static float EyeImpact(float elapsed)
    {
        if (elapsed < 0.06f) return Mathf.Lerp(0.82f, 1.10f, Mathf.Clamp01(elapsed / 0.06f));
        if (elapsed < 0.14f) return Mathf.Lerp(1.10f, 0.93f, (elapsed - 0.06f) / 0.08f);
        return Mathf.Lerp(0.93f, 1f, Mathf.Clamp01((elapsed - 0.14f) / 0.08f));
    }

    private void AnimateEyes()
    {
        float now = Time.time;
        if (previousEyeExpression != CurrentExpression)
        {
            previousEyeExpression = CurrentExpression;
            eyeStateStarted = now;
            twitchStarted = -100f;
            nextTwitch = now + NextEyeInterval(CurrentExpression);
        }
        float elapsed = now - eyeStateStarted;
        float chargeProgress = (now - chargeStarted) / chargeDuration;
        float fireElapsed = now - (fireUntil - 0.4f);
        float hitElapsed = now - (hitUntil - 0.2f);
        if (readingNetworkPresentation)
        {
            elapsed = networkPresentationAge;
            hitElapsed = networkPresentationAge;
            // Random idle/rage twitches intentionally keep their local clocks.
        }
#if UNITY_EDITOR
        if (editorFacePreview && Application.isPlaying)
        {
            // Independent preview clock: never overwrite action/HP/event timers.
            elapsed = now - editorPreviewStarted;
            chargeProgress = elapsed / Mathf.Max(0.1f, editorChargeSeconds);
            fireElapsed = hitElapsed = elapsed;
            twitchStarted = editorPreviewStarted;
            nextTwitch = float.PositiveInfinity;
        }
#endif
        float scale = 1f;
        Vector2 offset = Vector2.zero;
        float opposingOffset = 0f;
        CurrentEyeAnimation = EyeAnimation.Rest;
        switch (CurrentExpression)
        {
            case Expression.Idle:
            case Expression.Rage:
            case Expression.FinalRage:
                if (now >= nextTwitch)
                {
                    twitchStarted = now;
                    nextTwitch = now + 0.22f + NextEyeInterval(CurrentExpression);
                }
                float twitch = (now - twitchStarted) / 0.22f;
                if (twitch >= 0f && twitch < 1f)
                {
                    float pulse = Mathf.Sin(twitch * Mathf.PI);
                    if (CurrentExpression == Expression.Idle)
                    {
                        CurrentEyeAnimation = EyeAnimation.IdleSquint;
                        scale = 1f - 0.1f * pulse;
                    }
                    else
                    {
                        bool final = CurrentExpression == Expression.FinalRage;
                        CurrentEyeAnimation = final ? EyeAnimation.FinalRageTwitch : EyeAnimation.RageTwitch;
                        opposingOffset = 0.005f * Mathf.Sin(twitch * Mathf.PI * 2f) * pulse;
                        scale = final ? 1f - 0.08f * pulse : 1f - 0.05f * pulse;
                    }
                }
                break;
            case Expression.SummonPrepare:
                CurrentEyeAnimation = EyeAnimation.SummonFocus;
                float focus = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));
                scale = 1f - 0.1f * focus;
                offset = (summonLook + Vector2.down * 0.003f) * focus;
                opposingOffset = 0.002f * focus;
                break;
            case Expression.SummonActivate:
                CurrentEyeAnimation = EyeAnimation.SummonImpact;
                float impact = Mathf.Clamp01(elapsed / 0.2f);
                scale = impact < 0.4f ? Mathf.Lerp(0.9f, 1.05f, impact / 0.4f) :
                    Mathf.Lerp(1.05f, 1f, (impact - 0.4f) / 0.6f);
                opposingOffset = -0.004f * Mathf.Sin(impact * Mathf.PI);
                offset.y = -0.003f * Mathf.Sin(impact * Mathf.PI);
                break;
            case Expression.SummonEnd:
                CurrentEyeAnimation = EyeAnimation.SummonSatisfied;
                offset.x = 0.003f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));
                break;
            case Expression.EnergyCharge:
                CurrentEyeAnimation = EyeAnimation.ChargeSquint;
                scale = 1f - 0.18f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chargeProgress));
                break;
            case Expression.EnergyFire:
                CurrentEyeAnimation = EyeAnimation.FireImpact;
                scale = EyeImpact(fireElapsed);
                break;
            case Expression.Hit:
                CurrentEyeAnimation = EyeAnimation.HitJitter;
                float hitProgress = Mathf.Clamp01(hitElapsed / 0.18f);
                offset.x = Mathf.Sin(hitProgress * 2f * Mathf.PI) * 0.004f;
                break;
        }
#if UNITY_EDITOR
        if (editorFacePreview && !editorAnimateEyes)
        {
            scale = 1f;
            offset = Vector2.zero;
            opposingOffset = 0f;
            CurrentEyeAnimation = EyeAnimation.Rest;
        }
#endif
        // A single owner writes the mesh, always from the original vertices.
        // Switching eye states cannot accumulate transforms or affect the mouth.
        for (int i = 0; i < restVertices.Length; i++)
        {
            Vector2 uv = cellUvs[i];
            bool left = uv.x < 0.5f;
            Rect region = eyeBounds[(int)CurrentExpression, left ? 0 : 1];
            Vector3 vertex = restVertices[i];
            if (eyeWeights[(int)CurrentExpression][i] > 0f)
            {
                float weight = eyeWeights[(int)CurrentExpression][i];
                vertex.x -= (offset.x + (left ? opposingOffset : -opposingOffset)) * weight;
                vertex.y += (offset.y + (uv.y - region.center.y) * (scale - 1f)) * weight;
            }
            animatedVertices[i] = vertex;
        }
        faceMesh.vertices = animatedVertices;
        faceMesh.RecalculateBounds();
    }
}
