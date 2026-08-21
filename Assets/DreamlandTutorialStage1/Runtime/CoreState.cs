using System;
using TMPro;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class CoreState : MonoBehaviour
    {
        [Header("Core Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float currentHealth = 100f;

        [Header("Core Health HUD Theme (스타트/로비씬과 통일)")]
        [Tooltip("CoreHealthHUD의 체력 숫자에 쓸 폰트입니다. Assets/Fonts/HSJiptokki-Black SDF를 지정하세요.")]
        [SerializeField] private TMP_FontAsset hudDisplayFont;
        [Tooltip("CoreHealthHUD의 \"코어 상태\" 라벨에 쓸 폰트입니다. Assets/Fonts/HS두꺼비체 SDF를 지정하세요.")]
        [SerializeField] private TMP_FontAsset hudBodyFont;
        [Tooltip(
            "로봇 대화창/로비 JobSelectPanel과 같은 반투명 네온 유리 패널 원본입니다. " +
            "Sci-Fi UI 아틀라스의 \"window\" 서브스프라이트(guid 56d84991286850f428b4e7df0cca7380, " +
            "fileID 21300000)를 직접 지정하세요.")]
        [SerializeField] private Sprite hudGlassPanel;

        [Header("Dream Energy")]
        [SerializeField, Min(0f)] private float currentEnergy;
        [SerializeField] private Transform energyTarget;

        [Header("Enemy Attack Target")]
        [Tooltip("코어 이펙트 루트 기준 실제 보이는 중심에 맞춘 공격 목표 오프셋입니다.")]
        [SerializeField] private Vector3 attackTargetLocalOffset = Vector3.zero;
        [SerializeField] private bool autoAlignEnergyTarget = true;
        [SerializeField] private bool lockAttackTargetToCoreRoot = true;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;
        public Transform EnergyTarget => energyTarget != null ? energyTarget : transform;
        public Vector3 AttackTargetPosition =>
            energyTarget != null ? energyTarget.position : transform.position;
        public bool IsDestroyed => currentHealth <= 0f;

        public event Action<float, float> HealthChanged;
        public event Action<float> EnergyChanged;
        public event Action CoreDestroyed;

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
            EnsureEnergyTarget();
            AlignEnergyTargetToVisibleCore();
            EnsureHealthHud();
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
            AlignEnergyTargetToVisibleCore();
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetEnergyTarget(Transform target)
        {
            energyTarget = target;
            EnsureEnergyTarget();
            AlignEnergyTargetToVisibleCore();
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

        private void EnsureHealthHud()
        {
            CoreHealthHUD hud = GetComponent<CoreHealthHUD>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<CoreHealthHUD>();
            }

            // CoreHealthHUD는 AddComponent로 런타임에 붙기 때문에 씬 파일에서
            // 직접 필드를 연결할 방법이 없다. 대신 씬에 이미 배치돼 있는 CoreState
            // 쪽에서 테마 에셋(폰트/유리 패널)을 받아 그대로 전달한다.
            hud.Configure(hudDisplayFont, hudBodyFont, hudGlassPanel);
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
            target.transform.localPosition = attackTargetLocalOffset;
            energyTarget = target.transform;
        }

        private void AlignEnergyTargetToVisibleCore()
        {
            if (!autoAlignEnergyTarget || energyTarget == null)
            {
                return;
            }

            // 현재 씬의 DreamEnergyTarget은 Y=1.5라 실제 코어보다 위를 가리켰습니다.
            // 코어 루트의 보이는 중심 근처로 내려 총알/레이저/근접 방향을 일치시킵니다.
            if (energyTarget.parent == transform)
            {
                energyTarget.localPosition =
                    lockAttackTargetToCoreRoot
                        ? Vector3.zero
                        : attackTargetLocalOffset;
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
    }
}
