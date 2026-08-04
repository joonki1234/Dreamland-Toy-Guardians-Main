using System.Collections;
using UnityEngine;

public enum ElementalType { None, Mud, Fire, Water, Electric }

[DisallowMultipleComponent]
public sealed class StatusReceiver : MonoBehaviour
{
    [Header("상태 이상 여부 (디버그용)")]
    [SerializeField] private bool isMuddy = false;
    [SerializeField] private bool isWet = false;
    [SerializeField] private bool isOnFire = false;
    [SerializeField] private bool isShocked = false;

    [Header("고정된 넉백 연출 세팅")]
    [SerializeField] private float shockDuration = 0.5f;     // 감전 넉백 연출 시간 (0.5초)
    [SerializeField] private float knockbackDistance = 0.07f;// 뒤로 슬라이딩하는 거리 (0.07m)

    [Header("시각 이펙트 프리팹 (VFX)")]
    [SerializeField] private GameObject waterEffectPrefab;
    [SerializeField] private GameObject electricSynergyVFX;
    [SerializeField] private GameObject fireSynergyVFX;

    [Header("속성 밸런스 옵션")]
    [SerializeField, Min(1)] private int maxShockCount = 3;  // 물 상태에서 넉백 가능한 최대 횟수
    [SerializeField, Min(1f)] private float wetDuration = 20f; // 물 오라 유지 시간
    [SerializeField, Range(0f, 1f)] private float darkFactor = 0.32f; // 감전 시 밝기 비율

    [Header("모델 루트 설정")]
    [SerializeField] private Transform modelRoot;

    private int currentShockCount = 0;
    private GameObject currentWaterEffectInstance;
    private Renderer[] monsterRenderers;
    private Color[] originalColors;

    private Coroutine wetCoroutine;
    private Coroutine shockCoroutine;

    private void Awake()
    {
        monsterRenderers = GetComponentsInChildren<Renderer>();
        if (monsterRenderers != null && monsterRenderers.Length > 0)
        {
            originalColors = new Color[monsterRenderers.Length];
            for (int i = 0; i < monsterRenderers.Length; i++)
            {
                if (monsterRenderers[i] != null && monsterRenderers[i].material.HasProperty("_Color"))
                {
                    originalColors[i] = monsterRenderers[i].material.color;
                }
            }
        }

        // 자동 루트 탐색
        if (modelRoot == null)
        {
            Transform foundRoot = transform.Find("BrickToy_3D_LP_Warrior_Robots_2_1");
            if (foundRoot != null) modelRoot = foundRoot;
            else if (transform.childCount > 0) modelRoot = transform.GetChild(0);
            else modelRoot = transform;
        }
    }

    public void ApplyElementalAttack(ElementalType type, float damage, Vector3 attackerPosition = default)
    {
        switch (type)
        {
            case ElementalType.Water:
                ApplyWetStatus(wetDuration, damage);
                break;

            case ElementalType.Electric:
                if (isWet)
                {
                    if (currentShockCount < maxShockCount)
                    {
                        TriggerElectricShockSynergy(damage * 1.5f, attackerPosition);
                    }
                    else
                    {
                        TakeDamage(damage);
                    }
                }
                else
                {
                    TakeDamage(damage);
                }
                break;

            case ElementalType.Mud:
                isMuddy = true;
                TakeDamage(damage);
                break;

            case ElementalType.Fire:
                if (isMuddy) TriggerFireSynergy(damage * 2.5f);
                else TakeDamage(damage);
                break;
        }
    }

    private void ApplyWetStatus(float duration, float damage)
    {
        isWet = true;
        currentShockCount = 0;
        TakeDamage(damage);

        if (currentWaterEffectInstance == null && waterEffectPrefab != null)
        {
            currentWaterEffectInstance = Instantiate(waterEffectPrefab, transform.position, Quaternion.identity);
            currentWaterEffectInstance.transform.SetParent(transform);
            currentWaterEffectInstance.transform.localPosition = Vector3.zero;
        }

        if (wetCoroutine != null) StopCoroutine(wetCoroutine);
        wetCoroutine = StartCoroutine(RemoveWetRoutine(duration));
    }

    private void TriggerElectricShockSynergy(float bonusDamage, Vector3 attackerPosition)
    {
        currentShockCount++;
        TakeDamage(bonusDamage);

        if (electricSynergyVFX != null)
        {
            GameObject spark = Instantiate(electricSynergyVFX, transform.position, Quaternion.identity);
            spark.transform.SetParent(transform);
            Destroy(spark, 0.4f);
        }

        if (shockCoroutine != null) StopCoroutine(shockCoroutine);
        shockCoroutine = StartCoroutine(SafeSlidingKnockbackRoutine());

        if (currentShockCount >= maxShockCount)
        {
            ClearWetStatus();
        }
    }

    // ⚡ [안전한 슬라이딩 넉백] 위로 뜨지 않고 뒤로만 살짝 슥 밀렸다 원복됨 (충돌 겹침 방지)
    private IEnumerator SafeSlidingKnockbackRoutine()
    {
        isShocked = true;
        SetMonsterBrightness(darkFactor);

        Transform targetModel = modelRoot != null ? modelRoot : transform;
        Vector3 originalLocalPos = targetModel.localPosition;

        float elapsed = 0f;
        while (elapsed < shockDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shockDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // 💡 Y축 점프 높이 제거 -> 로컬 뒤쪽(-Z)으로만 슬라이딩 후 복원
            float currentZOffset = -Mathf.Sin(smoothT * Mathf.PI) * knockbackDistance;

            targetModel.localPosition = originalLocalPos + new Vector3(0, 0, currentZOffset);

            yield return null;
        }

        targetModel.localPosition = originalLocalPos;
        SetMonsterBrightness(1.0f);

        isShocked = false;
    }

    private void SetMonsterBrightness(float factor)
    {
        if (monsterRenderers == null) return;

        for (int i = 0; i < monsterRenderers.Length; i++)
        {
            if (monsterRenderers[i] != null && monsterRenderers[i].material.HasProperty("_Color"))
            {
                Color baseColor = originalColors[i];
                monsterRenderers[i].material.color = new Color(baseColor.r * factor, baseColor.g * factor, baseColor.b * factor, baseColor.a);
            }
        }
    }

    private void ClearWetStatus()
    {
        isWet = false;
        if (currentWaterEffectInstance != null)
        {
            Destroy(currentWaterEffectInstance);
            currentWaterEffectInstance = null;
        }
    }

    private IEnumerator RemoveWetRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        ClearWetStatus();
    }

    private void TriggerFireSynergy(float bonusDamage)
    {
        isMuddy = false;
        isOnFire = true;
        TakeDamage(bonusDamage);
        if (fireSynergyVFX != null) Instantiate(fireSynergyVFX, transform.position, Quaternion.identity);
    }

    public void TakeDamage(float amount)
    {
        Debug.Log($"{gameObject.name}이(가) {amount} 데미지를 입었습니다. (현재 감전 넉백 횟수: {currentShockCount}/{maxShockCount})");
    }
}