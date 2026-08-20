using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool damageEnabled = true;
        [SerializeField, Min(8)] private int rememberedShotCount = 128;

        // 몬스터 사망 시 재생할 효과음. 비워두면 Resources/SFX/Enemy/death를 자동으로 불러온다.
        [SerializeField] private AudioClip deathSfx;
        [SerializeField, Range(0f, 1f)] private float deathSfxVolume = 0.7f;
        private static AudioClip cachedDeathSfx;
        private const string DeathSfxResourcePath = "SFX/Enemy/death";

        private readonly HashSet<string> processedShotKeys = new HashSet<string>();
        private readonly Queue<string> processedShotOrder = new Queue<string>();
        private RoleSynergyTracker synergyTracker;
        private float currentHealth;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public bool IsDead { get; private set; }
        public bool DamageEnabled => damageEnabled;

        public event Action<EnemyHealth, float, float> HealthChanged;
        public event Action<EnemyHealth, DamageInfo> HitRegistered;
        public event Action<EnemyHealth, DamageInfo> Died;

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = maxHealth;
            synergyTracker = GetComponent<RoleSynergyTracker>();
        }

        public void Configure(float newMaxHealth, bool canTakeDamage)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            currentHealth = maxHealth;
            damageEnabled = canTakeDamage;
            IsDead = false;
            processedShotKeys.Clear();
            processedShotOrder.Clear();
            HealthChanged?.Invoke(this, currentHealth, maxHealth);
        }

        public void SetDamageEnabled(bool enabled)
        {
            damageEnabled = enabled;
        }

        public void RestoreFullHealth()
        {
            currentHealth = maxHealth;
            IsDead = false;
            HealthChanged?.Invoke(this, currentHealth, maxHealth);
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead || IsDuplicateShot(info))
            {
                return false;
            }

            bool wasDamageEnabled = damageEnabled;

            RememberShot(info);
            HitRegistered?.Invoke(this, info);
            DreamGameEvents.RaiseEnemyHit(this, info);

            SynergyResult synergyResult = SynergyResult.None;
            if (info.allowSynergy)
            {
                synergyTracker ??= GetComponent<RoleSynergyTracker>();
                if (synergyTracker != null)
                {
                    synergyResult = synergyTracker.RegisterHit(info.role);
                }
            }

            if (!wasDamageEnabled)
            {
                return true;
            }

            float multiplier = synergyTracker != null ? synergyTracker.CurrentDamageMultiplier : 1f;
            float totalDamage = Mathf.Max(0f, info.amount * multiplier + synergyResult.BonusDamage);

            if (totalDamage <= 0f)
            {
                return true;
            }

            currentHealth = Mathf.Max(0f, currentHealth - totalDamage);
            HealthChanged?.Invoke(this, currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die(info);
            }

            return true;
        }

        private void Die(DamageInfo killingBlow)
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            damageEnabled = false;
            PlayDeathSfx();
            Died?.Invoke(this, killingBlow);
            DreamGameEvents.RaiseEnemyDied(this, killingBlow);
        }

        private void PlayDeathSfx()
        {
            AudioClip clip = deathSfx;

            if (clip == null)
            {
                if (cachedDeathSfx == null)
                {
                    cachedDeathSfx = Resources.Load<AudioClip>(DeathSfxResourcePath);
                }

                clip = cachedDeathSfx;
            }

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, deathSfxVolume);
            }
        }

        private bool IsDuplicateShot(DamageInfo info)
        {
            if (info.shotId < 0)
            {
                return false;
            }

            return processedShotKeys.Contains(BuildShotKey(info));
        }

        private void RememberShot(DamageInfo info)
        {
            if (info.shotId < 0)
            {
                return;
            }

            string key = BuildShotKey(info);
            if (!processedShotKeys.Add(key))
            {
                return;
            }

            processedShotOrder.Enqueue(key);
            while (processedShotOrder.Count > Mathf.Max(8, rememberedShotCount))
            {
                processedShotKeys.Remove(processedShotOrder.Dequeue());
            }
        }

        private static string BuildShotKey(DamageInfo info)
        {
            string playerId = string.IsNullOrWhiteSpace(info.playerId) ? "LOCAL" : info.playerId;
            return playerId + ":" + info.shotId;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            rememberedShotCount = Mathf.Max(8, rememberedShotCount);
        }
    }
}
