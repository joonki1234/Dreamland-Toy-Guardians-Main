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

    [SerializeField]
    private LobbyContactController lobbyContactController;


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


    [Header("로비 BGM")]
    [SerializeField]
    private AudioSource lobbyBGM;

    // BGM이 서서히 커지는 시간
    [SerializeField, Min(0.1f)]
    private float bgmFadeInDuration = 1.5f;

    // 최종 BGM 볼륨
    [SerializeField, Range(0f, 1f)]
    private float lobbyBGMVolume = 0.45f;


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
    private Coroutine bgmFadeRoutine;

    private bool hasOpenedJobSelection;
    private bool isShowingLoading;


    private void Start()
    {
        // 로비에 들어오자마자 BGM이 나오지 않도록 초기화
        if (lobbyBGM != null)
        {
            lobbyBGM.Stop();
            lobbyBGM.volume = 0f;
        }

        ShowIntroScreen();

        if (useAutomaticTestTransition)
        {
            introRoutine = StartCoroutine(
                IntroSequenceRoutine()
            );
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
            introDescriptionText.text =
                "잠시만 기다려주세요";
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
                string dots =
                    new string('.', dotCount);

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
            StartCoroutine(
                LoadingTextRoutine()
            );
    }


    private void StopLoadingTextAnimation()
    {
        isShowingLoading = false;

        if (loadingTextRoutine != null)
        {
            StopCoroutine(
                loadingTextRoutine
            );

            loadingTextRoutine = null;
        }
    }


    private IEnumerator IntroSequenceRoutine()
    {
        // 처음 연결 중 화면 유지
        yield return new WaitForSeconds(
            introDuration
        );

        StopLoadingTextAnimation();


        // 연결 완료 표시
        if (introTitleText != null)
        {
            introTitleText.text =
                "연결 완료!";
        }

        if (introDescriptionText != null)
        {
            introDescriptionText.text =
                "직업을 선택해주세요";
        }


        // 연결 완료 문구 잠시 보여주기
        yield return new WaitForSeconds(
            0.8f
        );


        // 검은 인트로 화면 페이드 아웃
        yield return StartCoroutine(
            FadeOutIntroRoutine()
        );


        // 페이드 아웃이 완전히 끝난 뒤
        // 직업 선택 화면 표시
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
            StopCoroutine(
                introRoutine
            );

            introRoutine = null;
        }

        introRoutine =
            StartCoroutine(
                CompleteLobbyTransitionRoutine()
            );
    }


    private IEnumerator CompleteLobbyTransitionRoutine()
    {
        // 네트워크 연결 완료 후 인트로 화면의 페이드 아웃이
        // 완전히 끝날 때까지 로비 연락 시퀀스를 시작하지 않는다.
        if (lobbyIntroPanel != null &&
            lobbyIntroPanel.activeSelf &&
            introCanvasGroup != null &&
            introCanvasGroup.alpha > 0f)
        {
            yield return StartCoroutine(
                FadeOutIntroRoutine()
            );
        }

        // 인트로 화면 끄기
        if (lobbyIntroPanel != null)
        {
            lobbyIntroPanel.SetActive(false);
        }


        // 연락 시퀀스가 연결된 경우 직업 선택 화면은 대화가 끝난 뒤 켠다.
        if (jobSelectPanel != null)
        {
            jobSelectPanel.SetActive(
                lobbyContactController == null
            );
        }


        // 플레이어 상태창 켜기
        if (playerStatusGroup != null)
        {
            playerStatusGroup.SetActive(true);
        }


        if (lobbyContactController != null)
        {
            lobbyContactController.BeginContactSequence(
                StartLobbyBGM
            );
        }
        else
        {
            // 연락 기능이 없으면 기존 시점에 BGM을 재생한다.
            StartLobbyBGM();
        }

        introRoutine = null;
    }


    private void StartLobbyBGM()
    {
        if (lobbyBGM == null)
        {
            return;
        }


        // 이미 실행 중인 페이드가 있다면 중지
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(
                bgmFadeRoutine
            );
        }


        bgmFadeRoutine =
            StartCoroutine(
                FadeInBGMRoutine()
            );
    }


    private IEnumerator FadeInBGMRoutine()
    {
        // 처음에는 무음
        lobbyBGM.volume = 0f;


        // 음악 재생 시작
        if (!lobbyBGM.isPlaying)
        {
            lobbyBGM.Play();
        }


        float elapsed = 0f;

        while (elapsed < bgmFadeInDuration)
        {
            elapsed += Time.deltaTime;

            lobbyBGM.volume =
                Mathf.Lerp(
                    0f,
                    lobbyBGMVolume,
                    elapsed / bgmFadeInDuration
                );

            yield return null;
        }


        // 마지막 값 정확하게 맞추기
        lobbyBGM.volume =
            lobbyBGMVolume;

        bgmFadeRoutine = null;

    }


    public void ReturnToIntroScreen()
    {
        if (introRoutine != null)
        {
            StopCoroutine(
                introRoutine
            );

            introRoutine = null;
        }


        // BGM 페이드 코루틴 중지
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(
                bgmFadeRoutine
            );

            bgmFadeRoutine = null;
        }


        // 다시 인트로로 돌아가면 BGM 정지
        if (lobbyBGM != null)
        {
            lobbyBGM.Stop();
            lobbyBGM.volume = 0f;
        }


        StopLoadingTextAnimation();

        ShowIntroScreen();


        if (useAutomaticTestTransition)
        {
            introRoutine =
                StartCoroutine(
                    IntroSequenceRoutine()
                );
        }
    }


    private void OnDisable()
    {
        if (introRoutine != null)
        {
            StopCoroutine(
                introRoutine
            );

            introRoutine = null;
        }


        if (bgmFadeRoutine != null)
        {
            StopCoroutine(
                bgmFadeRoutine
            );

            bgmFadeRoutine = null;
        }


        StopLoadingTextAnimation();
    }
}
