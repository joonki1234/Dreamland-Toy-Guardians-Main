using UnityEngine;

/// <summary>
/// 직업 스킬이 실행될 때 필요한 공통 공간 정보입니다.
/// 실제 스킬 효과가 추가되어도 입력 장치에 의존하지 않습니다.
/// </summary>
public readonly struct JobSkillContext
{
    public JobSkillContext(Transform origin, Transform direction)
    {
        Origin = origin;
        Direction = direction;
    }

    public Transform Origin { get; }
    public Transform Direction { get; }
    public Vector3 Position => Origin.position;
    public Vector3 Forward => Direction.forward;
}
