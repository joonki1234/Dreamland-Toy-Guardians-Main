using UnityEngine;
// 최신 입력 시스템 라이브러리를 가져옵니다.
using UnityEngine.InputSystem; 

public class FPSController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.1f;

    private float verticalRotation = 0f;
    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = GetComponentInChildren<Camera>().transform;
        
        // 화면 클릭 시 마우스 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // 1. New Input System 방식으로 마우스 움직임 읽기 (에러 완벽 방지)
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

            // 좌우 회전 (몸통)
            transform.Rotate(Vector3.up * mouseDelta.x);

            // 상하 회전 (머리)
            verticalRotation -= mouseDelta.y;
            verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
        }

        // 2. New Input System 방식으로 키보드 WASD 읽기
        if (Keyboard.current != null)
        {
            float moveX = 0f;
            float moveZ = 0f;

            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;

            Vector3 moveDirection = (transform.forward * moveZ) + (transform.right * moveX);
            transform.position += moveDirection.normalized * moveSpeed * Time.deltaTime;
        }
    }
}