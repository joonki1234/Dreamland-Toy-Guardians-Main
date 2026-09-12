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

    /// <summary>
    /// 로비에서 고른 개인별 PC/VR 플레이 모드. 기본값은 PlayMode.VR(0)이라
    /// 기존 씬/캐릭터를 그대로 써도 동작이 바뀌지 않는다.
    /// PC를 고르면 팔 IK 없이 무기가 카메라 자식으로 고정된 예전(2026-08-20 무렵) 방식으로 동작한다.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnPlayModeChanged))]
    public PlayMode CurrentPlayMode { get; set; }

    /// <summary>내 화면(입력 권한 보유)이면서 PC 모드를 골랐는지 여부.</summary>
    private bool IsLocalPcMode =>
        Object != null && Object.HasInputAuthority && CurrentPlayMode == PlayMode.PC;


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


    [Header("상대방 시점 무기 위치 보정 (RightHandGripReference 기준 로컬 오프셋)")]
    [Tooltip(
        "무기 프리팹마다 원래 만들어진 기준점(피벗) 위치가 제각각이라, " +
        "손 위치(RightHandGripReference)에 그냥 딱 붙이면 무기마다 손 안에 " +
        "파묻히거나(안 보임) 엉뚱한 곳에 떠 보일 수 있다. 유니티 에디터에서 " +
        "Play 하면서 이 값을 조금씩 바꿔보고, 다른 사람 화면(또는 2번째 캐릭터로 " +
        "접속해서)에서 자연스럽게 손에 쥔 것처럼 보일 때까지 맞추면 된다.")]
    public Vector3 weaponPoliceGripOffset = Vector3.zero;
    public Vector3 weaponPoliceGripRotationOffset = Vector3.zero;

    [Header("로컬 VR Police Grip (Right Controller 기준)")]
    [Tooltip("로컬 Police 총 전용 위치 보정. 원격 표시용 weaponPoliceGripOffset과 좌표 기준을 공유하지 않습니다.")]
    [SerializeField]
    private Vector3 localVrPoliceGripPosition = Vector3.zero;

    [Tooltip("로컬 Police 총 전용 회전 보정(Euler). (0,0,0)이면 기존 GripPoint 방향을 LocalWeaponAnchor에 정렬합니다.")]
    [SerializeField]
    private Vector3 localVrPoliceGripRotation = Vector3.zero;

    [Tooltip("기존 직렬화 참조 보존용. LocalWeaponAnchor는 Rig_IK 아래 독립적으로 생성합니다.")]
    [SerializeField]
    private Transform policeWeaponAnchor;

    public Vector3 weaponFirefighterGripOffset = Vector3.zero;
    public Vector3 weaponFirefighterGripRotationOffset = Vector3.zero;

    public Vector3 weaponChefGripOffset = Vector3.zero;
    public Vector3 weaponChefGripRotationOffset = Vector3.zero;

    public Vector3 weaponBuilderGripOffset = Vector3.zero;
    public Vector3 weaponBuilderGripRotationOffset = Vector3.zero;


    [Header("PC 모드 무기 위치 (내 화면 전용, Camera 기준 로컬 오프셋)")]
    [Tooltip(
        "PlayMode.PC를 고른 플레이어 본인 화면에서만 쓰인다. VR 손 IK 대신 " +
        "예전(2026-08-20 무렵) 시스템처럼 무기를 카메라의 자식으로 그대로 붙이고, " +
        "이 오프셋으로 화면 안에 자연스럽게 보이도록 위치를 잡는다. " +
        "무기마다 피벗이 달라 값 튜닝이 필요할 수 있다.")]
    public Vector3 weaponPolicePcOffset = new Vector3(0.25f, -0.2f, 0.45f);
    public Vector3 weaponPolicePcRotationOffset = Vector3.zero;

    public Vector3 weaponFirefighterPcOffset = new Vector3(0.25f, -0.2f, 0.45f);
    public Vector3 weaponFirefighterPcRotationOffset = Vector3.zero;

    public Vector3 weaponChefPcOffset = new Vector3(0.25f, -0.2f, 0.45f);
    public Vector3 weaponChefPcRotationOffset = Vector3.zero;

    public Vector3 weaponBuilderPcOffset = new Vector3(0.25f, -0.2f, 0.45f);
    public Vector3 weaponBuilderPcRotationOffset = Vector3.zero;


    [Header("건축가(Builder) 흙 발사 기본 설정")]
    public GameObject dirtPrefab;

    public Transform shovelFirePoint;

    public ParticleSystem dirtParticleSystem;

    public Light dirtFlashLight;

    [Tooltip("각 흙 파편을 앞으로 발사하는 힘")]
    public float throwForce = 32f;

    [Tooltip("건축가 공격 쿨타임")]
    public float builderCooldown = 0.5f;

    [Tooltip("삽질 시 재생할 효과음. 비워두면 Resources/SFX/Builder/dirt_throw를 자동으로 불러온다.")]
    public AudioClip dirtThrowSfx;

    [Range(0f, 1f)]
    public float dirtThrowVolume = 0.35f;

    private static AudioClip cachedDirtThrowSfx;
    private const string DirtThrowSfxResourcePath = "SFX/Builder/dirt_throw";


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

    [Tooltip("화면 중앙(크로스헤어) 기준으로 흙을 던질 최대 거리/레이어. 산탄 부채꼴의 중심 방향 계산에 쓰인다.")]
    public float dirtAimDistance = 20f;
    public LayerMask dirtAimMask = ~0;

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


    [Header("XR 컨트롤러 입력")]
    [Tooltip(
        "XRI Default Input Actions의 'XRI Right Interaction/Activate' " +
        "액션을 연결하면 VR 컨트롤러 트리거로 공격할 수 있습니다.")]
    [SerializeField]
    private InputActionReference xrActivateAction;

    [SerializeField]
    private InputActionAsset xrInputActionAsset;


    private float lastAttackTime = -999f;
    private bool isSwinging;
    private InputAction xrActivateInput;
    private bool firefighterTriggerHeld;

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
        Application.onBeforeRender += ApplyLocalWeaponAnchorPoseBeforeRender;
        if (localWeaponAnchor != null) localWeaponAnchor.gameObject.SetActive(true);
        xrActivateInput = ResolveXrActivateAction();
        if (xrActivateInput != null)
        {
            xrActivateInput.Enable();
        }
    }


    private void OnDisable()
    {
        Application.onBeforeRender -= ApplyLocalWeaponAnchorPoseBeforeRender;
        if (localWeaponAnchor != null) localWeaponAnchor.gameObject.SetActive(false);
        if (firefighterTriggerHeld)
        {
            // 다른 클라이언트에도 물줄기가 멈췄다는 걸 알려야 하지만, 컴포넌트가
            // 비활성화/파괴되는 시점이라 RPC 전송이 안전하지 않을 수 있다 -
            // 최소한 내 화면에서는 즉시 멈추고, 네트워크 전파는 시도만 한다.
            GetComponentInChildren<FireHoseController>(true)?.StopWater();

            if (Object != null && Object.IsValid && Object.HasInputAuthority)
            {
                RPC_SetFirefighterWaterActive(false);
            }

            firefighterTriggerHeld = false;
        }

        if (xrActivateInput != null)
        {
            xrActivateInput.Disable();
        }
    }


    private void Update()
    {
        // 아직 Fusion에 스폰되지 않은 인스턴스(예: 씬에 직접 남아있는 옛날 오브젝트)라면
        // Object가 null이라 여기서 죽는다 - 안전하게 무시한다.
        if (Object == null) return;

        // 내 캐릭터(입력 권한을 가진 클라이언트)만 입력에 반응한다.
        if (!Object.HasInputAuthority) return;

#if UNITY_EDITOR
        PollEditorJobDebugInput();
#endif

        bool attackPressed;
        bool attackPressedThisFrame;

        if (CurrentPlayMode == PlayMode.PC)
        {
            // PC 모드: 시뮬레이터의 왼손/오른손 조작(스페이스바 등) 없이,
            // 그냥 마우스 왼쪽 클릭만으로 바로 발사되게 한다.
            attackPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            attackPressedThisFrame = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
        else
        {
            xrActivateInput = ResolveXrActivateAction();

            // Quest와 Editor XR Device Simulator 모두 같은 XRI 오른손 Activate 액션을 사용한다.
            attackPressed = xrActivateInput != null && xrActivateInput.IsPressed();
            attackPressedThisFrame = xrActivateInput != null && xrActivateInput.WasPressedThisFrame();
        }

        if (CurrentJob == PlayerJob.Firefighter)
        {
            if (attackPressed && !firefighterTriggerHeld)
            {
                RPC_SetFirefighterWaterActive(true);
            }
            else if (!attackPressed && firefighterTriggerHeld)
            {
                RPC_SetFirefighterWaterActive(false);
            }

            firefighterTriggerHeld = attackPressed;
        }

        if (attackPressedThisFrame && CurrentJob != PlayerJob.Firefighter)
        {
            Attack();
        }
    }

    private InputAction ResolveXrActivateAction()
    {
        if (xrActivateAction != null && xrActivateAction.action != null)
        {
            return xrActivateAction.action;
        }

        return xrInputActionAsset != null
            ? xrInputActionAsset.FindAction("XRI Right Interaction/Activate", false)
            : null;
    }


#if UNITY_EDITOR
    /// <summary>
    /// 게임 씬을 직접 실행했을 때 로컬 플레이어의 직업을 빠르게 바꾸는 Editor 전용 입력입니다.
    /// 실제 변경은 기존 SetJob을 거쳐 Fusion State Authority 규칙을 그대로 따릅니다.
    /// </summary>
    private void PollEditorJobDebugInput()
    {
        if (Keyboard.current == null || !Object.HasStateAuthority)
        {
            return;
        }

        PlayerJob debugJob;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            debugJob = PlayerJob.Police;
        }
        else if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            debugJob = PlayerJob.Firefighter;
        }
        else if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            debugJob = PlayerJob.Chef;
        }
        else if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            debugJob = PlayerJob.Builder;
        }
        else
        {
            return;
        }

        SetJob(debugJob);
        Debug.Log($"[Job Debug/Editor] Local Player → {debugJob}", this);
    }
