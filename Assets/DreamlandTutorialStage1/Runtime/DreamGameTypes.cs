using System;
using UnityEngine;

namespace DreamGuardians
{
    public enum PlayerRole
    {
        None = 0,
        Police = 1,
        Firefighter = 2,
        Astronomer = 3,
        Architect = 4
    }

    public enum SynergyKind
    {
        None = 0,
        EmergencySuppression = 1,
        StarlightBlueprint = 2
    }

    public enum TutorialStage1State
    {
        Idle = 0,
        Intro = 1,
        ShootingPractice = 2,
        SynergyPractice = 3,
        PurifyTutorialEnemy = 4,
        TutorialClear = 5,
        Wave1 = 6,
        Complete = 7
    }

    [Serializable]
    public struct DamageInfo
    {
        public float amount;
        public string playerId;
        public PlayerRole role;
        public int shotId;
        public Vector3 hitPoint;
        public bool allowSynergy;

        public DamageInfo(
            float amount,
            string playerId,
            PlayerRole role,
            int shotId,
            Vector3 hitPoint,
            bool allowSynergy = true)
        {
            this.amount = Mathf.Max(0f, amount);
            this.playerId = string.IsNullOrWhiteSpace(playerId) ? "LOCAL" : playerId;
            this.role = role;
            this.shotId = shotId;
            this.hitPoint = hitPoint;
            this.allowSynergy = allowSynergy;
        }
    }

    public readonly struct SynergyResult
    {
        public static readonly SynergyResult None = new SynergyResult(
            SynergyKind.None,
            0f,
            PlayerRole.None,
            PlayerRole.None);

        public SynergyKind Kind { get; }
        public float BonusDamage { get; }
        public PlayerRole FirstRole { get; }
        public PlayerRole SecondRole { get; }
        public bool Triggered => Kind != SynergyKind.None;

        public SynergyResult(
            SynergyKind kind,
            float bonusDamage,
            PlayerRole firstRole,
            PlayerRole secondRole)
        {
            Kind = kind;
            BonusDamage = Mathf.Max(0f, bonusDamage);
            FirstRole = firstRole;
            SecondRole = secondRole;
        }
    }

    public readonly struct SynergyEventData
    {
        public EnemyHealth Enemy { get; }
        public SynergyKind Kind { get; }
        public PlayerRole FirstRole { get; }
        public PlayerRole SecondRole { get; }
        public float BonusDamage { get; }

        public SynergyEventData(EnemyHealth enemy, SynergyResult result)
        {
            Enemy = enemy;
            Kind = result.Kind;
            FirstRole = result.FirstRole;
            SecondRole = result.SecondRole;
            BonusDamage = result.BonusDamage;
        }
    }

    public static class DreamGameText
    {
        public static string GetRoleName(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Police => "경찰",
                PlayerRole.Firefighter => "소방관",
                PlayerRole.Astronomer => "천문학자",
                PlayerRole.Architect => "건축가",
                _ => "미지정"
            };
        }

        public static string GetSynergyName(SynergyKind kind)
        {
            return kind switch
            {
                SynergyKind.EmergencySuppression => "긴급 진압",
                SynergyKind.StarlightBlueprint => "별빛 설계",
                _ => "시너지"
            };
        }
    }
}
