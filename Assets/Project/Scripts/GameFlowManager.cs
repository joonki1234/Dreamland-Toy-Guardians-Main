using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [Header("Game Phases")]
    [SerializeField] private GameObject phase01Reality;
    [SerializeField] private GameObject phase02Crack;
    [SerializeField] private GameObject phase03Dreamland;
    [SerializeField] private GameObject phase04Boss;
    [SerializeField] private GameObject phase05Clear;

    private GameObject[] phases;

    private void Awake()
    {
        phases = new GameObject[]
        {
            phase01Reality,
            phase02Crack,
            phase03Dreamland,
            phase04Boss,
            phase05Clear
        };
    }

    private void Start()
    {
        // 게임 시작 시 Phase 1만 활성화
        SetPhase(0);
    }

    public void SetPhase(int phaseIndex)
    {
        if (phaseIndex < 0 || phaseIndex >= phases.Length)
        {
            Debug.LogWarning("잘못된 Phase 번호입니다.");
            return;
        }

        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i] != null)
            {
                phases[i].SetActive(i == phaseIndex);
            }
        }

        Debug.Log("Phase " + (phaseIndex + 1) + " 시작");
    }

    [ContextMenu("Test Phase 1")]
    private void TestPhase1()
    {
        SetPhase(0);
    }

    [ContextMenu("Test Phase 2")]
    private void TestPhase2()
    {
        SetPhase(1);
    }

    [ContextMenu("Test Phase 3")]
    private void TestPhase3()
    {
        SetPhase(2);
    }

    [ContextMenu("Test Phase 4")]
    private void TestPhase4()
    {
        SetPhase(3);
    }

    [ContextMenu("Test Phase 5")]
    private void TestPhase5()
    {
        SetPhase(4);
    }
}