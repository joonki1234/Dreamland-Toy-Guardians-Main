using UnityEngine;

public sealed class BuilderMagicHammerVisual : MonoBehaviour
{
    [SerializeField] private GameObject animationTarget;
    [SerializeField] private Renderer[] hammerRenderers;
    [SerializeField] private Transform impactPoint;

    public GameObject AnimationTarget => animationTarget;
    public Renderer[] HammerRenderers => hammerRenderers;
    public Transform ImpactPoint => impactPoint;

#if UNITY_EDITOR
    public void Configure(GameObject target, Transform hammerImpactPoint)
    {
        animationTarget = target;
        hammerRenderers = target != null
            ? target.GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();
        impactPoint = hammerImpactPoint;
    }
#endif
}
