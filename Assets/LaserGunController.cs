using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LaserGunController : MonoBehaviour
{
    [Header("발사 설정")]
    public Transform firePoint;         // 새 총의 총구 위치
    public float maxLaserDistance = 30f; // 레이저 최대 거리
    
    [Header("딜레이 설정")]
    public float shootDelay = 0.7f;    // 켤 때 딜레이 (기 모으는 시간)
    public float turnOffDelay = 0.2f;   // [추가] 마우스를 떼고 레이저가 꺼지기까지 걸리는 시간 (잔상 시간)

    [Header("카메라 설정")]
    public Camera playerCamera;         // Main Camera

    [Header("광선 컴포넌트")]
    public LineRenderer lineRenderer;   // LaserLine의 Line Renderer

    private float pressTime = 0f;       // 누르고 있는 시간 측정용
    private Coroutine turnOffCoroutine;  // 꺼짐 지연을 제어할 코루틴 변수
    private bool isLaserActive = false;  // 현재 레이저가 실제로 발사 중인지 여부

    void Update()
    {
        if (Pointer.current == null || lineRenderer == null || firePoint == null || playerCamera == null) return;

        // 1. 마우스를 꾹 누르고 있는 동안
        if (Mouse.current.leftButton.isPressed)
        {
            // 꺼지려고 대기 중이던 타이머가 있다면 취소합니다. (연사 대응)
            if (turnOffCoroutine != null)
            {
                StopCoroutine(turnOffCoroutine);
                turnOffCoroutine = null;
            }

            pressTime += Time.deltaTime; // 누르는 시간 누적

            // 켤 때 딜레이(차징 시간)를 넘었을 때만 발사 시작
            if (pressTime >= shootDelay)
            {
                isLaserActive = true;
                ShootLaser();
            }
        }
        // 2. 마우스에서 손을 떼는 순간
        else
        {
            pressTime = 0f; // 누른 시간 리셋

            // 레이저가 켜져 있는 상태에서 손을 뗐다면, 0.7초 딜레이 코루틴을 돌립니다.
            if (isLaserActive && turnOffCoroutine == null)
            {
                turnOffCoroutine = StartCoroutine(DisableLaserAfterDelay());
            }
        }

        // 레이저가 완전히 꺼지기 직전까지는 계속 총구를 쫓아다니며 빔을 그려줍니다.
        if (isLaserActive && lineRenderer.enabled)
        {
            UpdateLaserPositions();
        }
    }

    // 레이저 발사 시작 세팅
    void ShootLaser()
    {
        lineRenderer.enabled = true;
        UpdateLaserPositions();
    }

    // 레이저 선의 시작점과 끝점을 실시간으로 갱신하는 함수
    void UpdateLaserPositions()
    {
        lineRenderer.SetPosition(0, firePoint.position); // 시작점: 총구 위치

        Vector3 rayDirection = playerCamera.transform.forward;
        Vector3 targetPosition = firePoint.position + (rayDirection * maxLaserDistance);

        RaycastHit hit;
        // 카메라 정면 방향 물리 충돌 체크
        if (Physics.Raycast(playerCamera.transform.position, rayDirection, out hit, maxLaserDistance))
        {
            targetPosition = hit.point;
        }

        lineRenderer.SetPosition(1, targetPosition); // 끝점 연결
    }

    // [핵심] 설정한 시간(turnOffDelay)만큼 대기한 뒤 레이저를 끄는 코루틴
    IEnumerator DisableLaserAfterDelay()
    {
        yield return new WaitForSeconds(turnOffDelay); // 0.7초 대기
        
        lineRenderer.enabled = false; // 레이저 끄기
        isLaserActive = false;        // 발사 상태 해제
        turnOffCoroutine = null;
    }
}