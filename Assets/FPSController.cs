using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드와 마우스를 이용한 테스트용 FPS 플레이어 컨트롤러.
///
/// 플레이어는 PlayerBoundaryShield가 만든 원형 범위 바깥으로
/// 이동할 수 없다.
/// </summary>
public class FPSController : MonoBehaviour
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


    private float verticalRotation = 0f;
    private Transform cameraTransform;
    private SphereCollider boundarySphereCollider;


    private void Start()
    {
        Camera childCamera = GetComponentInChildren<Camera>();

        if (childCamera != null)
        {
            cameraTransform = childCamera.transform;
        }
        else
        {
            Debug.LogWarning(
                "[FPSController] 자식 오브젝트에서 Camera를 찾지 못했습니다.");
        }

        if (playerBoundaryShield != null)
        {
            boundarySphereCollider =
                playerBoundaryShield.GetComponent<SphereCollider>();
        }
        else
        {
            Debug.LogWarning(
                "[FPSController] Player Boundary Shield가 연결되지 않았습니다.");
        }

        // 화면 클릭 후 마우스 커서를 게임 화면 가운데에 고정한다.
        Cursor.lockState = CursorLockMode.Locked;
    }


    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }


    /// <summary>
    /// 마우스를 이용해 플레이어 몸과 카메라를 회전시킨다.
    /// </summary>
    private void HandleMouseLook()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue() * mouseSensitivity;

        // 플레이어 몸통 좌우 회전
        transform.Rotate(Vector3.up * mouseDelta.x);

        // 카메라 상하 회전
        verticalRotation -= mouseDelta.y;
        verticalRotation =
            Mathf.Clamp(verticalRotation, -80f, 80f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation =
                Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }


    /// <summary>
    /// WASD 입력을 받아 플레이어를 이동시킨다.
    /// 이동 예정 위치가 보호막 밖이면 경계 안으로 제한한다.
    /// </summary>
    private void HandleMovement()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        float moveX = 0f;
        float moveZ = 0f;

        if (Keyboard.current.wKey.isPressed)
        {
            moveZ += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            moveZ -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            moveX += 1f;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            moveX -= 1f;
        }

        Vector3 moveDirection =
            (transform.forward * moveZ) +
            (transform.right * moveX);

        if (moveDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector3 movement =
            moveDirection.normalized * moveSpeed * Time.deltaTime;

        Vector3 targetPosition = transform.position + movement;

        targetPosition =
            ClampPositionInsideBoundary(targetPosition);

        transform.position = targetPosition;
    }


    /// <summary>
    /// 이동 예정 위치를 보호막의 원형 바닥 범위 안으로 제한한다.
    ///
    /// Y축은 바꾸지 않고 XZ 평면만 제한하므로,
    /// 플레이어는 바닥 위에서 반구 경계를 넘지 못한다.
    /// </summary>
    private Vector3 ClampPositionInsideBoundary(
        Vector3 targetPosition)
    {
        if (playerBoundaryShield == null)
        {
            return targetPosition;
        }

        Vector3 boundaryCenter =
            playerBoundaryShield.position;

        float boundaryRadius =
            GetBoundaryWorldRadius();

        float allowedRadius =
            Mathf.Max(0f, boundaryRadius - boundaryPadding);

        Vector2 centerXZ =
            new Vector2(boundaryCenter.x, boundaryCenter.z);

        Vector2 targetXZ =
            new Vector2(targetPosition.x, targetPosition.z);

        Vector2 centerToTarget =
            targetXZ - centerXZ;

        if (centerToTarget.sqrMagnitude >
            allowedRadius * allowedRadius)
        {
            Vector2 clampedXZ =
                centerXZ +
                centerToTarget.normalized * allowedRadius;

            targetPosition.x = clampedXZ.x;
            targetPosition.z = clampedXZ.y;
        }

        return targetPosition;
    }


    /// <summary>
    /// PlayerBoundaryShield의 실제 월드 반지름을 계산한다.
    ///
    /// Unity 기본 Sphere는 원래 반지름이 0.5이므로,
    /// Scale이 15라면 실제 반지름은 약 7.5가 된다.
    /// </summary>
    private float GetBoundaryWorldRadius()
    {
        if (boundarySphereCollider != null)
        {
            Vector3 scale =
                playerBoundaryShield.lossyScale;

            float largestHorizontalScale =
                Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.z));

            return boundarySphereCollider.radius *
                   largestHorizontalScale;
        }

        Vector3 fallbackScale =
            playerBoundaryShield.lossyScale;

        return Mathf.Max(
                   Mathf.Abs(fallbackScale.x),
                   Mathf.Abs(fallbackScale.z))
               * 0.5f;
    }
}