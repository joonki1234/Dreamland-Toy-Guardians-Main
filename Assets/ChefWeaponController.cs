using System.Collections;
using UnityEngine;

public class ChefWeaponController : MonoBehaviour
{
    [Header("컴포넌트 연결")]
    public Transform panTransform;            // 후라이팬 Transform (Frying Pan 그대로 연결)
    public Transform foodSpawnPoint;          // 음식이 생성될 위치
    public GameObject[] foodPrefabs;          // Food Pack의 음식 프리팹들

    [Header("손잡이 축 설정 (코드 피벗 Offset)")]
    [Tooltip("손잡이 끝부분까지의 거리 (후라이팬 중심 기준 손잡이 방향 오프셋)")]
    public Vector3 handleOffset = new Vector3(-0.35f, 0f, 0f);

    [Header("웍질(Wok Swing) 모션 설정")]
    [Tooltip("위로 쳐올리는 각도")]
    public float wokUpAngle = -45f;           
    [Tooltip("뒤로 살짝 빼는 예비 동작 각도")]
    public float wokBackAngle = 15f;          

    [Header("웍질 타이밍 (자연스러운 3단계 모션)")]
    public float windUpDuration = 0.08f;      // 1단계: 뒤로 살짝 빼는 시간
    public float tossDuration = 0.12f;        // 2단계: 위로 쳐올리는 시간 (음식 발사)
    public float recoveryDuration = 0.35f;    // 3단계: 천천히 부드럽게 복귀하는 시간
    public float attackCooldown = 0.5f;       // 공격 쿨타임

    [Header("음식 발사 물리 설정")]
    public float launchForce = 14f;           
    public float upwardForce = 8f;            
    public float torqueAmount = 12f;          

    private bool isAttacking = false;

    private void Update()
    {
        if (Input.GetButtonDown("Fire1") && !isAttacking)
        {
            StartCoroutine(WokSwingRoutine());
        }
    }

    private IEnumerator WokSwingRoutine()
    {
        isAttacking = true;

        // 기준 위치 및 회전 저장
        Vector3 originPos = panTransform.localPosition;
        Quaternion originRot = panTransform.localRotation;

        // Y축 -90도 회전 모델 특성에 맞춘 각도 지정
        Quaternion backRot = originRot * Quaternion.Euler(0f, 0f, wokBackAngle);
        Quaternion tossRot = originRot * Quaternion.Euler(0f, 0f, -wokUpAngle);

        float elapsed = 0f;

        // [STEP 1] 예비 동작 (손잡이를 중심으로 뒤로 회전)
        while (elapsed < windUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / windUpDuration);
            Quaternion currentRot = Quaternion.Slerp(originRot, backRot, t);
            
            // 손잡이를 중심 축(Pivot)으로 위치 오프셋 재계산
            ApplyRotationAroundHandle(originPos, originRot, currentRot);
            yield return null;
        }

        // [STEP 2] 쳐올리기 (손잡이를 중심으로 머리부분 위로 쳐올림)
        elapsed = 0f;
        while (elapsed < tossDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / tossDuration);
            Quaternion currentRot = Quaternion.Slerp(backRot, tossRot, t);
            
            ApplyRotationAroundHandle(originPos, originRot, currentRot);
            yield return null;
        }

        // 🍳 최정점에서 음식 발사
        LaunchRandomFood();

        // [STEP 3] 복귀 동작 (손잡이 중심으로 부드럽게 복귀)
        elapsed = 0f;
        while (elapsed < recoveryDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / recoveryDuration);
            Quaternion currentRot = Quaternion.Slerp(tossRot, originRot, t);
            
            ApplyRotationAroundHandle(originPos, originRot, currentRot);
            yield return null;
        }

        // 원래 위치 및 회전으로 원복
        panTransform.localPosition = originPos;
        panTransform.localRotation = originRot;

        float totalMotionTime = windUpDuration + tossDuration + recoveryDuration;
        float remainCooldown = attackCooldown - totalMotionTime;
        if (remainCooldown > 0f)
        {
            yield return new WaitForSeconds(remainCooldown);
        }

        isAttacking = false;
    }

    // 💡 손잡이 오프셋 위치를 축으로 삼아 위치와 회전을 동시에 맞춰주는 핵심 함수
    private void ApplyRotationAroundHandle(Vector3 originPos, Quaternion originRot, Quaternion targetRot)
    {
        panTransform.localRotation = targetRot;
        
        // 손잡이 축 좌표 변환 계산
        Vector3 handleWorldPivot = panTransform.parent != null 
            ? panTransform.parent.TransformPoint(originPos) + originRot * handleOffset 
            : originPos + originRot * handleOffset;

        Vector3 offsetPos = targetRot * handleOffset;
        panTransform.localPosition = originPos + (originRot * handleOffset - offsetPos);
    }

    private void LaunchRandomFood()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0 || foodSpawnPoint == null)
        {
            Debug.LogWarning("음식 프리팹 목록이나 SpawnPoint가 비어있습니다!");
            return;
        }

        int randomIndex = Random.Range(0, foodPrefabs.Length);
        GameObject selectedFood = foodPrefabs[randomIndex];
        GameObject spawnedFood = Instantiate(selectedFood, foodSpawnPoint.position, Random.rotation);

        // 생성된 모든 음식에 요리사 투사체 표시를 자동으로 붙인다.
        if (spawnedFood.GetComponent<ChefFoodProjectile>() == null)
        {
            spawnedFood.AddComponent<ChefFoodProjectile>();
        }

        if (spawnedFood.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            Vector3 launchDirection = (foodSpawnPoint.forward * launchForce) + (Vector3.up * upwardForce);
            rb.AddForce(launchDirection, ForceMode.Impulse);

            Vector3 randomTorque = new Vector3(
                Random.Range(-torqueAmount, torqueAmount),
                Random.Range(-torqueAmount, torqueAmount),
                Random.Range(-torqueAmount, torqueAmount)
            );
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }

        Destroy(spawnedFood, 5f);
    }
}