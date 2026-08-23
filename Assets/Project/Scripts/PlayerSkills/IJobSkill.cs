/// <summary>
/// 모든 직업 스킬이 따르는 공통 실행 규약입니다.
/// </summary>
public interface IJobSkill
{
    void Execute(JobSkillContext context);
}
