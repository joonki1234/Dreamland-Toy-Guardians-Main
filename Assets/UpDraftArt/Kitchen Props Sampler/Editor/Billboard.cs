using UnityEditor;
using UnityEngine;

namespace UpDraftArt.EditorTools
{
    [InitializeOnLoad]
    public static class Billboard
    {
        private const string FullPackUrl = "https://assetstore.unity.com/packages/slug/359316";

        static Billboard()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(120, 20, 260, 70), "UpDraft Art", GUI.skin.window);

            GUILayout.Label("Need the complete kitchen?");

            if (GUILayout.Button("Get Full Kitchen Pack"))
            {
                Application.OpenURL(FullPackUrl);
            }

            GUILayout.EndArea();

            Handles.EndGUI();
        }
    }
}