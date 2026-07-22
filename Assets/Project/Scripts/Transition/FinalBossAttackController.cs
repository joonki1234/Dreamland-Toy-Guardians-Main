using DreamGuardians;
using UnityEngine;

/// <summary>
/// 프로토타입 최종 보스가 일정 간격으로 코어에 피해를 주도록 합니다.
/// 실제 보스 패턴이 구현되면 이 컴포넌트를 패턴 시스템으로 교체할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalBossAttackController : MonoBehaviour
{
    [SerializeField]
    private CoreState targetCore;

    [Min(0f)]
    [SerializeField]
    private float coreDamage = 20f;

    [Min(0.1f)]
    [SerializeField]
    private float attackInterval = 2.5f;

    [Min(0f)]
    [SerializeField]
    private float firstAttackDelay = 4f;

    private EnemyHealth health;
    private float nextAttackTime;
    private bool configured;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        health ??= GetComponent<EnemyHealth>();

        if (health != null)
        {
            health.Died -= HandleBossDied;
            health.Died += HandleBossDied;
        }

        nextAttackTime = Time.time + firstAttackDelay;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleBossDied;
        }
    }

    private void Update()
    {
        if (!configured ||
            targetCore == null ||
            targetCore.IsDestroyed ||
            (health != null && health.IsDead))
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackInterval;
        targetCore.TakeDamage(coreDamage);

        Debug.Log(
            "[FinalBoss] 보스가 코어에 " +
            coreDamage.ToString("0.#") + " 피해를 가했습니다.",
            this);
    }

    public void Configure(
        CoreState core,
        float damage,
        float interval,
        float initialDelay)
    {
        targetCore = core;
        coreDamage = Mathf.Max(0f, damage);
        attackInterval = Mathf.Max(0.1f, interval);
        firstAttackDelay = Mathf.Max(0f, initialDelay);
        nextAttackTime = Time.time + firstAttackDelay;
        configured = true;
        enabled = true;
    }

    private void HandleBossDied(EnemyHealth _, DamageInfo __)
    {
        configured = false;
        enabled = false;
    }

    private void OnValidate()
    {
        coreDamage = Mathf.Max(0f, coreDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
    }
}
