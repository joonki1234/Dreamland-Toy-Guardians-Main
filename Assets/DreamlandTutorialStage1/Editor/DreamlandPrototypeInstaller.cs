#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamGuardians.Editor
{
    public static class DreamlandPrototypeInstaller
    {
        private const string RootName = "[Dreamland Tutorial + Stage1 Prototype]";
        private const string EnemyPrefabPath = "Assets/Project/Prefabs/Enemy_Test.prefab";
        private const string DialogueFolderPath = "Assets/GameData/Dreamland";
        private const string DialogueAssetPath = DialogueFolderPath + "/TutorialDialogueData.asset";

        [MenuItem("Dreamland/튜토리얼 + Wave 1/프로토타입 설치", priority = 10)]
        private static void InstallPrototype()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Dreamland", "먼저 Dreamland_map_2 씬을 열어주세요.", "확인");
                return;
            }

            if (!scene.name.Contains("Dreamland_map_2", StringComparison.OrdinalIgnoreCase))
            {
                bool continueInstall = EditorUtility.DisplayDialog(
                    "Dreamland",
                    $"현재 씬은 '{scene.name}'입니다. 그래도 프로토타입을 설치할까요?",
                    "설치",
                    "취소");

                if (!continueInstall)
                {
                    return;
                }
            }

            GameObject existingRoot = FindInScene(scene, RootName);
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }

            Camera playerCamera = FindSceneCamera(scene);
            if (playerCamera == null)
            {
                EditorUtility.DisplayDialog("Dreamland", "씬에서 Camera를 찾지 못했습니다.", "확인");
                return;
            }

            GameObject coreObject = FindInScene(scene, "Portal_ and Core") ?? FindByPartialName(scene, "Core");
            if (coreObject == null)
            {
                coreObject = new GameObject("Prototype Dream Core");
                Undo.RegisterCreatedObjectUndo(coreObject, "Create Prototype Core");
                SceneManager.MoveGameObjectToScene(coreObject, scene);
                coreObject.transform.position = Vector3.zero;
            }

            CoreState core = GetOrAddWithUndo<CoreState>(coreObject);
            Transform energyTarget = EnsureChild(coreObject.transform, "DreamEnergyTarget");
            energyTarget.localPosition = Vector3.up * 1.5f;
            core.SetEnergyTarget(energyTarget);

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install Dreamland Prototype");
            SceneManager.MoveGameObjectToScene(root, scene);

            MissionBannerUI missionUI = Undo.AddComponent<MissionBannerUI>(root);
            DreamEnemySpawner spawner = Undo.AddComponent<DreamEnemySpawner>(root);
            Stage1WaveController stage1 = Undo.AddComponent<Stage1WaveController>(root);
            TutorialStage1Director director = Undo.AddComponent<TutorialStage1Director>(root);

            Transform spawnRoot = EnsureChild(root.transform, "Stage1 Spawn Points");
            List<Transform> spawnPoints = CreateFourDirectionSpawnPoints(spawnRoot, coreObject.transform.position, 15f);

            Transform tutorialSpawnPoint = EnsureChild(root.transform, "Tutorial Enemy Spawn");
            PlaceTutorialSpawnTowardCastle(tutorialSpawnPoint, playerCamera, 9f);
            if (tutorialSpawnPoint.GetComponent<FloorRiftMarker>() == null)
            {
                Undo.AddComponent<FloorRiftMarker>(tutorialSpawnPoint.gameObject);
            }

            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            spawner.Configure(enemyPrefab, core, spawnPoints);
            spawner.ApplyPrototypeDefaultsV6();
            stage1.ApplyPrototypePacingV5();
            stage1.Configure(spawner, missionUI, core);
            director.Configure(spawner, stage1, missionUI, tutorialSpawnPoint, 1);

            TutorialDialogueData dialogueData = GetOrCreateDialogueData();
            director.SetDialogueData(dialogueData);
            director.ApplyStoryDefaultsV8();
            stage1.SetDialogueData(dialogueData);

            missionUI.Configure(playerCamera);

            PrototypeRayWeapon prototypeWeapon = playerCamera.GetComponent<PrototypeRayWeapon>();
            if (prototypeWeapon == null)
            {
                prototypeWeapon = Undo.AddComponent<PrototypeRayWeapon>(playerCamera.gameObject);
            }
            prototypeWeapon.Configure(playerCamera, missionUI);

            PrototypeAimReticle aimReticle = AttachGlobalReticle(root, playerCamera, scene);

            SetPlayerMovementSpeedToZero(scene);

            EditorUtility.SetDirty(core);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(stage1);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(missionUI);
            EditorUtility.SetDirty(prototypeWeapon);
            EditorUtility.SetDirty(aimReticle);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            string enemyMessage = enemyPrefab != null
                ? "기존 Enemy_Test 프리팹을 사용합니다."
                : "Enemy_Test를 찾지 못해 실행 중 임시 큐브를 사용합니다.";

            EditorUtility.DisplayDialog(
                "Dreamland 프로토타입 설치 완료",
                "Play를 누르면 튜토리얼부터 자동 시작됩니다.\n\n" +
                "· 마우스 왼쪽 클릭: 테스트 발사\n" +
                "· 숫자 1~4: 직업 변경\n" +
                "· 플레이어 이동속도: 0으로 설정\n" +
                "· 튜토리얼: 3회 명중 후 처치\n" +
                "· Wave 1: 3 → 3 → 6\n\n" +
                enemyMessage,
                "확인");
        }

        [MenuItem("Dreamland/튜토리얼 + Wave 1/전투 가독성 v6 적용", priority = 11)]
        private static void ApplyCombatReadabilityV6()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = FindInScene(scene, RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Dreamland",
                    "먼저 '프로토타입 설치'를 실행해주세요.",
                    "확인");
                return;
            }

            DreamEnemySpawner spawner = root.GetComponent<DreamEnemySpawner>();
            Stage1WaveController stage1 = root.GetComponent<Stage1WaveController>();
            MissionBannerUI missionUI = root.GetComponent<MissionBannerUI>();
            CoreState core = UnityEngine.Object.FindAnyObjectByType<CoreState>();

            if (spawner == null || stage1 == null || core == null)
            {
                EditorUtility.DisplayDialog(
                    "Dreamland",
                    "Spawner, Stage1 또는 Core 연결을 찾지 못했습니다. 프로토타입을 다시 설치해주세요.",
                    "확인");
                return;
            }

            Transform spawnRoot = EnsureChild(root.transform, "Stage1 Spawn Points");
            List<Transform> spawnPoints = CreateFourDirectionSpawnPoints(
                spawnRoot,
                core.transform.position,
                15f);

            spawner.Configure(spawner.EnemyPrefab, core, spawnPoints);
            spawner.ApplyPrototypeDefaultsV6();
            stage1.ApplyPrototypePacingV5();
            stage1.Configure(spawner, missionUI, core);

            Camera playerCamera = FindSceneCamera(scene);
            Transform tutorialSpawnPoint = EnsureChild(root.transform, "Tutorial Enemy Spawn");
            if (playerCamera != null)
            {
                PlaceTutorialSpawnTowardCastle(tutorialSpawnPoint, playerCamera, 9f);
            }
            if (tutorialSpawnPoint.GetComponent<FloorRiftMarker>() == null)
            {
                Undo.AddComponent<FloorRiftMarker>(tutorialSpawnPoint.gameObject);
            }

            if (playerCamera != null)
            {
                PrototypeRayWeapon weapon = playerCamera.GetComponent<PrototypeRayWeapon>();
                if (weapon == null)
                {
                    weapon = Undo.AddComponent<PrototypeRayWeapon>(playerCamera.gameObject);
                }
                weapon.Configure(playerCamera, missionUI);

                PrototypeAimReticle reticle = AttachGlobalReticle(root, playerCamera, scene);
                EditorUtility.SetDirty(weapon);
                EditorUtility.SetDirty(reticle);
            }

            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(stage1);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            EditorUtility.DisplayDialog(
                "전투 가독성 v6 적용 완료",
                "· 튜토리얼 적은 정면 성 방향 9m에서 가만히 대기\n" +
                "· 튜토리얼과 Wave 적이 바닥 균열에서 등장\n" +
                "· Wave 적 이동속도 감소 및 코어에서 더 멀리 정지\n" +
                "· HP 바를 낮추고 크기를 확대\n" +
                "· 화면 중앙 조준 표식 추가",
                "확인");
        }

        [MenuItem("Dreamland/튜토리얼 + Wave 1/조준점 + 이동속도 v7 적용", priority = 12)]
        private static void ApplyAimAndSpeedV7()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = FindInScene(scene, RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Dreamland",
                    "먼저 '프로토타입 설치'를 실행해주세요.",
                    "확인");
                return;
            }

            DreamEnemySpawner spawner = root.GetComponent<DreamEnemySpawner>();
            MissionBannerUI missionUI = root.GetComponent<MissionBannerUI>();
            Camera playerCamera = FindSceneCamera(scene);

            if (spawner == null || playerCamera == null)
            {
                EditorUtility.DisplayDialog(
                    "Dreamland",
                    "Spawner 또는 플레이어 Camera를 찾지 못했습니다.",
                    "확인");
                return;
            }

            spawner.ApplyPrototypeDefaultsV7();

            PrototypeAimReticle reticle = AttachGlobalReticle(root, playerCamera, scene);

            PrototypeRayWeapon weapon = playerCamera.GetComponent<PrototypeRayWeapon>();
            if (weapon == null)
            {
                weapon = Undo.AddComponent<PrototypeRayWeapon>(playerCamera.gameObject);
            }
            weapon.Configure(playerCamera, missionUI);

            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(reticle);
            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            EditorUtility.DisplayDialog(
                "조준점 + 이동속도 v7 적용 완료",
                "· 화면 중앙에 검은 외곽선이 있는 고정 조준점 표시\n" +
                "· 적 조준 시 분홍색, 명중 시 초록색으로 변경\n" +
                "· Wave 적 이동속도 0.35로 감소",
                "확인");
        }

        [MenuItem("Dreamland/튜토리얼 + Wave 1/스토리 + 수정가능 대사 v8 적용", priority = 13)]
        private static void ApplyEditableDialogueV8()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = FindInScene(scene, RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Dreamland",
                    "먼저 '프로토타입 설치'를 실행해주세요.",
                    "확인");
                return;
            }

            TutorialStage1Director director = root.GetComponent<TutorialStage1Director>();
            Stage1WaveController stage1 = root.GetComponent<Stage1WaveController>();
            if (director == null || stage1 == null)
            {
                EditorUtility.DisplayDialog(
                    "Dreamland",
                    "TutorialStage1Director 또는 Stage1WaveController를 찾지 못했습니다.",
                    "확인");
                return;
            }

            TutorialDialogueData dialogueData = GetOrCreateDialogueData();

            Undo.RecordObject(director, "Apply Dreamland Dialogue Data");
            Undo.RecordObject(stage1, "Apply Dreamland Dialogue Data");
            director.SetDialogueData(dialogueData);
            director.ApplyStoryDefaultsV8();
            stage1.SetDialogueData(dialogueData);

            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(stage1);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Selection.activeObject = dialogueData;
            EditorGUIUtility.PingObject(dialogueData);

            EditorUtility.DisplayDialog(
                "스토리 + 수정가능 대사 v8 적용 완료",
                "· 미션 시작 배너와 시작 대사가 순서대로 재생됩니다.\n" +
                "· 튜토리얼 적 등장 전 안내 대사가 추가됩니다.\n" +
                "· 튜토리얼, Wave 사이, 완료 대사를 한 파일에서 수정할 수 있습니다.\n\n" +
                "수정 위치:\n" + DialogueAssetPath,
                "확인");
        }

        [MenuItem("Dreamland/튜토리얼 + Wave 1/프로토타입 제거", priority = 14)]
        private static void RemovePrototype()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = FindInScene(scene, RootName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }

            Camera camera = FindSceneCamera(scene);
            if (camera != null)
            {
                PrototypeRayWeapon weapon = camera.GetComponent<PrototypeRayWeapon>();
                if (weapon != null)
                {
                    Undo.DestroyObjectImmediate(weapon);
                }

                PrototypeAimReticle reticle = camera.GetComponent<PrototypeAimReticle>();
                if (reticle != null)
                {
                    Undo.DestroyObjectImmediate(reticle);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Dreamland", "프로토타입 루트를 제거했습니다.", "확인");
        }

        private static PrototypeAimReticle AttachGlobalReticle(
            GameObject root,
            Camera playerCamera,
            Scene scene)
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                PrototypeAimReticle[] existingReticles =
                    sceneRoot.GetComponentsInChildren<PrototypeAimReticle>(true);

                foreach (PrototypeAimReticle existing in existingReticles)
                {
                    if (existing != null && existing.gameObject != root)
                    {
                        Undo.DestroyObjectImmediate(existing);
                    }
                }
            }

            PrototypeAimReticle reticle = root.GetComponent<PrototypeAimReticle>();
            if (reticle == null)
            {
                reticle = Undo.AddComponent<PrototypeAimReticle>(root);
            }

            reticle.Configure(playerCamera);
            return reticle;
        }

        private static List<Transform> CreateFourDirectionSpawnPoints(
            Transform parent,
            Vector3 center,
            float radius)
        {
            string[] names = { "North", "East", "South", "West" };
            Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            List<Transform> points = new List<Transform>(4);

            for (int i = 0; i < directions.Length; i++)
            {
                Transform point = EnsureChild(parent, names[i] + " Spawn");
                point.position = center + directions[i] * radius + Vector3.up * 0.05f;

                if (point.GetComponent<FloorRiftMarker>() == null)
                {
                    Undo.AddComponent<FloorRiftMarker>(point.gameObject);
                }

                Vector3 lookDirection = center - point.position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.0001f)
                {
                    point.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                }

                points.Add(point);
            }

            return points;
        }

        private static void PlaceTutorialSpawnTowardCastle(
            Transform tutorialSpawnPoint,
            Camera playerCamera,
            float distance)
        {
            if (tutorialSpawnPoint == null || playerCamera == null)
            {
                return;
            }

            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 position = playerCamera.transform.position + forward * Mathf.Max(1f, distance);
            Vector3 probeOrigin = position + Vector3.up * 10f;
            if (Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit hit,
                30f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + 0.05f;
            }
            else
            {
                position.y = playerCamera.transform.position.y - 1.55f;
            }

            tutorialSpawnPoint.position = position;
            Vector3 lookDirection = playerCamera.transform.position - position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                tutorialSpawnPoint.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        private static TutorialDialogueData GetOrCreateDialogueData()
        {
            TutorialDialogueData data =
                AssetDatabase.LoadAssetAtPath<TutorialDialogueData>(DialogueAssetPath);
            if (data != null)
            {
                return data;
            }

            EnsureAssetFolder(DialogueFolderPath);
            data = ScriptableObject.CreateInstance<TutorialDialogueData>();
            data.ResetToPrototypeDefaults();
            AssetDatabase.CreateAsset(data, DialogueAssetPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }

        private static void SetPlayerMovementSpeedToZero(Scene scene)
        {
            GameObject player = FindInScene(scene, "Player");
            if (player == null)
            {
                return;
            }

            foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "FPSController")
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(behaviour);
                SerializedProperty speedProperty = serializedObject.FindProperty("moveSpeed");
                if (speedProperty != null)
                {
                    speedProperty.floatValue = 0f;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(behaviour);
                }
            }
        }

        private static Camera FindSceneCamera(Scene scene)
        {
            GameObject cameraObject = FindInScene(scene, "Camera") ?? FindInScene(scene, "Main Camera");
            if (cameraObject != null && cameraObject.TryGetComponent(out Camera camera))
            {
                return camera;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera found = root.GetComponentInChildren<Camera>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindInScene(Scene scene, string exactName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name == exactName)
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }

        private static GameObject FindByPartialName(Scene scene, string partialName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(childObject, "Create " + childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static T GetOrAddWithUndo<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }
    }
}
#endif
