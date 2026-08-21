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
        "X: 좌우, Y: 위아래(음수면 아래), Z: 앞쪽 거리. " +
        "(useScreenSpaceOverlay가 켜져 있으면 사용되지 않는다.)"
    )]
    [SerializeField]
    private Vector3 localOffset = new Vector3(0f, -0.15f, 1f);

    [Tooltip("카메라 회전을 그대로 따라가게 할지 여부입니다. (useScreenSpaceOverlay가 켜져 있으면 사용되지 않는다.)")]
    [SerializeField]
    private bool matchCameraRotation = true;

    [Tooltip(
        "켜두면 이 Canvas를 Screen Space - Overlay로 강제 전환한다. " +
        "총/무기 같은 1인칭 뷰모델 3D 메시보다 항상 앞에 그려지도록 보장하기 위함이다. " +
        "(World Space는 3D 깊이 테스트를 받아서 카메라에 가까운 무기 메시에 가려질 수 있다.)"
    )]
    [SerializeField]
    private bool useScreenSpaceOverlay = true;

    // 외부(NetworkPlayerMovement 등)에서 명시적으로 카메라를 지정했는지 여부.
    // 이게 true면 Camera.main으로 자동 갱신하지 않고 지정된 카메라만 계속 따라간다.
    private bool _cameraExplicitlySet;

    private Canvas _canvas;
    private bool _overlayApplied;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        ApplyOverlayModeIfNeeded();
    }

    /// <summary>
    /// Screen Space - Overlay는 3D 씬의 깊이 버퍼와 무관하게 항상 화면 맨 위에
    /// 그려지므로, 무기 뷰모델처럼 카메라에 아주 가까운 3D 메시에도 절대 가려지지
    /// 않는다. World Space Canvas + 카메라 추적 스크립트 방식은 위치는 맞아도
    /// 3D 깊이 테스트를 받기 때문에 카메라 코앞의 무기 메시에 가려질 수 있었다.
    /// </summary>
    private void ApplyOverlayModeIfNeeded()
    {
        if (!useScreenSpaceOverlay || _canvas == null || _overlayApplied)
        {
            return;
        }

        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.worldCamera = null;
        _overlayApplied = true;
    }


    /// <summary>
    /// 멀티플레이 환경에서는 씬에 카메라가 여러 개(다른 플레이어 것 포함) 있을 수 있어
    /// Camera.main이 가끔 엉뚱한(비활성이거나 남의) 카메라를 가리킬 위험이 있다.
    /// 스폰 시점에 "내" 카메라가 확정되면 이 메서드로 명시적으로 넘겨준다.
    /// (Screen Space - Overlay 모드에서는 카메라가 필요 없지만, World Space로
    /// 되돌릴 경우를 대비해 계속 저장해 둔다.)
    /// </summary>
    public void SetCamera(Camera camera)
    {
        targetCamera = camera;
        _cameraExplicitlySet = camera != null;
    }


    private void LateUpdate()
    {
        if (useScreenSpaceOverlay)
        {
            // Overlay 모드는 화면에 고정되어 있어 매 프레임 위치/회전 계산이 필요 없다.
            ApplyOverlayModeIfNeeded();
            return;
        }

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
