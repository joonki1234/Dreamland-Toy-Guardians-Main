using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 경찰 총의 발사 입력, 총알 생성, 발사 방향과 총구 효과를 담당한다.
///
/// 조준점 Raycast는 발사 방향 계산에만 사용하며
/// 실제 피해는 PoliceBulletProjectile의 물리 충돌이 처리한다.
/// </summary>
public class GunController : MonoBehaviour
{
    [Header("발사 설정")]

    [Tooltip("실제로 날아갈 총알 프리팹")]
    public GameObject bulletPrefab;

    [Tooltip("총알이 생성될 총구 위치")]
    public Transform firePoint;

    [Tooltip("총알 비행 속도")]
    public float bulletSpeed = 40f;

    [Tooltip("총알 한 발의 피해량")]
    public float bulletDamage = 10f;

    [Tooltip("연속 발사 사이의 최소 간격")]
    public float fireInterval = 0.25f;

    [Tooltip("총알이 자동으로 사라지는 시간")]
    public float bulletLifetime = 3f;


    [Header("조준 설정 (총구 기준)")]

    [Tooltip("기존 Inspector 참조 호환용. Police 기본 공격 방향에는 사용하지 않습니다.")]
    public Camera playerCamera;

    [Tooltip("조준 가능한 최대 거리")]
    public float aimDistance = 50f;

    [Tooltip("조준 방향을 계산할 때 확인할 레이어")]
    public LayerMask aimMask = ~0;


    [Header("총알 모델 회전 보정")]

    [Tooltip("총알 모델이 옆으로 눕는 경우 사용하는 회전 보정값")]
    public Vector3 bulletRotationOffset =
        new Vector3(90f, 0f, 0f);


    [Header("총구 이펙트")]

    public Light muzzleFlashLight;

    [Tooltip("총구 불빛이 켜지는 시간")]
    public float flashDuration = 0.05f;


    [Header("발사음")]

    [Tooltip("비워두면 Resources/SFX/Police/gun_shot을 자동으로 불러온다.")]
    public AudioClip gunShotSfx;

    [Range(0f, 1f)]
    public float gunShotVolume = 0.35f;

    private static AudioClip cachedGunShotSfx;
    private const string GunShotSfxResourcePath = "SFX/Police/gun_shot";


    private Coroutine flashCoroutine;
    private float nextFireTime;

    private static int nextPoliceShotId =
        500000;

    // 이 무기가 붙어 있는 플레이어의 NetworkObject.
    // 멀티플레이에서는 "내가 조종하는 캐릭터의 무기"일 때만 내 입력에 반응해야 한다.
    // 이게 없으면 다른 플레이어의(=화면에 보이는 원격 캐릭터의) 총도
    // 내가 클릭할 때마다 같이 발사돼 버린다.
    private NetworkObject ownerNetworkObject;


    private void Awake()
    {
        ownerNetworkObject = GetComponentInParent<NetworkObject>();
    }


    private void Update()
    {
        if (ownerNetworkObject != null && !ownerNetworkObject.HasInputAuthority)
        {
            return;
        }

    }

    public void TriggerShoot(bool dealsDamage = true)
    {
        Shoot(dealsDamage);
    }


