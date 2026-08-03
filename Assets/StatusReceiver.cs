using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum ElementalType { None, Mud, Fire, Water, Electric }

public class StatusReceiver : MonoBehaviour
{
    [Header("상태 이상 여부")]
    public bool isMuddy = false;     // 빌더: 진흙 묻음
    public bool isWet = false;       // 소방관: 물 오라 감쌈
    public bool isOnFire = false;    // [시너지] 불붙음
    public bool isShocked = false;   // [시너지] 감전 중

    [Header("시각 이펙트 프리팹 (VFX)")]
    [Tooltip("FlexUnit > WaterSpell > Prefabs 안의 물 오라 프리팹")]
    public GameObject waterEffectPrefab;    
    public GameObject electricSynergyVFX;   
    public GameObject fireSynergyVFX;       

    [Header("감전 연출 옵션")]
    public float shockDuration = 0.1f;      // 0.1초 순간 경직/어두워짐
    public float knockbackForce = 1.5f;     // 넉백 세기
    [Range(0f, 1f)]
    public float darkFactor = 0.3f;         // 감전 시 순간 밝기 비율 (0.3 = 30%로 어두워짐)

    private GameObject currentWaterEffectInstance;
    private Renderer[] monsterRenderers;
    private Color[] originalColors;
    private Rigidbody rb;
    private NavMeshAgent agent;

    private Coroutine wetCoroutine;
    private Coroutine shockCoroutine;

    private void Awake()
    {
        // 몬스터의 Renderer와 기본 머티리얼 색상 저장
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

        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
    }

    // 피격 로직 (속성 전달)
    public void ApplyElementalAttack(ElementalType type, float damage, Vector3 attackerPosition = default)
    {
        switch (type)
        {
            case ElementalType.Water:
                ApplyWetStatus(5f, damage);
                break;

            case ElementalType.Electric:
                if (isWet)
                {
                    // ⚡ 물 오라 상태에서 총알 적중 ➡️ 감전 시너지 연출
                    TriggerElectricShockSynergy(damage * 2.0f, attackerPosition);
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

    // 🌊 1. 물 오라 감싸기
    private void ApplyWetStatus(float duration, float damage)
    {
        isWet = true;
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

    // ⚡ 2. 감전 (어두워짐 + 넉백)
    private void TriggerElectricShockSynergy(float bonusDamage, Vector3 attackerPosition)
    {
        TakeDamage(bonusDamage);

        if (electricSynergyVFX != null)
        {
            GameObject spark = Instantiate(electricSynergyVFX, transform.position, Quaternion.identity);
            spark.transform.SetParent(transform);
            Destroy(spark, 0.5f);
        }

        if (shockCoroutine != null) StopCoroutine(shockCoroutine);
        shockCoroutine = StartCoroutine(ElectricShockStutterRoutine(attackerPosition));
    }

    // ⏱️ 0.1초 순간 연출
    private IEnumerator ElectricShockStutterRoutine(Vector3 attackerPosition)
    {
        isShocked = true;

        // [넉백] 총알 위치 반대 방향으로 추진력 전달
        Vector3 knockbackDir = (transform.position - attackerPosition).normalized;
        knockbackDir.y = 0;

        if (agent != null && agent.enabled)
        {
            agent.velocity = knockbackDir * knockbackForce;
        }
        else if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);
        }

        // [밝기 감소] 0.1초간 색상 어둡게 변경
        SetMonsterBrightness(darkFactor);

        yield return new WaitForSeconds(shockDuration);

        // [복원] 원래 밝기 및 이동 속도 재개
        SetMonsterBrightness(1.0f);
        if (agent != null) agent.velocity = Vector3.zero;

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

    private IEnumerator RemoveWetRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        isWet = false;
        if (currentWaterEffectInstance != null)
        {
            Destroy(currentWaterEffectInstance);
            currentWaterEffectInstance = null;
        }
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
        Debug.Log($"{gameObject.name}이(가) {amount} 데미지를 입었습니다.");
    }
}