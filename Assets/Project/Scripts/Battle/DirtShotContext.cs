using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건축가가 한 번 삽질했을 때 생성되는 모든 흙 파편이
/// 함께 사용하는 공격 정보다.
///
/// 같은 적에게 여러 파편이 맞았을 때 피해량을 제한하고,
/// 한 번의 공격에서 MudSplat이 하나만 생성되도록 관리한다.
/// </summary>
public sealed class DirtShotContext
{
    private readonly Dictionary<int, float> accumulatedDamageByEnemy =
        new Dictionary<int, float>();

    private readonly float firstShardDamage;
    private readonly float additionalShardDamage;
    private readonly float maximumDamagePerEnemy;

    private bool mudSplatCreated;

    public DirtShotContext(
        float firstShardDamage,
        float additionalShardDamage,
        float maximumDamagePerEnemy)
    {
        this.firstShardDamage =
            Mathf.Max(0f, firstShardDamage);

        this.additionalShardDamage =
            Mathf.Max(0f, additionalShardDamage);

        this.maximumDamagePerEnemy =
            Mathf.Max(0f, maximumDamagePerEnemy);
    }

    /// <summary>
    /// 같은 공격에서 해당 적에게 이번 파편이 줄 피해량을 반환한다.
    ///
    /// 첫 번째 파편은 큰 피해를 주고,
    /// 두 번째 파편부터는 감소된 피해를 준다.
    /// 최대 피해량에 도달하면 0을 반환한다.
    /// </summary>
    public float ClaimDamage(int enemyInstanceId)
    {
        accumulatedDamageByEnemy.TryGetValue(
            enemyInstanceId,
            out float accumulatedDamage
        );

        float requestedDamage =
            accumulatedDamage <= 0f
                ? firstShardDamage
                : additionalShardDamage;

        float remainingDamage =
            maximumDamagePerEnemy - accumulatedDamage;

        float appliedDamage =
            Mathf.Clamp(
                requestedDamage,
                0f,
                remainingDamage
            );

        accumulatedDamageByEnemy[enemyInstanceId] =
            accumulatedDamage + appliedDamage;

        return appliedDamage;
    }

    /// <summary>
    /// 한 번의 공격에서 가장 먼저 바닥에 닿은 파편만
    /// MudSplat 생성 권한을 얻는다.
    /// </summary>
    public bool TryClaimMudSplat()
    {
        if (mudSplatCreated)
        {
            return false;
        }

        mudSplatCreated = true;
        return true;
    }
}