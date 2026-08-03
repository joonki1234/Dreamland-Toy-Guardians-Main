using UnityEngine;

/// <summary>
/// 소방관 물 Particle System의 시작과 정지를 담당한다.
///
/// 마우스를 놓으면 새로운 물 생성만 멈추고,
/// 이미 발사된 물은 수명이 끝날 때까지 계속 날아간다.
///
/// 다시 누르면 기존 물은 그대로 진행되는 동시에
/// 새로운 물줄기가 바로 생성된다.
///
/// 실제 충돌 피해는 WaterParticleHit가 처리한다.
/// </summary>
public class FireHoseController : MonoBehaviour
{
    [Header("컴포넌트 연결")]
    [SerializeField]
    private ParticleSystem waterParticle;

    private bool isWaterActive;

    private void Awake()
    {
        if (waterParticle == null)
        {
            Transform waterTransform =
                transform.Find("FirePoint/waterParticle");

            if (waterTransform != null)
            {
                waterParticle =
                    waterTransform.GetComponent<ParticleSystem>();
            }
        }
    }

    private void Start()
    {
        if (waterParticle == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: waterParticle이 연결되지 않았습니다."
            );

            return;
        }

        // 게임 시작 시에만 기존 파티클까지 완전히 비운다.
        waterParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        isWaterActive = false;
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            StartWater();
        }

        if (Input.GetButtonUp("Fire1"))
        {
            StopWater();
        }
    }

    /// <summary>
    /// 기존에 날아가고 있는 물은 지우지 않고
    /// 새로운 물 파티클 생성을 다시 시작한다.
    /// </summary>
    public void StartWater()
    {
        if (waterParticle == null)
        {
            return;
        }

        // Stop 또는 Clear를 먼저 하지 않는다.
        // 기존 물은 그대로 날아가고 새 물줄기만 추가로 방출된다.
        waterParticle.Play(true);

        isWaterActive = true;
    }

    /// <summary>
    /// 새로운 파티클 생성만 중지한다.
    /// 이미 발사된 물은 수명이 끝날 때까지 계속 움직인다.
    /// </summary>
    public void StopWater()
    {
        if (waterParticle == null || !isWaterActive)
        {
            return;
        }

        waterParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmitting
        );

        isWaterActive = false;
    }

    private void OnDisable()
    {
        isWaterActive = false;

        if (waterParticle != null)
        {
            // 직업 자체가 비활성화될 때는 남은 물까지 정리한다.
            waterParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }
}