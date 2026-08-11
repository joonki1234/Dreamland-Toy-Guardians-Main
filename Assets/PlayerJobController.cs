using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerJob
{
    Police,
    Firefighter,
    Chef,
    Builder
}

public class PlayerJobController : NetworkBehaviour
{
    // Fusion 2 네트워크 프로퍼티: 값이 바뀌면 모든 클라이언트에서 OnJobChanged가 호출된다.
    // (currentJob 필드 대신 사용 - [Networked]는 자동 구현 프로퍼티({ get; set; })여야 함)
    [Networked, OnChangedRender(nameof(OnJobChanged))]
    public PlayerJob CurrentJob { get; set; }


    [Header("직업별 모델링 (Models)")]
    public GameObject modelPolice;
    public GameObject modelFirefighter;
    public GameObject modelChef;
    public GameObject modelBuilder;


    [Header("직업별 무기 (Camera 자식들)")]
    public GameObject weaponPolice;
    public GameObject weaponFirefighter;
    public GameObject weaponChef;
    public GameObject weaponBuilder;


    [Header("건축가(Builder) 흙 발사 기본 설정")]
    public GameObject dirtPrefab;

    public Transform shovelFirePoint;

    public ParticleSystem dirtParticleSystem;

    public Light dirtFlashLight;

    [Tooltip("각 흙 파편을 앞으로 발사하는 힘")]
    public float throwForce = 32f;

    [Tooltip("건축가 공격 쿨타임")]
    public float builderCooldown = 0.5f;


    [Header("건축가 흙 산탄 설정")]

    [Tooltip("한 번의 삽질에서 발사할 흙 파편 수")]
    [Range(1, 12)]
    public int dirtShardCount = 6;

    [Tooltip("좌우로 퍼지는 최대 각도")]
    [Range(0f, 30f)]
    public float horizontalSpreadAngle = 12f;

    [Tooltip("위아래로 퍼지는 최대 각도")]
    [Range(0f, 20f)]
    public float verticalSpreadAngle = 5f;

    [Tooltip("각 파편에 추가되는 위쪽 힘")]
    public float shardUpwardForce = 3f;

    [Tooltip("파편 크기의 최소·최대 무작위 배율")]
    public Vector2 shardScaleMultiplierRange =
        new Vector2(0.75f, 1.05f);


    [Header("건축가 흙 산탄 피해")]

    [Tooltip("같은 적에게 가장 먼저 맞은 파편의 피해")]
    public float firstShardDamage = 8f;

    [Tooltip("같은 적에게 두 번째부터 맞는 파편의 피해")]
    public float additionalShardDamage = 2f;

    [Tooltip("한 번의 삽질로 같은 적에게 줄 수 있는 최대 피해")]
    public float maxShotDamagePerEnemy = 18f;


    [Header("XR 컨트롤러 입력 (선택)")]
    [Tooltip(
        "XRI Default Input Actions의 'XRI Right Interaction/Activate' " +
        "(또는 Left) 액션을 연결하면 VR 컨트롤러 트리거로도 공격할 수 있습니다. " +
        "비워두면 마우스 클릭만으로 동작합니다(PC 테스트용).")]
    [SerializeField]
    private InputActionReference xrActivateAction;


    private float lastAttackTime = -999f;
    private bool isSwinging;

    private static int nextBuilderProjectileShotId =
        300000;


    public override void Spawned()
    {
        // Start() 대신 Spawned()에서 초기화한다.
        // Spawned() 시점에는 Object/Runner가 준비되어 있고, CurrentJob도
        // 스폰 시 RoomManager가 onBeforeSpawned에서 세팅한 값으로 이미 채워져 있다.
        ApplyJobSettings(CurrentJob);
    }


    private void OnEnable()
    {
        if (xrActivateAction != null && xrActivateAction.action != null)
        {
            xrActivateAction.action.Enable();
        }
    }


    private void OnDisable()
    {
        if (xrActivateAction != null && xrActivateAction.action != null)
        {
            xrActivateAction.action.Disable();
        }
    }


