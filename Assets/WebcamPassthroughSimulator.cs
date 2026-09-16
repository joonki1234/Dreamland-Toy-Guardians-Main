using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 실기기(Quest) 패스스루 테스트가 불가능한 상황에서, PC 데모용으로
/// "현실 위에 게임이 겹쳐 보이는" MR 느낌을 흉내내기 위한 임시 스크립트다.
///
/// 진짜 Meta Quest 패스스루가 아니라 웹캠 영상을 배경으로 깔아주는
/// 방식이다 - 발표/시연 목적의 시뮬레이션이라는 점을 발표 자료에서도
/// 명확히 밝히는 게 좋다("PC 프로토타입 데모, 실기기 패스스루는 다음 단계").
///
/// [사용 준비 - 유니티 에디터에서 한 번만 세팅]
/// 1) 새 Layer를 하나 만든다. 이름 예: "MRBackground"
/// 2) 빈 GameObject("MR_BackgroundCamera")를 만들고 Camera 컴포넌트를 추가한다.
///    - Clear Flags: Solid Color (아무 색이나, 웹캠이 안 켜졌을 때만 보임)
///    - Culling Mask: "MRBackground" 레이어만 체크
///    - Depth: -10 (게임 카메라보다 먼저 그려지도록 낮은 값)
/// 3) Canvas를 하나 만든다(이름 예: "MR_BackgroundCanvas").
///    - Render Mode: Screen Space - Camera
///    - Render Camera: 위에서 만든 MR_BackgroundCamera
///    - Layer: "MRBackground"
///    - 그 안에 화면을 꽉 채우는 RawImage를 하나 추가한다.
/// 4) 이 스크립트를 아무 오브젝트에나 붙이고, backgroundImage에 그 RawImage를 연결한다.
/// 5) gameCamera는 비워둬도 된다. 01_Player는 씬에 미리 있는 오브젝트가 아니라
///    Fusion이 접속 시점에 런타임으로 스폰하기 때문에, Inspector로 미리 연결할
///    방법이 없다 - 대신 스폰이 끝날 때까지 기다렸다가 NetworkPlayerMovement가
///    노출하는 로컬 플레이어 카메라(NetworkPlayerMovement.LocalPlayerCamera)를
///    자동으로 찾아서 쓴다. 특정 카메라를 강제로 쓰고 싶을 때만 수동으로 연결하면 된다.
/// </summary>
public class WebcamPassthroughSimulator : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("웹캠 영상을 표시할 화면 전체 크기의 RawImage")]
    [SerializeField] private RawImage backgroundImage;

    [Tooltip(
        "웹캠 영상을 그리는 배경 전용 카메라(MR_BackgroundCamera). 이 프로젝트는 " +
        "URP를 쓰고 있어서, 예전 Built-in RP 방식인 Camera.clearFlags만으로는 " +
        "두 카메라 화면이 합성되지 않는다 - URP의 Camera Stacking(Base+Overlay) " +
        "기능을 코드로 직접 구성해야 한다. 그래서 이 참조가 필요하다.")]
    [SerializeField] private Camera backgroundCamera;

    [Tooltip(
        "게임 3D 요소(몬스터, 무기 등)를 그리는 플레이어 카메라. 비워두면 " +
        "NetworkPlayerMovement.LocalPlayerCamera(로컬 플레이어 스폰 후 자동 설정됨)를 " +
        "기다렸다가 자동으로 사용한다. 특정 카메라를 강제 지정하고 싶을 때만 연결.")]
    [SerializeField] private Camera gameCamera;

    [Header("웹캠 설정")]
    [Tooltip(
        "Webcam MR 배경 시뮬레이션을 켭니다. 웹캠이 없는 개발 환경(예: 웹캠 " +
        "없는 노트북)에서만 Inspector에서 직접 꺼두면 된다. " +
        "기본값을 true로 바꿨다 - 씬에 이미 배치된 WebcamPassthroughSimulator " +
        "오브젝트는 이 필드가 추가되기 전에 저장된 씬이라 값이 비어 있었고, " +
        "비어 있으면 이전 기본값(false)을 그대로 썼다 - 그래서 웹캠 배경이 " +
        "켜지지 않았다.")]
    [SerializeField] private bool enableWebcamSimulation = true;

    [Tooltip(
        "비워두면 시스템 기본 웹캠을 사용한다. 특정 웹캠을 쓰고 싶으면 " +
        "WebCamTexture.devices에서 확인한 이름을 넣는다.")]
    [SerializeField] private string preferredDeviceName = "";

    [Tooltip("웹캠 요청 해상도 (실제 웹캠이 지원하는 값으로 자동 보정될 수 있다)")]
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;

    [Tooltip("씬을 시작하자마자 자동으로 MR 모드(웹캠 배경)를 켤지 여부")]
    [SerializeField] private bool startEnabledOnAwake = true;

    [Header("Stage 2 틴트")]
    [Tooltip(
        "Stage 2 동안 웹캠 배경 위에 은은하게 씌우는 분홍색 틴트. RawImage.color를 " +
        "이 값으로 바꾸는 방식이라 웹캠 화면(현실) 자체는 그대로 보이고 색상만 이 " +
        "값과 곱해진다 - '카메라는 계속 보이지만 살짝 핑크빛이 도는' 연출용이다.")]
    [SerializeField] private Color stage2PinkTint = new Color(1f, 0.78f, 0.85f, 1f);

    private WebCamTexture webCamTexture;
    private CameraClearFlags originalGameCameraClearFlags;
    private bool originalFlagsCached;
    private Color originalBackgroundImageColor = Color.white;
    private bool originalBackgroundColorCached;
    private bool isRunning;
    private bool isStarting;


    /// <summary>
    /// Stage 2 시작 시 호출: 웹캠 배경(현실)은 그대로 유지한 채, RawImage 색상만
    /// 분홍 틴트로 바꿔서 화면에 은은하게 핑크빛이 돌게 한다. 완전 꿈나라 전환
    /// (EndMrAndSwitchToFullVr) 시점에 원래 색으로 되돌아간다.
    /// </summary>
    public void ApplyStage2PinkTint()
    {
        if (backgroundImage == null)
        {
            return;
        }

        if (!originalBackgroundColorCached)
        {
            originalBackgroundImageColor = backgroundImage.color;
            originalBackgroundColorCached = true;
        }

        backgroundImage.color = stage2PinkTint;
    }


    private void Start()
    {
        // 예전에는 이 호출 자체가 #if UNITY_EDITOR로 감싸져 있어서, Unity
        // 에디터 안에서 Play할 때만 웹캠 배경이 켜지고 실제로 빌드한 exe로
        // 실행하면(지금 하는 것처럼 여러 대의 PC에 빌드해서 테스트하는
        // 경우) 이 메서드 자체가 아예 호출되지 않았다 - "이전엔 됐는데
        // 지금은 배경(스카이박스)이 다시 보인다"는 증상의 원인이었다.
        // 빌드에서도 각자 자기 로컬 웹캠을 배경으로 써야 하므로 에디터
        // 여부와 상관없이 항상 시도한다.
        if (startEnabledOnAwake)
        {
            EnableMrBackground();
        }
    }


    /// <summary>
    /// 웹캠을 켜고, 배경을 웹캠 영상으로 바꾼다("MR 모드 시작").
    /// 게임 종료 시점에 EndMrAndSwitchToFullVr()를 호출하면 원래 스카이박스로 복귀한다.
    /// </summary>
    public void EnableMrBackground()
    {
        // 예전에는 이 메서드 전체가 #if !UNITY_EDITOR로 막혀 있어서 실제
        // 빌드에서는 무조건 그냥 리턴하고 끝났다(웹캠이 켜지지도, 배경이
        // 바뀌지도 않음). 각 플레이어가 자기 PC에 빌드한 실행 파일로
        // 테스트하는 게 실제 사용 방식이라, 에디터 전용 제한을 없애고
        // 빌드에서도 정상 동작하게 한다.
        if (isRunning || isStarting)
        {
            return;
        }

        if (!enableWebcamSimulation)
        {
            isRunning = false;
            isStarting = false;
            return;
        }

        if (backgroundImage == null)
        {
            Debug.LogError(
                "[WebcamPassthroughSimulator] backgroundImage가 연결되지 않았습니다. " +
                "Inspector에서 화면 전체 크기의 RawImage를 연결하세요.");
            return;
        }

        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            ClearFailedWebcamState();
            Debug.LogWarning(
                "[WebcamPassthroughSimulator] 이 PC에서 웹캠을 찾지 못했습니다. " +
                "웹캠 배경 없이 기존 화면 그대로 진행합니다.");
            return;
        }

        string deviceName = null;
        foreach (WebCamDevice device in devices)
        {
            if (!string.IsNullOrWhiteSpace(device.name))
            {
                deviceName = device.name;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            ClearFailedWebcamState();
            Debug.LogWarning(
                "[WebcamPassthroughSimulator] 웹캠 목록은 존재하지만 유효한 장치 이름이 " +
                "없습니다. Webcam MR 배경 없이 게임을 계속 진행합니다.");
            return;
        }

        bool preferredDeviceFound = string.IsNullOrWhiteSpace(preferredDeviceName);

        if (!preferredDeviceFound)
        {
            foreach (WebCamDevice device in devices)
            {
                if (device.name == preferredDeviceName)
                {
                    deviceName = device.name;
                    preferredDeviceFound = true;
                    break;
                }
            }
        }

        if (!preferredDeviceFound)
        {
            Debug.LogWarning(
                "[WebcamPassthroughSimulator] preferredDeviceName과 일치하는 웹캠을 " +
                $"찾지 못해 첫 번째 사용 가능 장치 '{deviceName}'를 사용합니다.");
        }

        /*
         * 1280x720을 강제로 요청하면 일부 Windows 웹캠/가상 카메라 드라이버가
         * 해당 Media Foundation 프로필을 열지 못해 "Couldn't config the stream!"
         * 을 발생시킨다. 이 기능은 Editor MR 배경 시뮬레이터이므로 선택한 장치의
         * 기본(드라이버가 보장하는) 스트림 설정을 사용한다. requestedWidth/
         * requestedHeight는 Inspector 호환성과 진단용으로 유지한다.
         */
        webCamTexture = new WebCamTexture(deviceName);
        backgroundImage.texture = webCamTexture;
        isStarting = true;

        try
        {
            webCamTexture.Play();
        }
        catch (System.Exception exception)
        {
            FailWebcamStart(
                $"장치 '{deviceName}'의 스트림을 시작하지 못했습니다: {exception.Message}");
            return;
        }

        StartCoroutine(FitBackgroundAspectWhenReady(deviceName));
    }


    /// <summary>
    /// 01_Player는 씬에 미리 놓인 오브젝트가 아니라 접속 시점에 Fusion이
    /// 런타임으로 스폰한다. 그래서 이 스크립트가 켜지는 시점엔 아직 로컬
    /// 플레이어 카메라가 존재하지 않을 수 있다 - 스폰될 때까지 기다렸다가
    /// (NetworkPlayerMovement.LocalPlayerCamera가 채워질 때까지 폴링)
    /// URP 카메라 스태킹을 코드로 구성한다.
    ///
    /// 처음에는 Built-in RP 방식(Camera.clearFlags = Depth)으로 시도했는데,
    /// 이 프로젝트는 URP를 쓰고 있어서 그 방식이 안 먹혔다(웹캠 카메라 LED는
    /// 켜지는데 화면엔 여전히 스카이박스만 보이는 증상으로 확인됨) - URP에서는
    /// Camera.clearFlags 대신 UniversalAdditionalCameraData의
    /// Base/Overlay 카메라 스택 기능을 써야 두 카메라 화면이 합성된다.
    /// </summary>
    private System.Collections.IEnumerator ResolveGameCameraAndSetUpUrpStack()
    {
        float timeout = 30f;
        float elapsed = 0f;

        while (gameCamera == null && elapsed < timeout)
        {
            if (NetworkPlayerMovement.LocalPlayerCamera != null)
            {
                gameCamera = NetworkPlayerMovement.LocalPlayerCamera;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (gameCamera == null)
        {
            Debug.LogWarning(
                "[WebcamPassthroughSimulator] 로컬 플레이어 카메라를 찾지 못했습니다. " +
                "웹캠 배경만 켜지고, 게임 화면과 합성은 되지 않을 수 있습니다.");
            yield break;
        }

        if (backgroundCamera == null)
        {
            Debug.LogError(
                "[WebcamPassthroughSimulator] backgroundCamera(MR_BackgroundCamera)가 " +
                "연결되지 않았습니다. URP 카메라 스택을 구성할 수 없어 웹캠 배경이 " +
                "게임 화면과 합성되지 않습니다.");
            yield break;
        }

        originalGameCameraClearFlags = gameCamera.clearFlags;
        originalFlagsCached = true;

        UniversalAdditionalCameraData backgroundCamData =
            backgroundCamera.GetComponent<UniversalAdditionalCameraData>();

        if (backgroundCamData == null)
        {
            // URP는 카메라마다 이 컴포넌트가 있어야 스택 정보를 들고 있을 수 있다.
            // 씬에 코드로 직접 만든 카메라라 에디터를 거치지 않아서 자동으로
            // 안 붙어 있을 수 있으므로 필요하면 여기서 붙인다.
            backgroundCamData = backgroundCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        backgroundCamData.renderType = CameraRenderType.Base;

        UniversalAdditionalCameraData gameCamData =
            gameCamera.GetComponent<UniversalAdditionalCameraData>();

        if (gameCamData == null)
        {
            gameCamData = gameCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        // 게임 카메라를 "Overlay"로 만들어서, 배경(Base) 카메라가 이미 그린
        // 웹캠 영상 위에 3D 게임 요소만 겹쳐 그리도록 한다.
        gameCamData.renderType = CameraRenderType.Overlay;

        if (backgroundCamData.cameraStack == null)
        {
            Debug.LogError(
                "[WebcamPassthroughSimulator] backgroundCamera에 Camera Stack 리스트가 없습니다. " +
                "URP 버전 호환 문제일 수 있습니다.");
            yield break;
        }

        if (!backgroundCamData.cameraStack.Contains(gameCamera))
        {
            backgroundCamData.cameraStack.Add(gameCamera);
        }
    }


    /// <summary>
    /// 웹캠 해상도가 실제로 확정되는 데 한두 프레임 걸릴 수 있어서,
    /// 실제 해상도가 나온 뒤에 화면 비율을 맞춰준다(웹캠 영상이
    /// 찌그러져 보이지 않도록).
    /// </summary>
    private System.Collections.IEnumerator FitBackgroundAspectWhenReady(string deviceName)
    {
        // webCamTexture.width가 웹캠 초기화 전엔 임시값(예: 16)으로 나올 수 있다.
        float timeout = 3f;
        float elapsed = 0f;

        while (webCamTexture != null &&
               elapsed < timeout &&
               (!webCamTexture.isPlaying ||
                !webCamTexture.didUpdateThisFrame ||
                webCamTexture.width <= 16 ||
                webCamTexture.height <= 16))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (webCamTexture == null ||
            !webCamTexture.isPlaying ||
            webCamTexture.width <= 16 ||
            webCamTexture.height <= 16)
        {
            FailWebcamStart(
                $"장치 '{deviceName}'에서 {timeout:0.#}초 안에 유효한 프레임을 받지 못했습니다. " +
                $"요청값은 {requestedWidth}x{requestedHeight}였으며 장치 기본 설정으로도 시작하지 못했습니다.");
            yield break;
        }

        isStarting = false;
        isRunning = true;

        RectTransform rectTransform = backgroundImage.rectTransform;
        float webcamAspect = (float)webCamTexture.width / webCamTexture.height;

        AspectRatioFitter fitter = backgroundImage.GetComponent<AspectRatioFitter>();

        if (fitter == null)
        {
            fitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
        }

        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = webcamAspect;

        StartCoroutine(ResolveGameCameraAndSetUpUrpStack());
    }


    /// <summary>
    /// "게임 종료 시 완전한 VR 환경으로 전환" 연출용. 웹캠 배경을 끄고
    /// 게임 카메라를 원래 스카이박스(Clear Flags) 상태로 되돌린다.
    /// 스테이지 클리어/보스 처치 등 엔딩 트리거 지점에서 호출하면 된다.
    /// </summary>
    public void EndMrAndSwitchToFullVr()
    {
        StopAllCoroutines();

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            webCamTexture = null;
        }

        if (backgroundImage != null)
        {
            backgroundImage.texture = null;

            if (originalBackgroundColorCached)
            {
                backgroundImage.color = originalBackgroundImageColor;
                originalBackgroundColorCached = false;
            }
        }

        if (gameCamera != null && originalFlagsCached)
        {
            gameCamera.clearFlags = originalGameCameraClearFlags;
        }

        // URP 카메라 스택에서도 빼고 게임 카메라를 다시 독립적인 Base 카메라로
        // 되돌린다 - 안 그러면 웹캠을 꺼도 화면이 계속 비어 보인다.
        if (gameCamera != null)
        {
            UniversalAdditionalCameraData gameCamData =
                gameCamera.GetComponent<UniversalAdditionalCameraData>();

            if (gameCamData != null)
            {
                gameCamData.renderType = CameraRenderType.Base;
            }
        }

        if (backgroundCamera != null)
        {
            UniversalAdditionalCameraData backgroundCamData =
                backgroundCamera.GetComponent<UniversalAdditionalCameraData>();

            if (backgroundCamData != null && backgroundCamData.cameraStack != null && gameCamera != null)
            {
                backgroundCamData.cameraStack.Remove(gameCamera);
            }
        }

        isRunning = false;
        isStarting = false;
    }


    private void FailWebcamStart(string reason)
    {
        ClearFailedWebcamState();
        Debug.LogWarning(
            "[WebcamPassthroughSimulator] " + reason +
            " Webcam MR 배경만 비활성화하고 게임은 계속 진행합니다.",
            this);
    }


    private void ClearFailedWebcamState()
    {
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }

            webCamTexture = null;
        }

        if (backgroundImage != null)
        {
            backgroundImage.texture = null;
        }

        isRunning = false;
        isStarting = false;
    }


    private void OnDisable()
    {
        EndMrAndSwitchToFullVr();
    }


    private void OnDestroy()
    {
        EndMrAndSwitchToFullVr();
    }
}
