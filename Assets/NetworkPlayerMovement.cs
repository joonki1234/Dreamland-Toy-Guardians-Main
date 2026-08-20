using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

/// <summary>
/// FPSController.cs를 Photon Fusion 2 Shared Mode 네트워크 이동으로 이식한 버전.
/// 원본의 마우스 시점 회전 + WASD 이동 + 원형 경계 제한 로직을 그대로 유지한다.
///
/// FPSController.cs와 달리 이동은 FixedUpdateNetwork()에서, 즉시 반응해야 하는
/// 카메라 회전만 Update()에서 처리한다. (Runner.DeltaTime을 사용해야 틱 동기화가 맞는다.)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("이동 및 시점 설정")]

    [Tooltip("플레이어 이동 속도")]
    public float moveSpeed = 5f;

    [Tooltip("마우스 회전 감도")]
    public float mouseSensitivity = 0.1f;


    [Header("플레이어 이동 경계")]

    [Tooltip("플레이어 이동 범위를 표시하는 PlayerBoundaryShield 오브젝트")]
    [SerializeField]
    private Transform playerBoundaryShield;

    [Tooltip(
        "플레이어가 보호막 표면에 정확히 붙지 않도록 " +
        "경계 안쪽에 남겨두는 여유 거리")]
    [SerializeField]
    private float boundaryPadding = 0.5f;

    [Tooltip(
        "게임 시작 시 존재하는 네모난 시작 스테이지(길)의 오브젝트 이름. " +
        "이 오브젝트의 실제 렌더러 크기를 기준으로 사각형 이동 제한을 건다. " +
        "나중에 십자가 모양으로 열리는 다른 길들은 여기에 포함되지 않으므로 " +
        "플레이어가 처음부터 그쪽으로 걸어나갈 수 없다.")]
    [SerializeField]
    private string startStageObjectName = "Road_0";

    [Tooltip("시작 스테이지 가장자리에 딱 붙지 않도록 안쪽으로 남겨두는 여유 거리")]
    [SerializeField, Min(0f)]
    private float startStagePadding = 0.6f;


    [Header("로컬 전용 오브젝트 (내 화면에만 필요)")]

    [Tooltip("플레이어 자식의 Camera")]
    [SerializeField]
    private Camera playerCamera;

    [Tooltip("플레이어 자식의 AudioListener")]
    [SerializeField]
    private AudioListener audioListener;


    [Header("발자국 소리")]

    [Tooltip("발소리 사이 간격(초)")]
    [SerializeField, Min(0.05f)]
    private float footstepInterval = 0.45f;

    [SerializeField, Range(0f, 1f)]
    private float footstepVolume = 0.35f;


    private CharacterController _cc;
    private SphereCollider _boundarySphereCollider;
    private TrackedPoseDriver _headTrackedPoseDriver;
    private float _verticalRotation;
    private AudioSource _footstepAudioSource;
    private AudioClip[] _footstepClips;
    private float _footstepTimer;

    private bool _hasStageBounds;
    private float _stageMinX;
    private float _stageMaxX;
    private float _stageMinZ;
    private float _stageMaxZ;


    public override void Spawned()
    {
        _cc = GetComponent<CharacterController>();

        // Assets/Audio/Resources/SFX/Footsteps/ 안의 발소리 클립들을 전부 불러온다.
        _footstepClips = Resources.LoadAll<AudioClip>("SFX/Footsteps");

        _footstepAudioSource = GetComponent<AudioSource>();
        if (_footstepAudioSource == null)
        {
            _footstepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        _footstepAudioSource.playOnAwake = false;
        _footstepAudioSource.spatialBlend = 1f;

        // Player Camera에 TrackedPoseDriver(XR 헤드셋 트래킹)가 붙어 있다면
        // 그쪽이 이미 카메라 회전을 담당하므로, 아래 Update()에서 마우스로 덮어쓰지 않는다.
        if (playerCamera != null)
        {
            _headTrackedPoseDriver = playerCamera.GetComponent<TrackedPoseDriver>();
        }

        if (playerBoundaryShield != null)
        {
            _boundarySphereCollider =
                playerBoundaryShield.GetComponent<SphereCollider>();
        }
        else
        {
            Debug.LogWarning(
                "[NetworkPlayerMovement] Player Boundary Shield가 연결되지 않았습니다.");
        }

        ComputeStartStageBounds();

        // 입력 권한을 가진(=내가 조종하는) 캐릭터만 카메라/오디오리스너를 켠다.
        // 다른 클라이언트 화면에는 남의 카메라가 보이거나 소리가 겹치면 안 되기 때문.
        bool isMine = Object.HasInputAuthority;

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(isMine);
        }
        else
        {
            Debug.LogWarning(
                "[NetworkPlayerMovement] Player Camera가 연결되지 않았습니다.");
        }

        if (audioListener != null)
        {
            audioListener.enabled = isMine;
        }

        if (isMine)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }


    public override void FixedUpdateNetwork()
    {
        // Shared Mode에서는 이 오브젝트의 State Authority(=스폰한 본인)만
        // 실제 이동을 시뮬레이션한다. 그렇지 않으면 다른 클라이언트가 대신
        // 위치를 계산해 버려 캐릭터가 겹치거나 튄다.
        if (!Object.HasStateAuthority) return;

        Vector2 input = ReadMovementInput();

        // 몸통(transform)이 아니라 "지금 보고 있는 방향"(카메라) 기준으로 이동해야 한다.
        // 마우스 모드에서는 좌우 회전이 몸통에도 적용되어 transform.forward로도 어느 정도
        // 맞았지만, XR 헤드트래킹(TrackedPoseDriver)은 카메라만 돌리고 몸통은 그대로 두기
        // 때문에 transform.forward를 쓰면 시점이 바뀌어도 이동 방향이 고정되어 있었다.
        // 카메라의 forward/right를 수평으로 눕혀서(피치 무시) 이동 방향을 계산한다.
        Transform directionSource =
            playerCamera != null ? playerCamera.transform : transform;

        Vector3 forward = directionSource.forward;
        forward.y = 0f;

        Vector3 right = directionSource.right;
        right.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
            right.y = 0f;
        }

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            (forward * input.y + right * input.x).normalized;

        if (direction.sqrMagnitude <= 0f)
        {
            // 멈추면 타이머를 리셋해서, 다시 움직이기 시작하자마자 바로 첫 발소리가 나게 한다.
            _footstepTimer = footstepInterval;
            return;
        }

        Vector3 movement = direction * moveSpeed * Runner.DeltaTime;
        _cc.Move(movement);

        ClampPositionInsideBoundary();
        UpdateFootsteps();
    }


    /// <summary>
    /// 이동 중일 때 일정 간격으로 무작위 발소리를 재생한다.
    /// </summary>
    private void UpdateFootsteps()
    {
        if (_footstepClips == null || _footstepClips.Length == 0 || _footstepAudioSource == null)
        {
            return;
        }

        _footstepTimer += Runner.DeltaTime;

        if (_footstepTimer < footstepInterval)
        {
            return;
        }

        _footstepTimer = 0f;

        AudioClip clip = _footstepClips[Random.Range(0, _footstepClips.Length)];
        _footstepAudioSource.pitch = Random.Range(0.95f, 1.05f);
        _footstepAudioSource.PlayOneShot(clip, footstepVolume);
    }


    private Vector2 ReadMovementInput()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current == null) return input;

        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;

        return input;
    }


    private void Update()
    {
        // 스폰 직후 첫 프레임 등 Fusion이 아직 Object를 연결하기 전에
        // Update()가 먼저 호출될 수 있다 - 안전하게 무시한다.
        if (Object == null) return;

        // 카메라 회전은 네트워크 동기화가 필요 없는 순수 로컬 연출이므로
        // FixedUpdateNetwork가 아닌 일반 Update에서 즉시 처리한다.
        if (!Object.HasInputAuthority) return;

        // XR 헤드셋(TrackedPoseDriver)이 이미 카메라 회전을 담당하고 있다면
        // 마우스 회전 코드가 그 위에 덮어써서 시야가 안 돌아가는 문제가 생긴다 - 건너뛴다.
        if (_headTrackedPoseDriver != null && _headTrackedPoseDriver.enabled) return;

        if (Mouse.current == null) return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue() * mouseSensitivity;

        // 플레이어 몸통 좌우 회전 (NetworkTransform이 이 회전값을 동기화한다)
        transform.Rotate(Vector3.up * mouseDelta.x);

        // 카메라 상하 회전 (로컬 전용, 동기화 불필요)
        _verticalRotation -= mouseDelta.y;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -80f, 80f);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation =
                Quaternion.Euler(_verticalRotation, 0f, 0f);
        }
    }


    /// <summary>
    /// 원본 FPSController.ClampPositionInsideBoundary()와 동일한 로직 + 시작 스테이지
    /// 사각형 제한을 함께 적용한다. CharacterController.Move() 이후 위치를 보정하는
    /// 방식으로 이식했다.
    /// </summary>
    private void ClampPositionInsideBoundary()
    {
        Vector3 position = transform.position;
        bool changed = false;

        if (playerBoundaryShield != null)
        {
            Vector3 boundaryCenter = playerBoundaryShield.position;
            float boundaryRadius = GetBoundaryWorldRadius();
            float allowedRadius = Mathf.Max(0f, boundaryRadius - boundaryPadding);

            Vector2 centerXZ = new Vector2(boundaryCenter.x, boundaryCenter.z);
            Vector2 posXZ = new Vector2(position.x, position.z);
            Vector2 centerToPos = posXZ - centerXZ;

            if (centerToPos.sqrMagnitude > allowedRadius * allowedRadius)
            {
                Vector2 clampedXZ = centerXZ + centerToPos.normalized * allowedRadius;
                position.x = clampedXZ.x;
                position.z = clampedXZ.y;
                changed = true;
            }
        }

        // 시작 스테이지(네모난 길) 바깥으로는 아예 나갈 수 없도록 사각형으로 한 번 더 제한한다.
        // 나중에 십자가 모양으로 열리는 다른 길들은 이 범위 밖이라 여기 걸리면 못 나간다.
        if (_hasStageBounds)
        {
            float clampedX = Mathf.Clamp(position.x, _stageMinX, _stageMaxX);
            float clampedZ = Mathf.Clamp(position.z, _stageMinZ, _stageMaxZ);

            if (!Mathf.Approximately(clampedX, position.x) ||
                !Mathf.Approximately(clampedZ, position.z))
            {
                position.x = clampedX;
                position.z = clampedZ;
                changed = true;
            }
        }

        if (!changed) return;

        // CharacterController는 위치를 직접 대입하기 전에 잠깐 꺼야 텔레포트가 안전하게 적용된다.
        _cc.enabled = false;
        transform.position = position;
        _cc.enabled = true;
    }


    /// <summary>
    /// 씬에서 시작 스테이지(startStageObjectName, 기본 "Road_0") 오브젝트를 찾아
    /// 그 안의 모든 Renderer를 합친 실제 월드 크기로 사각형 이동 제한 범위를 계산한다.
    /// 프리팹은 씬 오브젝트를 직접 참조할 수 없기 때문에, 런타임에 이름으로 찾는 방식을 쓴다.
    /// </summary>
    private void ComputeStartStageBounds()
    {
        if (string.IsNullOrEmpty(startStageObjectName))
        {
            return;
        }

        GameObject stageObject = GameObject.Find(startStageObjectName);

        if (stageObject == null)
        {
            Debug.LogWarning(
                $"[NetworkPlayerMovement] 시작 스테이지 오브젝트 '{startStageObjectName}'를 " +
                "씬에서 찾지 못했습니다. 사각형 이동 제한이 적용되지 않습니다.");
            return;
        }

        Renderer[] renderers = stageObject.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning(
                $"[NetworkPlayerMovement] '{startStageObjectName}'에 Renderer가 없어 " +
                "사각형 이동 제한 범위를 계산할 수 없습니다.");
            return;
        }

        Bounds worldBounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        _stageMinX = worldBounds.min.x + startStagePadding;
        _stageMaxX = worldBounds.max.x - startStagePadding;
        _stageMinZ = worldBounds.min.z + startStagePadding;
        _stageMaxZ = worldBounds.max.z - startStagePadding;

        // 패딩이 너무 크면 범위가 뒤집힐 수 있으니 안전하게 보정한다.
        if (_stageMinX > _stageMaxX)
        {
            float mid = (_stageMinX + _stageMaxX) * 0.5f;
            _stageMinX = _stageMaxX = mid;
        }

        if (_stageMinZ > _stageMaxZ)
        {
            float mid = (_stageMinZ + _stageMaxZ) * 0.5f;
            _stageMinZ = _stageMaxZ = mid;
        }

        _hasStageBounds = true;
    }


    /// <summary>
    /// PlayerBoundaryShield의 실제 월드 반지름을 계산한다.
    /// Unity 기본 Sphere는 원래 반지름이 0.5이므로, Scale이 15라면 실제 반지름은 약 7.5가 된다.
    /// </summary>
    private float GetBoundaryWorldRadius()
    {
        Vector3 scale = playerBoundaryShield.lossyScale;
        float largestHorizontalScale =
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

        if (_boundarySphereCollider != null)
        {
            return _boundarySphereCollider.radius * largestHorizontalScale;
        }

        return largestHorizontalScale * 0.5f;
    }
}
