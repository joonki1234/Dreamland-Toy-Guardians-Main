using UnityEditor;
using UnityEngine;

namespace UpDraftArt.EditorTools
{
    [InitializeOnLoad]
    public static class UpDraftSamplerReviewBanner
    {
        private const string ReviewUrl = "https://assetstore.unity.com/packages/slug/373744";
        private const string DismissPrefsKey = "UpDraftArt_SamplerReviewBannerDismissed";

        private const float BannerWidth = 420f;
        private const float BannerHeight = 96f;

        static UpDraftSamplerReviewBanner()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (EditorPrefs.GetBool(DismissPrefsKey, false))
                return;

            Handles.BeginGUI();

            Rect rect = new Rect(425, 16, BannerWidth, BannerHeight);

            GUILayout.BeginArea(rect, GUI.skin.window);

            GUILayout.Label("Enjoying the UpDraft Kitchen sampler?", EditorStyles.boldLabel);

            GUILayout.Label(
                "A quick review helps support future free samples and updates.",
                EditorStyles.wordWrappedLabel
            );

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Leave a Review", GUILayout.Height(28)))
            {
                Application.OpenURL(ReviewUrl);
            }

            if (GUILayout.Button("Don't show again", GUILayout.Height(28), GUILayout.Width(130)))
            {
                EditorPrefs.SetBool(DismissPrefsKey, true);
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            Handles.EndGUI();
        }
    }
}