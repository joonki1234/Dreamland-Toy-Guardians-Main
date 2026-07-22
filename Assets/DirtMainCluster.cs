using UnityEngine;

public class DirtMainCluster : MonoBehaviour
{
    [Header("포물선 자연 분산 세팅")]
    public GameObject subDirtPrefab;   // 흙 알갱이 프리팹
    public int subShardCount = 8;      // 흩뿌려질 알갱이 개수
    public float splitTime = 0.22f;    // 포물선으로 날아가다 퍼지는 시간 (0.22초)
    public float spreadAmount = 2.2f;  // 옆으로 흩뿌려지는 폭

    private bool isSplit = false;
    private Rigidbody mainRb;

    private void Awake()
    {
        mainRb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // 지정된 시간(splitTime) 후 포물선 중간에서 분산
        Invoke(nameof(SplitIntoDirtPebbles), splitTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 땅이나 벽에 먼저 닿으면 즉시 퍼짐
        if (!isSplit)
        {
            CancelInvoke(nameof(SplitIntoDirtPebbles));
            SplitIntoDirtPebbles();
        }
    }

    private void SplitIntoDirtPebbles()
    {
        if (isSplit) return;
        isSplit = true;

        Vector3 currentVelocity = mainRb != null ? mainRb.linearVelocity : transform.forward * 16f;

        if (subDirtPrefab != null)
        {
            for (int i = 0; i < subShardCount; i++)
            {
                Vector3 spawnOffset = Random.insideUnitSphere * 0.15f;
                GameObject shard = Instantiate(subDirtPrefab, transform.position + spawnOffset, Quaternion.identity);

                // 1. 생성된 파편에서 분열 스크립트를 즉시 완전 제거 (자가 복제/자폭 차단)
                DirtMainCluster childCluster = shard.GetComponent<DirtMainCluster>();
                if (childCluster != null)
                {
                    DestroyImmediate(childCluster);
                }

                // 2. 안전한 파편 전용 스크립트 부착
                DirtShardItem shardItem = shard.GetComponent<DirtShardItem>();
                if (shardItem == null) shardItem = shard.AddComponent<DirtShardItem>();

                // 3. 눈에 확실히 보이는 적당한 흙 알갱이 크기 (0.35 ~ 0.5)
                float randomScale = Random.Range(0.35f, 0.5f);
                shard.transform.localScale = Vector3.one * randomScale;

                // 4. 관성(포물선) + 옆으로 부채꼴 흩뿌리기
                Rigidbody shardRb = shard.GetComponent<Rigidbody>();
                if (shardRb != null)
                {
                    Vector3 spreadVector = new Vector3(
                        Random.Range(-spreadAmount, spreadAmount),
                        Random.Range(-0.2f, 1.2f),
                        Random.Range(-0.2f, 0.2f)
                    );

                    shardRb.linearVelocity = currentVelocity + spreadVector;
                }
            }
        }

        // 메인 덩어리 삭제
        Destroy(gameObject);
    }
}