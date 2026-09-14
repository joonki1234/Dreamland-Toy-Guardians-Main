using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DreamGuardians;
using UnityEditor;
using UnityEngine;

// Small edit-mode regression checks; no scene, asset, progression or network mutations.
public static class SynergyRegressionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Dream Guardians/Validate Synergy Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run synergy regression checks outside Play Mode.");
        var go = new GameObject("SynergyRegression") { hideFlags = HideFlags.HideAndDontSave };
        var results = new List<string>();
        try
        {
            var tracker = go.AddComponent<RoleSynergyTracker>();
            var hits = (Dictionary<PlayerRole, float>)typeof(RoleSynergyTracker)
                .GetField("lastHitTimes", Private).GetValue(tracker);
            var triggers = (Dictionary<SynergyKind, float>)typeof(RoleSynergyTracker)
                .GetField("lastTriggerTimes", Private).GetValue(tracker);
            var tryTrigger = typeof(RoleSynergyTracker).GetMethod("TryTrigger", Private);
            SynergyResult Trigger(float now) => (SynergyResult)tryTrigger.Invoke(tracker,
                new object[] { SynergyKind.EmergencySuppression, PlayerRole.Police,
                    PlayerRole.Firefighter, 30f, now });

            hits[PlayerRole.Police] = 10f;
            Check(!Trigger(10f).Triggered, "One role cannot trigger", results);
            hits[PlayerRole.Firefighter] = 13f;
            Check(Trigger(13f).BonusDamage == 30f, "Three-second boundary triggers", results);
            Check(!Trigger(13f).Triggered, "Same-time duplicate suppressed", results);
            hits[PlayerRole.Police] = 14f;
            hits[PlayerRole.Firefighter] = 14f;
            Check(!Trigger(14f).Triggered, "Cooldown suppresses repeated hits", results);
            hits[PlayerRole.Police] = 15.5f;
            hits[PlayerRole.Firefighter] = 15.5f;
            Check(Trigger(15.5f).Triggered, "New hits after cooldown trigger", results);
            triggers.Clear();
            hits[PlayerRole.Police] = 10f;
            hits[PlayerRole.Firefighter] = 13.01f;
            Check(!Trigger(13.01f).Triggered, "Expired window does not trigger", results);

            var health = go.AddComponent<EnemyHealth>();
            var remember = typeof(EnemyHealth).GetMethod("RememberShot", Private);
            var duplicate = typeof(EnemyHealth).GetMethod("IsDuplicateShot", Private);
            var a = new DamageInfo(10f, "A/POLICE", PlayerRole.Police, 1, Vector3.zero);
            var b = new DamageInfo(10f, "B/POLICE", PlayerRole.Police, 1, Vector3.zero);
            remember.Invoke(health, new object[] { a });
            Check((bool)duplicate.Invoke(health, new object[] { a }) &&
                !(bool)duplicate.Invoke(health, new object[] { b }),
                "Same shot deduplicated per player", results);

            foreach (Type type in new[] { typeof(PoliceBulletProjectile),
                typeof(ChefFoodProjectile), typeof(DirtProjectile) })
            {
                var projectile = (MonoBehaviour)go.AddComponent(type);
                projectile.enabled = false;
                type.GetMethod("OnCollisionEnter", Private).Invoke(projectile, new object[] { null });
                Check(true, type.Name + " ignores disabled collision", results);
            }
            Check(!go.GetComponent<ChefFoodProjectile>().TryConsumeForSynergy(),
                "Visual food cannot activate mud", results);
            results.Add("PASS: 11 checks. Network transport / two-peer physics not covered.");
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllLines("Temp/synergy-regression-results.txt", results);
            Debug.Log(string.Join("\n", results));
        }
    }

    private static void Check(bool condition, string name, List<string> results)
    {
        if (!condition) throw new Exception(name);
        results.Add("PASS: " + name);
    }
}
