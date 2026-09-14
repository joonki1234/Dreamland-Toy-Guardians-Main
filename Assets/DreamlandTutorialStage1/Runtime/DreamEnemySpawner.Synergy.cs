using Fusion;

namespace DreamGuardians
{
    public sealed partial class DreamEnemySpawner : IAfterSpawned
    {
        [Networked, OnChangedRender(nameof(HandleSynergyUnlockChanged))]
        public NetworkBool NetworkedSynergyUnlocked { get; private set; }

        // Separate callback keeps the existing boss lifecycle untouched.
        public void AfterSpawned()
        {
            bool pendingUnlock = RoleSynergyProgression.IsUnlocked;
            if (Object.HasStateAuthority) NetworkedSynergyUnlocked = pendingUnlock;
            RoleSynergyProgression.Bind(this);
            if (pendingUnlock && !Object.HasStateAuthority) SetSynergyUnlocked(true);
        }

        internal void SetSynergyUnlocked(bool unlocked)
        {
            if (!IsTutorialSessionReady) return;
            if (Object.HasStateAuthority)
            {
                NetworkedSynergyUnlocked = unlocked;
                HandleSynergyUnlockChanged();
            }
            else if (unlocked)
            {
                // Existing Skip/Stage2 direct-test calls may originate on any peer.
                // Only the authority may reset a stage; a late peer's Lock cannot undo it.
                RPC_RequestSynergyUnlock();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestSynergyUnlock()
        {
            SetSynergyUnlocked(true);
        }

        private void HandleSynergyUnlockChanged()
        {
            RoleSynergyProgression.ApplySnapshot(NetworkedSynergyUnlocked);
            SynergyNetLog.Write($"Unlock={NetworkedSynergyUnlocked} Authority={Object.StateAuthority}", this);
        }
    }
}
