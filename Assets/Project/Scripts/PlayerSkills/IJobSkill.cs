/// <summary>
/// 모든 직업 스킬이 따르는 공통 실행 규약입니다.
/// </summary>
public interface IJobSkill
{
    /// <summary>
    /// dealsDamage가 false면(다른 클라이언트에서 재생되는 "보여주기용" 실행) 연출은
    /// 그대로 재생하되 실제 피해는 적용하지 않는다 - 스킬을 실제로 쓴 사람의
    /// 화면(dealsDamage=true)에서만 피해가 적용돼야, 모든 클라이언트가 각자
    /// 연출을 재생해도 피해가 인원수만큼 중복으로 들어가지 않는다.
    /// </summary>
    /// <returns>True only after the existing skill effect has been created and initialized.</returns>
    bool Execute(JobSkillContext context, bool dealsDamage);
}
