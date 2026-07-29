using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UpDraftArt.EditorTools
{
    [InitializeOnLoad]
    public class UpDraftReviewPrompt : EditorWindow
    {
        private const string PromptShownKey = "UpDraft_KitchenFree_PromptShown";
        private const string DismissedKey = "UpDraft_KitchenFree_Dismissed";
        private const string FirstPlacementTicksKey = "UpDraft_KitchenFree_FirstPlacementUtcTicks";
        private const string NextPromptTicksKey = "UpDraft_KitchenFree_NextPromptUtcTicks";

        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan RemindLaterDelay = TimeSpan.FromDays(3);

        private const string AssetPathMarker = "UpDraftArt";

        private const string ReviewUrl = "https://assetstore.unity.com/packages/slug/373744";
        private const string FullPackUrl = "https://assetstore.unity.com/packages/3d/props/stylized-medieval-kitchen-props-pack-359316";

        static UpDraftReviewPrompt()
        {
            if (EditorPrefs.GetBool(DismissedKey, false))
                return;

            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnHierarchyChanged()
        {
            if (EditorPrefs.GetBool(DismissedKey, false))
                return;

            if (EditorPrefs.HasKey(FirstPlacementTicksKey))
                return;

            GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject go in sceneObjects)
            {
                if (go == null)
                    continue;

                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;

                if (EditorUtility.IsPersistent(go))
                    continue;

                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                if (!prefabPath.Contains(AssetPathMarker, StringComparison.OrdinalIgnoreCase))
                    continue;

                SetUtcTicks(FirstPlacementTicksKey, DateTime.UtcNow.Ticks);
                break;
            }
        }

        private static void OnEditorUpdate()
        {
            if (EditorPrefs.GetBool(DismissedKey, false))
            {
                Cleanup();
                return;
            }

            if (HasOpenInstances<UpDraftReviewPrompt>())
                return;

            long nowTicks = DateTime.UtcNow.Ticks;

            if (TryGetUtcTicks(NextPromptTicksKey, out long nextPromptTicks))
            {
                if (nowTicks < nextPromptTicks)
                    return;

                ShowPromptWindow();
                return;
            }

            if (TryGetUtcTicks(FirstPlacementTicksKey, out long firstPlacementTicks))
            {
                if (nowTicks >= firstPlacementTicks + InitialDelay.Ticks)
                {
                    ShowPromptWindow();
                }
            }
        }

        private static void ShowPromptWindow()
        {
            if (EditorPrefs.GetBool(PromptShownKey, false))
                return;

            EditorPrefs.SetBool(PromptShownKey, true);

            UpDraftReviewPrompt window = CreateInstance<UpDraftReviewPrompt>();
            window.titleContent = new GUIContent("UpDraft Art");
            window.minSize = new Vector2(420, 240);
            window.maxSize = new Vector2(420, 240);
            window.ShowUtility();
        }

        private static void Cleanup()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void SetUtcTicks(string key, long ticks)
        {
            EditorPrefs.SetString(key, ticks.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryGetUtcTicks(string key, out long ticks)
        {
            ticks = 0;

            if (!EditorPrefs.HasKey(key))
                return false;

            return long.TryParse(
                EditorPrefs.GetString(key),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out ticks
            );
        }

        private void OnGUI()
        {
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                fontSize = 14
            };

            GUIStyle bodyStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 12
            };

            GUILayout.Space(12);
            GUILayout.Label("Thanks for trying the Free Kitchen Sampler!", titleStyle);

            GUILayout.Space(10);
            GUILayout.Label(
                "If this sampler was useful, I’d really appreciate an honest rating or review on the Asset Store. It helps a lot as a solo creator.",
                bodyStyle);

            GUILayout.Space(8);
            GUILayout.Label(
                "If you want more props in this style, you can also check out the full Bakery Props Pack below.",
                bodyStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Leave a Review", GUILayout.Height(36)))
            {
                Application.OpenURL(ReviewUrl);
                EditorPrefs.SetBool(DismissedKey, true);
                Close();
            }

            GUILayout.Space(6);

            if (GUILayout.Button("View Full Pack", GUILayout.Height(32)))
            {
                Application.OpenURL(FullPackUrl);
                EditorPrefs.SetBool(DismissedKey, true);
                Close();
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Remind Me Later", GUILayout.Height(28)))
            {
                SetUtcTicks(NextPromptTicksKey, DateTime.UtcNow.Add(RemindLaterDelay).Ticks);
                EditorPrefs.SetBool(PromptShownKey, false);
                Close();
            }

            if (GUILayout.Button("Dismiss", GUILayout.Height(28)))
            {
                EditorPrefs.SetBool(DismissedKey, true);
                Close();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(12);
        }
    }
}
