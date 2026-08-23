using System;
using UnityEngine;

[Serializable]
public sealed class ChefSkill : IJobSkill
{
    public void Execute(JobSkillContext context)
    {
        // TODO: 스페셜 메뉴 효과를 이 클래스에서 구현합니다.
        Debug.Log($"[ChefSkill] 스페셜 메뉴 호출 (Origin: {context.Origin.name})");
    }
}
