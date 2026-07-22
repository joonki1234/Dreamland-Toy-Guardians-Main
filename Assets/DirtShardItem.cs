using UnityEngine;

public class DirtShardItem : MonoBehaviour
{
    public float lifeTime = 3.5f; // 파편이 땅에 남아서 유지되는 시간
    public int damage = 10;       // 적에게 줄 데미지

    private void Start()
    {
        // 3.5초 뒤에 삭제 (에러 없이 씬에 안전하게 유지)
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 적 태그가 유니티에 없을 때 크래시가 나지 않도록 안전하게 검사
        if (collision.gameObject.HasTag("Enemy")) 
        {
            // 적에게 부딪혔을 때만 삭제
            Destroy(gameObject);
        }
        else if (collision.gameObject.name.Contains("Enemy") || collision.gameObject.name.Contains("Monster"))
        {
            Destroy(gameObject);
        }
        // 땅, 벽, 자기들끼리 부딪힐 때는 삭제되지 않고 튕기기만 함!
    }
}

// 태그 미등록으로 인한 에러를 방지해 주는 확장 메서드
public static class GameObjectExtensions
{
    public static bool HasTag(this GameObject go, string tagName)
    {
        try
        {
            return go.CompareTag(tagName);
        }
        catch
        {
            return false;
        }
    }
}