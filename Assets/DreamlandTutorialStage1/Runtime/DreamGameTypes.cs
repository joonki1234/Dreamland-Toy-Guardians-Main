using System;
using UnityEngine;

namespace DreamGuardians
{
    public enum PlayerRole
    {
        None = 0,
        Police = 1,
        Firefighter = 2,
        // 기존 Astronomer가 사용하던 값 3을 그대로 유지해
        // 씬과 프리팹에 저장된 직업 값이 깨지지 않게 합니다.
        Chef = 3,
        Architect = 4
    }

    public enum SynergyKind
    {
        None = 0,
        EmergencySuppression = 1,
        ChefArchitectCombo = 2
    }

    public enum TutorialStage1State
    {
        Idle = 0,
        Intro = 1,
        RoleSelection = 2,
        ShootingPractice = 3,
        SynergyPractice = 4,
        PurifyTutorialEnemy = 5,
        TutorialClear = 6,
        Wave1 = 7,
        Complete = 8
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
                PlayerRole.Chef => "요리사",
                PlayerRole.Architect => "건축가",
                _ => "미지정"
            };
        }

        public static string GetSynergyName(SynergyKind kind)
        {
            return kind switch
            {
                SynergyKind.EmergencySuppression => "긴급 진압",
                SynergyKind.ChefArchitectCombo => "협동 제작",
                _ => "시너지"
            };
        }
    }

    /// <summary>
    /// Stage 1 Wave 2 전후의 시너지 잠금 상태를 공유합니다.
    /// 적이 런타임에 계속 생성되기 때문에 개별 Enemy가 아니라
    /// 한 곳에서 잠금 상태를 관리합니다.
    /// </summary>
    public static class RoleSynergyProgression
    {
        public static bool IsUnlocked { get; private set; }

        public static event Action Unlocked;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsUnlocked = false;
            Unlocked = null;
        }

        public static void Lock()
        {
            IsUnlocked = false;
        }

        public static bool Unlock()
        {
            if (IsUnlocked)
            {
                return false;
            }

            IsUnlocked = true;
            Unlocked?.Invoke();
            return true;
        }
    }
}
