using TMPro;
using UnityEngine;

/// <summary>
/// 로비 오른쪽에 표시되는 플레이어 상태 목록 UI를 관리한다.
///
/// 이 스크립트는 네트워크 접속이나 동기화를 직접 처리하지 않는다.
/// 네트워크 담당 코드가 플레이어 이름, 직업, Ready 상태를 전달하면
/// 해당 내용을 화면에 표시하는 UI 전용 스크립트다.
/// </summary>
public class LobbyPlayerStatusUI : MonoBehaviour
{
    [Header("플레이어 상태 글자 1~8")]
    [Tooltip(
        "PlayerStatusItem_1부터 PlayerStatusItem_8까지의 " +
        "PlayerStatusText를 순서대로 연결합니다."
    )]
    [SerializeField]
    private TMP_Text[] playerStatusTexts = new TMP_Text[8];

    [Header("에디터 테스트")]
    [Tooltip(
        "체크하면 네트워크가 없는 상태에서 " +
        "플레이어 1 한 명만 접속한 것처럼 표시합니다."
    )]
    [SerializeField]
    private bool showLocalTestPlayer = true;

    private const int MaximumPlayerCount = 8;

    private void Start()
    {
        HideAllPlayerStatusItems();

        if (showLocalTestPlayer)
        {
            SetPlayerStatus(
                0,
                "플레이어 1",
                "직업 없음",
                false,
                true
            );
        }
    }

    /// <summary>
    /// 지정한 위치에 플레이어 상태를 표시한다.
    ///
    /// playerIndex는 0부터 시작한다.
    /// 0은 첫 번째 줄, 7은 여덟 번째 줄이다.
    /// </summary>
    public void SetPlayerStatus(
        int playerIndex,
        string playerName,
        string jobName,
        bool isReady,
        bool isConnected
    )
    {
        if (!IsValidPlayerIndex(playerIndex))
        {
            Debug.LogWarning(
                $"LobbyPlayerStatusUI: " +
                $"잘못된 플레이어 인덱스입니다. " +
                $"입력값: {playerIndex}"
            );

            return;
        }

        TMP_Text statusText =
            playerStatusTexts[playerIndex];

        if (statusText == null)
        {
            Debug.LogWarning(
                $"LobbyPlayerStatusUI: " +
                $"{playerIndex + 1}번 상태 글자가 연결되지 않았습니다."
            );

            return;
        }

        GameObject statusItem =
            statusText.transform.parent.gameObject;

        if (!isConnected)
        {
            statusItem.SetActive(false);
            return;
        }

        statusItem.SetActive(true);

        string readyText =
            isReady ? "준비 완료" : "준비 전";

        statusText.text =
            $"{playerName} | {jobName} | {readyText}";
    }

    /// <summary>
    /// 특정 플레이어가 로비에서 나갔을 때
    /// 해당 상태 줄을 숨긴다.
    /// </summary>
    public void RemovePlayerStatus(int playerIndex)
    {
        if (!IsValidPlayerIndex(playerIndex))
        {
            return;
        }

        TMP_Text statusText =
            playerStatusTexts[playerIndex];

        if (statusText == null)
        {
            return;
        }

        statusText.transform.parent.gameObject.SetActive(false);
    }

    /// <summary>
    /// 모든 플레이어 상태 줄을 숨긴다.
    /// 새 체험 회차를 시작할 때도 사용할 수 있다.
    /// </summary>
    public void HideAllPlayerStatusItems()
    {
        for (int i = 0; i < playerStatusTexts.Length; i++)
        {
            TMP_Text statusText =
                playerStatusTexts[i];

            if (statusText == null)
            {
                continue;
            }

            statusText.transform.parent.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 전달받은 플레이어 위치가
    /// 0~7 범위이고 배열에도 존재하는지 확인한다.
    /// </summary>
    private bool IsValidPlayerIndex(int playerIndex)
    {
        if (playerIndex < 0 ||
            playerIndex >= MaximumPlayerCount)
        {
            return false;
        }

        if (playerStatusTexts == null ||
            playerIndex >= playerStatusTexts.Length)
        {
            return false;
        }

        return true;
    }
}