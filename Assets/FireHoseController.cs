using System.Collections;
using Fusion;
using UnityEngine;

public class FireHoseController : MonoBehaviour
{
    [Header("컴포넌트 연결")]
    public ParticleSystem waterParticle; // waterParticle 오브젝트
    public Transform firePoint;          // FirePoint 오브젝트

    [Header("물호스 옵션")]
    public float maxDistance = 20f;      // 물 사거리
    public float waterDamage = 10f;      // 물 데미지
    public LayerMask targetLayer;        // Everything 권장

    [Header("조준 설정 (화면 중앙 크로스헤어 기준)")]
    [Tooltip("비워두면 기존처럼 호스가 물리적으로 향한 방향(firePoint.forward)으로 물이 나갑니다.")]
    public Camera playerCamera;

    private Coroutine stopRoutine;
    private float defaultSpeed;
    private bool isShooting = false;

    // 내가 조종하는 캐릭터의 무기일 때만 반응하도록 하는 소유권 체크용.
    private NetworkObject ownerNetworkObject;

    // 물 분사음(루프). Assets/Audio/Resources/SFX/Firefighter/hose_spray.mp3를 자동으로 불러온다.
    private AudioSource sfxAudioSource;
    private const string SpraySfxResourcePath = "SFX/Firefighter/hose_spray";

    private void Awake()
    {
        ownerNetworkObject = GetComponentInParent<NetworkObject>();

        sfxAudioSource = GetComponent<AudioSource>();
        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = true;
        sfxAudioSource.spatialBlend = 1f;
        sfxAudioSource.volume = 0.22f;

        if (sfxAudioSource.clip == null)
        {
            sfxAudioSource.clip = Resources.Load<AudioClip>(SpraySfxResourcePath);
        }
    }

    private void Start()
    {
        if (waterParticle != null)
        {
            var main = waterParticle.main;
            defaultSpeed = main.startSpeed.constant;
            waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (ownerNetworkObject != null && !ownerNetworkObject.HasInputAuthority) return;

        if (isShooting && firePoint != null) ProcessWaterHit();
    }

    public void StartWater()
    {
        if (waterParticle == null) return;
        if (stopRoutine != null) StopCoroutine(stopRoutine);

        var main = waterParticle.main;
        main.startSpeed = defaultSpeed;
        waterParticle.Play();
        isShooting = true;

        if (sfxAudioSource != null && sfxAudioSource.clip != null && !sfxAudioSource.isPlaying)
        {
            sfxAudioSource.Play();
        }
    }

    public void StopWater()
    {
        if (!isShooting) return;
        if (stopRoutine != null) StopCoroutine(stopRoutine);
        stopRoutine = StartCoroutine(PressureDropRoutine());
    }

    private IEnumerator PressureDropRoutine()
    {
        var main = waterParticle.main;
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            main.startSpeed = Mathf.Lerp(defaultSpeed, 2f, elapsed / duration);
            yield return null;
        }

        waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        main.startSpeed = defaultSpeed;
        isShooting = false;

        if (sfxAudioSource != null && sfxAudioSource.isPlaying)
        {
            sfxAudioSource.Stop();
        }
    }

    /// <summary>
    /// 화면 중앙(크로스헤어) 기준 조준 방향을 구한다. 카메라가 없으면
    /// 기존처럼 호스가 향한 방향을 그대로 쓴다.
    /// </summary>
    private Vector3 ComputeAimShootDirection()
    {
        if (playerCamera == null)
        {
            return firePoint.forward;
        }

        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;

        Vector3 targetPoint = Physics.Raycast(rayOrigin, rayDirection, out RaycastHit camHit, maxDistance, targetLayer)
            ? camHit.point
            : rayOrigin + rayDirection * maxDistance;

        return (targetPoint - firePoint.position).normalized;
    }

    private void ProcessWaterHit()
    {
        Vector3 shootDirection = ComputeAimShootDirection();

        // 노즐(물 파티클)도 크로스헤어 방향으로 같이 회전시켜서 실제로
        // 보이는 물줄기와 데미지 판정 방향을 일치시킨다.
        if (waterParticle != null)
        {
            waterParticle.transform.rotation = Quaternion.LookRotation(shootDirection, Vector3.up);
        }

        if (Physics.Raycast(firePoint.position, shootDirection, out RaycastHit hit, maxDistance, targetLayer))
        {
            // 부모/자식 관계없이 StatusReceiver를 감지하여 물 속성 전달
            StatusReceiver statusReceiver = hit.collider.GetComponentInParent<StatusReceiver>();
            if (statusReceiver == null) statusReceiver = hit.collider.GetComponentInChildren<StatusReceiver>();

            if (statusReceiver != null)
            {
                statusReceiver.ApplyElementalAttack(ElementalType.Water, waterDamage * Time.deltaTime);
            }

            Debug.DrawLine(firePoint.position, hit.point, Color.cyan);
        }
    }
}
