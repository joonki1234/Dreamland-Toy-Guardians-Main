using TMPro;
using UnityEngine;

/// <summary>
/// 게임플레이 맵(Dreamland_map_3)에서 장난감 친구를 3D 공간에 정확히
/// 배치/회전시키려다 생기는 문제(엉뚱한 방향으로 뜨거나 카메라 코앞에
/// 나타나는 등)를 피하기 위해, 로비 화면처럼 로봇 아이콘 + 말풍선을
/// 화면(시야) 좌측 상단에 고정 표시하는 HUD다.
///
/// ViewLockedHudFollower와 함께 같은 오브젝트(또는 부모)에 붙여서
/// 항상 카메라 기준 고정 위치에 떠 있도록 한다.
///
/// ToyFriendController가 말을 시작/종료할 때 이 컴포넌트를 찾아
/// ShowMessage()/Hide()를 호출해준다.
/// </summary>
public class ToyFriendViewHud : MonoBehaviour
{
    public static ToyFriendViewHud Instance { get; private set; }

    [Tooltip("말풍선 전체(배경+텍스트)를 켜고 끌 오브젝트입니다.")]
    [SerializeField]
    private GameObject bubbleRoot;

    [Tooltip("말풍선 안의 대사 텍스트입니다.")]
    [SerializeField]
    private TMP_Text messageText;

    private ToyFriendDialogueHUD sharedDialogueHud;

    private void Awake()
    {
        Instance = this;

        sharedDialogueHud = ToyFriendDialogueHUD.GetOrCreate();

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowMessage(string message)
    {
        if (sharedDialogueHud != null)
        {
            sharedDialogueHud.Show(message);
            return;
        }

        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (sharedDialogueHud != null)
        {
            sharedDialogueHud.Hide();
            return;
        }

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }
    }
}