#endif


    public void Attack()
    {
        if (Time.time <
            lastAttackTime + builderCooldown)
        {
            return;
        }

        lastAttackTime = Time.time;

        // 예전에는 각 무기 컨트롤러(GunController 등)를 이 클라이언트에서만
        // 직접 Instantiate()했다 - 그래서 총알/이펙트/모션이 쏜 사람 화면에만
        // 보이고 다른 플레이어에게는 전혀 보이지 않았다. RPC로 모든 클라이언트에
        // "이 직업이 공격했다"를 알려서 각자 자기 화면에 있는 같은 캐릭터의
        // 무기 컴포넌트를 똑같이 실행하게 한다.
        RPC_PlayAttackEffect(CurrentJob);
    }


    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayAttackEffect(PlayerJob job)
    {
        // 이 RPC 코드는 모든 클라이언트에서 똑같이 실행된다. 하지만
        // Object.HasInputAuthority는 클라이언트마다 다르게 평가된다 - 실제로
        // 쏜 사람 화면에서만 true. 그래서 이 값으로 "진짜 피해를 줄지"를
        // 갈라서, 각자 화면에 보여주기용 총알/음식/흙이 생겨도 피해가
        // 인원수만큼 중복으로 들어가는 일이 없게 한다.
        bool dealsDamage = Object != null && Object.HasInputAuthority;

        switch (job)
        {
            case PlayerJob.Police:
                weaponPolice?.GetComponentInChildren<GunController>(true)?.TriggerShoot(dealsDamage);
                break;

            case PlayerJob.Firefighter:
                weaponFirefighter?.GetComponentInChildren<FireHoseController>(true)?.StartWater();
                break;

            case PlayerJob.Chef:
                weaponChef?.GetComponentInChildren<ChefWeaponController>(true)?.TriggerAttack(dealsDamage);
                break;

            case PlayerJob.Builder:
                if (weaponBuilder != null &&
                    !isSwinging)
                {
                    StartCoroutine(
                        ShovelScoopRoutine(dealsDamage)
                    );
                }

                break;
        }
    }


    private PlayerJobSkillController jobSkillController;
    public bool SkillTutorialCompleted { get; private set; }
    private DreamGuardians.DreamEnemySpawner skillTutorialSpawner;

    public void RequestTutorialSkillTarget(Vector3 groundPosition)
    {
        if (Object != null && Object.IsValid && Object.HasInputAuthority)
            RPC_RequestTutorialSkillTarget(groundPosition);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_RequestTutorialSkillTarget(Vector3 groundPosition)
    {
        if (!Runner.IsSharedModeMasterClient) return;
        if (skillTutorialSpawner == null)
            skillTutorialSpawner = FindAnyObjectByType<DreamGuardians.DreamEnemySpawner>();
        if (skillTutorialSpawner == null)
        {
            Debug.LogError("[SkillTutorial] DreamEnemySpawner reference is missing.", this);
            return;
        }
        skillTutorialSpawner.SpawnSkillTutorialTarget(Object.InputAuthority, groundPosition);
    }

    public void ReportTutorialSkillUsed()
    {
        if (Object != null && Object.IsValid && Object.HasInputAuthority)
            RPC_ReportTutorialSkillUsed();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_ReportTutorialSkillUsed()
    {
        SkillTutorialCompleted = true;
    }


    /// <summary>
    /// PlayerJobSkillController(직업별 P키/Shift+X 스킬)가 호출하는 진입점이다.
    /// PlayerJobSkillController는 일부러 일반 MonoBehaviour로 남겨뒀기 때문에
    /// (같은 프리팹에 NetworkBehaviour를 새로 추가하면 Fusion이 프리팹을
    /// 다시 bake해야 하는데, 코드만 수정하는 이 환경에서는 그게 안 되어
    /// 매칭/스폰이 깨진다), 이미 정상적으로 동작 중인 PlayerJobController
    /// (NetworkBehaviour)가 대신 RPC를 보낸다.
    /// </summary>
    public void RequestJobSkillExecute(PlayerJob job)
    {
        RPC_PlayJobSkillEffect(job);
    }


    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayJobSkillEffect(PlayerJob job)
    {
        // RPC_PlayAttackEffect와 동일한 패턴: 모든 클라이언트에서 똑같이
        // 실행되지만 Object.HasInputAuthority는 실제로 스킬을 쓴 사람
        // 화면에서만 true라서, 이 값으로 "진짜 피해를 줄지"를 가른다.
        bool dealsDamage = Object != null && Object.HasInputAuthority;

        if (jobSkillController == null)
        {
            jobSkillController = GetComponent<PlayerJobSkillController>();
        }

        jobSkillController?.ExecuteSkillLocally(job, dealsDamage);
    }


    /// <summary>
    /// 소방관의 물줄기는 (한 번 쏘고 끝나는 공격이 아니라) 누르고 있는 동안
    /// 계속 나오는 지속 효과라 RPC_PlayAttackEffect와 분리했다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_SetFirefighterWaterActive(bool active)
    {
        FireHoseController hose = weaponFirefighter?.GetComponentInChildren<FireHoseController>(true);

        if (hose == null)
        {
            return;
        }

        if (active)
        {
            hose.StartWater();
        }
        else
        {
            hose.StopWater();
        }
    }


    /// <summary>
    /// 삽을 아래로 내렸다가 위로 퍼 올리는 공격 모션이다.
    /// </summary>
    private IEnumerator ShovelScoopRoutine(bool dealsDamage)
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
                SpawnDirtCluster(dealsDamage);
                hasFired = true;
            }

            yield return null;
        }

        if (!hasFired)
        {
            SpawnDirtCluster(dealsDamage);
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
    /// 화면 중앙(크로스헤어) 기준 조준 방향을 구한다. 카메라를 못 찾으면
    /// 기존처럼 삽이 향한 방향을 그대로 쓴다.
    /// </summary>
    private Vector3 ComputeDirtAimDirection()
    {
        if (cachedLocalPlayerCamera == null)
        {
            cachedLocalPlayerCamera = GetComponentInChildren<Camera>(true);
        }

        if (cachedLocalPlayerCamera == null || shovelFirePoint == null)
        {
            return shovelFirePoint != null ? shovelFirePoint.forward : Vector3.forward;
        }

        Vector3 rayOrigin = cachedLocalPlayerCamera.transform.position;
        Vector3 rayDirection = cachedLocalPlayerCamera.transform.forward;

        Vector3 targetPoint = Physics.Raycast(rayOrigin, rayDirection, out RaycastHit camHit, dirtAimDistance, dirtAimMask)
            ? camHit.point
            : rayOrigin + rayDirection * dirtAimDistance;

        return (targetPoint - shovelFirePoint.position).normalized;
    }


    /// <summary>
    /// 삽질 한 번에 여러 개의 작은 흙 파편을
    /// 처음부터 부채꼴로 흩뿌린다.
    /// </summary>
    private void SpawnDirtCluster(bool dealsDamage)
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

        PlayDirtThrowSfx();

        Vector3 aimDirection = ComputeDirtAimDirection();

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
                    Vector3.up
                );

            Quaternion verticalRotation =
                Quaternion.AngleAxis(
                    verticalAngle,
                    Vector3.Cross(Vector3.up, aimDirection)
                );

            Vector3 launchDirection =
                horizontalRotation *
                verticalRotation *
                aimDirection;

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
                if (dealsDamage)
                {
                    projectile.Initialize(
                        shotContext,
                        nextBuilderProjectileShotId++
                    );
                }
                else
                {
                    // 다른 클라이언트에서 재생되는 보여주기용 흙 파편 - 충돌 콜백을
                    // 꺼서 적에게 중복으로 피해가 들어가지 않게 한다.
                    projectile.enabled = false;
                }
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


    private void PlayDirtThrowSfx()
    {
        AudioClip clip = dirtThrowSfx;

        if (clip == null)
        {
            if (cachedDirtThrowSfx == null)
            {
                cachedDirtThrowSfx = Resources.Load<AudioClip>(DirtThrowSfxResourcePath);
            }

            clip = cachedDirtThrowSfx;
        }

        if (clip != null && shovelFirePoint != null)
        {
            AudioSource.PlayClipAtPoint(clip, shovelFirePoint.position, dirtThrowVolume);
        }
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


    /// <summary>
    /// 로비에서 고른 PC/VR 플레이 모드를 실제로 적용한다.
    /// State Authority(이 캐릭터를 스폰한 본인)만 바꿀 수 있다.
    /// </summary>
    public void SetPlayMode(PlayMode mode)
    {
        if (Object.HasStateAuthority)
        {
            CurrentPlayMode = mode;
        }
    }


    private void OnJobChanged()
    {
        ApplyJobSettings(CurrentJob);
    }


    private void OnPlayModeChanged()
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
                    AttachPoliceWeaponForViewer();
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
                    AttachWeaponForViewer(weaponFirefighter, weaponFirefighterGripOffset, weaponFirefighterGripRotationOffset);
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
                    AttachWeaponForViewer(weaponChef, weaponChefGripOffset, weaponChefGripRotationOffset);
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
                    AttachWeaponForViewer(weaponBuilder, weaponBuilderGripOffset, weaponBuilderGripRotationOffset);
                }

                break;
        }
    }


    private Transform cachedHandGripAnchor;
    private bool triedResolveHandGripAnchor;

    private void AttachPoliceWeaponForViewer()
    {
        bool isLocalVrWeapon = Object != null && Object.HasInputAuthority;

        if (isLocalVrWeapon && IsLocalPcMode)
        {
            // PC 모드는 1인칭 시점이다 - 무기를 손 IK 앵커가 아니라 카메라의
            // 자식으로 그대로 붙여서, 시점이 어느 방향으로 돌아가든 항상 화면
            // 우측 같은 자리에 무기가 고정되어 보이게 한다(2026-08-20 무렵
            // 시스템과 동일). 이걸 손 앵커(AttachWeaponToAnchor)로 바꾸면
            // VR 손 IK가 없는 PC 모드에서는 앵커가 안정적으로 움직이지 않아
            // 시점을 돌릴 때마다 무기가 엉뚱하게 흔들리거나 안 따라온다.
            AttachWeaponToPcCamera(weaponPolice, weaponPolicePcOffset, weaponPolicePcRotationOffset);
            return;
        }

        if (isLocalVrWeapon)
        {
            AttachLocalWeapon(weaponPolice,
                localVrPoliceGripPosition, localVrPoliceGripRotation,
                weaponPoliceGripOffset, weaponPoliceGripRotationOffset);
            return;
        }

        AttachWeaponForViewer(
            weaponPolice,
            weaponPoliceGripOffset,
            weaponPoliceGripRotationOffset);
    }

    // Local weapons inherit the controller pose in Player space; remote attachment stays unchanged.
    private void AttachWeaponForViewer(GameObject weapon, Vector3 positionOffset, Vector3 rotationOffsetEuler)
    {
        if (weapon == null || Object == null)
        {
            return;
        }

        if (Object.HasInputAuthority)
        {
            if (IsLocalPcMode)
            {
                // PC 모드는 1인칭 시점이라 무기를 카메라의 자식으로 붙인다 -
                // 시점이 돌아가도 항상 같은 화면 위치(우측)에 무기가 고정된다.
                // (손 앵커로 붙이면 VR IK가 없는 PC 모드에서 앵커가 제대로
                // 움직이지 않아 시점 회전에 무기가 안 따라오거나 흔들린다.)
                Vector3 pcOffset = weapon == weaponFirefighter ? weaponFirefighterPcOffset
                    : weapon == weaponChef ? weaponChefPcOffset : weaponBuilderPcOffset;
                Vector3 pcRotationOffset = weapon == weaponFirefighter ? weaponFirefighterPcRotationOffset
                    : weapon == weaponChef ? weaponChefPcRotationOffset : weaponBuilderPcRotationOffset;
                AttachWeaponToPcCamera(weapon, pcOffset, pcRotationOffset);
                return;
            }

            LocalGripAdjustment adjustment = weapon == weaponFirefighter ? localFirefighterGrip
                : weapon == weaponChef ? localChefGrip : localBuilderGrip;
            AttachLocalWeapon(weapon, adjustment.position, adjustment.rotation,
                positionOffset, rotationOffsetEuler);
            return;
        }

        AttachWeaponToAnchor(weapon, ResolveHandGripAnchor(), positionOffset, rotationOffsetEuler);
    }

    private Camera cachedLocalPlayerCamera;

    /// <summary>
    /// PC 모드 전용: VR 손 IK/컨트롤러 트래킹을 전혀 거치지 않고, 예전(2026-08-20 무렵)
    /// 시스템처럼 무기를 그냥 카메라의 자식으로 붙인다. 카메라가 시점을 따라 회전하면
    /// 무기도 그대로 같이 따라온다 - 매 프레임 갱신이 필요 없는 단순한 부모-자식 관계다.
    /// </summary>
    private void AttachWeaponToPcCamera(GameObject weapon, Vector3 positionOffset, Vector3 rotationOffsetEuler)
    {
        if (weapon == null)
        {
            return;
        }

        if (cachedLocalPlayerCamera == null)
        {
            cachedLocalPlayerCamera = GetComponentInChildren<Camera>(true);
        }

        if (cachedLocalPlayerCamera == null)
        {
            Debug.LogWarning(
                "[PlayerJobController] PC 모드 무기를 붙일 Camera를 찾지 못했습니다.",
                this);
            return;
        }

        Transform cameraTransform = cachedLocalPlayerCamera.transform;

        if (weapon.transform.parent != cameraTransform)
        {
            weapon.transform.SetParent(cameraTransform, false);
            weapon.transform.localScale = Vector3.one;
        }

        weapon.transform.localPosition = positionOffset;
        weapon.transform.localRotation = Quaternion.Euler(rotationOffsetEuler);
    }

    [System.Serializable]
    private sealed class LocalGripAdjustment
    {
        public Vector3 position = Vector3.zero;
        public Vector3 rotation = Vector3.zero;
    }

    [Header("Local controller grip adjustments (metres / Euler degrees)")]
    [SerializeField] private LocalGripAdjustment localFirefighterGrip = new LocalGripAdjustment();
    [SerializeField] private LocalGripAdjustment localChefGrip = new LocalGripAdjustment();
    [SerializeField] private LocalGripAdjustment localBuilderGrip = new LocalGripAdjustment();
    private readonly Dictionary<Transform, Vector3> localWeaponWorldScales = new Dictionary<Transform, Vector3>();

    private Transform localWeaponAnchor;
    private VRHandTargetFollower localTracking;
    private GameObject pendingLocalWeapon;
    private Vector3 pendingLocalPosition;
    private Vector3 pendingLocalRotation;
    private Vector3 pendingRemotePosition;
    private Vector3 pendingRemoteRotation;
    private GameObject activeLocalWeapon;
    private bool isLocalWeaponTrackingActive;
