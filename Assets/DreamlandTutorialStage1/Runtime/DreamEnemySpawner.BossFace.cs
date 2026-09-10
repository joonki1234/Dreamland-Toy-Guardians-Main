using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    // Presentation snapshot only: does not carry damage, HP, spawning or game-flow commands.
    public struct BossFaceSnapshot : INetworkStruct
    {
        public FinalBossFaceController.Expression Expression;
        public NetworkBool Visible;
        public int Revision;
        public double StartedAt;
    }

    public sealed partial class DreamEnemySpawner
    {
        [Networked] public BossFaceSnapshot BossFaceState { get; private set; }
        private bool bossFaceNetworkSpawned;
        private FinalBossFaceController localBossFace;
        private int lastBossFaceEvent = -1;
        private bool wasBossFaceAuthority;

        public bool IsBossFaceNetworkReady => bossFaceNetworkSpawned && Object != null &&
            Object.IsValid && Runner != null && Runner.IsRunning && Runner.GameMode == GameMode.Shared;
        public bool IsBossFaceAuthority => IsBossFaceNetworkReady &&
            Object.HasStateAuthority && Runner.IsSharedModeMasterClient;

        public override void Spawned() { bossFaceNetworkSpawned = true; }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            bossFaceNetworkSpawned = false;
            wasBossFaceAuthority = false;
        }

        public void BindBossFace(FinalBossFaceController face)
        {
            if (localBossFace != null && localBossFace != face) localBossFace.BindNetworkPresentation(null);
            localBossFace = face;
            lastBossFaceEvent = -1;
            if (face != null) face.BindNetworkPresentation(this);
        }

        public void UnbindBossFace(FinalBossFaceController face)
        {
            if (localBossFace != face) return;
            if (face != null) face.BindNetworkPresentation(null);
            localBossFace = null;
        }

        public bool TryReadBossFace(out BossFaceSnapshot state, out float age)
        {
            state = default;
            age = 0f;
            if (!IsBossFaceNetworkReady || IsBossFaceAuthority) return false;
            state = BossFaceState;
            age = Mathf.Max(0f, (float)(Runner.SimulationTime - state.StartedAt));
            return true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsBossFaceAuthority) { wasBossFaceAuthority = false; return; }
            // On master migration, publish the new authority's live presentation rather
            // than accepting writes from the old master. Combat migration is out of scope.
            if (!wasBossFaceAuthority) { lastBossFaceEvent = -1; wasBossFaceAuthority = true; }
            var expression = FinalBossFaceController.Expression.Idle;
            bool visible = false;
            int eventRevision = 0;
            if (localBossFace != null)
                localBossFace.ReadAuthoritativePresentation(out expression, out visible, out eventRevision);
            var previous = BossFaceState;
            if (previous.Expression == expression && (bool)previous.Visible == visible &&
                eventRevision == lastBossFaceEvent) return;
            BossFaceState = new BossFaceSnapshot
            {
                Expression = expression,
                Visible = visible,
                Revision = previous.Revision + 1,
                StartedAt = Runner.SimulationTime
            };
            lastBossFaceEvent = eventRevision;
        }

        public void RequestBossFaceHit()
        {
            if (IsBossFaceNetworkReady && !IsBossFaceAuthority) RPC_RequestBossFaceHit();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestBossFaceHit()
        {
            if (!IsBossFaceAuthority || localBossFace == null || !BossFaceState.Visible) return;
            localBossFace.SetExpression(FinalBossFaceController.Expression.Hit);
        }
    }
}
