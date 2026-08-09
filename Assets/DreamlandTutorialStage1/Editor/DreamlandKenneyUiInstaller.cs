#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamGuardians.EditorTools
{
    /// <summary>
    /// Copies only the Kenney UI Pack - Sci-Fi sprites used by Dreamland into
    /// Resources so the runtime-built HUD can load them without manual setup.
    /// The original ThirdParty/Kenney folder remains untouched.
    /// </summary>
    [InitializeOnLoad]
    internal static class DreamlandKenneyUiInstaller
    {
        private const string SearchRoot = "Assets/ThirdParty/Kenney";
        private const string DestinationRoot = "Assets/Resources/KenneySciFiUI";

        static DreamlandKenneyUiInstaller()
        {
            EditorApplication.delayCall += InstallSelectedAssets;
        }

        [MenuItem("Dreamland/UI/Install Kenney Sci-Fi HUD Assets")]
        private static void InstallSelectedAssets()
        {
            if (!AssetDatabase.IsValidFolder(SearchRoot))
            {
                Debug.LogWarning(
                    "[Dreamland UI] Kenney source folder not found: " + SearchRoot +
                    "\nImport UI Pack Sci-Fi under Assets/ThirdParty/Kenney first.");
                return;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder(DestinationRoot);

            bool changed = false;
            changed |= CopySelected("button_square_header_large_rectangle", "Blue", "Default", "mission_panel.png");
            changed |= CopySelected("button_square_header_notch_rectangle", "Blue", "Default", "counter_panel.png");
            changed |= CopySelected("bar_round_gloss_large", "Blue", "Default", "core_bar_blue.png");
            changed |= CopySelected("bar_round_gloss_large", "Green", "Default", "core_bar_green.png");
            changed |= CopySelected("bar_round_gloss_large", "Red", "Default", "core_bar_red.png");
            changed |= CopySelected("button_square_header_blade_rectangle", "Blue", "Double", "boss_panel.png");
            changed |= CopySelected("bar_round_gloss_large", "Blue", "Double", "boss_bar_blue.png");
            changed |= CopySelected("bar_round_gloss_large", "Red", "Double", "boss_bar_red.png");

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Dreamland UI] Kenney Sci-Fi HUD assets installed into Resources/KenneySciFiUI.");
            }
        }

        private static bool CopySelected(
            string assetName,
            string colorFolder,
            string variantFolder,
            string destinationFileName)
        {
            string destination = DestinationRoot + "/" + destinationFileName;
            if (File.Exists(Path.GetFullPath(destination)))
            {
                return false;
            }

            string source = FindSource(assetName, colorFolder, variantFolder);
            if (string.IsNullOrEmpty(source))
            {
                Debug.LogWarning(
                    $"[Dreamland UI] Could not find Kenney asset: {colorFolder}/{variantFolder}/{assetName}.png");
                return false;
            }

            if (!AssetDatabase.CopyAsset(source, destination))
            {
                Debug.LogWarning("[Dreamland UI] Failed to copy: " + source);
                return false;
            }

            return true;
        }

        private static string FindSource(
            string assetName,
            string colorFolder,
            string variantFolder)
        {
            string[] guids = AssetDatabase.FindAssets(assetName + " t:Texture2D", new[] { SearchRoot });
            string expected = "/PNG/" + colorFolder + "/" + variantFolder + "/";

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    assetName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return path;
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
