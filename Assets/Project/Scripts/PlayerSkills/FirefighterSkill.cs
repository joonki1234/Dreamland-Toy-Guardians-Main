using System;
using UnityEngine;

[Serializable]
public sealed class FirefighterSkill : IJobSkill
{
    public void Execute(JobSkillContext context)
    {
        // TODO: 소방차 출동 효과를 이 클래스에서 구현합니다.
        Debug.Log($"[FirefighterSkill] 소방차 출동 호출 (Origin: {context.Origin.name})");
    }
}
