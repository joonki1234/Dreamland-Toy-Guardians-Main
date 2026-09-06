using DreamGuardians;
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

    [Tooltip(
        "항상 아래로 적용하는 힘입니다. CharacterController.Move()는 자동으로 " +
        "중력을 적용하지 않아서, 이게 없으면 턱이나 지형 이음매를 살짝 타고 " +
        "오른 뒤 다시 내려올 방법이 없어 계속 위로 떠 있게 됩니다.")]
    [SerializeField, Min(0f)]
    private float groundStickForce = 12f;


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
    private float footstepVolume = 0.2f;


    private CharacterController _cc;
    private SphereCollider _boundarySphereCollider;
    private TrackedPoseDriver _headTrackedPoseDriver;
    private float _verticalRotation;

    // Update()에서 프레임마다 읽은 마우스 좌우 회전량을 여기 모아뒀다가,
    // FixedUpdateNetwork()(네트워크 시뮬레이션 틱)에서 한 번에 몸통에
    // 적용한다. 예전에는 transform.Rotate()를 Update()에서 직접
    // 호출했는데, NetworkTransform은 시뮬레이션 틱 시점의 상태를
    // 동기화하기 때문에 그 사이에 생긴 변화가 다른 클라이언트에게
    // 안정적으로 전달되지 않았다(그래서 상대방 화면에서는 내가 시점을
    // 돌려도 몸이 계속 같은 방향만 보고 있었다).
    private float _pendingYawDelta;
    private AudioSource _footstepAudioSource;
    private AudioClip[] _footstepClips;
    private float _footstepTimer;

    private bool _hasStageBounds;
    private float _stageMinX;
    private float _stageMaxX;
    private float _stageMinZ;
    private float _stageMaxZ;

    // PlayerBoundaryShield는 "06_PORTAL_EFFECTS" 오브젝트의 자식인데, 이 부모는
    // Stage 2 적 흡수 연출 중 스케일이 계속 펄스되고, 보스전으로 넘어가면
    // SetActive(false)로 꺼지기까지 한다. 경계 반지름을 매 프레임 그 오브젝트의
    // lossyScale에서 실시간으로 다시 계산하면 이동 범위가 연출에 따라 같이
    // 흔들리므로, 스폰 시점에 한 번만 계산해서 고정값으로 캐싱해 둔다.
    private bool _hasCachedBoundary;
    private Vector3 _cachedBoundaryCenter;
    private float _cachedBoundaryRadius;


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

        // TrackedPoseDriver는 프리팹에 기본적으로 Enabled 상태로 붙어 있는데,
        // 정작 이 값을 실제 VR 기기 연결 여부에 따라 꺼주는 코드가 어디에도
        // 없다. 그래서 실제 헤드셋이 연결되지 않은 마우스+키보드 테스트에서도
        // Update()의 "TrackedPoseDriver가 이미 회전을 담당한다" 분기에 걸려
        // 마우스 시점 회전이 통째로 막힌다. 실제 XR 기기가 연결돼 있을
        // 때만 켜진 상태로 두고, 아니면 강제로 꺼서 마우스 회전이 항상
        // 동작하게 한다.
        if (_headTrackedPoseDriver != null &&
            !UnityEngine.XR.XRSettings.isDeviceActive)
        {
            _headTrackedPoseDriver.enabled = false;
        }

        // 01_Player.prefab은 씬에 배치되지 않고 Fusion이 런타임에 에셋에서
        // 바로 스폰하는 방식이라, playerBoundaryShield처럼 "다른 씬의 특정
        // 오브젝트"를 가리키는 Inspector 참조는 프리팹을 독립적으로 열어서
        // 저장하기만 해도 쉽게 끊어진다(실제로 한 번 끊어졌었다). 참조가
        // 비어있으면 이름으로 다시 찾아서 자동 복구한다.
        if (playerBoundaryShield == null)
        {
            GameObject shieldObject =
                FindObjectByNameIncludingInactive("PlayerBoundaryShield");

            if (shieldObject != null)
            {
                playerBoundaryShield = shieldObject.transform;
            }
            else
            {
                Debug.LogWarning(
                    "[NetworkPlayerMovement] PlayerBoundaryShield를 " +
                    "씬에서 찾지 못했습니다. 이동 범위 제한이 적용되지 않습니다.");
            }
        }

        if (playerBoundaryShield != null)
        {
            _boundarySphereCollider =
                playerBoundaryShield.GetComponent<SphereCollider>();

            // "범위를 그냥 뚫고 나간다"는 증상이 재발할 경우, 참조가
            // 실제로 붙었는지와 반지름이 얼마로 계산됐는지 바로 확인할 수
            // 있도록 남겨둔다.
            _cachedBoundaryCenter = playerBoundaryShield.position;
            _cachedBoundaryRadius = GetBoundaryWorldRadius();
            _hasCachedBoundary = true;

            Debug.Log(
                "[NetworkPlayerMovement] Player Boundary Shield 연결됨: " +
                playerBoundaryShield.name +
                " / 콜라이더 존재: " + (_boundarySphereCollider != null) +
                " / 계산된 월드 반지름(고정값으로 캐싱됨): " + _cachedBoundaryRadius);
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
            // 예전에는 카메라 오브젝트 자체를 SetActive(isMine)로 껐다.
            // 문제는 직업별 무기(Weapon_Police 등)가 전부 이 카메라의
            // 자식으로 붙어 있다는 점이다 - 오브젝트를 통째로 끄면
            // 그 밑의 무기까지 같이 비활성화되어, 다른 플레이어 화면에서
            // 내 무기가 아예 안 보이게 된다. 실제로 꺼야 하는 건
            // "렌더링(Camera 컴포넌트)"뿐이므로, 컴포넌트만 비활성화해
            // 오브젝트(와 그 자식 무기)는 계속 활성 상태로 둔다.
            playerCamera.enabled = isMine;
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

        // 씬(예: Dreamland_map_3)에 편집용으로 남아있는 카메라가 태그나
        // 이름과 상관없이 AudioListener를 켠 채로 있으면, 내 플레이어
        // 오디오리스너를 켜는 순간 리스너 2개가 동시에 활성화된다.
        // ("There are 2 audio listeners in the scene" 경고가 계속 뜨고,
        // 화면도 그 씬 카메라가 계속 우선해 플레이어 시점으로 안 넘어가는
        // 것처럼 보일 수 있다.) 태그/이름에 의존하지 않도록, 씬에 있는
        // 모든 AudioListener 중 내 것이 아닌 것을 전부 찾아서 끈다.
        if (isMine)
        {
            AudioListener[] sceneAudioListeners =
                FindObjectsByType<AudioListener>(
                    FindObjectsSortMode.None);

            foreach (AudioListener sceneAudioListener in sceneAudioListeners)
            {
                if (sceneAudioListener == audioListener)
                {
                    continue;
                }

                sceneAudioListener.enabled = false;

                Camera sceneCamera =
                    sceneAudioListener.GetComponent<Camera>();

                if (sceneCamera != null &&
                    (playerCamera == null ||
                     sceneCamera != playerCamera))
                {
                    sceneCamera.enabled = false;
                }
            }
        }

        // 캐릭터 몸(Models 하위 전부)은 프리팹에서 "LocalPlayer" 레이어로
        // 만들어져 있고, 카메라의 Culling Mask는 그 레이어를 제외하고
        // 있다 - 그래야 1인칭 시점에서 내 몸이 카메라 바로 앞을 가리지
        // 않는다. 문제는 이 레이어/마스크가 모든 플레이어에게 동일하게
        // 적용된다는 점이다: 아무 처리도 안 하면 "다른 플레이어의 카메라"
        // 역시 이 레이어를 제외하므로, 다른 사람 눈에도 내 몸이 안 보이게
        // 된다. 그래서 "이 캐릭터가 내 캐릭터가 아닐 때"(즉 상대방이 내
        // 화면에 보이는 경우)만 그 오브젝트의 레이어를 다시 기본
        // 레이어로 바꿔서, 상대방에게는 정상적으로 보이게 한다.
        if (!isMine)
        {
            Transform modelsRoot = transform.Find("Models");

            if (modelsRoot != null)
            {
                SetLayerRecursively(modelsRoot.gameObject, LayerMask.NameToLayer("Default"));
            }
        }

        // 시점 회전 동기화가 또 안 된다는 리포트가 계속 나와서, 다음 테스트 때
        // 콘솔에서 바로 원인을 특정할 수 있도록 스폰 시점 권한 상태를 로그로 남긴다.
        // (이 캐릭터가 "내 캐릭터"인지, StateAuthority를 실제로 갖고 있는지 확인용.
        //  이후 문제 없으면 지워도 된다.)
        Debug.Log(
            $"[NetworkPlayerMovement] Spawned: isMine(InputAuthority)={isMine}, " +
            $"HasStateAuthority={Object.HasStateAuthority}, " +
            $"초기 rotation.y={transform.eulerAngles.y:F1}");

        if (isMine)
        {
            Cursor.lockState = CursorLockMode.Locked;

            // 화면 고정 HUD(ToyFriendMapHud 등)가 Camera.main에 의존하면
            // 멀티플레이에서 남의 카메라를 잘못 따라갈 수 있으므로,
            // "내" 카메라가 확정된 지금 명시적으로 넘겨준다.
            if (playerCamera != null)
            {
                ViewLockedHudFollower[] hudFollowers =
                    FindObjectsByType<ViewLockedHudFollower>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

                for (int i = 0; i < hudFollowers.Length; i++)
                {
                    hudFollowers[i].SetCamera(playerCamera);
                }

                // MissionBannerUI(스테이지 배너/남은 적 수)와 CoreHealthHUD(코어 체력)도
                // 같은 이유로 카메라가 확정되지 않으면 화면에 아무것도 안 그려진다.
                // 이 프로젝트의 플레이어 카메라는 MainCamera 태그를 쓰지 않으므로
                // Camera.main에 의존하는 두 HUD 모두 여기서 명시적으로 카메라를 넘겨준다.
                MissionBannerUI missionBannerUI =
                    FindAnyObjectByType<MissionBannerUI>(
                        FindObjectsInactive.Include);
                missionBannerUI?.Configure(playerCamera);

                CoreHealthHUD coreHealthHud =
                    FindAnyObjectByType<CoreHealthHUD>(
                        FindObjectsInactive.Include);
                coreHealthHud?.SetCamera(playerCamera);

                // ToyFriendController도 playerLookTarget이 비어 있으면 Camera.main에
                // 의존하는데(항상 null), 그러면 장난감 친구가 말할 때/평상시에
                // 플레이어를 전혀 바라보지 못한다. 여기서 명시적으로 넘겨준다.
                ToyFriendController toyFriend =
                    FindAnyObjectByType<ToyFriendController>(
                        FindObjectsInactive.Include);
                toyFriend?.SetPlayerLookTarget(playerCamera.transform);

                // FinalBossAttackController의 눈알도 같은 이유로 Camera.main에
                // 의존하면 항상 fallback(코어 방향)만 보게 되어 부자연스럽게
                // 배치된다. 보스가 아직 스폰되지 않았을 수도 있으므로
                // 정적 필드로 넘겨 나중에 보스가 참조하게 한다.
                FinalBossAttackController.SetLocalViewerCamera(playerCamera);
            }
        }
    }


    public override void FixedUpdateNetwork()
    {
        // Shared Mode에서는 이 오브젝트의 State Authority(=스폰한 본인)만
        // 실제 이동을 시뮬레이션한다. 그렇지 않으면 다른 클라이언트가 대신
        // 위치를 계산해 버려 캐릭터가 겹치거나 튄다.
        if (!Object.HasStateAuthority) return;

        // Update()에서 모아둔 마우스 좌우 회전을 시뮬레이션 틱 안에서
        // 적용한다 - 이래야 NetworkTransform이 이 값을 안정적으로
        // 캡처해서 다른 클라이언트에게 동기화한다.
        if (_pendingYawDelta != 0f)
        {
            transform.Rotate(Vector3.up * _pendingYawDelta);
            _pendingYawDelta = 0f;
        }

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

        bool isMoving = direction.sqrMagnitude > 0f;

        Vector3 movement =
            isMoving
                ? direction * moveSpeed * Runner.DeltaTime
                : Vector3.zero;

        // 중력은 입력 여부와 상관없이 항상 적용한다. 안 그러면 서 있을 때는
        // 물론이고, 걷는 도중 지형 턱을 살짝 타고 오른 뒤에도 다시 내려올
        // 방법이 없어 계속 떠 있게 된다.
        movement += Vector3.down * groundStickForce * Runner.DeltaTime;

        _cc.Move(movement);

        ClampPositionInsideBoundary();

        if (isMoving)
        {
            UpdateFootsteps();
        }
        else
        {
            // 멈추면 타이머를 리셋해서, 다시 움직이기 시작하자마자 바로 첫 발소리가 나게 한다.
            _footstepTimer = footstepInterval;
        }
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


    private float _rotationDebugLogTimer;

    private void Update()
    {
        // 스폰 직후 첫 프레임 등 Fusion이 아직 Object를 연결하기 전에
        // Update()가 먼저 호출될 수 있다 - 안전하게 무시한다.
        if (Object == null) return;

        // 시점 회전이 상대방 화면에 안 보인다는 리포트를 진단하기 위한 임시 로그.
        // isMine 여부와 상관없이(=내 캐릭터든 다른 사람이 조종하는 캐릭터의 proxy든)
        // 2초마다 현재 y축 회전값을 찍는다. 다른 사람이 실제로 마우스를 돌릴 때
        // 이 값이 콘솔에서 실제로 바뀌는지 보면, 네트워크 동기화 문제인지
        // 단순히 눈으로 보이는(렌더링) 문제인지 구분할 수 있다.
        // 문제 없는 게 확인되면 이 블록은 지워도 된다.
        _rotationDebugLogTimer += Time.deltaTime;
        if (_rotationDebugLogTimer >= 2f)
        {
            _rotationDebugLogTimer = 0f;
            Debug.Log(
                $"[NetworkPlayerMovement][회전진단] isMine={Object.HasInputAuthority}, " +
                $"HasStateAuthority={Object.HasStateAuthority}, rotation.y={transform.eulerAngles.y:F1}");
        }

        // 마우스 입력은 내 클라이언트에서만 읽는다. 좌우 회전량은 아래에서
        // 모아뒀다가 FixedUpdateNetwork()에서 실제로 적용하고(동기화 필요),
        // 카메라 상하 회전은 로컬 전용이라 여기서 바로 적용한다.
        if (!Object.HasInputAuthority) return;

        // XR 헤드셋(TrackedPoseDriver)이 이미 카메라 회전을 담당하고 있다면
        // 마우스 회전 코드가 그 위에 덮어써서 시야가 안 돌아가는 문제가 생긴다 - 건너뛴다.
        if (_headTrackedPoseDriver != null && _headTrackedPoseDriver.enabled) return;

        if (Mouse.current == null) return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue() * mouseSensitivity;

        // 플레이어 몸통 좌우 회전은 여기서 바로 적용하지 않고 모아뒀다가
        // FixedUpdateNetwork()에서 적용한다(위 _pendingYawDelta 설명 참고).
        _pendingYawDelta += mouseDelta.x;

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

        if (_hasCachedBoundary)
        {
            Vector3 boundaryCenter = _cachedBoundaryCenter;
            float allowedRadius = Mathf.Max(0f, _cachedBoundaryRadius - boundaryPadding);

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

        // GameObject.Find()는 "현재 비활성 상태인" 오브젝트를 찾지 못한다.
        // Road_0은 DreamRoadRevealController가 씬 시작 시 잠깐 꺼두었다가
        // 스토리 진행에 맞춰 다시 켜는 연출용 오브젝트라서, 스폰 시점에
        // 마침 비활성 상태면 기존 GameObject.Find()로는 절대 찾을 수 없었다.
        // (이것이 "경계 제한이 전혀 적용되지 않는" 버그의 원인이었다.)
        // 비활성 오브젝트도 포함해서 이름으로 찾도록 바꾼다.
        GameObject stageObject = FindObjectByNameIncludingInactive(startStageObjectName);

        if (stageObject == null)
        {
            Debug.LogWarning(
                $"[NetworkPlayerMovement] 시작 스테이지 오브젝트 '{startStageObjectName}'를 " +
                "씬에서 찾지 못했습니다. 사각형 이동 제한이 적용되지 않습니다.");
            return;
        }

        // 마찬가지로 Road_0이 비활성 상태일 때는 그 자식 렌더러들도
        // GetComponentsInChildren(false)로는 찾지 못하므로 true를 넘겨야 한다.
        Renderer[] renderers = stageObject.GetComponentsInChildren<Renderer>(true);

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
    /// GameObject.Find()와 달리 비활성 상태인 오브젝트도 이름으로 찾는다.
    /// (씬 시작 연출 때문에 잠깐 꺼져 있는 오브젝트를 찾을 때 사용한다.)
    /// </summary>
    private static GameObject FindObjectByNameIncludingInactive(string name)
    {
        // 이 클래스(NetworkBehaviour)에는 이미 인스턴스 프로퍼티 "Object"(Fusion의
        // NetworkObject 접근자)가 있어서, 정적 메서드 안에서 그냥 "Object"라고 쓰면
        // UnityEngine.Object가 아니라 그 인스턴스 프로퍼티로 해석되어 컴파일 에러가 난다.
        // 그래서 UnityEngine.Object로 완전한 이름을 명시한다.
        Transform[] allTransforms =
            UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i] != null && allTransforms[i].name == name)
            {
                return allTransforms[i].gameObject;
            }
        }

        return null;
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


    /// <summary>
    /// target과 그 모든 자식의 레이어를 재귀적으로 바꾼다.
    /// 다른 플레이어(proxy) 캐릭터의 몸을 "LocalPlayer" 레이어에서 빼내
    /// 내 카메라의 Culling Mask에 다시 걸리게 할 때 사용한다.
    /// </summary>
    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;

        Transform targetTransform = target.transform;

        for (int i = 0; i < targetTransform.childCount; i++)
        {
            SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
        }
    }
}
