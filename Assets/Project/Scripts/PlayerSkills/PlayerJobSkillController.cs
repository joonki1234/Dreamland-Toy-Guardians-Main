using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 현재 직업에 맞는 스킬을 선택하고 쿨타임을 관리합니다.
/// 입력 장치와 실제 스킬 효과 사이의 연결점은 TryUseCurrentJobSkill 하나뿐입니다.
///
/// 예전에는 GetSkill(job).Execute(context)를 이 클라이언트에서만 직접 호출했다 -
/// 그래서 스킬 연출(꿈빛 총/소방차/특제 메뉴/망치)이 스킬을 쓴 사람 화면에만
/// 보이고 다른 플레이어에게는 전혀 보이지 않았다.
///
/// 이 클래스는 일부러 NetworkBehaviour가 아니라 그냥 MonoBehaviour로 둔다 -
/// Photon Fusion은 NetworkObject 프리팹에 새 NetworkBehaviour를 추가하면
/// 그 프리팹을 에디터에서 다시 열어 저장(bake)해야 하는데, 이 파일은 코드
/// 편집만으로 반영되는 환경이라 그 bake가 이뤄지지 않는다. 그 상태로 실제로
/// 실행하면 NetworkObject의 baked NetworkBehaviour 목록이 어긋나 스폰/매칭
/// 자체가 깨질 수 있다(로비에서 상대방이 아예 인식되지 않는 등). 그래서 RPC는
/// 이미 정상적으로 baked돼 있는 PlayerJobController(NetworkBehaviour)에게
/// 맡기고, 이 클래스는 PlayerJobController.RequestJobSkillExecute(job)을
/// 호출만 하는 순수 로컬 컨트롤러로 남긴다.
/// </summary>
[RequireComponent(typeof(PlayerJobController))]
public sealed class PlayerJobSkillController : MonoBehaviour
{
    [Header("직업 정보")]
    [SerializeField] private PlayerJobController jobController;

    [Header("스킬 기준 Transform")]
    [Tooltip("스킬이 시작되는 위치입니다. 비워두면 이 GameObject의 Transform을 사용합니다.")]
    [SerializeField] private Transform skillOrigin;

    [Tooltip("스킬이 조준하는 방향입니다. 비워두면 Skill Origin을 함께 사용합니다.")]
    [SerializeField] private Transform skillDirection;

    [Header("직업별 쿨타임 (초)")]
    [Min(0f)] [SerializeField] private float policeCooldown = 10f;
    [Min(0f)] [SerializeField] private float firefighterCooldown = 15f;
    [Min(0f)] [SerializeField] private float chefCooldown = 0f;
    [Min(0f)] [SerializeField] private float builderCooldown = 12f;

    [Header("직업별 스킬 구현")]
    [SerializeField] private PoliceSkill policeSkill = new PoliceSkill();
    [SerializeField] private FirefighterSkill firefighterSkill = new FirefighterSkill();
    [SerializeField] private ChefSkill chefSkill = new ChefSkill();
    [SerializeField] private BuilderSkill builderSkill = new BuilderSkill();

    public event System.Action<PlayerJob> OnSkillActivated;
    private bool tutorialPracticeActive;
    private bool tutorialCooldownReset;
    private PlayerJob tutorialUsedJob;
    private bool tutorialSkillUsed;

    public void BeginTutorialPractice()
    {
        if (!CanUseLocalInput() || tutorialSkillUsed) return;
        tutorialPracticeActive = true;
    }

    public void EndTutorialPractice()
    {
        tutorialPracticeActive = false;
        if (CanUseLocalInput() && tutorialSkillUsed && !tutorialCooldownReset)
        {
            SetReadyTime(tutorialUsedJob, Time.time);
            tutorialCooldownReset = true;
        }
    }

