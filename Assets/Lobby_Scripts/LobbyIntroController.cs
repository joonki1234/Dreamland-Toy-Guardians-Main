using System.Collections;
using TMPro;
using UnityEngine;

public class LobbyIntroController : MonoBehaviour
{
    [Header("로비 화면 연결")]
    [SerializeField]
    private GameObject lobbyIntroPanel;

    [SerializeField]
    private GameObject jobSelectPanel;

    [SerializeField]
    private GameObject playerStatusGroup;

    [Header("안내 글자 연결")]
    [SerializeField]
    private TMP_Text introTitleText;

    [SerializeField]
    private TMP_Text introDescriptionText;

    [Header("인트로 페이드")]
    [SerializeField]
    private CanvasGroup introCanvasGroup;

    [SerializeField, Min(0.1f)]
    private float fadeOutDuration = 1f;

    [Header("PC 테스트 설정")]
    [SerializeField]
    private bool useAutomaticTestTransition = true;

    [SerializeField, Min(0f)]
    private float introDuration = 2f;

    [Header("로딩 연출 설정")]
    [SerializeField, Min(0.05f)]
    private float loadingDotInterval = 0.35f;

    private Coroutine introRoutine;
    private Coroutine loadingTextRoutine;

    private bool hasOpenedJobSelection;
    private bool isShowingLoading;

    private void Start()
    {
        ShowIntroScreen();

        if (useAutomaticTestTransition)
        {
            introRoutine = StartCoroutine(IntroSequenceRoutine());
        }
    }

    private void ShowIntroScreen()
    {
        hasOpenedJobSelection = false;
        isShowingLoading = true;

        if (lobbyIntroPanel != null)
        {
            lobbyIntroPanel.SetActive(true);
        }

        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 1f;
            introCanvasGroup.interactable = true;
            introCanvasGroup.blocksRaycasts = true;
        }

        if (jobSelectPanel != null)
        {
            jobSelectPanel.SetActive(false);
        }

        if (playerStatusGroup != null)
        {
            playerStatusGroup.SetActive(false);
        }

        if (introDescriptionText != null)
        {
            introDescriptionText.text = "잠시만 기다려주세요";
        }

        StartLoadingTextAnimation();
    }

    private IEnumerator LoadingTextRoutine()
    {
        int dotCount = 0;

        while (isShowingLoading)
        {
            if (introTitleText != null)
            {
                string dots = new string('.', dotCount);

                introTitleText.text =
                    $"꿈나라 수호대 연결 중{dots}";
            }

            dotCount++;

            if (dotCount > 3)
            {
                dotCount = 0;
            }

            yield return new WaitForSeconds(
                loadingDotInterval
            );
        }
    }

    private void StartLoadingTextAnimation()
    {
        StopLoadingTextAnimation();

        isShowingLoading = true;

        loadingTextRoutine =
            StartCoroutine(LoadingTextRoutine());
    }

    private void StopLoadingTextAnimation()
    {
        isShowingLoading = false;

        if (loadingTextRoutine != null)
        {
            StopCoroutine(loadingTextRoutine);
            loadingTextRoutine = null;
        }
    }

    private IEnumerator IntroSequenceRoutine()
    {
        yield return new WaitForSeconds(
            introDuration
        );

        StopLoadingTextAnimation();

        if (introTitleText != null)
        {
            introTitleText.text = "연결 완료!";
        }

        if (introDescriptionText != null)
        {
            introDescriptionText.text =
                "직업을 선택해주세요";
        }

        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(
            FadeOutIntroRoutine()
        );

        ShowJobSelectionScreen();
    }

    private IEnumerator FadeOutIntroRoutine()
    {
        if (introCanvasGroup == null)
        {
            yield break;
        }

        introCanvasGroup.interactable = false;
        introCanvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;

            introCanvasGroup.alpha =
                Mathf.Lerp(
                    1f,
                    0f,
                    elapsed / fadeOutDuration
                );

            yield return null;
        }

        introCanvasGroup.alpha = 0f;
    }

    public void ShowJobSelectionScreen()
    {
        if (hasOpenedJobSelection)
        {
            return;
        }

        hasOpenedJobSelection = true;

        StopLoadingTextAnimation();

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        if (lobbyIntroPanel != null)
        {
            lobbyIntroPanel.SetActive(false);
        }

        if (jobSelectPanel != null)
        {
            jobSelectPanel.SetActive(true);
        }

        if (playerStatusGroup != null)
        {
            playerStatusGroup.SetActive(true);
        }
    }

    public void ReturnToIntroScreen()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        StopLoadingTextAnimation();
        ShowIntroScreen();

        if (useAutomaticTestTransition)
        {
            introRoutine =
                StartCoroutine(IntroSequenceRoutine());
        }
    }

    private void OnDisable()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        StopLoadingTextAnimation();
    }
}