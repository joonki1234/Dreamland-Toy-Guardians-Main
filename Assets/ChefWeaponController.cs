using System.Collections;
using Fusion;
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

    [Header("조준 설정 (화면 중앙 크로스헤어 기준)")]
    [Tooltip("비워두면 기존처럼 foodSpawnPoint가 향한 방향으로 음식이 나갑니다.")]
    public Camera playerCamera;
    public float aimDistance = 20f;
    public LayerMask aimMask = ~0;

    [Header("웍질 효과음")]
    [Tooltip("비워두면 Resources/SFX/Chef/pan_swing을 자동으로 불러온다.")]
    public AudioClip panSwingSfx;

    [Range(0f, 1f)]
    public float panSwingVolume = 0.35f;

    private static AudioClip cachedPanSwingSfx;
    private const string PanSwingSfxResourcePath = "SFX/Chef/pan_swing";

    private bool isAttacking = false;

    // 내가 조종하는 캐릭터의 무기일 때만 반응하도록 하는 소유권 체크용.
    private NetworkObject ownerNetworkObject;

    private void Awake()
    {
        ownerNetworkObject = GetComponentInParent<NetworkObject>();
    }

    private void Update()
    {
        if (ownerNetworkObject != null && !ownerNetworkObject.HasInputAuthority) return;

    }

    public void TriggerAttack(bool dealsDamage = true)
    {
        // 직업 동기화(OnJobChanged)가 아직 처리되기 전에 공격 RPC가 먼저
        // 도착하면, 이 프리팹이 아직 비활성 상태라 StartCoroutine이 실패한다
        // (Unity가 비활성 오브젝트에서는 코루틴을 못 돌림). isActiveAndEnabled로
        // 그 순간을 걸러내고 조용히 무시한다 - 어차피 아직 준비 안 된 프레임의
        // 공격이라 처리할 게 없다.
        if (!isAttacking && isActiveAndEnabled)
        {
            StartCoroutine(WokSwingRoutine(dealsDamage));
        }
    }

    private IEnumerator WokSwingRoutine(bool dealsDamage)
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
        LaunchRandomFood(dealsDamage);
        PlayPanSwingSfx();

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

    private void PlayPanSwingSfx()
    {
        AudioClip clip = panSwingSfx;

        if (clip == null)
        {
            if (cachedPanSwingSfx == null)
            {
                cachedPanSwingSfx = Resources.Load<AudioClip>(PanSwingSfxResourcePath);
            }

            clip = cachedPanSwingSfx;
        }

        if (clip != null && panTransform != null)
        {
            AudioSource.PlayClipAtPoint(clip, panTransform.position, panSwingVolume);
        }
    }

    /// <summary>
    /// 화면 중앙(크로스헤어) 기준 조준 방향을 구한다. 카메라가 없으면
    /// 기존처럼 foodSpawnPoint가 향한 방향을 그대로 쓴다.
    /// </summary>
    private Vector3 ComputeAimLaunchDirection()
    {
        if (playerCamera == null)
        {
            return foodSpawnPoint.forward;
        }

        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;

        Vector3 targetPoint = Physics.Raycast(rayOrigin, rayDirection, out RaycastHit camHit, aimDistance, aimMask)
            ? camHit.point
            : rayOrigin + rayDirection * aimDistance;

        return (targetPoint - foodSpawnPoint.position).normalized;
    }

    private void LaunchRandomFood(bool dealsDamage)
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
        ChefFoodProjectile foodProjectile = spawnedFood.GetComponent<ChefFoodProjectile>();

        if (foodProjectile == null)
        {
            foodProjectile = spawnedFood.AddComponent<ChefFoodProjectile>();
        }

        if (!dealsDamage)
        {
            // 다른 클라이언트에서 재생되는 보여주기용 음식 - 충돌 콜백을 꺼서
            // 적에게 중복으로 피해가 들어가지 않게 한다.
            foodProjectile.enabled = false;
        }

        if (spawnedFood.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            Vector3 aimDirection = ComputeAimLaunchDirection();
            Vector3 launchDirection = (aimDirection * launchForce) + (Vector3.up * upwardForce);
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
