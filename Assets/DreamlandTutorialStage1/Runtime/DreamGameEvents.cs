using System;
using UnityEngine;

namespace DreamGuardians
{
    public static class DreamGameEvents
    {
        public static event Action<EnemyHealth, DamageInfo> EnemyHit;
        public static event Action<EnemyHealth, DamageInfo> EnemyDied;
        public static event Action<EnemyPurification, float> EnergyAbsorbed;
        public static event Action<SynergyEventData> SynergyTriggered;
        public static event Action<int> EnvironmentPhaseRequested;
        public static event Action WeaponUpgradeRequested;

        internal static void RaiseEnemyHit(EnemyHealth enemy, DamageInfo info)
        {
            EnemyHit?.Invoke(enemy, info);
        }

        internal static void RaiseEnemyDied(EnemyHealth enemy, DamageInfo info)
        {
            EnemyDied?.Invoke(enemy, info);
        }

        internal static void RaiseEnergyAbsorbed(EnemyPurification purification, float amount)
        {
            EnergyAbsorbed?.Invoke(purification, amount);
        }

        internal static void RaiseSynergyTriggered(SynergyEventData data)
        {
            SynergyTriggered?.Invoke(data);
        }

        public static void RequestEnvironmentPhase(int phaseIndex)
        {
            EnvironmentPhaseRequested?.Invoke(phaseIndex);
        }

        public static void RequestWeaponUpgrade()
        {
            WeaponUpgradeRequested?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            EnemyHit = null;
            EnemyDied = null;
            EnergyAbsorbed = null;
            SynergyTriggered = null;
            EnvironmentPhaseRequested = null;
            WeaponUpgradeRequested = null;
        }
    }
}
