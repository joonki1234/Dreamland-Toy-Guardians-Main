using System.Collections.Generic;
using DreamGuardians;
using Fusion;
using UnityEngine;

public struct SharedMudState : INetworkStruct
{
    public Vector3 Position;
    public Quaternion Rotation;
    public double Deadline;
    public int Phase; // 0 idle, 1 activated, 2 luring, 3 exploded
    public BossAttackStamp BossStamp;
}

public partial class PlayerJobController
{
    // Stored on the already baked player NetworkBehaviour, including for late joiners.
    [Networked, Capacity(64)]
    private NetworkDictionary<int, SharedMudState> SharedMud => default;
    [Networked] private int NextMudId { get; set; }
    private readonly Dictionary<int, MudSplatSynergy> mudViews = new();
    private readonly List<int> mudKeys = new();

    public void CreateNetworkMud(Vector3 position, Quaternion rotation, float lifetime, BossAttackStamp bossStamp)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority) return;
        if (SharedMud.Count >= 64)
        {
            Debug.LogWarning("Active MudSplat limit reached (64).", this);
            return;
        }
        SharedMud.Add(++NextMudId, new SharedMudState
        {
            Position = position, Rotation = rotation, BossStamp = bossStamp,
            Deadline = Runner.SimulationTime + lifetime
        });
    }

    public void RequestMudActivation(int id)
    {
        if (Object != null && Object.IsValid) RPC_ActivateMud(id);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ActivateMud(int id)
    {
        if (!RoleSynergyProgression.IsUnlocked || !SharedMud.TryGet(id, out var state) ||
            state.Phase != 0 || Runner.SimulationTime >= state.Deadline) return;
        var view = GetMudView(id, state);
        if (view == null) return;
        state.Phase = 1;
        state.Deadline = Runner.SimulationTime + view.LureStartDelay;
        SharedMud.Set(id, state);
        RPC_MudActivated(id);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_MudActivated(int id)
    {
        // Event is transient; late joiners restore the trap without replaying tutorial progress.
        DreamGameEvents.RaiseSynergyTriggered(new SynergyEventData(null,
            new SynergyResult(SynergyKind.ChefArchitectCombo, 0f,
                PlayerRole.Chef, PlayerRole.Architect)));
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        mudKeys.Clear();
        foreach (var entry in SharedMud) mudKeys.Add(entry.Key);
        foreach (int id in mudKeys)
        {
            var state = SharedMud[id];
            if (Runner.SimulationTime < state.Deadline) continue;
            var view = GetMudView(id, state);
            if (state.Phase == 0 || state.Phase == 3 || view == null)
            {
                SharedMud.Remove(id);
                continue;
            }
            if (state.Phase == 1)
            {
                view.ApplyNetworkLure();
                state.Phase = 2;
                state.Deadline = Runner.SimulationTime + view.LureDuration;
            }
            else
            {
                view.ApplyNetworkExplosion();
                state.Phase = 3;
                state.Deadline = Runner.SimulationTime + 1.0;
            }
            SharedMud.Set(id, state);
        }
    }

    public override void Render()
    {
        foreach (var entry in SharedMud)
            GetMudView(entry.Key, entry.Value)?.PresentNetworkPhase(entry.Value.Phase);
        mudKeys.Clear();
        foreach (var entry in mudViews)
            if (!SharedMud.ContainsKey(entry.Key)) mudKeys.Add(entry.Key);
        foreach (int id in mudKeys)
        {
            if (mudViews[id] != null) Destroy(mudViews[id].gameObject);
            mudViews.Remove(id);
        }
    }

    private MudSplatSynergy GetMudView(int id, SharedMudState state)
    {
        if (mudViews.TryGetValue(id, out var view) && view != null) return view;
        var prefab = dirtPrefab != null ? dirtPrefab.GetComponent<DirtProjectile>()?.MudSplatPrefab : null;
        if (prefab == null) return null;
        var instance = Instantiate(prefab, state.Position, state.Rotation);
        view = instance.GetComponent<MudSplatSynergy>();
        if (view == null) { Destroy(instance); return null; }
        view.BindNetworkMud(this, id, state.BossStamp);
        mudViews[id] = view;
        return view;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        foreach (var view in mudViews.Values)
            if (view != null) Destroy(view.gameObject);
        mudViews.Clear();
    }
}