    private void Update()
    {
        // 아직 Fusion에 스폰되지 않은 인스턴스(예: 씬에 직접 남아있는 옛날 오브젝트)라면
        // Object가 null이라 여기서 죽는다 - 안전하게 무시한다.
        if (Object == null) return;

        // 내 캐릭터(입력 권한을 가진 클라이언트)만 입력에 반응한다.
        if (!Object.HasInputAuthority) return;

        // PC 테스트: 마우스 왼쪽 클릭. VR: 컨트롤러 트리거(Activate 액션이 연결된 경우).
        bool mouseFire = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool xrFire = xrActivateAction != null && xrActivateAction.action != null
            && xrActivateAction.action.WasPressedThisFrame();

        if (mouseFire || xrFire)
        {
            Attack();
        }
    }


    public void Attack()
    {
        if (Time.time <
            lastAttackTime + builderCooldown)
        {
            return;
        }

        switch (CurrentJob)
        {
            case PlayerJob.Police:
                lastAttackTime = Time.time;
                break;

            case PlayerJob.Firefighter:
                lastAttackTime = Time.time;
                break;

            case PlayerJob.Chef:
                lastAttackTime = Time.time;
                break;

            case PlayerJob.Builder:
                lastAttackTime = Time.time;

                if (weaponBuilder != null &&
                    !isSwinging)
                {
                    StartCoroutine(
                        ShovelScoopRoutine()
                    );
                }

                break;
        }
    }


    /// <summary>
    /// 삽을 아래로 내렸다가 위로 퍼 올리는 공격 모션이다.
    /// </summary>
    private IEnumerator ShovelScoopRoutine()
    {
        isSwinging = true;

        Transform targetTransform =
            weaponBuilder.transform;

        Transform shovelChild =
            weaponBuilder.transform.Find(
                "Shovel_001"
            );

        if (shovelChild != null)
        {
            targetTransform = shovelChild;
        }

        Vector3 originalPosition =
            targetTransform.localPosition;

        Vector3 originalEuler =
            targetTransform.localEulerAngles;

        Vector3 downPosition =
            originalPosition +
            new Vector3(
                0f,
                -0.2f,
                -0.1f
            );

        Vector3 downEuler =
            originalEuler +
            new Vector3(
                35f,
                0f,
                0f
            );

        Vector3 upPosition =
            originalPosition +
            new Vector3(
                0f,
                0.2f,
                0.15f
            );

        Vector3 upEuler =
            originalEuler +
            new Vector3(
                -25f,
                0f,
                0f
            );

        float elapsed = 0f;
        float durationDownToUp = 0.14f;

        bool hasFired = false;

        while (elapsed < durationDownToUp)
        {
            elapsed += Time.deltaTime;

            float t =
                elapsed / durationDownToUp;

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            targetTransform.localPosition =
                Vector3.Lerp(
                    downPosition,
                    upPosition,
                    smoothT
                );

            targetTransform.localEulerAngles =
                new Vector3(
                    Mathf.LerpAngle(
                        downEuler.x,
                        upEuler.x,
                        smoothT
                    ),
                    originalEuler.y,
                    originalEuler.z
                );

            if (!hasFired && t >= 0.65f)
            {
                SpawnDirtCluster();
                hasFired = true;
            }

            yield return null;
        }

        if (!hasFired)
        {
            SpawnDirtCluster();
        }

        elapsed = 0f;

        float returnDuration = 0.16f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                elapsed / returnDuration;

            targetTransform.localPosition =
                Vector3.Lerp(
                    upPosition,
                    originalPosition,
                    t
                );

            targetTransform.localEulerAngles =
                new Vector3(
                    Mathf.LerpAngle(
                        upEuler.x,
                        originalEuler.x,
                        t
                    ),
                    originalEuler.y,
                    originalEuler.z
                );

            yield return null;
        }

        targetTransform.localPosition =
            originalPosition;

        targetTransform.localEulerAngles =
            originalEuler;

