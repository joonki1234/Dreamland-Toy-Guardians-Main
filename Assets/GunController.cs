using System.Collections; // 코루틴을 쓰기 위해 추가해야 합니다.
using UnityEngine;
using UnityEngine.InputSystem; 

public class GunController : MonoBehaviour
{
    [Header("발사 설정")]
    public GameObject bulletPrefab; // 광선 총알 프리팹
    public Transform firePoint;     // 총알이 태어날 기준점 (총구)
    public float bulletSpeed = 40f; // 총알 속도

    [Header("카메라 설정")]
    public Camera playerCamera;     // Main Camera

    [Header("이펙트 설정")]
    public Light muzzleFlashLight;  // 방금 만든 총구 불빛(Point Light)
    public float flashDuration = 0.05f; // 불빛이 번쩍이고 켜져 있을 시간 (초)

    private Coroutine flashCoroutine; // 번쩍임 제어를 위한 변수

    void Update()
    {
        // 마우스 왼쪽 버튼 클릭 감지 (New Input System)
        if (Pointer.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null || playerCamera == null) return;

        // 1. 카메라 정면 기준으로 총알 스폰 위치 보정
        Vector3 shootDirection = playerCamera.transform.forward;
        Vector3 spawnPosition = playerCamera.transform.position + (shootDirection * 1.2f);

        // 2. 총알 생성 및 눕히기
        Quaternion bulletRotation = Quaternion.LookRotation(shootDirection);
        GameObject bullet = Instantiate(bulletPrefab, spawnPosition, bulletRotation);
        bullet.transform.Rotate(90f, 0f, 0f);

        // 3. 물리 속도 주기
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = shootDirection * bulletSpeed;
            rb.angularVelocity = Vector3.zero; // 회전 방지
        }

        // 4. [추가] 총구 화염 번쩍임 코루틴 실행
        if (muzzleFlashLight != null)
        {
            // 이미 켜져 있는 번쩍임이 있다면 중복 방지를 위해 꺼줍니다.
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashMuzzle());
        }

        // 5. 3초 뒤 자동 파괴
        Destroy(bullet, 3f);
    }

    // [추가] 아주 잠깐 불빛을 켰다가 끄는 코루틴 함수입니다.
    IEnumerator FlashMuzzle()
    {
        muzzleFlashLight.enabled = true; // 불빛 켜기
        yield return new WaitForSeconds(flashDuration); // 0.05초 대기
        muzzleFlashLight.enabled = false; // 불빛 끄기
    }
}