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
        private float cooldown = 5f;


        [Header("경찰 + 소방관")]
        [SerializeField, Min(0f)]
        private float emergencyBonusDamage = 20f;

        [SerializeField, Min(0f)]
        private float emergencyStunDuration = 2f;


        [Header("요리사 + 건축가")]
        [SerializeField, Min(0f)]
        private float chefBuilderBonusDamage = 15f;

        [SerializeField, Min(1f)]
        private float chefBuilderDamageMultiplier = 1.25f;

        [SerializeField, Min(0f)]
        private float chefBuilderDuration = 4f;


        private readonly Dictionary<PlayerRole, float> lastHitTimes =
            new Dictionary<PlayerRole, float>();

        private readonly Dictionary<SynergyKind, float> lastTriggerTimes =
            new Dictionary<SynergyKind, float>();


        private EnemyHealth owner;
        private EnemyCoreMover mover;

        private float vulnerableUntil;


        /// <summary>
        /// 요리사 + 건축가 시너지가 발동한 동안
        /// 해당 적이 추가로 받는 피해 배율이다.
        /// </summary>
        public float CurrentDamageMultiplier =>
            Time.time < vulnerableUntil
                ? Mathf.Max(1f, chefBuilderDamageMultiplier)
                : 1f;


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

                PlayerRole.Chef => TryTrigger(
                    SynergyKind.StarlightBlueprint,
                    PlayerRole.Chef,
                    PlayerRole.Architect,
                    chefBuilderBonusDamage,
                    now),

                PlayerRole.Architect => TryTrigger(
                    SynergyKind.StarlightBlueprint,
                    PlayerRole.Chef,
                    PlayerRole.Architect,
                    chefBuilderBonusDamage,
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
                        emergencyStunDuration
                    );
                    break;

                case SynergyKind.StarlightBlueprint:
                    vulnerableUntil = Mathf.Max(
                        vulnerableUntil,
                        Time.time + chefBuilderDuration
                    );
                    break;
            }
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

            chefBuilderDamageMultiplier = Mathf.Max(
                1f,
                chefBuilderDamageMultiplier
            );

            chefBuilderDuration = Mathf.Max(
                0f,
                chefBuilderDuration
            );
        }
    }
}