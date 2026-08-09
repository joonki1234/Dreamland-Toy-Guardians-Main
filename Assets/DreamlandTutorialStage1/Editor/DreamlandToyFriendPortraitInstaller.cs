#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamGuardians.EditorTools
{
    /// <summary>
    /// PF_ToyFriend prefab의 Unity Asset Preview를 2D 통신창용 초상화로 저장합니다.
    /// 별도의 그림 파일이 없어도 실제 프로젝트의 장난감 친구 모습을 사용합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class DreamlandToyFriendPortraitInstaller
    {
        private const string PrefabPath =
            "Assets/Project/Prefabs/ToyFriend/PF_ToyFriend.prefab";
        private const string DestinationPath =
            "Assets/Resources/DreamlandUI/toy_friend_portrait.png";

        private static int attemptCount;
        private const int MaxAttempts = 80;

        static DreamlandToyFriendPortraitInstaller()
        {
            EditorApplication.delayCall += TryGenerateAutomatically;
        }

        [MenuItem("Dreamland/UI/Generate Toy Friend Portrait")]
        private static void GenerateFromMenu()
        {
            attemptCount = 0;
            TryGeneratePortrait(true);
        }

        private static void TryGenerateAutomatically()
        {
            if (File.Exists(DestinationPath))
            {
                ConfigureImporter();
                return;
            }

            TryGeneratePortrait(false);
        }

        private static void TryGeneratePortrait(bool logFailure)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                if (logFailure)
                {
                    Debug.LogWarning(
                        "[Dreamland UI] 장난감 친구 프리팹을 찾지 못했습니다: " +
                        PrefabPath);
                }
                return;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            if (preview == null)
            {
                attemptCount++;
                if (attemptCount < MaxAttempts)
                {
                    EditorApplication.delayCall += () => TryGeneratePortrait(logFailure);
                }
                else if (logFailure)
                {
                    Debug.LogWarning(
                        "[Dreamland UI] 장난감 친구 Preview 생성이 아직 준비되지 않았습니다. " +
                        "Dreamland > UI > Generate Toy Friend Portrait 메뉴를 한 번 더 실행해 주세요.");
                }
                return;
            }

            string directory = Path.GetDirectoryName(DestinationPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] bytes = CopyPreviewToPng(preview);
            File.WriteAllBytes(DestinationPath, bytes);
            AssetDatabase.ImportAsset(DestinationPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter();

            Debug.Log(
                "[Dreamland UI] 장난감 친구 2D 초상화를 생성했습니다: " +
                DestinationPath);
        }


        private static byte[] CopyPreviewToPng(Texture2D source)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);

            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D readable = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false);
            readable.ReadPixels(
                new Rect(0f, 0f, source.width, source.height),
                0,
                0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            byte[] bytes = readable.EncodeToPNG();
            Object.DestroyImmediate(readable);
            return bytes;
        }

        private static void ConfigureImporter()
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(DestinationPath) as TextureImporter;

            if (importer == null)
            {
                return;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.maxTextureSize != 256)
            {
                importer.maxTextureSize = 256;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
