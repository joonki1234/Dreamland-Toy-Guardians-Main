using System;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class CoreState : MonoBehaviour
    {
        [Header("Core Health")]
        [SerializeField, Min(1f)] private float maxHealth = 1000f;
        [SerializeField, Min(0f)] private float currentHealth = 1000f;

        [Header("Dream Energy")]
        [SerializeField, Min(0f)] private float currentEnergy;
        [SerializeField] private Transform energyTarget;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;
        public Transform EnergyTarget => energyTarget != null ? energyTarget : transform;
        public bool IsDestroyed => currentHealth <= 0f;

        public event Action<float, float> HealthChanged;
        public event Action<float> EnergyChanged;
        public event Action CoreDestroyed;

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
            EnsureEnergyTarget();
        }

        public void Configure(float newMaxHealth, Transform newEnergyTarget = null)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            currentHealth = maxHealth;

            if (newEnergyTarget != null)
            {
                energyTarget = newEnergyTarget;
            }

            EnsureEnergyTarget();
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetEnergyTarget(Transform target)
        {
            energyTarget = target;
            EnsureEnergyTarget();
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDestroyed)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                CoreDestroyed?.Invoke();
            }
        }

        public void AddEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentEnergy += amount;
            EnergyChanged?.Invoke(currentEnergy);
        }

        public void ResetCore()
        {
            currentHealth = maxHealth;
            currentEnergy = 0f;
            HealthChanged?.Invoke(currentHealth, maxHealth);
            EnergyChanged?.Invoke(currentEnergy);
        }

        private void EnsureEnergyTarget()
        {
            if (energyTarget != null)
            {
                return;
            }

            Transform existing = transform.Find("DreamEnergyTarget");
            if (existing != null)
            {
                energyTarget = existing;
                return;
            }

            GameObject target = new GameObject("DreamEnergyTarget");
            target.transform.SetParent(transform, false);
            target.transform.localPosition = Vector3.up * 1.5f;
            energyTarget = target.transform;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
    }
}
