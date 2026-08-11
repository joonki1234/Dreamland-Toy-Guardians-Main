using UnityEngine;

/// <summary>
/// Dreamland_map_3를 로비를 거치지 않고 이 씬에서 바로 Play를 눌러
/// 테스트하고 싶을 때를 위한 개발용 진입점.
///
/// StartScene -> LobbyScene을 거쳐 정상적으로 들어온 경우에는
/// LobbyScene에서 만들어진 RoomManager가 DontDestroyOnLoad로 이미 살아있으므로
/// 이 스크립트는 아무 것도 하지 않고 스스로를 비활성화한다.
///
/// 반대로 이 씬을 단독으로 열어서 Play를 눌렀다면(=RoomManager가 없다면)
/// 직접 RoomManager를 하나 만들어서 접속하고, 직업 선택 화면 없이
/// 바로 devDefaultJob으로 캐릭터를 스폰한다.
/// </summary>
public class DreamlandMapDevEntry : MonoBehaviour
{
    [Tooltip("LobbyScene의 '@RoomManager'와 동일한 프리팹(필드가 전부 채워진 것)을 연결하세요.")]
    [SerializeField] private RoomManager roomManagerPrefab;

    [Tooltip("로비 없이 바로 시작할 때 사용할 기본 직업입니다.")]
    [SerializeField] private PlayerJob devDefaultJob = PlayerJob.Police;

    private void Awake()
    {
        // 로비를 거쳐 들어온 정상 흐름이면 RoomManager가 이미 존재한다 - 중복 접속 방지.
        if (FindAnyObjectByType<RoomManager>() != null)
        {
            Destroy(gameObject);
            return;
        }

        if (roomManagerPrefab == null)
        {
            Debug.LogError("[DreamlandMapDevEntry] Room Manager Prefab이 연결되지 않았습니다.");
            return;
        }

        RoomManager roomManager = Instantiate(roomManagerPrefab);
        roomManager.EnableDevDirectMode(devDefaultJob);
    }
}