    /// <summary>
    /// dealsDamage가 false면(다른 클라이언트에서 재생되는 "보여주기용" 총알) 총알은
    /// 그대로 날아가고 총구 이펙트/소리도 그대로 나지만, 충돌 시 실제 피해는 주지 않는다.
    /// 진짜 피해는 쏜 사람 본인 화면에서만(dealsDamage=true) 적용된다 - 그래야 모든
    /// 클라이언트가 각자 총알을 하나씩 만들어도 피해가 중복으로 들어가지 않는다.
    /// </summary>
    private void Shoot(bool dealsDamage)
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        if (bulletPrefab == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: Bullet Prefab이 비어 있습니다."
            );

            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: Fire Point가 비어 있습니다."
            );

            return;
        }

        // 3인칭에서는 총구(firePoint)가 화면 중앙 크로스헤어와 다른 곳을
        // 향할 수 있다 (무기가 손 위치에 붙어있으므로). 카메라 중앙에서
        // 레이캐스트로 조준점을 구하고, 총구에서 그 지점을 향하도록 방향을
        // 계산해야 크로스헤어가 가리키는 곳으로 총알이 날아간다.
        Vector3 shootDirection = ComputeAimShootDirection();

        Quaternion bulletRotation =
            Quaternion.LookRotation(
                shootDirection,
                Vector3.up
            ) *
            Quaternion.Euler(
                bulletRotationOffset
            );

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            bulletRotation
        );

        PoliceBulletProjectile projectile =
            bullet.GetComponent<PoliceBulletProjectile>();

        if (projectile == null)
        {
            projectile =
                bullet.AddComponent<PoliceBulletProjectile>();
        }

        if (dealsDamage)
        {
            projectile.Initialize(
                bulletDamage,
                nextPoliceShotId++
            );
        }
        else
        {
            // 다른 클라이언트에서 재생되는 보여주기용 총알 - 충돌 콜백 자체를 꺼서
            // 적에게 중복으로 피해가 들어가지 않게 한다.
            projectile.enabled = false;
        }

        nextFireTime = Time.time + fireInterval;

        IgnorePlayerCollision(bullet);

        Rigidbody bulletRigidbody =
            bullet.GetComponent<Rigidbody>();

        if (bulletRigidbody != null)
        {
            bulletRigidbody.linearVelocity =
                shootDirection * bulletSpeed;

            bulletRigidbody.angularVelocity =
                Vector3.zero;
        }
        else
        {
            Debug.LogWarning(
                $"{bullet.name}: Rigidbody가 없습니다."
            );
        }

        PlayMuzzleFlash();
        PlayGunShotSfx();

        Destroy(
            bullet,
            Mathf.Max(0.1f, bulletLifetime)
        );
    }


    /// <summary>
    /// 화면 중앙(크로스헤어) 기준 조준 방향을 구한다. 카메라가 없으면
    /// 기존처럼 총구가 향한 방향을 그대로 쓴다.
    /// </summary>
    private Vector3 ComputeAimShootDirection()
    {
        if (playerCamera == null)
        {
            return firePoint.forward;
        }

        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;

        Vector3 targetPoint = Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, aimDistance, aimMask)
            ? hit.point
            : rayOrigin + rayDirection * aimDistance;

        return (targetPoint - firePoint.position).normalized;
    }


    /// <summary>
    /// 총알이 생성 직후 플레이어 본인이나 무기에 부딪히지 않도록 한다.
    /// </summary>
    private void IgnorePlayerCollision(
        GameObject bullet)
    {
        if (bullet == null)
        {
            return;
        }

        Collider[] bulletColliders =
            bullet.GetComponentsInChildren<Collider>(true);

        Collider[] playerColliders =
            transform.root.GetComponentsInChildren<Collider>(true);

        foreach (Collider bulletCollider in bulletColliders)
        {
            if (bulletCollider == null)
            {
                continue;
            }

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider == null ||
                    playerCollider.transform.IsChildOf(
                        bullet.transform))
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    bulletCollider,
                    playerCollider,
                    true
                );
            }
        }
    }


    private void PlayMuzzleFlash()
    {
        if (muzzleFlashLight == null)
        {
            return;
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine =
            StartCoroutine(
                FlashMuzzleRoutine()
            );
    }


    private void PlayGunShotSfx()
    {
        AudioClip clip = gunShotSfx;

        if (clip == null)
        {
            if (cachedGunShotSfx == null)
            {
                cachedGunShotSfx = Resources.Load<AudioClip>(GunShotSfxResourcePath);
            }

            clip = cachedGunShotSfx;
        }

        if (clip != null && firePoint != null)
        {
            AudioSource.PlayClipAtPoint(clip, firePoint.position, gunShotVolume);
        }
    }


    private IEnumerator FlashMuzzleRoutine()
    {
        muzzleFlashLight.enabled = true;

        yield return new WaitForSeconds(
            Mathf.Max(0.01f, flashDuration)
        );

        muzzleFlashLight.enabled = false;
        flashCoroutine = null;
    }


    private void OnValidate()
    {
        bulletSpeed =
            Mathf.Max(0f, bulletSpeed);

        bulletDamage =
            Mathf.Max(0f, bulletDamage);

        fireInterval =
            Mathf.Max(0.02f, fireInterval);

        bulletLifetime =
            Mathf.Max(0.1f, bulletLifetime);

        aimDistance =
            Mathf.Max(0.1f, aimDistance);

        flashDuration =
            Mathf.Max(0.01f, flashDuration);
    }
}
