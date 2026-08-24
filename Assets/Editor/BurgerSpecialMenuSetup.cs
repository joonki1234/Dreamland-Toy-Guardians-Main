using System.IO;
using UnityEditor;
using UnityEngine;

public static class BurgerSpecialMenuSetup
{
    [MenuItem("Tools/Chef/Bake Burger Special Menu Prefab")]
    public static void BakeBurgerSpecialMenuPrefab()
    {
        const string sourcePath = "Assets/Burger_low-poly.fbx";
        const string targetFolder = "Assets/Project/Models/Skills/Chef/Burger";
        const string prefabPath = "Assets/Project/Prefabs/PlayerSkills/Chef/BurgerSpecialMenu.prefab";
        const string playerPrefabPath = "Assets/01_Player.prefab";

        if (!File.Exists(Path.Combine(Application.dataPath, "Burger_low-poly.fbx")))
        {
            Debug.LogError("Burger source FBX not found at Assets/Burger_low-poly.fbx");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project"))
        {
            AssetDatabase.CreateFolder("Assets", "Project");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Models"))
        {
            AssetDatabase.CreateFolder("Assets/Project", "Models");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Models/Skills"))
        {
            AssetDatabase.CreateFolder("Assets/Project/Models", "Skills");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Models/Skills/Chef"))
        {
            AssetDatabase.CreateFolder("Assets/Project/Models/Skills", "Chef");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Models/Skills/Chef/Burger"))
        {
            AssetDatabase.CreateFolder("Assets/Project/Models/Skills/Chef", "Burger");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Project", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Prefabs/PlayerSkills"))
        {
            AssetDatabase.CreateFolder("Assets/Project/Prefabs", "PlayerSkills");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Project/Prefabs/PlayerSkills/Chef"))
        {
            AssetDatabase.CreateFolder("Assets/Project/Prefabs/PlayerSkills", "Chef");
        }

        string copiedModelPath = targetFolder + "/Burger_low-poly.fbx";
        if (!File.Exists(Path.Combine(Application.dataPath, copiedModelPath.Substring("Assets/".Length))))
        {
            AssetDatabase.CopyAsset(sourcePath, copiedModelPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(copiedModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("Failed to load burger FBX as GameObject asset at: " + copiedModelPath);
            return;
        }

        var root = new GameObject("BurgerSpecialMenu");
        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, null);
        if (modelInstance == null)
        {
            modelInstance = Object.Instantiate(modelAsset);
        }

        modelInstance.name = "Burger_Model";
        modelInstance.transform.SetParent(root.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        if (savedPrefab == null)
        {
            Debug.LogError("Failed to create prefab asset: " + prefabPath);
            return;
        }

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError("Failed to load player prefab: " + playerPrefabPath);
            return;
        }

        var controller = playerPrefab.GetComponentInChildren<PlayerJobSkillController>(true);
        if (controller == null)
        {
            Debug.LogError("PlayerJobSkillController was not found on the player prefab.");
            return;
        }

        var serialized = new SerializedObject(controller);
        var chefSkillProp = serialized.FindProperty("chefSkill");
        if (chefSkillProp == null)
        {
            Debug.LogError("Serialized field 'chefSkill' could not be found on PlayerJobSkillController.");
            return;
        }

        var selectedPrefabProp = chefSkillProp.FindPropertyRelative("specialMenuFoodPrefab");
        if (selectedPrefabProp == null)
        {
            Debug.LogError("Property 'specialMenuFoodPrefab' was not found inside 'chefSkill'.");
            return;
        }

        selectedPrefabProp.objectReferenceValue = savedPrefab;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        var giantProp = chefSkillProp.FindPropertyRelative("giantFoodScaleMultiplier");
        if (giantProp != null)
        {
            giantProp.floatValue = 10f;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Burger special menu prefab created and connected: " + prefabPath);
    }
}
