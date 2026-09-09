using DreamGuardians;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local presentation only. Never writes combat or network state.</summary>
[DisallowMultipleComponent]
public sealed class FinalBossFaceController : MonoBehaviour
{
    public enum Expression
    {
        IdlePhase1 = 0, Summon = 1, ApproachPhase2 = 2, IdlePhase2 = 3,
        ApproachPhase3 = 4, IdlePhase3 = 5, SlamWindup = 6, SlamImpact = 7,
        Spin = 8, Hit = 9, DeathShock = 10, DeathWeak = 11,
        CleanseTransition = 12, CleanseGentle = 13
    }

    [SerializeField] private Texture2D faceAtlas;
    [SerializeField] private Material faceMaterial;
    [SerializeField] private Vector2Int atlasGrid = new Vector2Int(4, 4);
    [Tooltip("Model-local rotation. GiftBox's -90 degree pitch requires +90 here.")]
    [SerializeField] private Vector3 faceLocalEuler = new Vector3(90f, 0f, 0f);
    [Tooltip("Offset in face-frame bounds fractions: right, up, outward.")]
    [SerializeField] private Vector3 faceOffset = new Vector3(0f, -0.08f, 0f);
    [Tooltip("Width / height as fractions of model bounds in the face frame.")]
    [SerializeField] private Vector2 faceSize = new Vector2(0.8f, 0.65f);
    [Tooltip("Outward separation as a fraction of model depth.")]
    [SerializeField, Min(0f)] private float surfaceOffset = 0.015f;

    private EnemyHealth health;
    private GameObject faceObject;
    private MeshRenderer faceRenderer;
    private Mesh faceMesh;
    private Material runtimeMaterial;
    private Bounds faceBounds;
    private bool hasBounds;
    private Expression? action;
    private float impactUntil, summonUntil, hitUntil;
    private float deathTime = -1f;
    private float cleanseProgress = -1f;
    public Expression CurrentExpression { get; private set; }

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
        health.Died += OnDied;
    }
    private void OnDisable()
    {
        if (health != null)
        {
            health.HitRegistered -= OnHit;
            health.Died -= OnDied;
        }
        health = null;
        action = null;
        impactUntil = summonUntil = hitUntil = 0f;
        if (faceRenderer != null) faceRenderer.enabled = false;
    }
    private void OnHit(EnemyHealth source, DamageInfo damage) { hitUntil = Time.time + 0.12f; }
    private void OnDied(EnemyHealth source, DamageInfo damage) { ShowDeath(); }
    public void ShowDeath()
    {
        if (deathTime < 0f) deathTime = Time.time;
    }
    public void ShowSummon() { summonUntil = Time.time + 0.6f; }
    public void BeginAction(Expression expression)
    {
        if (expression != Expression.ApproachPhase2 && expression != Expression.ApproachPhase3 &&
            expression != Expression.SlamWindup && expression != Expression.Spin) return;
        action = expression;
        impactUntil = 0f;
    }
    public void EndAction() { action = null; }
    public void ShowSlamImpact() { impactUntil = Time.time + 0.12f; }
    public void SetCleanseProgress(float progress)
    {
        cleanseProgress = Mathf.Clamp01(progress);
    }

    private void LateUpdate()
    {
        BindHealth();
        if (health != null && health.IsDead) ShowDeath();
        CurrentExpression = ResolveExpression();
        if (faceAtlas == null || faceMaterial == null || faceMaterial.shader == null ||
            faceMaterial.shader.name != "Universal Render Pipeline/Unlit")
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            return;
        }
        if (faceObject == null) CreateFace();
        if (!hasBounds) return;
        faceRenderer.enabled = true;
        PlaceFace();
        runtimeMaterial.SetTexture("_BaseMap", faceAtlas);
        int index = (int)CurrentExpression;
        runtimeMaterial.SetTextureScale("_BaseMap", new Vector2(0.25f, 0.25f));
        runtimeMaterial.SetTextureOffset("_BaseMap",
            new Vector2((index % 4) * 0.25f, (3 - index / 4) * 0.25f));
    }

    private Expression ResolveExpression()
    {
        if (cleanseProgress >= 0f)
            return cleanseProgress >= 0.5f ? Expression.CleanseGentle : Expression.CleanseTransition;
        if (deathTime >= 0f)
            return Time.time - deathTime < 0.35f ? Expression.DeathShock : Expression.DeathWeak;
        if (Time.time < impactUntil) return Expression.SlamImpact;
        if (action.HasValue) return action.Value;
        if (Time.time < summonUntil) return Expression.Summon;
        if (Time.time < hitUntil) return Expression.Hit;
        float hp = health != null ? health.NormalizedHealth : 1f;
        return hp > 2f / 3f ? Expression.IdlePhase1 :
            hp > 1f / 3f ? Expression.IdlePhase2 : Expression.IdlePhase3;
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
        faceMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
        faceMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        faceMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
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
        faceObject.transform.localScale = new Vector3(faceBounds.size.x * faceSize.x, faceBounds.size.y * faceSize.y, 1f);
    }
    private void OnValidate()
    {
        atlasGrid = new Vector2Int(4, 4);
        faceSize = new Vector2(Mathf.Max(0.01f, faceSize.x), Mathf.Max(0.01f, faceSize.y));
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
    }
    private void OnDestroy()
    {
        if (faceObject != null) Destroy(faceObject);
        if (faceMesh != null) Destroy(faceMesh);
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }
}
