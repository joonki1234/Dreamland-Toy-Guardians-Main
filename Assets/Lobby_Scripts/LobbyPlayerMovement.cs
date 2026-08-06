using UnityEngine;

/// <summary>
/// 로비에서 로컬 플레이어를 키보드로 이동시키는
/// PC 테스트용 이동 스크립트다.
///
/// 현재는 WASD와 방향키를 사용한다.
/// 최종 VR 버전에서는 XR 이동 시스템으로 교체할 수 있다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class LobbyPlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("플레이어의 초당 이동 속도입니다.")]
    [SerializeField]
    private float moveSpeed = 3f;

    [Tooltip("플레이어가 이동 방향을 바라보는 회전 속도입니다.")]
    [SerializeField]
    private float rotationSpeed = 10f;

    [Header("중력 설정")]
    [Tooltip("플레이어에게 적용할 중력의 세기입니다.")]
    [SerializeField]
    private float gravity = -20f;

    [Tooltip(
        "바닥에 붙어 있을 때 적용할 작은 아래 방향 속도입니다. " +
        "경사면이나 바닥에서 떠오르는 현상을 줄입니다."
    )]
    [SerializeField]
    private float groundedVerticalSpeed = -2f;

    [Header("이동 기준 카메라")]
    [Tooltip(
        "이동 방향의 기준으로 사용할 카메라입니다. " +
        "비어 있으면 Main Camera를 자동으로 찾습니다."
    )]
    [SerializeField]
    private Transform cameraTransform;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        if (cameraTransform == null &&
            Camera.main != null)
        {
            cameraTransform =
                Camera.main.transform;
        }
    }

    private void Update()
    {
        MovePlayer();
    }

    /// <summary>
    /// 키보드 입력을 받아 카메라 기준으로 플레이어를 이동시킨다.
    /// </summary>
    private void MovePlayer()
    {
        float horizontalInput =
            Input.GetAxisRaw("Horizontal");

        float verticalInput =
            Input.GetAxisRaw("Vertical");

        Vector3 inputDirection =
            new Vector3(
                horizontalInput,
                0f,
                verticalInput
            );

        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        Vector3 moveDirection =
            GetCameraRelativeDirection(inputDirection);

        ApplyRotation(moveDirection);
        ApplyGravity();

        Vector3 finalMovement =
            moveDirection * moveSpeed;

        finalMovement.y = verticalVelocity;

        characterController.Move(
            finalMovement * Time.deltaTime
        );
    }

    /// <summary>
    /// 입력 방향을 카메라가 바라보는 방향 기준으로 변환한다.
    /// </summary>
    private Vector3 GetCameraRelativeDirection(
        Vector3 inputDirection
    )
    {
        if (cameraTransform == null)
        {
            return inputDirection;
        }

        Vector3 cameraForward =
            cameraTransform.forward;

        Vector3 cameraRight =
            cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 direction =
            cameraForward * inputDirection.z +
            cameraRight * inputDirection.x;

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        return direction;
    }

    /// <summary>
    /// 이동 중이면 플레이어가 이동 방향을 바라보도록 회전시킨다.
    /// </summary>
    private void ApplyRotation(
        Vector3 moveDirection
    )
    {
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(moveDirection);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }

    /// <summary>
    /// CharacterController에 중력을 적용한다.
    /// </summary>
    private void ApplyGravity()
    {
        if (characterController.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity =
                groundedVerticalSpeed;
        }
        else
        {
            verticalVelocity +=
                gravity * Time.deltaTime;
        }
    }
}