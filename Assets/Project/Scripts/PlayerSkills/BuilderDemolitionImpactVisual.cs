using System.Collections;
using UnityEngine;

public sealed class BuilderDemolitionImpactVisual : MonoBehaviour
{
    [SerializeField] private MeshRenderer contactBurst;
    [SerializeField] private MeshRenderer groundCrack;
    [SerializeField] private MeshRenderer shockwaveRing;
    [SerializeField] private Transform debrisOrigin;
    [SerializeField] private Transform dustOrigin;

    private MaterialPropertyBlock propertyBlock;

    public Transform DebrisOrigin => debrisOrigin != null ? debrisOrigin : transform;
    public Transform DustOrigin => dustOrigin != null ? dustOrigin : transform;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Play()
    {
        SetVisual(contactBurst, 0.4f, 1f);
        SetVisual(groundCrack, 0.5f, 1f);
        SetVisual(shockwaveRing, 0.6f, 1f);
        StartCoroutine(AnimateImpact());
    }

    private IEnumerator AnimateImpact()
    {
        const float contactDuration = 0.11f;
        const float crackExpandDuration = 0.12f;
        const float crackSettleDuration = 0.06f;
        const float crackHoldDuration = 1.2f;
        const float crackFadeDuration = 0.3f;
        const float shockwaveDuration = 0.34f;
        const float totalDuration = crackHoldDuration + crackFadeDuration;

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            float contactT = Mathf.Clamp01(elapsed / contactDuration);
            SetVisual(contactBurst, Mathf.Lerp(0.4f, 2.2f, Smooth(contactT)), 1f - contactT);

            float crackT = Mathf.Clamp01(elapsed / crackExpandDuration);
            float crackDiameter = Mathf.Lerp(0.5f, 4.5f, Smooth(crackT));
            if (elapsed > crackExpandDuration)
            {
                float settleT = Mathf.Clamp01((elapsed - crackExpandDuration) / crackSettleDuration);
                crackDiameter = Mathf.Lerp(4.5f, 4.3f, Smooth(settleT));
            }
            float crackAlpha = elapsed <= crackHoldDuration
                ? 1f
                : 1f - Mathf.Clamp01((elapsed - crackHoldDuration) / crackFadeDuration);
            SetVisual(groundCrack, crackDiameter, crackAlpha);

            float ringT = Mathf.Clamp01(elapsed / shockwaveDuration);
            SetVisual(shockwaveRing, Mathf.Lerp(0.6f, 5.5f, Smooth(ringT)), 1f - ringT);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetVisual(MeshRenderer renderer, float diameter, float alpha)
    {
        if (renderer == null) return;
        renderer.enabled = alpha > 0.001f;
        renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        renderer.GetPropertyBlock(propertyBlock);
        Color color = Color.white;
        if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
            color = renderer.sharedMaterial.GetColor("_BaseColor");
        color.a *= Mathf.Clamp01(alpha);
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(propertyBlock);
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
}
