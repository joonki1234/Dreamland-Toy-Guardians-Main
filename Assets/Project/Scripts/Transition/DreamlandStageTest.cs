using UnityEngine;

public class DreamlandStageTest : MonoBehaviour
{
    [Header("World Groups")]
    [SerializeField] private GameObject realityWorld;
    [SerializeField] private GameObject interiorDream;
    [SerializeField] private GameObject finalDreamland;

    [Header("Portal Effects")]
    [SerializeField] private GameObject portalEffects;

    private void Start()
    {
        // 게임이 시작하면 1단계 현실 상태로 시작
        ShowStage1Reality();
    }

    [ContextMenu("Stage 1 - Reality")]
    public void ShowStage1Reality()
    {
        // 현실 영역은 유지
        SetActiveSafe(realityWorld, true);

        // 꿈나라 바닥과 길은 아직 숨김
        SetActiveSafe(interiorDream, false);

        // 최종 꿈나라 배경도 숨김
        SetActiveSafe(finalDreamland, false);

        // 포탈 효과도 아직 숨김
        SetActiveSafe(portalEffects, false);

        Debug.Log("Stage 1: Reality");
    }

    [ContextMenu("Stage 2 - Portal Expansion")]
    public void ShowStage2PortalExpansion()
    {
        // 현실 영역은 계속 유지
        SetActiveSafe(realityWorld, true);

        // 꿈나라 바닥과 길은 아직 숨김
        SetActiveSafe(interiorDream, false);

        // 최종 꿈나라 배경도 아직 숨김
        SetActiveSafe(finalDreamland, false);

        // 포탈 효과만 나타냄
        SetActiveSafe(portalEffects, true);

        Debug.Log("Stage 2: Portal Expansion");
    }

    [ContextMenu("Stage 3 - Interior Dream")]
    public void ShowStage3InteriorDream()
    {
        // 현실 영역은 아직 유지
        SetActiveSafe(realityWorld, true);

        // 꿈나라 바닥과 길을 나타냄
        SetActiveSafe(interiorDream, true);

        // 최종 꿈나라 배경은 아직 숨김
        SetActiveSafe(finalDreamland, false);

        // 포탈 효과는 계속 유지
        SetActiveSafe(portalEffects, true);

        Debug.Log("Stage 3: Interior Dream");
    }

    [ContextMenu("Stage 4 - Final Dreamland")]
    public void ShowStage4FinalDreamland()
    {
        // 현실 영역 제거
        SetActiveSafe(realityWorld, false);

        // 꿈나라 바닥과 길 유지
        SetActiveSafe(interiorDream, true);

        // 최종 꿈나라 배경 활성화
        SetActiveSafe(finalDreamland, true);

        // 포탈 경계는 최종 전환 후 제거
        SetActiveSafe(portalEffects, false);

        Debug.Log("Stage 4: Final Dreamland");
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        // Inspector에 오브젝트가 연결되어 있을 때만 실행
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}