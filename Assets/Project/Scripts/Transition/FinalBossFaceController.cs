using DreamGuardians;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local presentation only. Atlas cells run left-to-right, top-to-bottom.</summary>
[DisallowMultipleComponent]
public sealed class FinalBossFaceController : MonoBehaviour
{
    public enum Expression
    {
        IdlePhase1, Summon, ApproachPhase2, IdlePhase2, ApproachPhase3,
        IdlePhase3, SlamWindup, SlamImpact, Spin, Hit,
        DeathShock, DeathWeak, CleanseTransition, CleanseGentle
    }

    [Header("Required assets (no placeholder face is generated)")]
    [SerializeField] private Texture2D faceAtlas;
    [Tooltip("URP/Unlit, Transparent, Alpha blending, white Base Color. Kept as an asset reference for builds.")]
    [SerializeField] private Material faceMaterial;
    [SerializeField] private Vector2Int atlasGrid = new Vector2Int(4, 4);

    [Header("GiftBox local face placement (-Y front, +Z up)")]
    [Tooltip("Fraction of the root BoxCollider width/height.")]
    [SerializeField] private Vector2 faceSize = new Vector2(0.85f, 0.8f);
    [Tooltip("Offset as a fraction of the root BoxCollider size, in model-local axes.")]
    [SerializeField] private Vector3 faceOffset;
    [SerializeField, Min(0.001f)] private float surfaceOffset = 0.008f;

    private EnemyHealth health;
    private Transform face;
    private MeshRenderer faceRenderer;
    private Material runtimeMaterial;
    private Expression? action;
    private Expression displayed = (Expression)(-1);
    private float summonUntil;
    private float hitUntil;
    private float impactUntil;
    private int summonFrame = -1;
    private bool dead;
    private bool cleansing;
    private float deathTime;
    private float cleanseTime;
    private float cleanseDuration;
    private Color currentTint = new Color(1f, 0.04f, 0.08f, 1f);
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public Expression CurrentExpression { get; private set; }

    // Called after the existing hitbox/palette setup, so the face never changes those bounds.
    public void Initialize()
    {
        if (face != null) return;
        health = GetComponent<EnemyHealth>();
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BossFace";
        quad.layer = gameObject.layer;
        Collider quadCollider = quad.GetComponent<Collider>();
        quadCollider.enabled = false;
        Destroy(quadCollider);
        face = quad.transform;
        face.SetParent(transform, false);
        faceRenderer = quad.GetComponent<MeshRenderer>();
        faceRenderer.enabled = false;
        faceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        faceRenderer.receiveShadows = false;
        faceRenderer.lightProbeUsage = LightProbeUsage.Off;
        faceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        ApplyPlacement();

        if (faceAtlas == null || faceMaterial == null)
        {
            Debug.LogWarning("[FinalBossFace] Assign the boss face atlas and URP Unlit material on Boss_GiftBox. Face remains hidden.", this);
            return;
        }
        if (!faceMaterial.HasProperty(BaseMap) || !faceMaterial.HasProperty(BaseColor) ||
            atlasGrid.x < 1 || atlasGrid.y < 1 || atlasGrid.x * atlasGrid.y < 14)
        {
            Debug.LogWarning("[FinalBossFace] Requires a URP Unlit material and at least 14 atlas cells.", this);
            return;
        }
        runtimeMaterial = new Material(faceMaterial) { name = "BossFace_Runtime" };
        runtimeMaterial.SetTexture(BaseMap, faceAtlas);
        faceRenderer.sharedMaterial = runtimeMaterial;
        faceRenderer.enabled = enabled;
    }

    private void ApplyPlacement()
    {
        if (face == null) return;
        BoxCollider box = GetComponent<BoxCollider>();
        // Boss_GiftBox's existing local hitbox is a roughly 0.02-unit cube before import scaling.
        Vector3 size = box != null ? box.size : Vector3.one * 0.02f;
        Vector3 center = box != null ? box.center : Vector3.zero;
        face.localPosition = center + Vector3.Scale(faceOffset, size) +
            Vector3.down * size.y * (0.5f + surfaceOffset);
        // Unity Quad's visible normal is -Z. Point it out of the model's -Y face.
        face.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        face.localScale = new Vector3(size.x * faceSize.x, size.z * faceSize.y, 1f);
    }

    public void PlaySummon()
    {
        if (dead || summonFrame == Time.frameCount) return;
        summonFrame = Time.frameCount;
        summonUntil = Time.time + 0.6f;
    }

