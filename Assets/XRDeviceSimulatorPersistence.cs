using UnityEngine;

/// <summary>
/// XR Device Simulator가 씬 전환(로비 -> 게임플레이 맵) 이후에도 하나만 유지되도록 관리한다.
///
/// 문제 상황: LobbyScene과 Dreamland_map_3 양쪽에 각자 "XR Device Simulator" 프리팹 인스턴스가
/// 배치되어 있는데, Fusion의 Runner.LoadScene(Single)로 씬을 전환할 때 Unity 내부 처리 순서에 따라
/// 새 씬의 Simulator가 Awake되는 시점에 이전 씬의 Simulator가 아직 완전히 정리되지 않은 경우가 있다.
/// 이 경우 XR Interaction Toolkit의 SimulatedDeviceLifecycleManager가 "이미 인스턴스가 존재한다"고
/// 판단해 새로 로드된(=현재 활성 씬의) Simulator 오브젝트를 통째로 Destroy 해버려서,
/// 결과적으로 새 씬에는 동작하는 Simulator가 하나도 남지 않게 된다. (우클릭 시야 회전이 안 되는 원인)
///
/// 해결: 이 스크립트를 XR Device Simulator 프리팹 루트에 붙여서, 씬 전체를 통틀어 딱 하나의
/// Simulator만 살아남도록 직접 관리한다. 처음 생성된 인스턴스는 DontDestroyOnLoad로 유지되고,
/// 이후 다른 씬에서 새로 로드되는 Simulator는 스스로를 파괴해 기존 인스턴스에게 양보한다.
/// (StartScene에는 Simulator가 없고 LobbyScene에서 처음 생성되므로, 로비부터 게임플레이 맵까지
/// 계속 같은 Simulator 하나가 이어져서 사용된다. Dreamland_map_3를 단독 실행하는 경우에도
/// 그 시점에 살아있는 인스턴스가 없으므로 정상적으로 새로 생성/유지된다.)
/// </summary>
public class XRDeviceSimulatorPersistence : MonoBehaviour
{
    private static XRDeviceSimulatorPersistence _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
