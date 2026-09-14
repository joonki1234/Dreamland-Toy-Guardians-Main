#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DreamGuardians;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

// Edit-mode checks only. This does not start a room or claim a multiplayer test.
[InitializeOnLoad]
public static class SynergyNetworkValidation
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/SynergyNetworkQA/"));
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();

    static SynergyNetworkValidation()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Root + "request")) return;
            File.Delete(Root + "request");
            Validate();
        };
    }

    [MenuItem("Tools/Dream Guardians/Validate Multiplayer Synergy")]
    public static void Validate()
    {
        Directory.CreateDirectory(Root);
        results.Clear();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.WriteAllText(Root + "result.txt", "BLOCKED: exit Play Mode before edit-mode validation.");
            return;
        }
        bool unlocked = RoleSynergyProgression.IsUnlocked;
        GameObject first = null, second = null, attacks = null, trap = null;
        try
        {
            BakeMudSplat();
            RoleSynergyProgression.Unlock();
            first = MakeEnemy("SynergyQA_X");
            second = MakeEnemy("SynergyQA_Y");
            var tracker = first.GetComponent<RoleSynergyTracker>();
            var other = second.GetComponent<RoleSynergyTracker>();
            var hitTimes = (Dictionary<PlayerRole, float>)typeof(RoleSynergyTracker)
                .GetField("lastHitTimes", Private).GetValue(tracker);
            var cooldowns = (Dictionary<SynergyKind, float>)typeof(RoleSynergyTracker)
                .GetField("lastTriggerTimes", Private).GetValue(tracker);

            Check(!tracker.RegisterHit(PlayerRole.Police, "P1").Triggered, "first Police hit waits");
            Check(!other.RegisterHit(PlayerRole.Firefighter, "P2").Triggered, "different enemies do not combine");
            Check(tracker.RegisterHit(PlayerRole.Firefighter, "P2").Triggered, "Police -> Firefighter triggers");
            Check(!tracker.RegisterHit(PlayerRole.Police, "P1").Triggered, "cooldown suppresses repeat");
            hitTimes.Clear(); cooldowns.Clear();
            Check(!tracker.RegisterHit(PlayerRole.Firefighter, "P2").Triggered, "first Firefighter hit waits");
            Check(tracker.RegisterHit(PlayerRole.Police, "P1").Triggered, "Firefighter -> Police triggers");
            hitTimes.Clear(); cooldowns.Clear();
            hitTimes[PlayerRole.Police] = Time.time - 4f;
            Check(!tracker.RegisterHit(PlayerRole.Firefighter, "P2").Triggered, "expired window does not trigger");

            var health = first.GetComponent<EnemyHealth>();
            var keyMethod = typeof(EnemyHealth).GetMethod("BuildShotKey", Private);
            var info = new DamageInfo(10f, "POLICE_BULLET_PROJECTILE", PlayerRole.Police, 10, Vector3.zero);
            var p1 = PlayerRef.FromIndex(0);
            var p2 = PlayerRef.FromIndex(1);
            string key1 = (string)keyMethod.Invoke(health, new object[] { info, p1 });
            string key2 = (string)keyMethod.Invoke(health, new object[] { info, p2 });
            Check(key1 != key2, "same source/shot ID from distinct PlayerRefs has distinct keys");
            typeof(EnemyHealth).GetMethod("RememberShot", Private).Invoke(health, new object[] { info, p1 });
            var duplicate = typeof(EnemyHealth).GetMethod("IsDuplicateShot", Private);
            Check((bool)duplicate.Invoke(health, new object[] { info, p1 }), "same player shot is deduplicated");
            Check(!(bool)duplicate.Invoke(health, new object[] { info, p2 }), "other player shot remains eligible");

            hitTimes.Clear(); cooldowns.Clear(); health.Configure(1000f, true);
            health.TakeDamage(new DamageInfo(10, "POLICE", PlayerRole.Police, 1, Vector3.zero));
            health.TakeDamage(new DamageInfo(10, "WATER", PlayerRole.Firefighter, 1, Vector3.zero));
            Check(Mathf.Approximately(health.CurrentHealth, 950), "base damage + one 30-point synergy bonus");
            health.TakeDamage(new DamageInfo(10, "WATER", PlayerRole.Firefighter, 1, Vector3.zero));
            Check(Mathf.Approximately(health.CurrentHealth, 950), "duplicate damage does not repeat bonus or base damage");

            var presentation = typeof(RoleSynergyTracker).GetMethod("Present", Private | BindingFlags.Public);
            presentation.Invoke(tracker, new object[] {
                new SynergyResult(SynergyKind.EmergencySuppression, 30, PlayerRole.Police, PlayerRole.Firefighter) });
            Check(Mathf.Approximately(health.CurrentHealth, 950), "presentation does not apply damage");

            // Run the real callback entry points with disabled presentation components.
            var collider = first.AddComponent<BoxCollider>();
            attacks = new GameObject("SynergyQA_Remote") { hideFlags = HideFlags.HideAndDontSave };
            var bullet = attacks.AddComponent<PoliceBulletProjectile>(); bullet.enabled = false;
            typeof(PoliceBulletProjectile).GetMethod("OnTriggerEnter", Private).Invoke(bullet, new object[] { collider });
            var food = attacks.AddComponent<ChefFoodProjectile>(); food.enabled = false;
            typeof(ChefFoodProjectile).GetMethod("OnCollisionEnter", Private).Invoke(food, new object[] { null });
            Check(!food.CanActivateMudSplat && !food.TryConsumeForMudSplat(), "remote food cannot activate a trap");
            var dirt = attacks.AddComponent<DirtProjectile>(); dirt.enabled = false;
            typeof(DirtProjectile).GetMethod("OnCollisionEnter", Private).Invoke(dirt, new object[] { null });
            var water = attacks.AddComponent<WaterParticleHit>(); water.enabled = false;
            typeof(WaterParticleHit).GetMethod("OnParticleCollision", Private).Invoke(water, new object[] { first });
            Check(Mathf.Approximately(health.CurrentHealth, 950), "remote bullet/food/dirt/water callbacks cause no damage");

            trap = new GameObject("SynergyQA_MudSplat") { hideFlags = HideFlags.HideAndDontSave };
            var mud = trap.AddComponent<MudSplatSynergy>();
            typeof(MudSplatSynergy).GetField("enemyLayer", Private).SetValue(mud, (LayerMask)~0);
            first.transform.position = new Vector3(10000, 10000, 10000);
            trap.transform.position = first.transform.position;
            first.AddComponent<SphereCollider>();
            health.Configure(1000, true);
            Physics.SyncTransforms();
            typeof(MudSplatSynergy).GetMethod("Explode", Private).Invoke(mud, null);
            Check(Mathf.Approximately(health.CurrentHealth, 970), "MudSplat damages an enemy with two colliders once");

            Check(typeof(MudSplatSynergy).IsSubclassOf(typeof(NetworkBehaviour)), "MudSplat uses Fusion NetworkBehaviour");
            Check(typeof(MudSplatSynergy).GetProperty("Phase").GetCustomAttributes(true)
                .Any(a => a.GetType().Name.Contains("Networked")), "MudSplat activation phase is networked");
            results.Add("PASS: Unity loaded compiled runtime/editor assemblies after Fusion IL post-processing.");
            results.Add("NOT RUN: two-client Shared Mode, packet transport, VFX/SFX, VR input.");
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (first != null) UnityEngine.Object.DestroyImmediate(first);
            if (second != null) UnityEngine.Object.DestroyImmediate(second);
            if (attacks != null) UnityEngine.Object.DestroyImmediate(attacks);
            if (trap != null) UnityEngine.Object.DestroyImmediate(trap);
            if (!unlocked) RoleSynergyProgression.Lock();
            File.WriteAllLines(Root + "result.txt", results);
        }
    }

    private static GameObject MakeEnemy(string name)
    {
        var target = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        var health = target.AddComponent<EnemyHealth>();
        var tracker = target.AddComponent<RoleSynergyTracker>();
        typeof(EnemyHealth).GetMethod("Awake", Private).Invoke(health, null);
        typeof(RoleSynergyTracker).GetMethod("Awake", Private).Invoke(tracker, null);
        health.Configure(1000, true);
        return target;
    }

    private static void BakeMudSplat()
    {
        const string path = "Assets/MudSplat.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var networkObject = root.GetComponent<NetworkObject>();
            if (networkObject == null) networkObject = root.AddComponent<NetworkObject>();
            networkObject.Flags = NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
            new NetworkObjectBakerEditTime().Bake(root);
            Check(networkObject.NetworkedBehaviours.Contains(root.GetComponent<MudSplatSynergy>()),
                "MudSplat NetworkObject bake includes MudSplatSynergy");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Check(AssetDatabase.GetLabels(asset).Contains("FusionPrefab"), "MudSplat registered as FusionPrefab");
        Check(NetworkProjectConfigUtilities.TryGetPrefabId(path, out var prefabId) && prefabId.IsValid,
            "MudSplat resolves in the Fusion prefab table");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
}
#endif
