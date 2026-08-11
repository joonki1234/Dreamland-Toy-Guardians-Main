using UnityEngine;

public class LobbyPanelToggle : MonoBehaviour
{
    [Header("로비 UI 연결")]
    [SerializeField]
    private GameObject jobSelectPanel;

    public void TogglePanel()
    {
        if (jobSelectPanel == null)
        {
            return;
        }

        bool isOpen = jobSelectPanel.activeSelf;

        jobSelectPanel.SetActive(!isOpen);
    }
}