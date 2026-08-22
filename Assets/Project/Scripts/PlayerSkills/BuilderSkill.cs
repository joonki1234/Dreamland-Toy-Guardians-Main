using System;
using UnityEngine;

[Serializable]
public sealed class BuilderSkill : IJobSkill
{
    public void Execute(JobSkillContext context)
    {
        // TODO: 긴급 철거 효과를 이 클래스에서 구현합니다.
        Debug.Log($"[BuilderSkill] 긴급 철거 호출 (Origin: {context.Origin.name})");
    }
}