#if UNITY_EDITOR
#pragma warning disable CS0618 // Use the project's Classic XR Device Simulator.
    private UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator editorWeaponSimulator;
#pragma warning restore CS0618
    private bool editorWeaponContinuity;
    private bool editorWeaponBaselineCaptured;
    private int editorWeaponBaselineFrame;
    private Vector3 editorControllerStartPosition;
    private Quaternion editorControllerStartRotation;
    private Vector3 editorAnchorStartPosition;
    private Quaternion editorAnchorStartRotation;
#endif
    private Vector3 alignedLocalWeaponPosition;
    private Quaternion alignedLocalWeaponRotation;

    private void LateUpdate()
    {
        if (IsLocalPcMode)
        {
            // PC 모드는 무기가 카메라의 단순한 자식이라 VR 컨트롤러 트래킹/앵커
            // 갱신이 전혀 필요 없다 - 매 프레임 아무것도 하지 않는다.
            if (localWeaponAnchor != null) localWeaponAnchor.gameObject.SetActive(false);
            return;
        }

        UpdateLocalWeaponTrackingMode();
        if (localWeaponAnchor != null)
            localWeaponAnchor.gameObject.SetActive(Object != null && Object.HasInputAuthority);
        // XR Origin can appear after Fusion Spawned. Retry only the pending attachment.
        if (pendingLocalWeapon != null && Object != null && Object.HasInputAuthority)
            AttachLocalWeapon(pendingLocalWeapon, pendingLocalPosition, pendingLocalRotation,
                pendingRemotePosition, pendingRemoteRotation);

        ApplyLocalWeaponAnchorPose();
    }

    private void OnDestroy()
    {
        // Clean up the runtime anchor when this component is destroyed.
        if (localWeaponAnchor != null) Destroy(localWeaponAnchor.gameObject);
    }

    private void AttachLocalWeapon(GameObject weapon, Vector3 position, Vector3 rotation,
        Vector3 remotePosition, Vector3 remoteRotation)
    {
        if (Object == null || !Object.HasInputAuthority || weapon == null) return;
        pendingLocalWeapon = weapon;
        pendingLocalPosition = position;
        pendingLocalRotation = rotation;
        pendingRemotePosition = remotePosition;
        pendingRemoteRotation = remoteRotation;
        if (localTracking == null) localTracking = GetComponent<VRHandTargetFollower>();
        Transform weaponTarget = localTracking != null ? localTracking.ResolveLocalWeaponTarget() : null;
        if (weaponTarget == null || ResolveHandGripAnchor() == null) return;

        AlignLocalWeaponToController(weapon, position, rotation);
        activeLocalWeapon = weapon;
        ApplyLocalWeaponAnchorPose();
        pendingLocalWeapon = null;
    }

    private void AlignLocalWeaponToController(GameObject weapon, Vector3 position, Vector3 rotation)
    {
        Transform weaponTarget = localTracking.ResolveLocalWeaponTarget();
        if (weaponTarget == null) return;

        Transform grip = ResolveWeaponGrip(weapon);
        if (grip == null) return;
        Transform weaponTransform = weapon.transform;
        if (!localWeaponWorldScales.TryGetValue(weaponTransform, out Vector3 worldScale))
        {
            worldScale = weaponTransform.lossyScale;
            localWeaponWorldScales.Add(weaponTransform, worldScale);
        }
        Quaternion gripRotationInWeapon = Quaternion.Inverse(weaponTransform.rotation) * grip.rotation;

        if (localWeaponAnchor == null)
            localWeaponAnchor = new GameObject("LocalWeaponAnchor").transform;
        localWeaponAnchor.SetParent(ResolveHandGripAnchor().parent, false);
        localWeaponAnchor.localScale = Vector3.one;
        localWeaponAnchor.SetPositionAndRotation(
            weaponTarget.TransformPoint(position),
            weaponTarget.rotation * Quaternion.Euler(rotation));

        weaponTransform.SetParent(localWeaponAnchor, false);
        Vector3 anchorScale = localWeaponAnchor.lossyScale;
        weaponTransform.localScale = new Vector3(worldScale.x / anchorScale.x,
            worldScale.y / anchorScale.y, worldScale.z / anchorScale.z);
        weaponTransform.localRotation = Quaternion.Inverse(gripRotationInWeapon);
        weaponTransform.position += localWeaponAnchor.position - grip.position;

        // Keep the existing local grip correction on the weapon, so the independent
        // anchor itself can copy HandTarget_R's world pose without an extra offset.
        Vector3 alignedWorldPosition = weaponTransform.position;
        Quaternion alignedWorldRotation = weaponTransform.rotation;
        localWeaponAnchor.SetPositionAndRotation(weaponTarget.position, weaponTarget.rotation);
        weaponTransform.SetPositionAndRotation(alignedWorldPosition, alignedWorldRotation);
        alignedLocalWeaponPosition = weaponTransform.localPosition;
        alignedLocalWeaponRotation = weaponTransform.localRotation;
    }

    private void ApplyLocalWeaponAnchorPoseBeforeRender()
    {
        if (IsLocalPcMode) return;
#if UNITY_EDITOR
        // Capture after the follower's LateUpdate, without changing the displayed
        // anchor on the activation frame. Both baselines use the same Rig_IK space.
        if (editorWeaponContinuity && isLocalWeaponTrackingActive && !editorWeaponBaselineCaptured &&
            Object != null && Object.HasInputAuthority && activeLocalWeapon != null &&
            localWeaponAnchor != null && localTracking != null)
        {
            Transform target = localTracking.ResolveLocalWeaponTarget();
            Transform space = localWeaponAnchor.parent;
            if (target == null || space == null) return;
            editorControllerStartPosition = space.InverseTransformPoint(target.position);
            editorControllerStartRotation = Quaternion.Inverse(space.rotation) * target.rotation;
            editorAnchorStartPosition = localWeaponAnchor.localPosition;
            editorAnchorStartRotation = localWeaponAnchor.localRotation;
            editorWeaponBaselineFrame = Time.frameCount;
            editorWeaponBaselineCaptured = true;
        }
#endif
        ApplyLocalWeaponAnchorPose();
    }

    private void ApplyLocalWeaponAnchorPose()
    {
        if (IsLocalPcMode) return;

        // Also run before rendering so this anchor sees HandTarget_R after all LateUpdates.
        if (Object == null || !Object.HasInputAuthority || activeLocalWeapon == null ||
            localWeaponAnchor == null || localTracking == null) return;

        UpdateLocalWeaponTrackingMode();
        if (isLocalWeaponTrackingActive)
        {
            Transform weaponTarget = localTracking.ResolveLocalWeaponTarget();
            if (weaponTarget == null) return;
#if UNITY_EDITOR
            if (editorWeaponContinuity)
            {
                if (!editorWeaponBaselineCaptured || editorWeaponBaselineFrame == Time.frameCount) return;
                Transform space = localWeaponAnchor.parent;
                if (space == null) return;
                Vector3 position = space.InverseTransformPoint(weaponTarget.position);
                Quaternion rotation = Quaternion.Inverse(space.rotation) * weaponTarget.rotation;
                localWeaponAnchor.SetLocalPositionAndRotation(
                    editorAnchorStartPosition + (position - editorControllerStartPosition),
                    (rotation * Quaternion.Inverse(editorControllerStartRotation)) * editorAnchorStartRotation);
                return;
            }
#endif
            localWeaponAnchor.SetPositionAndRotation(
                weaponTarget.position, weaponTarget.rotation);
            return;
        }

        Transform defaultReference = ResolveHandGripAnchor();
        if (defaultReference == null) return;

        // Preserve the existing default weapon pose while keeping its grip alignment fixed.
        Quaternion anchorRotation = defaultReference.rotation * Quaternion.Euler(pendingRemoteRotation)
            * Quaternion.Inverse(alignedLocalWeaponRotation);
        Vector3 weaponPosition = defaultReference.TransformPoint(pendingRemotePosition);
        localWeaponAnchor.SetPositionAndRotation(
            weaponPosition - anchorRotation * Vector3.Scale(
                localWeaponAnchor.lossyScale, alignedLocalWeaponPosition),
            anchorRotation);
    }

    private void UpdateLocalWeaponTrackingMode()
    {
        if (isLocalWeaponTrackingActive || Object == null || !Object.HasInputAuthority) return;

#if UNITY_EDITOR
#pragma warning disable CS0618
        if (editorWeaponSimulator == null)
            editorWeaponSimulator = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
#pragma warning restore CS0618
        if (editorWeaponSimulator != null && editorWeaponSimulator.isActiveAndEnabled)
        {
            // HMD/FPS input can initialize tracking before Space. Keep the idle
            // weapon until the right controller is explicitly selected (hold/toggle).
            if (!editorWeaponSimulator.manipulatingRightController) return;
            editorWeaponContinuity = true;
            isLocalWeaponTrackingActive = true;
            return;
        }
#endif
        // Quest and XR Device Simulator both follow the tracked XR controller.
        // Selecting a device with Space must not switch the weapon pose source.
        foreach (var device in InputSystem.devices)
        {
            if (!(device is UnityEngine.InputSystem.XR.XRController controller) ||
                !controller.added || !controller.isTracked.isPressed) continue;
            foreach (var usage in controller.usages)
            {
                if (usage != CommonUsages.RightHand) continue;
                isLocalWeaponTrackingActive = true;
                return;
            }
        }
    }

    private Transform ResolveWeaponGrip(GameObject weapon)
    {
        Transform existing = FindWeaponTransform(weapon.transform, "GripPoint");
        if (existing != null) return existing;

        Vector3 gripPosition;
        if (weapon == weaponChef)
        {
            ChefWeaponController chef = weapon.GetComponentInChildren<ChefWeaponController>(true);
            if (chef == null || chef.panTransform == null) return null;
            // Read the existing handle definition; do not modify the pan or its attack controller.
            gripPosition = chef.panTransform.TransformPoint(chef.handleOffset);
        }
        else if (weapon == weaponFirefighter)
        {
            Transform nozzle = FindWeaponTransform(weapon.transform, "Nozzle");
            if (nozzle == null) return null;
            gripPosition = nozzle.position;
        }
        else if (weapon == weaponBuilder)
        {
            // The source FBX uses a Cyrillic C in the upper shaft mesh name.
            Transform handle = FindWeaponTransform(weapon.transform, "Shovel_001_С05");
            MeshFilter mesh = handle != null ? handle.GetComponent<MeshFilter>() : null;
            if (mesh == null || mesh.sharedMesh == null)
            {
                Debug.LogError("[PlayerJobController] Builder upper shaft grip mesh missing.", this);
                return null;
            }
            gripPosition = handle.TransformPoint(mesh.sharedMesh.bounds.center);
        }
        else
        {
            Debug.LogError("[PlayerJobController] Police GripPoint missing.", this);
            return null;
        }

        Transform grip = new GameObject("GripPoint").transform;
        grip.SetParent(weapon.transform, false);
        grip.position = gripPosition;
        grip.localRotation = Quaternion.identity;
        return grip;
    }

    private static Transform FindWeaponTransform(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;
        return null;
    }

    // 무기 프리팹마다 원래 만들어질 때의 스케일이 다르다(예: Chef 프라이팬은
    // 자식 메시가 40배로 보정돼 있어서 루트 자체는 0.025). 처음 붙는 시점의
    // 스케일을 기억해뒀다가 그대로 복원해야지, Vector3.one으로 밀어버리면
    // 그 보정이 깨져서 무기가 엉뚱한 크기로 보인다.
    private static readonly Dictionary<Transform, Vector3> originalWeaponLocalScales = new Dictionary<Transform, Vector3>();

    private static void AttachWeaponToAnchor(
        GameObject weapon,
        Transform gripAnchor,
        Vector3 positionOffset,
        Vector3 rotationOffsetEuler)
    {
        if (weapon == null || gripAnchor == null)
        {
            return;
        }

        if (!originalWeaponLocalScales.TryGetValue(weapon.transform, out Vector3 originalScale))
        {
            originalScale = weapon.transform.localScale;
            originalWeaponLocalScales[weapon.transform] = originalScale;
        }

        if (weapon.transform.parent != gripAnchor)
        {
            weapon.transform.SetParent(gripAnchor, false);
            weapon.transform.localScale = originalScale;
        }

        // 무기마다 원래 피벗 위치가 달라서 (0,0,0)만으로는 다 안 맞는다.
        // Inspector에서 잡별 오프셋 값을 조절해서 맞출 수 있다.
        weapon.transform.localPosition = positionOffset;
        weapon.transform.localRotation = Quaternion.Euler(rotationOffsetEuler);
    }


    private Transform ResolveHandGripAnchor()
    {
        if (cachedHandGripAnchor != null)
        {
            return cachedHandGripAnchor;
        }

        if (triedResolveHandGripAnchor)
        {
            return null;
        }

        triedResolveHandGripAnchor = true;

        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            if (candidate != null && candidate.name == "RightHandGripReference")
            {
                cachedHandGripAnchor = candidate;
                break;
            }
        }

        if (cachedHandGripAnchor == null)
        {
            Debug.LogWarning(
                "[PlayerJobController] 'RightHandGripReference'를 찾지 못해 " +
                "무기를 손에 고정하지 못했습니다. 카메라 자식 상태로 남습니다.",
                this);
        }

        return cachedHandGripAnchor;
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
