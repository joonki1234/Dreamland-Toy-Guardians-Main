using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class RoleSynergyTracker : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField, Min(0.1f)] private float triggerWindow = 3f;
        [SerializeField, Min(0f)] private float cooldown = 5f;

        [Header("Police + Firefighter")]
        [SerializeField, Min(0f)] private float emergencyBonusDamage = 20f;
        [SerializeField, Min(0f)] private float emergencyStunDuration = 2f;

        [Header("Astronomer + Architect")]
        [SerializeField, Min(0f)] private float starlightBonusDamage = 15f;
        [SerializeField, Min(1f)] private float starlightDamageMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float starlightDuration = 4f;

        private readonly Dictionary<PlayerRole, float> lastHitTimes = new Dictionary<PlayerRole, float>();
        private readonly Dictionary<SynergyKind, float> lastTriggerTimes = new Dictionary<SynergyKind, float>();
        private EnemyHealth owner;
        private EnemyCoreMover mover;
        private float vulnerableUntil;

        public float CurrentDamageMultiplier => Time.time < vulnerableUntil
            ? Mathf.Max(1f, starlightDamageMultiplier)
            : 1f;

        private void Awake()
        {
            owner = GetComponent<EnemyHealth>();
            mover = GetComponent<EnemyCoreMover>();
        }

        public SynergyResult RegisterHit(PlayerRole role)
        {
            if (role == PlayerRole.None)
            {
                return SynergyResult.None;
            }

            float now = Time.time;
            lastHitTimes[role] = now;

            SynergyResult result = role switch
            {
                PlayerRole.Police => TryTrigger(
                    SynergyKind.EmergencySuppression,
                    PlayerRole.Police,
                    PlayerRole.Firefighter,
                    emergencyBonusDamage,
                    now),

                PlayerRole.Firefighter => TryTrigger(
                    SynergyKind.EmergencySuppression,
                    PlayerRole.Police,
                    PlayerRole.Firefighter,
                    emergencyBonusDamage,
                    now),

                PlayerRole.Astronomer => TryTrigger(
                    SynergyKind.StarlightBlueprint,
                    PlayerRole.Astronomer,
                    PlayerRole.Architect,
                    starlightBonusDamage,
                    now),

                PlayerRole.Architect => TryTrigger(
                    SynergyKind.StarlightBlueprint,
                    PlayerRole.Astronomer,
                    PlayerRole.Architect,
                    starlightBonusDamage,
                    now),

                _ => SynergyResult.None
            };

            if (!result.Triggered)
            {
                return result;
            }

            ApplyEffect(result.Kind);
            DreamGameEvents.RaiseSynergyTriggered(new SynergyEventData(owner, result));
            return result;
        }

        private SynergyResult TryTrigger(
            SynergyKind kind,
            PlayerRole firstRole,
            PlayerRole secondRole,
            float bonusDamage,
            float now)
        {
            if (!lastHitTimes.TryGetValue(firstRole, out float firstTime) ||
                !lastHitTimes.TryGetValue(secondRole, out float secondTime))
            {
                return SynergyResult.None;
            }

            if (Mathf.Abs(firstTime - secondTime) > triggerWindow)
            {
                return SynergyResult.None;
            }

            if (lastTriggerTimes.TryGetValue(kind, out float lastTriggerTime) &&
                now - lastTriggerTime < cooldown)
            {
                return SynergyResult.None;
            }

            lastTriggerTimes[kind] = now;
            return new SynergyResult(kind, bonusDamage, firstRole, secondRole);
        }

        private void ApplyEffect(SynergyKind kind)
        {
            switch (kind)
            {
                case SynergyKind.EmergencySuppression:
                    mover ??= GetComponent<EnemyCoreMover>();
                    mover?.ApplyStun(emergencyStunDuration);
                    break;

                case SynergyKind.StarlightBlueprint:
                    vulnerableUntil = Mathf.Max(vulnerableUntil, Time.time + starlightDuration);
                    break;
            }
        }

        private void OnValidate()
        {
            triggerWindow = Mathf.Max(0.1f, triggerWindow);
            cooldown = Mathf.Max(0f, cooldown);
            starlightDamageMultiplier = Mathf.Max(1f, starlightDamageMultiplier);
        }
    }
}
