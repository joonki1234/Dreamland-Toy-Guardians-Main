using System.Collections;
using UnityEngine;

public class FireHoseController : MonoBehaviour
{
    [Header("컴포넌트 연결")]
    public ParticleSystem waterParticle; // WaterParticle 프리팹/오브젝트
    public Transform firePoint;          // 물 발사 위치

    [Header("물호스 옵션")]
    public float maxDistance = 15f;      // 물 사거리
    public float waterDamage = 10f;      // 불 끄는 데미지/파워
    public LayerMask targetLayer;        // 불/적 레이어

    private Coroutine stopRoutine;
    private float defaultSpeed;
    private bool isShooting = false;

    private void Start()
    {
        if (waterParticle != null)
        {
            var main = waterParticle.main;
            defaultSpeed = main.startSpeed.constant;

            // 시작할 때 완전히 멈춰두기
            waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        // 마우스 왼쪽 버튼을 누르는 동안 발사
        if (Input.GetButtonDown("Fire1"))
        {
            StartWater();
        }
        else if (Input.GetButtonUp("Fire1"))
        {
            StopWater();
        }

        // 물이 뿜어져 나오는 동안 continuous하게 타격 판정 진행
        if (isShooting && firePoint != null)
        {
            ProcessWaterHit();
        }
    }

    // 💧 물 발사 시작
    public void StartWater()
    {
        if (waterParticle == null) return;

        if (stopRoutine != null) StopCoroutine(stopRoutine);

        var main = waterParticle.main;
        main.startSpeed = defaultSpeed; // 기본 수압 속도 복원

        waterParticle.Play();
        isShooting = true;
    }

    // 💧 물 발사 중단 (수압 감소 연출)
    public void StopWater()
    {
        if (!isShooting) return;

        if (stopRoutine != null) StopCoroutine(stopRoutine);
        stopRoutine = StartCoroutine(PressureDropRoutine());
    }

    // 수압 감소 코루틴
    private IEnumerator PressureDropRoutine()
    {
        var main = waterParticle.main;
        float duration = 0.25f; // 수압 떨어지는 시간 (0.25초)
        float elapsed = 0f;

        // 수압이 떨어지는 동안에도 타격 판정 유지
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 속도를 줄여서 바닥으로 뚝 떨어지게 함
            main.startSpeed = Mathf.Lerp(defaultSpeed, 2f, elapsed / duration);
            yield return null;
        }

        // 이미 생성된 물방울은 날아가게 두고 새 입자 생성만 중단
        waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        main.startSpeed = defaultSpeed;
        isShooting = false; // 수압이 완전히 떨어진 후 타격 종료
    }

    // 🎯 물 타격 판정 (Raycast 기반)
    private void ProcessWaterHit()
    {
        Vector3 shootDirection = firePoint.forward;

        if (Physics.Raycast(firePoint.position, shootDirection, out RaycastHit hit, maxDistance, targetLayer))
        {
            // TODO: 타격 대상 처리 (예: 불 끄기)
            // if (hit.collider.TryGetComponent<IFireTarget>(out var target))
            // {
            //     target.Extinguish(waterDamage * Time.deltaTime);
            // }

            Debug.DrawLine(firePoint.position, hit.point, Color.cyan);
        }
    }
}