using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    public enum BossPattern { None, AdvanceOne, AdvanceTwo, Slam, Spin, DarkBolt, Headbutt }

    // The scene NetworkObject identity prevents a projectile from a previous scene
    // matching a new spawner whose round counter happens to have the same value.
    public struct BossAttackStamp : INetworkStruct
    {
        public NetworkId Spawner;
        public int Round;
        public static BossAttackStamp Capture()
        {
            var source = RoleSynergyProgression.NetworkSource;
            return source != null && source.IsBossCombatReady
                ? new BossAttackStamp { Spawner = source.Object.Id, Round = source.BossCombat.Round }
                : default;
        }
    }

    public struct BossCombatSnapshot : INetworkStruct
    {
        public int Round;
        public float MaxHP;
        public float HP;
        public NetworkBool Active;
        public NetworkBool DamageEnabled;
        public NetworkBool Dead;
        public NetworkBool EndingIssued;
        public int Phase;
        public double StunnedUntil;
        public int PatternRevision;
        public BossPattern Pattern;
        public double PatternStartedAt;
        public Vector3 PatternPosition;
        public Quaternion PatternRotation;
        public int CoreDamageRevision;
        public float PendingCoreDamage;
    }

    public sealed partial class DreamEnemySpawner
    {
        [Networked] public BossCombatSnapshot BossCombat { get; private set; }
        private EnemyHealth combatBoss;
        private FinalBossAttackController combatAttack;
        private FinalBossDirector combatDirector;
        private int boundBossRound;
        private readonly Dictionary<PlayerRole, PlayerRef> bossRoleAttackers = new();
        public bool IsBossCombatReady => IsBossFaceNetworkReady;
        public bool IsBossCombatAuthority => IsBossFaceAuthority;
        public double BossNetworkTime => Runner.SimulationTime;
        public bool IsBossStunned => IsBossCombatReady && BossNetworkTime < BossCombat.StunnedUntil;

        public void BeginBossCombat(float maxHP)
        {
            if (!IsBossCombatAuthority) return;
            var old = BossCombat;
            BossCombat = new BossCombatSnapshot
            {
                Round = old.Round + 1, MaxHP = maxHP, HP = maxHP, Active = true
            };
            bossRoleAttackers.Clear();
            BossLog($"Round={BossCombat.Round} Begin HP={maxHP} Authority={Runner.LocalPlayer}");
        }

        public void BindBossCombat(EnemyHealth health, FinalBossAttackController attack, FinalBossDirector director)
        {
            combatDirector = director;
            combatBoss = health;
            combatAttack = attack;
            boundBossRound = BossCombat.Round;
            health.BindBossCombat(this, boundBossRound);
            attack.BindBossCombat(this, boundBossRound);
            ApplyBossCombatPresentation();
        }

        public void UnbindBossCombat(EnemyHealth health)
        {
            if (combatBoss != health) return;
            if (combatBoss != null) combatBoss.UnbindBossCombat();
            combatBoss = null;
            combatAttack = null;
            combatDirector = null;
        }

        public void PublishBossSummon(Vector3 position)
        {
            if (IsBossCombatAuthority) RPC_BossSummon(BossCombat.Round, position);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BossSummon(int round, Vector3 position)
        {
            if (round == BossCombat.Round && round == boundBossRound && combatDirector != null)
                combatDirector.PresentBossSummon(position);
        }

        public void EnableBossCombatDamage(int round)
        {
            if (!IsBossCombatAuthority || BossCombat.Round != round || !BossCombat.Active || BossCombat.Dead) return;
            var state = BossCombat;
            state.DamageEnabled = true;
            BossCombat = state;
            ApplyBossCombatPresentation();
        }

        public void StopBossCombat()
        {
            if (!IsBossCombatAuthority) return;
            var state = BossCombat;
            state.Active = false;
            state.DamageEnabled = false;
            state.PendingCoreDamage = 0;
            BossCombat = state;
        }

        public bool RequestBossDamage(DamageInfo info, BossAttackStamp stamp)
        {
            if (!IsBossCombatReady || stamp.Spawner != Object.Id || stamp.Round != BossCombat.Round ||
                !BossCombat.Active || !BossCombat.DamageEnabled || BossCombat.Dead) return false;
            if (IsBossCombatAuthority) AcceptBossDamage(stamp.Round, Runner.LocalPlayer, info);
            else RPC_RequestBossDamage(stamp.Round, info.amount, info.playerId, (int)info.role,
                info.shotId, info.hitPoint, info.allowSynergy);
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestBossDamage(int round, float amount, string attackType, int role,
            int shotId, Vector3 hitPoint, bool allowSynergy, RpcInfo rpc = default)
        {
            AcceptBossDamage(round, rpc.Source,
                new DamageInfo(amount, attackType, (PlayerRole)role, shotId, hitPoint, allowSynergy));
        }

        private void AcceptBossDamage(int round, PlayerRef attacker, DamageInfo info)
        {
            if (!IsBossCombatAuthority || round != BossCombat.Round || boundBossRound != round ||
                !BossCombat.Active || !BossCombat.DamageEnabled || BossCombat.Dead || combatBoss == null ||
                float.IsNaN(info.amount) || float.IsInfinity(info.amount)) return;
            info.playerId = attacker + "/" + info.playerId;
            BossLog($"Round={round} DamageRequest Attacker={attacker} Role={info.role} ShotId={info.shotId} BaseDamage={info.amount}");
            // The existing EnemyHealth shot cache and Tracker are used on this peer only.
            if (combatBoss.IsConfirmedBossDuplicate(info)) return;
            if (info.allowSynergy) bossRoleAttackers[info.role] = attacker;
            combatBoss.ApplyBossDamageAuthoritative(info);
        }

        internal void CommitBossHP(float hp)
        {
            if (!IsBossCombatAuthority || !BossCombat.Active || BossCombat.Dead) return;
            var state = BossCombat;
            float previous = state.HP;
            state.HP = Mathf.Max(0, hp);
            state.Dead = state.HP <= 0;
            state.Phase = state.HP <= state.MaxHP / 3f ? 2 : state.HP <= state.MaxHP * (2f / 3f) ? 1 : 0;
            if (state.Dead) { state.DamageEnabled = false; state.PendingCoreDamage = 0; }
            BossCombat = state;
            BossLog($"Round={state.Round} HP {previous} -> {hp} Authority={Runner.LocalPlayer}");
            if (state.Dead) BossLog($"BossDeath confirmed Round={state.Round}");
            ApplyBossCombatPresentation();
        }

        internal void PublishBossSynergy(SynergyResult result)
        {
            if (!IsBossCombatAuthority) return;
            bossRoleAttackers.TryGetValue(PlayerRole.Police, out var police);
            bossRoleAttackers.TryGetValue(PlayerRole.Firefighter, out var firefighter);
            BossLog($"Round={BossCombat.Round} Synergy={result.Kind} Police={police} Firefighter={firefighter} BonusDamage={result.BonusDamage}");
            RPC_BossSynergy(BossCombat.Round, (int)result.Kind, result.BonusDamage,
                (int)result.FirstRole, (int)result.SecondRole);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BossSynergy(int round, int kind, float bonus, int first, int second)
        {
            if (round != BossCombat.Round || round != boundBossRound || combatBoss == null) return;
            combatBoss.PresentConfirmedBossSynergy(new SynergyResult((SynergyKind)kind, bonus,
                (PlayerRole)first, (PlayerRole)second));
        }

        internal void StunBoss(float duration)
        {
            if (!IsBossCombatAuthority) return;
            var state = BossCombat;
            state.StunnedUntil = System.Math.Max(state.StunnedUntil, BossNetworkTime + duration);
            BossCombat = state;
        }

        public void StartBossPattern(BossPattern pattern, Vector3 position, Quaternion rotation)
        {
            if (!IsBossCombatAuthority || !BossCombat.Active || !BossCombat.DamageEnabled || BossCombat.Dead || IsBossStunned) return;
            var state = BossCombat;
            state.Pattern = pattern;
            state.PatternRevision++;
            state.PatternStartedAt = BossNetworkTime;
            state.PatternPosition = position;
            state.PatternRotation = rotation;
            BossCombat = state;
            BossLog($"Round={state.Round} PatternRevision={state.PatternRevision} Pattern={pattern} StartTime={state.PatternStartedAt}");
            combatAttack?.ApplyBossPattern(state);
        }

        public void RequestBossCoreDamage(int round, int patternRevision, float amount)
        {
            if (!IsBossCombatAuthority || round != BossCombat.Round || BossCombat.Dead || !BossCombat.Active ||
                patternRevision != BossCombat.PatternRevision || patternRevision <= BossCombat.CoreDamageRevision) return;
            var state = BossCombat;
            state.CoreDamageRevision = patternRevision;
            state.PendingCoreDamage += amount;
            BossCombat = state;
            FlushBossCoreDamage();
        }

        private void FlushBossCoreDamage()
        {
            if (!IsBossCombatAuthority || !BossCombat.Active || BossCombat.Dead || IsBossStunned || BossCombat.PendingCoreDamage <= 0) return;
            var state = BossCombat;
            float amount = state.PendingCoreDamage;
            state.PendingCoreDamage = 0;
            BossCombat = state; // commit before callbacks can re-enter
            targetCore?.TakeDamage(amount);
            BossLog($"Round={state.Round} CoreDamage={amount} PatternRevision={state.CoreDamageRevision}");
        }

        public bool TryClaimBossEnding(int round)
        {
            if (!IsBossCombatAuthority || round != BossCombat.Round || !BossCombat.Active || !BossCombat.Dead || BossCombat.EndingIssued) return false;
            var state = BossCombat;
            state.EndingIssued = true;
            BossCombat = state;
            return true;
        }

        private void TickBossCombat()
        {
            FlushBossCoreDamage();
            ApplyBossCombatPresentation();
        }

        public override void Render() => ApplyBossCombatPresentation();

        private void ApplyBossCombatPresentation()
        {
            if (!IsBossCombatReady || combatBoss == null || boundBossRound != BossCombat.Round) return;
            var state = BossCombat;
            combatBoss.ApplyConfirmedBossState(state);
            if (combatAttack != null && state.Active && !state.Dead)
                combatAttack.ApplyBossPattern(state);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void BossLog(string message) => Debug.Log("[BossNet] " + message);
    }
}
