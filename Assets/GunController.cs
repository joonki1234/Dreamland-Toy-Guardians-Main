using System;
using System.Collections;
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

    [Tooltip("총알이 자동으로 사라지는 시간")]
    public float bulletLifetime = 3f;


    [Header("조준 설정")]

    [Tooltip("화면 중앙 조준 방향을 계산할 플레이어 카메라")]
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


    private Coroutine flashCoroutine;

    private static int nextPoliceShotId =
        500000;


    private void Update()
    {
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }


    private void Shoot()
    {
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

        if (playerCamera == null)
        {
            playerCamera = Camera.main;

            if (playerCamera == null)
            {
                Debug.LogWarning(
                    $"{gameObject.name}: Player Camera가 비어 있습니다."
                );

                return;
            }
        }

        Vector3 targetPoint =
            FindAimTargetPoint();

        Vector3 shootDirection =
            targetPoint - firePoint.position;

        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            shootDirection =
                playerCamera.transform.forward;
        }

        shootDirection.Normalize();

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

        projectile.Initialize(
            bulletDamage,
            nextPoliceShotId++
        );

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

        Destroy(
            bullet,
            Mathf.Max(0.1f, bulletLifetime)
        );
    }


    /// <summary>
    /// 카메라 화면 중앙이 가리키는 지점을 찾는다.
    ///
    /// 이것은 피해 판정이 아니라,
    /// 총구에서 총알이 날아갈 방향만 계산하는 용도다.
    /// </summary>
    private Vector3 FindAimTargetPoint()
    {
        Ray aimRay = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        RaycastHit[] hits =
            Physics.RaycastAll(
                aimRay,
                aimDistance,
                aimMask,
                QueryTriggerInteraction.Ignore
            );

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(
                hits,
                (left, right) =>
                    left.distance.CompareTo(right.distance)
            );

            Transform playerRoot =
                transform.root;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                // 플레이어 자신의 Collider는 조준 대상에서 제외한다.
                if (hit.collider.transform.IsChildOf(playerRoot))
                {
                    continue;
                }

                return hit.point;
            }
        }

        return
            playerCamera.transform.position +
            playerCamera.transform.forward *
            aimDistance;
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

        bulletLifetime =
            Mathf.Max(0.1f, bulletLifetime);

        aimDistance =
            Mathf.Max(0.1f, aimDistance);

        flashDuration =
            Mathf.Max(0.01f, flashDuration);
    }
}