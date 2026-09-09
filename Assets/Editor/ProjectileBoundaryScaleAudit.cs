using UnityEditor;
using UnityEngine;

// Editor only: inspect actual loaded instances, including prefab and runtime children.
[InitializeOnLoad]
internal static class ProjectileBoundaryScaleAudit
{
    static ProjectileBoundaryScaleAudit()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) Audit(); };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            Audit();
    }

    [MenuItem("Tools/Diagnostics/Audit Loaded BoxCollider Scales")]
    private static void Audit()
    {
        foreach (BoxCollider box in Resources.FindObjectsOfTypeAll<BoxCollider>())
        {
            if (EditorUtility.IsPersistent(box) || !box.gameObject.scene.IsValid() ||
                !box.gameObject.scene.isLoaded) continue;
            Transform t = box.transform;
            // Only the previously identified leaf boundary is safe to normalize here.
            // Reflect its center with the scale signs: all eight box corners stay identical.
            if (t.name == "ProjectileBoundary_BottomFloor" && t.parent != null &&
                t.parent.name == "ProjectileBoundary" && t.childCount == 0 &&
                HasNegative(t.localScale))
            {
                if (!EditorApplication.isPlaying) Undo.RecordObjects(new Object[] { t, box }, "Normalize boundary scale");
                Vector3 s = t.localScale;
                box.center = Vector3.Scale(box.center, new Vector3(Mathf.Sign(s.x), Mathf.Sign(s.y), Mathf.Sign(s.z)));
                t.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                if (!EditorApplication.isPlaying)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
                Debug.Log($"[BoxCollider Audit] Normalized {Path(t)}: {s} -> {t.localScale}; box corners preserved.", box);
            }
            if (!HasNegative(t.lossyScale) && !HasNegative(box.size)) continue;
            string chain = "";
            for (Transform node = t; node != null; node = node.parent)
                chain += $"\n{Path(node)} local={node.localScale} lossy={node.lossyScale}";
            Debug.LogWarning($"[BoxCollider Audit] {Path(t)} size={box.size} center={box.center}{chain}", box);
        }
    }

    private static bool HasNegative(Vector3 value) => value.x < 0 || value.y < 0 || value.z < 0;
    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
