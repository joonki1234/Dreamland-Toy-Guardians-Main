using UnityEngine;

/// <summary>
/// Editor에서 첫 Classic Simulator를 씬 전환 후에도 유지한다.
/// 중복 프리팹은 XRI의 Awake/OnEnable 전에 비활성화해야 한다.
/// Destroy만 예약하면 중복 Simulator의 OnDisable이 공유 Action Asset을 끌 수 있다.
/// </summary>
[DefaultExecutionOrder(-32000)] // Before XRI lifecycle (-29995) and simulator (-29991).
public class XRDeviceSimulatorPersistence : MonoBehaviour
{
    private static XRDeviceSimulatorPersistence _instance;

    private void Awake()
    {
#if !UNITY_EDITOR
        // Scene-placed simulators must not replace physical XR devices in a player build.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return;
#else
        if (_instance != null && _instance != this)
        {
            // Prevent XRI callbacks before deferred destruction at the end of the frame.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnLoad()
    {
        _instance = null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