    public void BeginApproach(int phase) => BeginAction(
        phase >= 2 ? Expression.ApproachPhase3 : Expression.ApproachPhase2);
    public void BeginSlam() => BeginAction(Expression.SlamWindup);
    public void BeginSpin() => BeginAction(Expression.Spin);
    private void BeginAction(Expression expression)
    {
        if (dead) return;
        action = expression;
        impactUntil = 0f;
    }
    public void EndAction() => action = null;
    public void SlamImpact()
    {
        if (!dead) impactUntil = Time.time + 0.12f;
    }
    public void PlayHit(float duration)
    {
        if (!dead) hitUntil = Time.time + duration;
    }
    public void StopCombatPresentation()
    {
        action = null;
        summonUntil = hitUntil = impactUntil = 0f;
    }
    public void BeginDeath()
    {
        if (dead) return;
        dead = true;
        deathTime = Time.time;
        StopCombatPresentation();
    }
    public void BeginCleanse(float duration)
    {
        BeginDeath();
        if (cleansing) return;
        cleansing = true;
        cleanseTime = Time.time;
        cleanseDuration = Mathf.Max(0.01f, duration);
    }

    private Expression ResolveExpression()
    {
        if (dead)
        {
            float deathAge = Time.time - deathTime;
            if (deathAge < 0.18f) return Expression.DeathShock;
            if (deathAge < 0.28f) return Expression.DeathWeak;
            if (cleansing)
                return Time.time - cleanseTime < Mathf.Min(0.3f, cleanseDuration * 0.2f)
                    ? Expression.CleanseTransition : Expression.CleanseGentle;
            return Expression.DeathWeak;
        }
        if (Time.time < impactUntil) return Expression.SlamImpact;
        if (action.HasValue) return action.Value;
        if (Time.time < summonUntil) return Expression.Summon;
        if (Time.time < hitUntil) return Expression.Hit;
        float hp = health != null ? health.NormalizedHealth : 1f;
        return hp <= 1f / 3f ? Expression.IdlePhase3 :
            hp <= 2f / 3f ? Expression.IdlePhase2 : Expression.IdlePhase1;
    }

    private void LateUpdate()
    {
        CurrentExpression = ResolveExpression();
        if (runtimeMaterial == null) return;
        if (displayed != CurrentExpression)
        {
            displayed = CurrentExpression;
            int cell = (int)displayed;
            Vector2 tile = new Vector2(1f / atlasGrid.x, 1f / atlasGrid.y);
            runtimeMaterial.SetVector(BaseMapST, new Vector4(tile.x, tile.y,
                cell % atlasGrid.x * tile.x, 1f - (cell / atlasGrid.x + 1) * tile.y));
        }
        Color target = TintFor(CurrentExpression);
        currentTint = Color.Lerp(currentTint, target, 1f - Mathf.Exp(-Time.deltaTime * 18f));
        runtimeMaterial.SetColor(BaseColor, currentTint);
    }

    private Color TintFor(Expression expression)
    {
        if (expression == Expression.DeathShock) return new Color(1f, 0.15f, 0.25f, 1f);
        if (expression == Expression.DeathWeak) return new Color(0.35f, 0.04f, 0.08f, 1f);
        if (cleansing)
        {
            float t = Mathf.Clamp01((Time.time - cleanseTime) / Mathf.Min(0.45f, cleanseDuration * 0.35f));
            return Color.Lerp(new Color(0.65f, 0.08f, 0.3f, 1f), new Color(0.55f, 0.9f, 0.8f, 1f), t);
        }
        if (expression == Expression.Hit || expression == Expression.SlamImpact)
            return new Color(1.7f, 0.5f, 0.6f, 1f);
        if (expression == Expression.Summon || expression == Expression.ApproachPhase3 ||
            expression == Expression.IdlePhase3 || expression == Expression.Spin)
            return new Color(1.3f, 0.04f, 0.45f, 1f);
        return new Color(1f, 0.04f, 0.08f, 1f);
    }

    private void OnDisable()
    {
        StopCombatPresentation();
        if (faceRenderer != null) faceRenderer.enabled = false;
    }
    private void OnEnable()
    {
        if (faceRenderer != null) faceRenderer.enabled = runtimeMaterial != null;
    }
    private void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
        if (face != null) Destroy(face.gameObject);
    }
    private void OnValidate() => ApplyPlacement();
}