        isSwinging = false;
    }


    /// <summary>
    /// 삽질 한 번에 여러 개의 작은 흙 파편을
    /// 처음부터 부채꼴로 흩뿌린다.
    /// </summary>
    private void SpawnDirtCluster()
    {
        if (dirtParticleSystem != null)
        {
            dirtParticleSystem.Play();
        }

        if (dirtFlashLight != null)
        {
            StartCoroutine(
                FlashDirtLight()
            );
        }

        if (dirtPrefab == null ||
            shovelFirePoint == null)
        {
            Debug.LogWarning(
                "건축가 Dirt Prefab 또는 " +
                "Shovel Fire Point가 비어 있습니다."
            );

            return;
        }

        int shardCount =
            Mathf.Max(
                1,
                dirtShardCount
            );

        // 이번 삽질에서 생성되는 모든 파편이
        // 동일한 피해와 장판 생성 정보를 공유한다.
        DirtShotContext shotContext =
            new DirtShotContext(
                firstShardDamage,
                additionalShardDamage,
                maxShotDamagePerEnemy
            );

        List<Collider> spawnedColliders =
            new List<Collider>();

        float centerIndex =
            (shardCount - 1) * 0.5f;

        for (int i = 0; i < shardCount; i++)
        {
            float normalizedHorizontal =
                centerIndex <= 0f
                    ? 0f
                    : (i - centerIndex) /
                      centerIndex;

            float horizontalAngle =
                normalizedHorizontal *
                horizontalSpreadAngle;

            // 파편 배열이 너무 규칙적으로 보이지 않도록
            // 작은 무작위 각도를 추가한다.
            horizontalAngle +=
                Random.Range(
                    -1.2f,
                    1.2f
                );

            float verticalAngle =
                Random.Range(
                    -verticalSpreadAngle,
                    verticalSpreadAngle
                );

            Quaternion horizontalRotation =
                Quaternion.AngleAxis(
                    horizontalAngle,
                    shovelFirePoint.up
                );

            Quaternion verticalRotation =
                Quaternion.AngleAxis(
                    verticalAngle,
                    shovelFirePoint.right
                );

            Vector3 launchDirection =
                horizontalRotation *
                verticalRotation *
                shovelFirePoint.forward;

            GameObject dirtShard =
                Instantiate(
                    dirtPrefab,
                    shovelFirePoint.position,
                    Random.rotation
                );

            // 같은 모델만 반복되어 보이지 않도록
            // 파편마다 크기를 조금씩 다르게 만든다.
            float minimumScale =
                Mathf.Min(
                    shardScaleMultiplierRange.x,
                    shardScaleMultiplierRange.y
                );

            float maximumScale =
                Mathf.Max(
                    shardScaleMultiplierRange.x,
                    shardScaleMultiplierRange.y
                );

            float scaleMultiplier =
                Random.Range(
                    minimumScale,
                    maximumScale
                );

            dirtShard.transform.localScale *=
                scaleMultiplier;

            DirtProjectile projectile =
                dirtShard.GetComponent<DirtProjectile>();

            if (projectile != null)
            {
                projectile.Initialize(
                    shotContext,
                    nextBuilderProjectileShotId++
                );
            }
            else
            {
                Debug.LogWarning(
                    $"{dirtShard.name}에 " +
                    "DirtProjectile이 없습니다."
                );
            }

            Collider shardCollider =
                dirtShard.GetComponent<Collider>();

            if (shardCollider != null)
            {
                // 같은 삽질에서 만들어진 파편끼리는
                // 서로 충돌하지 않도록 설정한다.
                foreach (
                    Collider previousCollider
                    in spawnedColliders)
                {
                    if (previousCollider != null)
                    {
                        Physics.IgnoreCollision(
                            shardCollider,
                            previousCollider,
                            true
                        );
                    }
                }

                spawnedColliders.Add(
                    shardCollider
                );
            }

            Rigidbody dirtRigidbody =
                dirtShard.GetComponent<Rigidbody>();

            if (dirtRigidbody != null)
            {
                Vector3 impulse =
                    launchDirection.normalized *
                    throwForce +
                    Vector3.up *
                    shardUpwardForce;

                dirtRigidbody.AddForce(
                    impulse,
                    ForceMode.Impulse
                );
            }
        }
    }


    private IEnumerator FlashDirtLight()
    {
        dirtFlashLight.enabled = true;

        yield return new WaitForSeconds(
            0.1f
        );

        dirtFlashLight.enabled = false;
    }


    /// <summary>
    /// 로비에서 고른 직업을 실제로 적용한다.
    /// State Authority(이 캐릭터를 스폰한 본인)만 CurrentJob을 쓸 수 있다.
    /// 값이 바뀌면 OnJobChanged가 자동 호출되어 모든 클라이언트에서 모델/무기가 갱신된다.
    /// </summary>
    public void SetJob(PlayerJob job)
    {
        if (Object.HasStateAuthority)
        {
            CurrentJob = job;
        }
    }


    private void OnJobChanged()
    {
        ApplyJobSettings(CurrentJob);
    }


    private void ApplyJobSettings(PlayerJob job)
    {
        DisableAllObjects();

        switch (job)
        {
            case PlayerJob.Police:
                if (modelPolice != null)
                {
                    modelPolice.SetActive(true);
                }

                if (weaponPolice != null)
                {
                    weaponPolice.SetActive(true);
                }

                break;

            case PlayerJob.Firefighter:
                if (modelFirefighter != null)
                {
                    modelFirefighter.SetActive(true);
                }

                if (weaponFirefighter != null)
                {
                    weaponFirefighter.SetActive(true);
                }

                break;

            case PlayerJob.Chef:
                if (modelChef != null)
                {
                    modelChef.SetActive(true);
                }

                if (weaponChef != null)
                {
                    weaponChef.SetActive(true);
                }

                break;

            case PlayerJob.Builder:
                if (modelBuilder != null)
                {
                    modelBuilder.SetActive(true);
                }

                if (weaponBuilder != null)
                {
                    weaponBuilder.SetActive(true);
                }

                break;
        }
    }


    private void DisableAllObjects()
    {
        if (modelPolice != null)
        {
            modelPolice.SetActive(false);
        }

        if (modelFirefighter != null)
        {
            modelFirefighter.SetActive(false);
        }

        if (modelChef != null)
        {
            modelChef.SetActive(false);
        }

        if (modelBuilder != null)
        {
            modelBuilder.SetActive(false);
        }

        if (weaponPolice != null)
        {
            weaponPolice.SetActive(false);
        }

        if (weaponFirefighter != null)
        {
            weaponFirefighter.SetActive(false);
        }

        if (weaponChef != null)
        {
            weaponChef.SetActive(false);
        }

        if (weaponBuilder != null)
        {
            weaponBuilder.SetActive(false);
        }
    }


    private void OnValidate()
    {
        throwForce =
            Mathf.Max(0f, throwForce);

        builderCooldown =
            Mathf.Max(0.01f, builderCooldown);

        dirtShardCount =
            Mathf.Clamp(
                dirtShardCount,
                1,
                12
            );

        horizontalSpreadAngle =
            Mathf.Clamp(
                horizontalSpreadAngle,
                0f,
                30f
            );

        verticalSpreadAngle =
            Mathf.Clamp(
                verticalSpreadAngle,
                0f,
                20f
            );

        shardUpwardForce =
            Mathf.Max(
                0f,
                shardUpwardForce
            );

        shardScaleMultiplierRange.x =
            Mathf.Max(
                0.1f,
                shardScaleMultiplierRange.x
            );

        shardScaleMultiplierRange.y =
            Mathf.Max(
                0.1f,
                shardScaleMultiplierRange.y
            );

        firstShardDamage =
            Mathf.Max(
                0f,
                firstShardDamage
            );

        additionalShardDamage =
            Mathf.Max(
                0f,
                additionalShardDamage
            );

        maxShotDamagePerEnemy =
            Mathf.Max(
                firstShardDamage,
                maxShotDamagePerEnemy
            );
    }
}