    public Vector3 GetTutorialTargetPosition()
    {
        JobSkillContext context = new JobSkillContext(skillOrigin, skillDirection);
        if (jobController.CurrentJob == PlayerJob.Chef)
            return chefSkill.FindTargetGroundPoint(context);

        Vector3 forward = Vector3.ProjectOnPlane(context.Forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = context.Origin.root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        Quaternion frame = Quaternion.LookRotation(forward.normalized, Vector3.up);
        Vector3 offset;
        switch (jobController.CurrentJob)
        {
            case PlayerJob.Firefighter:
                offset = Vector3.forward * firefighterSkill.TutorialTargetDistance;
                break;
            case PlayerJob.Builder:
                offset = builderSkill.TutorialTargetOffset;
                break;
            default:
                offset = Vector3.forward * policeSkill.TutorialTargetDistance;
                break;
        }
        Vector3 position = context.Origin.root.position + frame * offset;
        if (Physics.Raycast(position + Vector3.up * 4f, Vector3.down,
                out RaycastHit hit, 12f, 1 << 8, QueryTriggerInteraction.Ignore))
            position = hit.point;
        return position;
    }

    public bool HasTutorialOrigin => skillOrigin != null && skillDirection != null;

    private float policeReadyTime;
    private float firefighterReadyTime;
    private float chefReadyTime;
    private float builderReadyTime;
    private InputAction leftPrimaryAction;

    private void Awake()
    {
        if (jobController == null)
        {
            jobController = GetComponent<PlayerJobController>();
        }

        if (skillOrigin == null)
        {
            skillOrigin = transform;
        }

        if (skillDirection == null)
        {
            skillDirection = skillOrigin;
        }

        policeSkill ??= new PoliceSkill();
        firefighterSkill ??= new FirefighterSkill();
        chefSkill ??= new ChefSkill();
        builderSkill ??= new BuilderSkill();
    }

    private void Update()
    {
        if (!CanUseLocalInput())
        {
            return;
        }

        // Quest X and the simulator both feed the same left XR controller button.
        // Own this action locally; do not enable or disable shared XRI action maps.
        leftPrimaryAction ??= new InputAction(
            "Left Primary Skill", InputActionType.Button,
            "<XRController>{LeftHand}/primaryButton");
        if (!leftPrimaryAction.enabled)
        {
            leftPrimaryAction.Enable();
        }

        // Preserve the existing P test fallback, with at most one request per frame.
        bool keyboardSkillPressed = Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
        if (leftPrimaryAction.WasPressedThisFrame() || keyboardSkillPressed)
        {
            TryUseCurrentJobSkill();
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            CancelBuilderSkill();
        }
    }

    /// <summary>
    /// 입력 종류와 무관한 공통 스킬 진입점입니다.
    /// </summary>
    public bool TryUseCurrentJobSkill()
    {
        if (!CanUseLocalInput() || skillOrigin == null || skillDirection == null)
        {
            return false;
        }

        PlayerJob job = jobController.CurrentJob;
        float now = Time.time;

        if (now < GetReadyTime(job) ||
            (job == PlayerJob.Builder && builderSkill.IsActive))
        {
            return false;
        }

        // 쿨타임/IsActive 판정은 실제로 스킬을 쓴 사람의 클라이언트에서만
        // 한 번 확인한다(로컬 전용). jobController 쪽에서 다시 검사하지
        // 않는 이유는 RPC가 이미 이 검사를 통과한 뒤에만 보내지기 때문이다.
        jobController.RequestJobSkillExecute(job);
        SetReadyTime(job, now + GetCooldown(job));
        return true;
    }

    /// <summary>
    /// PlayerJobController의 RPC_PlayJobSkillEffect가 모든 클라이언트에서
    /// 호출한다. dealsDamage는 호출한 클라이언트마다 다르게 평가된
    /// Object.HasInputAuthority 값이다(실제로 스킬을 쓴 사람 화면에서만 true).
    /// </summary>
    public void ExecuteSkillLocally(PlayerJob job, bool dealsDamage)
    {
        if (skillOrigin == null || skillDirection == null)
        {
            return;
        }

        JobSkillContext context = new JobSkillContext(skillOrigin, skillDirection);
        if (!GetSkill(job).Execute(context, dealsDamage)) return;
        // Only the input owner can confirm a real activation. Remote effects are presentation.
        if (!dealsDamage || !CanUseLocalInput()) return;
        if (tutorialPracticeActive && !tutorialSkillUsed)
        {
            tutorialSkillUsed = true;
            tutorialUsedJob = job;
            tutorialPracticeActive = false;
            jobController.ReportTutorialSkillUsed();
        }
        OnSkillActivated?.Invoke(job);
    }

    /// <summary>XR 입력과 분리된 건축가 긴급 철거 전용 진입점입니다.</summary>
    public bool TryActivateBuilderSkill()
    {
        if (jobController == null || jobController.CurrentJob != PlayerJob.Builder)
        {
            return false;
        }

        return TryUseCurrentJobSkill();
    }

    /// <summary>진행 중인 긴급 철거의 망치와 Swing VFX를 안전하게 정리합니다.</summary>
    public void CancelBuilderSkill()
    {
        builderSkill?.Cancel();
    }

    private void OnDisable()
    {
        EndTutorialPractice();
        leftPrimaryAction?.Disable();
        CancelBuilderSkill();
    }

    private void OnDestroy()
    {
        leftPrimaryAction?.Dispose();
        leftPrimaryAction = null;
        CancelBuilderSkill();
    }

    private bool CanUseLocalInput()
    {
        return jobController != null &&
               jobController.Object != null &&
               jobController.Object.HasInputAuthority;
    }

    private IJobSkill GetSkill(PlayerJob job)
    {
        switch (job)
        {
            case PlayerJob.Police: return policeSkill;
            case PlayerJob.Firefighter: return firefighterSkill;
            case PlayerJob.Chef: return chefSkill;
            case PlayerJob.Builder: return builderSkill;
            default: return policeSkill;
        }
    }

    private float GetCooldown(PlayerJob job)
    {
        switch (job)
        {
            case PlayerJob.Police: return policeCooldown;
            case PlayerJob.Firefighter: return firefighterCooldown;
            case PlayerJob.Chef: return chefCooldown;
            case PlayerJob.Builder: return builderCooldown;
            default: return 0f;
        }
    }

    private float GetReadyTime(PlayerJob job)
    {
        switch (job)
        {
            case PlayerJob.Police: return policeReadyTime;
            case PlayerJob.Firefighter: return firefighterReadyTime;
            case PlayerJob.Chef: return chefReadyTime;
            case PlayerJob.Builder: return builderReadyTime;
            default: return float.PositiveInfinity;
        }
    }

    private void SetReadyTime(PlayerJob job, float readyTime)
    {
        switch (job)
        {
            case PlayerJob.Police: policeReadyTime = readyTime; break;
            case PlayerJob.Firefighter: firefighterReadyTime = readyTime; break;
            case PlayerJob.Chef: chefReadyTime = readyTime; break;
            case PlayerJob.Builder: builderReadyTime = readyTime; break;
        }
    }

    private void OnValidate()
    {
        policeCooldown = Mathf.Max(0f, policeCooldown);
        firefighterCooldown = Mathf.Max(0f, firefighterCooldown);
        chefCooldown = Mathf.Max(0f, chefCooldown);
        builderCooldown = Mathf.Max(0f, builderCooldown);
    }
}
