using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lobby와 Dreamland가 함께 사용하는 단일 ToyFriend 대화 HUD.</summary>
public sealed class ToyFriendDialogueHUD : MonoBehaviour
{
    private const string ResourcePath = "UI/ToyFriendDialogueHUD";

    public static ToyFriendDialogueHUD Instance { get; private set; }

    [SerializeField] private GameObject background;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_FontAsset dialogueFont;

    public Image Portrait => portrait;
    public TMP_Text DialogueText => dialogueText;

    public static ToyFriendDialogueHUD GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindAnyObjectByType<ToyFriendDialogueHUD>();
        if (Instance != null)
        {
            return Instance;
        }

        ToyFriendDialogueHUD prefab = Resources.Load<ToyFriendDialogueHUD>(ResourcePath);
        return prefab != null ? Instantiate(prefab) : null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveAndConfigure();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show(string message)
    {
        if (dialogueText != null)
        {
            dialogueText.text = message ?? string.Empty;
        }

        SetVisualsActive(true);
    }

    public void Hide()
    {
        SetVisualsActive(false);
    }

    private void SetVisualsActive(bool active)
    {
        if (background != null) background.SetActive(active);
        if (portrait != null) portrait.gameObject.SetActive(active);
        if (dialogueText != null) dialogueText.gameObject.SetActive(active);
    }

    private void ResolveAndConfigure()
    {
        Transform backgroundTransform = transform.Find("Background");
        Transform portraitTransform = transform.Find("ToyFriendPortrait");
        Transform textTransform = transform.Find("DialogueText");

        if (backgroundTransform != null) background = backgroundTransform.gameObject;
        if (portraitTransform != null) portrait = portraitTransform.GetComponent<Image>();
        if (textTransform != null)
        {
            dialogueText = textTransform.GetComponent<TMP_Text>();
            if (dialogueText == null)
            {
                dialogueText = textTransform.gameObject.AddComponent<TextMeshProUGUI>();
            }
        }

        ConfigureRect(backgroundTransform as RectTransform, new Vector2(760f, 170f), new Vector2(0f, 100f));
        ConfigureRect(portraitTransform as RectTransform, new Vector2(130f, 130f), new Vector2(-285f, 100f));
        ConfigureRect(textTransform as RectTransform, new Vector2(550f, 150f), new Vector2(95f, 100f));

        if (dialogueText != null)
        {
            if (dialogueFont != null)
            {
                dialogueText.font = dialogueFont;
            }

            dialogueText.fontSize = 25f;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.enableWordWrapping = true;
            dialogueText.color = Color.white;
            dialogueText.raycastTarget = false;
        }
    }

    private static void ConfigureRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}
