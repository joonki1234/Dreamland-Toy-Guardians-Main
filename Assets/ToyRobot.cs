using UnityEngine;
using UnityEngine.AI;

public class ToyRobot : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform targetPlayer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Hierarchy 창에서 "Player"라는 이름을 가진 진짜 몸통을 찾아 타겟으로 잡습니다.
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
        }
    }

    void Update()
    {
        // 이제 쭌키님이 조작하는 캐릭터의 실시간 위치로 로봇이 쫓아옵니다.
        if (agent != null && targetPlayer != null)
        {
            agent.SetDestination(targetPlayer.position);
        }
    }
}