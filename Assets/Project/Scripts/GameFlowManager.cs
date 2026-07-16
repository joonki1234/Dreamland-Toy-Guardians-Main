using System.Collections;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [Header("Game Phases")]
    [SerializeField] private GameObject phase01Reality;
    [SerializeField] private GameObject phase02Crack;
    [SerializeField] private GameObject phase03Dreamland;
    [SerializeField] private GameObject phase04Boss;
    [SerializeField] private GameObject phase05Clear;

    [Header("Part 1 Steps")]
    [SerializeField] private GameObject p1PortalCore;
    [SerializeField] private GameObject p1ToyFriend;
    [SerializeField] private GameObject p1Weapon;
    [SerializeField] private GameObject p1TutorialEnemy;
    [SerializeField] private GameObject p1Wave1Intro;

    [Header("Wave 1 Steps")]
    [SerializeField] private GameObject w1Intro;
    [SerializeField] private GameObject w1FirstAttack;
    [SerializeField] private GameObject w1MRChange1;
    [SerializeField] private GameObject w1SecondAttack;
    [SerializeField] private GameObject w1MRChange2;
    [SerializeField] private GameObject w1FinalAttack;
    [SerializeField] private GameObject w1Clear;

    [Header("Enemy Spawn")]
    [SerializeField] private EnemySpawner enemySpawner;

    private GameObject[] wave1Steps;
    private Coroutine wave1Routine;
    private GameObject[] phases;
    private GameObject[] part1Steps;
    private int currentPart1Step = 0;

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

        part1Steps = new GameObject[]
        {
            p1PortalCore,
            p1ToyFriend,
            p1Weapon,
            p1TutorialEnemy,
            p1Wave1Intro
        };

        wave1Steps = new GameObject[]
        {
            w1Intro,
            w1FirstAttack,
            w1MRChange1,
            w1SecondAttack,
            w1MRChange2,
            w1FinalAttack,
            w1Clear
        };
    }

    private void Start()
    {
        SetPhase(0);

        // Part 1의 모든 이벤트 오브젝트를 처음에는 끈다.
        for (int i = 0; i < part1Steps.Length; i++)
        {
            if (part1Steps[i] != null)
            {
                part1Steps[i].SetActive(false);
            }
        }

        // 게임 시작 시 첫 번째 이벤트만 등장
        part1Steps[0].SetActive(true);

        currentPart1Step = 0;
        Debug.Log("Part 1 - Portal & Core 시작");
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

    public void SetPart1Step(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= part1Steps.Length)
        {
            Debug.LogWarning("잘못된 Part 1 단계입니다.");
            return;
        }

        // 현재 단계까지는 모두 활성화
        for (int i = 0; i < part1Steps.Length; i++)
        {
            if (part1Steps[i] != null)
            {
                part1Steps[i].SetActive(i <= stepIndex);
            }
        }

        Debug.Log("Part 1 Step " + (stepIndex + 1) + " 시작");
    }

    public void NextPart1Step()
    {
        int nextStep = currentPart1Step + 1;

        if (nextStep >= part1Steps.Length)
        {
            Debug.Log("Part 1의 모든 단계가 완료되었습니다.");
            return;
        }

        currentPart1Step = nextStep;
        SetPart1Step(currentPart1Step);
    }

    public void SetWave1Step(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= wave1Steps.Length)
        {
            Debug.LogWarning("잘못된 Wave 1 단계입니다.");
            return;
        }

        for (int i = 0; i < wave1Steps.Length; i++)
        {
            if (wave1Steps[i] != null)
            {
                wave1Steps[i].SetActive(i == stepIndex);
            }
        }

        Debug.Log("Wave 1 Step " + (stepIndex + 1) + " 시작");
    }

    public void StartWave1()
    {
        if (wave1Routine != null)
        {
            StopCoroutine(wave1Routine);
        }

        wave1Routine = StartCoroutine(Wave1StartRoutine());
    }

    private IEnumerator Wave1StartRoutine()
    {
        SetPhase(1);

        // WAVE 1 START UI 단계
        SetWave1Step(0);

        yield return new WaitForSeconds(2f);

        // 첫 번째 공격 단계
        SetWave1Step(1);

        if (enemySpawner != null)
        {
            enemySpawner.SpawnWave(3);
        }
        else
        {
            Debug.LogWarning("EnemySpawner가 연결되지 않았습니다.");
        }

        wave1Routine = null;
    }

    [ContextMenu("Test Start Wave 1")]
    private void TestStartWave1()
    {
        StartWave1();
    }

    [ContextMenu("Test Part 1 Step 1")]
    private void TestPart1Step1()
    {
        SetPart1Step(0);
    }

    [ContextMenu("Test Part 1 Step 2")]
    private void TestPart1Step2()
    {
        SetPart1Step(1);
    }

    [ContextMenu("Test Part 1 Step 3")]
    private void TestPart1Step3()
    {
        SetPart1Step(2);
    }

    [ContextMenu("Test Part 1 Step 4")]
    private void TestPart1Step4()
    {
        SetPart1Step(3);
    }

    [ContextMenu("Test Part 1 Step 5")]
    private void TestPart1Step5()
    {
        SetPart1Step(4);
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

    [ContextMenu("Next Part 1 Step")]
    private void TestNextPart1Step()
    {
        NextPart1Step();
    }
}