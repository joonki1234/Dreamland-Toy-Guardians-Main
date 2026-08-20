using UnityEngine;

/// <summary>
/// 이 컴포넌트가 붙은 Transform(로비의 로봇+말풍선 HUD 등)을
/// 항상 플레이어 카메라 앞 고정된 위치에 붙여서,
/// 고개를 돌려도 화면(시야) 하단에 계속 보이도록 한다.
///
/// World Space Canvas에 이 스크립트를 붙이면
/// 마치 VR 손목 UI처럼 "1인칭 시점 고정 HUD"가 된다.
/// </summary>
public class ViewLockedHudFollower : MonoBehaviour
{
    [Tooltip("따라다닐 카메라. 비워두면 Camera.main을 자동으로 찾는다.")]
    [SerializeField]
    private Camera targetCamera;

    [Tooltip(
        "카메라 기준 로컬 오프셋입니다. " +
        "X: 좌우, Y: 위아래(음수면 아래), Z: 앞쪽 거리."
    )]
    [SerializeField]
    private Vector3 localOffset = new Vector3(0f, -0.15f, 1f);

    [Tooltip("카메라 회전을 그대로 따라가게 할지 여부입니다. HUD처럼 보이려면 켜 두세요.")]
    [SerializeField]
    private bool matchCameraRotation = true;

    // 외부(NetworkPlayerMovement 등)에서 명시적으로 카메라를 지정했는지 여부.
    // 이게 true면 Camera.main으로 자동 갱신하지 않고 지정된 카메라만 계속 따라간다.
    private bool _cameraExplicitlySet;


    /// <summary>
    /// 멀티플레이 환경에서는 씬에 카메라가 여러 개(다른 플레이어 것 포함) 있을 수 있어
    /// Camera.main이 가끔 엉뚱한(비활성이거나 남의) 카메라를 가리킬 위험이 있다.
    /// 스폰 시점에 "내" 카메라가 확정되면 이 메서드로 명시적으로 넘겨준다.
    /// </summary>
    public void SetCamera(Camera camera)
    {
        targetCamera = camera;
        _cameraExplicitlySet = camera != null;
    }


    private void LateUpdate()
    {
        if (!_cameraExplicitlySet)
        {
            // 명시적으로 지정된 카메라가 없으면 매 프레임 Camera.main을 다시 확인한다.
            // (한 번 찾은 카메라가 나중에 비활성화되거나, 활성 카메라가 바뀌는
            // 경우에도 항상 지금 화면에 실제로 쓰이는 카메라를 따라가게 하기 위함.)
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 targetPosition =
            targetCamera.transform.TransformPoint(localOffset);

        Quaternion targetRotation =
            matchCameraRotation
                ? targetCamera.transform.rotation
                : transform.rotation;

        transform.SetPositionAndRotation(
            targetPosition,
            targetRotation
        );
    }
}
