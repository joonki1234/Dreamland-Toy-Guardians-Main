using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

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


    [Header("로컬 전용 오브젝트 (내 화면에만 필요)")]

    [Tooltip("플레이어 자식의 Camera")]
    [SerializeField]
    private Camera playerCamera;

    [Tooltip("플레이어 자식의 AudioListener")]
    [SerializeField]
    private AudioListener audioListener;


    private CharacterController _cc;
    private SphereCollider _boundarySphereCollider;
    private float _verticalRotation;


    public override void Spawned()
    {
        _cc = GetComponent<CharacterController>();

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
        Vector3 direction =
            (transform.forward * input.y + transform.right * input.x).normalized;

        if (direction.sqrMagnitude <= 0f) return;

        Vector3 movement = direction * moveSpeed * Runner.DeltaTime;
        _cc.Move(movement);

        ClampPositionInsideBoundary();
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
        // 카메라 회전은 네트워크 동기화가 필요 없는 순수 로컬 연출이므로
        // FixedUpdateNetwork가 아닌 일반 Update에서 즉시 처리한다.
        if (!Object.HasInputAuthority) return;
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
    /// 원본 FPSController.ClampPositionInsideBoundary()와 동일한 로직.
    /// CharacterController.Move() 이후 위치를 보정하는 방식으로 이식했다.
    /// </summary>
    private void ClampPositionInsideBoundary()
    {
        if (playerBoundaryShield == null) return;

        Vector3 boundaryCenter = playerBoundaryShield.position;
        float boundaryRadius = GetBoundaryWorldRadius();
        float allowedRadius = Mathf.Max(0f, boundaryRadius - boundaryPadding);

        Vector2 centerXZ = new Vector2(boundaryCenter.x, boundaryCenter.z);
        Vector2 posXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 centerToPos = posXZ - centerXZ;

        if (centerToPos.sqrMagnitude <= allowedRadius * allowedRadius) return;

        Vector2 clampedXZ = centerXZ + centerToPos.normalized * allowedRadius;
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = clampedXZ.x;
        clampedPosition.z = clampedXZ.y;

        // CharacterController는 위치를 직접 대입하기 전에 잠깐 꺼야 텔레포트가 안전하게 적용된다.
        _cc.enabled = false;
        transform.position = clampedPosition;
        _cc.enabled = true;
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
