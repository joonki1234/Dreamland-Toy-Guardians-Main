using System.Collections;
using UnityEngine;

public enum ElementalType { None, Mud, Fire, Water, Electric }

public class StatusReceiver : MonoBehaviour
{
    [Header("상태 이상 여부")]
    public bool isMuddy = false;     // 진흙 묻음
    public bool isWet = false;       // 물 묻음
    public bool isOnFire = false;    // 불붙음
    public bool isShocked = false;   // 감전 경직 상태

    [Header("시각 이펙트 프리팹 (VFX)")]
    [Tooltip("FlexUnit > WaterSpell > Prefabs 안의 물 오라/보호막 프리팹")]
    public GameObject waterEffectPrefab;    // 🌊 몬스터를 감싸는 물 오라
    public GameObject electricSynergyVFX;   // ⚡ 총알 적중 시 터지는 감전 이펙트
    public GameObject fireSynergyVFX;       // 🔥 화염 시너지 이펙트

    [Header("감전 딜레이 설정")]
    [Tooltip("총알 맞았을 때 움칫거리는 딜레이 시간 (초)")]
    public float shockStunDuration = 0.1f;  

    private GameObject currentWaterEffectInstance; // 현재 붙어있는 물 오라 객체
    private Coroutine wetCoroutine;
    private Coroutine shockCoroutine;

    // 타겟 피격 시 실행
    public void ApplyElementalAttack(ElementalType type, float damage)
    {
        switch (type)
        {
            case ElementalType.Water:
                // 🌊 소방관(Firefighter) 공격: 물 오라를 몬스터 주변에 감쌈
                ApplyWetStatus(5f, damage);
                break;

            case ElementalType.Electric:
                // ⚡ 경찰(Police) 총알 공격
                if (isWet)
                {
                    // 물이 묻은 상태라면 ⚡ 감전 시너지 연출 발생!
                    TriggerElectricShockSynergy(damage * 2.0f);
                }
                else
                {
                    // 일반 총알 피격
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

    // 🌊 1. 소방관 물 공격: 몬스터 주변을 물 오라가 감쌈
    private void ApplyWetStatus(float duration, float damage)
    {
        isWet = true;
        TakeDamage(damage);

        // 몬스터 주변에 물 오라 이펙트가 없다면 생성 후 몬스터 자식으로 붙임
        if (currentWaterEffectInstance == null && waterEffectPrefab != null)
        {
            currentWaterEffectInstance = Instantiate(waterEffectPrefab, transform.position, Quaternion.identity);
            currentWaterEffectInstance.transform.SetParent(transform);
            currentWaterEffectInstance.transform.localPosition = Vector3.zero; // 몬스터 중심에 위치
        }

        // 물 오라 지속시간 타이머 (이미 젖어있으면 시간 리셋)
        if (wetCoroutine != null) StopCoroutine(wetCoroutine);
        wetCoroutine = StartCoroutine(RemoveWetRoutine(duration));
    }

    // ⚡ 2. 경찰 총알 피격: 감전 터짐 + 0.1초 '움칫' 딜레이(Stun)
    private void TriggerElectricShockSynergy(float bonusDamage)
    {
        TakeDamage(bonusDamage);

        // 감전 전 스파크 이펙트 생성
        if (electricSynergyVFX != null)
        {
            GameObject spark = Instantiate(electricSynergyVFX, transform.position, Quaternion.identity);
            spark.transform.SetParent(transform); // 몬스터 위치에 스파크
            Destroy(spark, 0.5f);
        }

        // 💥 0.1초간 움칫거리는 감전 딜레이(경직) 코루틴 실행
        if (shockCoroutine != null) StopCoroutine(shockCoroutine);
        shockCoroutine = StartCoroutine(ElectricShockStutterRoutine());
    }

    // ⏱️ 0.1초 감전 '움칫' 딜레이 로직
    private IEnumerator ElectricShockStutterRoutine()
    {
        isShocked = true;

        // 1. 몬스터 이동 및 AI 멈춤 (NavMeshAgent 또는 이동 스크립트가 있다면 일시정지)
        // 예: GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;

        // 2. 0.1초 동안 감전으로 '움칫' 스톱
        yield return new WaitForSeconds(shockStunDuration);

        // 3. 0.1초 뒤 다시 정상 이동/동작 재개
        // 예: GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = false;

        isShocked = false;
    }

    // 🌊 물 오라 지속시간 종료 처리
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
        Debug.Log($"{gameObject.name} 이(가) {amount} 데미지를 입었습니다.");
    }
}