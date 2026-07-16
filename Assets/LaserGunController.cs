using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LaserGunController : MonoBehaviour
{
    [Header("발사 설정")]
    public Transform firePoint;         
    public float maxLaserDistance = 15f; 
    
    [Header("딜레이 설정")]
    public float shootDelay = 1.2f;    
    public float turnOffDelay = 0.5f;   

    [Header("카메라 설정")]
    public Camera playerCamera;         

    [Header("광선 컴포넌트")]
    public LineRenderer lineRenderer;   

    [Header("이펙트 설정")]
    public ParticleSystem muzzleParticles; 
    public ParticleSystem hitParticles;    

    private float pressTime = 0f;       
    private Coroutine turnOffCoroutine;  
    private bool isLaserActive = false;  

    // [추가] 게임이 시작될 때 딱 한 번 실행되는 함수
    void Start()
    {
        // 게임 시작하자마자 총구 이펙트를 강제로 정지시키고 꺼버립니다. (치트키)
        if (muzzleParticles != null)
        {
            muzzleParticles.Stop();
            muzzleParticles.gameObject.SetActive(false); 
        }

        if (hitParticles != null)
        {
            hitParticles.Stop();
            hitParticles.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (Pointer.current == null || lineRenderer == null || firePoint == null || playerCamera == null) return;

        if (Mouse.current.leftButton.isPressed)
        {
            if (turnOffCoroutine != null)
            {
                StopCoroutine(turnOffCoroutine);
                turnOffCoroutine = null;
            }

            pressTime += Time.deltaTime;

            if (pressTime >= shootDelay)
            {
                isLaserActive = true;
                ShootLaser();
            }
        }
        else
        {
            pressTime = 0f;

            if (isLaserActive && turnOffCoroutine == null)
            {
                turnOffCoroutine = StartCoroutine(DisableLaserAfterDelay());
            }
        }

        if (isLaserActive && lineRenderer.enabled)
        {
            UpdateLaserPositions();
        }
    }

    void ShootLaser()
    {
        lineRenderer.enabled = true;
        
        // 마우스를 누르면 그제서야 오브젝트를 켜고 재생합니다.
        if (muzzleParticles != null)
        {
            if (!muzzleParticles.gameObject.activeSelf)
            {
                muzzleParticles.gameObject.SetActive(true);
            }
            if (!muzzleParticles.isPlaying)
            {
                muzzleParticles.Play();
            }
        }

        if (hitParticles != null && !hitParticles.gameObject.activeSelf)
        {
            hitParticles.gameObject.SetActive(true);
            hitParticles.Play();
        }

        UpdateLaserPositions();
    }

    void UpdateLaserPositions()
    {
        lineRenderer.SetPosition(0, firePoint.position); 

        Vector3 rayDirection = playerCamera.transform.forward;
        Vector3 targetPosition = firePoint.position + (rayDirection * maxLaserDistance);

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, rayDirection, out hit, maxLaserDistance))
        {
            targetPosition = hit.point;

            if (hitParticles != null)
            {
                if (!hitParticles.gameObject.activeSelf)
                {
                    hitParticles.gameObject.SetActive(true);
                }
                if (!hitParticles.isPlaying)
                {
                    hitParticles.Play();
                }

                hitParticles.transform.position = hit.point + (hit.normal * 0.15f);
                hitParticles.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
        }
        else
        {
            if (hitParticles != null && hitParticles.isPlaying)
            {
                hitParticles.Stop();
                hitParticles.gameObject.SetActive(false);
            }
        }

        lineRenderer.SetPosition(1, targetPosition); 
    }

    IEnumerator DisableLaserAfterDelay()
    {
        yield return new WaitForSeconds(turnOffDelay); 
        
        lineRenderer.enabled = false; 
        isLaserActive = false;        
        turnOffCoroutine = null;

        // 마우스를 떼고 0.7초가 지나면 다시 완전히 꺼버립니다.
        if (muzzleParticles != null)
        {
            muzzleParticles.Stop();
            muzzleParticles.gameObject.SetActive(false); // 오브젝트 자체를 꺼서 확실히 비활성화
        }
        
        if (hitParticles != null)
        {
            hitParticles.Stop();
            hitParticles.gameObject.SetActive(false);
        }
    }
}