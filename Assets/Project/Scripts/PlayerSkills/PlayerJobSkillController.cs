using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 현재 직업에 맞는 스킬을 선택하고 쿨타임을 관리합니다.
/// 입력 장치와 실제 스킬 효과 사이의 연결점은 TryUseCurrentJobSkill 하나뿐입니다.
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
    [Min(0f)] [SerializeField] private float builderCooldown = 8f;

    [Header("직업별 스킬 구현")]
    [SerializeField] private PoliceSkill policeSkill = new PoliceSkill();
    [SerializeField] private FirefighterSkill firefighterSkill = new FirefighterSkill();
    [SerializeField] private ChefSkill chefSkill = new ChefSkill();
    [SerializeField] private BuilderSkill builderSkill = new BuilderSkill();

    private float policeReadyTime;
    private float firefighterReadyTime;
    private float chefReadyTime;
    private float builderReadyTime;

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
        PollKeyboardTestInput();
    }

    /// <summary>
    /// PC 동작 확인만을 위한 임시 입력입니다.
    /// XR 연결 시 이 메서드 호출을 제거하고 왼손 입력에서
    /// TryUseCurrentJobSkill을 호출하면 됩니다.
    /// </summary>
    private void PollKeyboardTestInput()
    {
        if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame)
        {
            return;
        }

        TryUseCurrentJobSkill();
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

        if (now < GetReadyTime(job))
        {
            return false;
        }

        JobSkillContext context = new JobSkillContext(skillOrigin, skillDirection);
        GetSkill(job).Execute(context);
        SetReadyTime(job, now + GetCooldown(job));
        return true;
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
