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
        "비워두면 시스템 기본 웹캠을 사용한다. 특정 웹캠을 쓰고 싶으면 " +
        "WebCamTexture.devices에서 확인한 이름을 넣는다.")]
    [SerializeField] private string preferredDeviceName = "";

    [Tooltip("웹캠 요청 해상도 (실제 웹캠이 지원하는 값으로 자동 보정될 수 있다)")]
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;

    [Tooltip("씬을 시작하자마자 자동으로 MR 모드(웹캠 배경)를 켤지 여부")]
    [SerializeField] private bool startEnabledOnAwake = true;

#if UNITY_EDITOR
    [Tooltip("사용 가능한 PC 웹캠을 확인한 경우에만 켜세요. 기본값은 Editor 웹캠 시작 차단이며 기기 빌드에는 적용되지 않습니다.")]
    [SerializeField] private bool allowEditorWebcam;
#endif

    private WebCamTexture webCamTexture;
    private CameraClearFlags originalGameCameraClearFlags;
    private bool originalFlagsCached;
    private bool isRunning;
    private bool isStarting;
    private bool startupFailed;
    private bool nativeStreamError;


    private void Start()
    {
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
        if (isRunning || isStarting || startupFailed || !isActiveAndEnabled)
        {
            return;
        }

#if UNITY_EDITOR
        // Device enumeration cannot establish that a listed/virtual camera can stream.
        // Require explicit opt-in before calling the native Play API in the Editor.
        if (!allowEditorWebcam)
        {
            FailStartup("Editor 웹캠 시작을 건너뜁니다. 사용 가능한 장치를 확인한 뒤 Allow Editor Webcam을 켜세요.");
            return;
        }
#endif

        if (backgroundImage == null)
        {
            FailStartup("backgroundImage가 연결되지 않았습니다.");
            return;
        }

        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            FailStartup("이 PC에서 웹캠을 찾지 못했습니다.");
            return;
        }

        string deviceName = devices[0].name;

        if (!string.IsNullOrEmpty(preferredDeviceName))
        {
            foreach (WebCamDevice device in devices)
            {
                if (device.name == preferredDeviceName)
                {
                    deviceName = device.name;
                    break;
                }
            }
        }

        isStarting = true;
        Application.logMessageReceived += HandleStartupLog;
        try
        {
            webCamTexture = new WebCamTexture(deviceName, requestedWidth, requestedHeight);
            webCamTexture.Play();
        }
        catch (System.Exception exception)
        {
            FailStartup(exception.Message);
            return;
        }
        StartCoroutine(WaitForFirstFrame());
    }

    // Unity's native stream failure is logged rather than necessarily thrown.
    // Record it only; release the texture outside the logging callback.
    private void HandleStartupLog(string message, string stackTrace, LogType type)
    {
        if (isStarting && message.Contains("Couldn't config the stream") &&
            (stackTrace.Contains("WebCamTexture") || stackTrace.Contains(nameof(WebcamPassthroughSimulator))))
            nativeStreamError = true;
    }

    private System.Collections.IEnumerator WaitForFirstFrame()
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!nativeStreamError && webCamTexture != null && Time.realtimeSinceStartup < deadline)
        {
            if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame &&
                webCamTexture.width > 16 && webCamTexture.height > 16)
            {
                Application.logMessageReceived -= HandleStartupLog;
                isStarting = false;
                isRunning = true;
                backgroundImage.texture = webCamTexture;
                StartCoroutine(FitBackgroundAspectWhenReady());
                StartCoroutine(ResolveGameCameraAndSetUpUrpStack());
                yield break;
            }
            yield return null;
        }
        FailStartup("웹캠 스트림의 첫 프레임을 받지 못했습니다.");
    }

    private void FailStartup(string reason)
    {
        Application.logMessageReceived -= HandleStartupLog;
        isStarting = false;
        if (!startupFailed && !nativeStreamError)
            Debug.LogWarning($"[WebcamPassthroughSimulator] {reason} MR 배경 없이 진행합니다.");
        startupFailed = true; // Do not retry a failed device on subsequent enable requests.
        ReleaseWebcam();
        if (backgroundImage != null) backgroundImage.enabled = false;
        if (backgroundCamera != null) backgroundCamera.enabled = false;
        // The game camera has not been changed yet: stacking starts only after a frame arrives.
    }

    private void ReleaseWebcam()
    {
        if (backgroundImage != null) backgroundImage.texture = null;
        if (webCamTexture == null) return;
        webCamTexture.Stop();
        Destroy(webCamTexture);
        webCamTexture = null;
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
    private System.Collections.IEnumerator FitBackgroundAspectWhenReady()
    {
        // webCamTexture.width가 웹캠 초기화 전엔 임시값(예: 16)으로 나올 수 있다.
        float timeout = 3f;
        float elapsed = 0f;

        while (webCamTexture != null && webCamTexture.width <= 16 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (webCamTexture == null || webCamTexture.width <= 16)
        {
            yield break;
        }

        RectTransform rectTransform = backgroundImage.rectTransform;
        float webcamAspect = (float)webCamTexture.width / webCamTexture.height;

        AspectRatioFitter fitter = backgroundImage.GetComponent<AspectRatioFitter>();

        if (fitter == null)
        {
            fitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
        }

        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = webcamAspect;
    }


    /// <summary>
    /// "게임 종료 시 완전한 VR 환경으로 전환" 연출용. 웹캠 배경을 끄고
    /// 게임 카메라를 원래 스카이박스(Clear Flags) 상태로 되돌린다.
    /// 스테이지 클리어/보스 처치 등 엔딩 트리거 지점에서 호출하면 된다.
    /// </summary>
    public void EndMrAndSwitchToFullVr()
    {
        Application.logMessageReceived -= HandleStartupLog;
        StopAllCoroutines();
        isStarting = false;
        ReleaseWebcam();
        if (!isRunning)
        {
            return;
        }

        if (backgroundImage != null)
        {
            backgroundImage.texture = null;
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
    }


    private void OnDisable()
    {
        EndMrAndSwitchToFullVr();
    }
}
