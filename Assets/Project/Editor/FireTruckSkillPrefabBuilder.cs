using UnityEditor;
using UnityEngine;

public static class FireTruckSkillPrefabBuilder
{
    private const string SourcePrefabPath =
        "Assets/Scenes/Jucho/FirefightersPack/Models/Prefabs/Vehicles/FireTruck1.prefab";
    private const string OutputPrefabPath =
        "Assets/Project/Prefabs/PlayerSkills/FireTruckSkill.prefab";
    private const string PlayerPrefabPath = "Assets/01_Player.prefab";

    [MenuItem("Dreamland/Skills/Rebuild Fire Truck Skill Prefab")]
    public static void Build()
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (sourcePrefab == null)
        {
            throw new System.InvalidOperationException(
                $"FireTruck1 프리팹을 찾을 수 없습니다: {SourcePrefabPath}");
        }

        GameObject wrapperRoot = new GameObject("FireTruckSkill");

        try
        {
            wrapperRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            wrapperRoot.transform.localScale = Vector3.one;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            visual.name = "FireTruck1";
            visual.transform.SetParent(wrapperRoot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new System.InvalidOperationException(
                    "FireTruck1 하위에서 Renderer를 찾지 못했습니다.");
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            float originalMinimumY = combinedBounds.min.y;
            float localYOffset = wrapperRoot.transform.position.y - originalMinimumY;
            visual.transform.localPosition = new Vector3(0f, localYOffset, 0f);

            Bounds alignedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                alignedBounds.Encapsulate(renderers[i].bounds);
            }

            if (Mathf.Abs(alignedBounds.min.y - wrapperRoot.transform.position.y) > 0.0001f)
            {
                throw new System.InvalidOperationException(
                    $"FireTruck1 바닥 정렬 검증 실패: minY={alignedBounds.min.y:F6}");
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wrapperRoot, OutputPrefabPath);
            if (savedPrefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Wrapper Prefab 저장에 실패했습니다: {OutputPrefabPath}");
            }

            AssignToPlayerPrefab(savedPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[FireTruckSkill Builder] 생성 완료 | " +
                $"원본 Renderer minY={originalMinimumY:F6}, " +
                $"자식 Local Y={localYOffset:F6}, " +
                $"정렬 후 minY={alignedBounds.min.y:F6}");
        }
        finally
        {
            Object.DestroyImmediate(wrapperRoot);
        }
    }

    private static void AssignToPlayerPrefab(GameObject fireTruckSkillPrefab)
    {
        GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            PlayerJobSkillController skillController =
                playerRoot.GetComponent<PlayerJobSkillController>();
            if (skillController == null)
            {
                throw new System.InvalidOperationException(
                    "01_Player.prefab에서 PlayerJobSkillController를 찾지 못했습니다.");
            }

            SerializedObject serializedController = new SerializedObject(skillController);
            SerializedProperty fireTruckProperty =
                serializedController.FindProperty("firefighterSkill.fireTruckPrefab");
            SerializedProperty heightOffsetProperty =
                serializedController.FindProperty("firefighterSkill.spawnHeightOffset");

            if (fireTruckProperty == null || heightOffsetProperty == null)
            {
                throw new System.InvalidOperationException(
                    "Firefighter Skill 직렬화 필드를 찾지 못했습니다.");
            }

            fireTruckProperty.objectReferenceValue = fireTruckSkillPrefab;
            heightOffsetProperty.floatValue = 0f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(playerRoot);
        }
    }
}
