using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class RoleSynergyTracker : MonoBehaviour
    {
        [Header("공통 설정")]
        [SerializeField, Min(0.1f)]
        private float triggerWindow = 3f;

        [SerializeField, Min(0f)]
        private float cooldown = 2.5f;


        [Header("경찰 + 소방관")]
        [SerializeField, Min(0f)]
        private float emergencyBonusDamage = 30f;

        [SerializeField, Min(0f)]
        private float emergencyStunDuration = 1f;

        private readonly Dictionary<PlayerRole, float> lastHitTimes =
            new Dictionary<PlayerRole, float>();

        private readonly Dictionary<SynergyKind, float> lastTriggerTimes =
            new Dictionary<SynergyKind, float>();

        private EnemyHealth owner;
        private EnemyCoreMover mover;


        /// <summary>
        /// 현재 적 피격 기반 시너지는 받는 피해 배율을 변경하지 않는다.
        /// Chef + Builder 공식 시너지는 MudSplatSynergy가 처리한다.
        /// </summary>
        public float CurrentDamageMultiplier => 1f;


        private void Awake()
        {
            owner = GetComponent<EnemyHealth>();
            mover = GetComponent<EnemyCoreMover>();
        }


        /// <summary>
        /// 적이 어떤 직업의 공격에 맞았는지 기록하고
        /// 가능한 시너지가 있는지 확인한다.
        /// </summary>
        public SynergyResult RegisterHit(PlayerRole role)
        {
            if (role == PlayerRole.None ||
                !RoleSynergyProgression.IsUnlocked)
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
                    GetAdjustedBonusDamage(emergencyBonusDamage),
                    now),

                PlayerRole.Firefighter => TryTrigger(
                    SynergyKind.EmergencySuppression,
                    PlayerRole.Police,
                    PlayerRole.Firefighter,
                    GetAdjustedBonusDamage(emergencyBonusDamage),
                    now),

                _ => SynergyResult.None
            };

            if (!result.Triggered)
            {
                return result;
            }

            ApplyEffect(result.Kind);

            DreamGameEvents.RaiseSynergyTriggered(
                new SynergyEventData(owner, result)
            );

            return result;
        }


        private SynergyResult TryTrigger(
            SynergyKind kind,
            PlayerRole firstRole,
            PlayerRole secondRole,
            float bonusDamage,
            float now)
        {
            if (!lastHitTimes.TryGetValue(
                    firstRole,
                    out float firstTime) ||
                !lastHitTimes.TryGetValue(
                    secondRole,
                    out float secondTime))
            {
                return SynergyResult.None;
            }

            if (Mathf.Abs(firstTime - secondTime) > triggerWindow)
            {
                return SynergyResult.None;
            }

            if (lastTriggerTimes.TryGetValue(
                    kind,
                    out float lastTriggerTime) &&
                now - lastTriggerTime < cooldown)
            {
                return SynergyResult.None;
            }

            lastTriggerTimes[kind] = now;

            return new SynergyResult(
                kind,
                bonusDamage,
                firstRole,
                secondRole
            );
        }


        private void ApplyEffect(SynergyKind kind)
        {
            switch (kind)
            {
                case SynergyKind.EmergencySuppression:
                    mover ??= GetComponent<EnemyCoreMover>();

                    mover?.ApplyStun(
                        emergencyStunDuration * (IsBoss() ? 0.35f : 1f)
                    );
                    break;
            }
        }

        private float GetAdjustedBonusDamage(float bonusDamage)
        {
            return bonusDamage * (IsBoss() ? 0.45f : 1f);
        }

        private bool IsBoss()
        {
            return GetComponent<FinalBossAttackController>() != null;
        }


        private void OnValidate()
        {
            triggerWindow = Mathf.Max(
                0.1f,
                triggerWindow
            );

            cooldown = Mathf.Max(
                0f,
                cooldown
            );

        }
    }
}
