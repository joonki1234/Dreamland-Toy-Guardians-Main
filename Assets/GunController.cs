using UnityEngine;
using UnityEngine.InputSystem; 

public class GunController : MonoBehaviour
{
    [Header("발사 설정")]
    public GameObject bulletPrefab; // 광선 총알 프리팹
    public Transform firePoint;     // 총알이 태어날 기준점 (총구)
    public float bulletSpeed = 40f; // 총알 속도

    [Header("카메라 설정")]
    public Camera playerCamera;     // Player 하위의 Main Camera를 드래그해 넣으세요!

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

        // 1. 카메라가 바라보는 월드 기준 정면 방향을 구합니다.
        Vector3 shootDirection = playerCamera.transform.forward;

        // [핵심] 총의 반전된 축(Scale -1) 영향을 받지 않기 위해, 
        // 카메라의 실시간 앞쪽 방향으로 1.2미터 전진한 안전한 위치를 생성 좌표로 잡습니다.
        Vector3 spawnPosition = playerCamera.transform.position + (shootDirection * 1.2f);

        // 2. 총알 생성 (총의 꼬인 회전값을 무시하고, 카메라가 보는 방향으로 생성)
        Quaternion bulletRotation = Quaternion.LookRotation(shootDirection);
        GameObject bullet = Instantiate(bulletPrefab, spawnPosition, bulletRotation);

        // 세로로 선 총알 모델을 날아가는 방향(정면)으로 90도 눕혀줍니다.
        bullet.transform.Rotate(90f, 0f, 0f);

        // 3. 물리 속도 주기 (카메라 정면 방향으로 시원하게 쏩니다)
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = shootDirection * bulletSpeed;
            rb.angularVelocity = Vector3.zero; // 회전 방지
        }

        // 4. 3초 뒤 자동 파괴
        Destroy(bullet, 3f);
    }
}