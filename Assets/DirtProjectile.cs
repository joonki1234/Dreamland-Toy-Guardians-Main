using UnityEngine;
using DreamGuardians;

/// <summary>
/// 건축가가 삽으로 흩뿌리는 흙 파편.
///
/// 적에게 실제 충돌하면 피해를 주고,
/// 바닥에 충돌한 파편 중 하나만 MudSplat을 생성한다.
/// </summary>
[DisallowMultipleComponent]
public class DirtProjectile : MonoBehaviour
{
    [Header("충돌 시 생성할 흙 함정")]
    [SerializeField]
    private GameObject mudSplatPrefab;

    [Header("흙 함정 유지 시간")]
    [SerializeField, Min(0.1f)]
    private float destroyDelay = 30f;

    [Header("바닥 판정")]
    [Tooltip("충돌 표면의 위쪽 방향이 이 값 이상일 때 바닥으로 판단합니다.")]
    [SerializeField, Range(0f, 1f)]
    private float minimumGroundNormalY = 0.45f;

    private DirtShotContext shotContext;
    private int projectileShotId = -1;
    private bool hasHit;

    /// <summary>
    /// PlayerJobController가 파편을 생성한 직후 호출한다.
    ///
    /// 같은 삽질에서 생성된 모든 파편은
    /// 동일한 DirtShotContext를 공유한다.
    /// </summary>
    public void Initialize(
        DirtShotContext context,
        int uniqueProjectileShotId)
    {
        shotContext = context;
        projectileShotId = uniqueProjectileShotId;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit)
        {
            return;
        }

        EnemyHealth enemy =
            collision.collider.GetComponentInParent<EnemyHealth>();

        if (enemy != null)
        {
            HitEnemy(enemy, collision);
            return;
        }

        HitEnvironment(collision);
    }

    /// <summary>
    /// 흙 파편이 몬스터에게 직접 충돌했을 때 실행한다.
    /// </summary>
    private void HitEnemy(
        EnemyHealth enemy,
        Collision collision)
    {
        hasHit = true;

        Vector3 hitPoint = transform.position;

        if (collision.contactCount > 0)
        {
            hitPoint = collision.GetContact(0).point;
        }

        // 정상적으로는 PlayerJobController에서 반드시 전달된다.
        // 혹시 직접 생성된 DirtBlock이 있다면 기본값으로 처리한다.
        shotContext ??= new DirtShotContext(
            8f,
            2f,
            18f
        );

        float damage =
            shotContext.ClaimDamage(
                enemy.GetInstanceID()
            );

        if (damage > 0f)
        {
            DamageInfo damageInfo = new DamageInfo(
                damage,
                "BUILDER_DIRT_SHARD",
                PlayerRole.Architect,
                projectileShotId,
                hitPoint,
                false
            );

            bool damageApplied =
                enemy.TakeDamage(damageInfo);

            if (damageApplied)
            {
                Debug.Log(
                    $"건축가 흙 파편 실제 충돌 피해: " +
                    $"{enemy.gameObject.name} / {damage}"
                );
            }
        }

        // 몬스터에게 맞은 파편은 장판을 만들지 않고 사라진다.
        Destroy(gameObject);
    }

    /// <summary>
    /// 흙 파편이 바닥이나 벽 같은 환경에 충돌했을 때 실행한다.
    /// </summary>
    private void HitEnvironment(Collision collision)
    {
        hasHit = true;

        if (collision.contactCount <= 0)
        {
            Destroy(gameObject);
            return;
        }

        ContactPoint contact =
            collision.GetContact(0);

        // 위쪽을 향하는 표면만 바닥으로 인정한다.
        // 벽이나 오브젝트 옆면에는 MudSplat을 생성하지 않는다.
        bool isGround =
            contact.normal.y >= minimumGroundNormalY;

        if (isGround)
        {
            shotContext ??= new DirtShotContext(
                8f,
                2f,
                18f
            );

            // 같은 삽질에서 가장 먼저 바닥에 닿은 파편만
            // MudSplat을 하나 생성한다.
            if (shotContext.TryClaimMudSplat())
            {
                CreateMudSplat(contact);
            }
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 바닥 표면 방향에 맞춰 MudSplat을 생성한다.
    /// </summary>
    private void CreateMudSplat(ContactPoint contact)
    {
        if (mudSplatPrefab == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: " +
                "MudSplat Prefab이 연결되지 않았습니다."
            );

            return;
        }

        Vector3 spawnPosition =
            contact.point +
            contact.normal * 0.01f;

        // Unity 기본 Quad의 앞면은 로컬 Z축 방향이다.
        // Quad의 앞면을 바닥 법선 방향과 일치시킨다.
        Quaternion surfaceRotation =
            Quaternion.FromToRotation(
                Vector3.forward,
                contact.normal
            );

        // 항상 같은 무늬 방향으로 보이지 않도록
        // 바닥 법선을 중심으로 무작위 회전을 추가한다.
        Quaternion randomRotation =
            Quaternion.AngleAxis(
                Random.Range(0f, 360f),
                contact.normal
            );

        GameObject splat = Instantiate(
            mudSplatPrefab,
            spawnPosition,
            randomRotation * surfaceRotation
        );

        Destroy(splat, destroyDelay);
    }

    private void OnValidate()
    {
        destroyDelay =
            Mathf.Max(0.1f, destroyDelay);

        minimumGroundNormalY =
            Mathf.Clamp01(minimumGroundNormalY);
    }
}