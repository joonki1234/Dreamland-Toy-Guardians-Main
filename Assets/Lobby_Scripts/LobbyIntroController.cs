using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 로비에 처음 들어왔을 때 연결 안내 화면을 표시하고,
/// 일정 시간이 지나면 직업 선택 화면으로 전환한다.
///
/// 현재는 PC 테스트를 위해 시간 기준으로 자동 전환한다.
/// 최종 네트워크 버전에서는 실제 연결 완료 신호를 받은 뒤
/// ShowJobSelectionScreen 함수를 호출하면 된다.
/// </summary>
public class LobbyIntroController : MonoBehaviour
{
    [Header("로비 화면 연결")]
    [Tooltip("처음에 표시할 연결 안내 패널입니다.")]
    [SerializeField]
    private GameObject lobbyIntroPanel;

    [Tooltip("직업 선택 버튼과 Ready 버튼이 들어 있는 패널입니다.")]
    [SerializeField]
    private GameObject jobSelectPanel;

    [Tooltip("오른쪽 위 플레이어 상태 목록입니다.")]
    [SerializeField]
    private GameObject playerStatusGroup;

    [Header("안내 글자 연결")]
    [Tooltip("안내 화면의 큰 제목 글자입니다.")]
    [SerializeField]
    private TMP_Text introTitleText;

    [Tooltip("안내 화면의 아래쪽 설명 글자입니다.")]
    [SerializeField]
    private TMP_Text introDescriptionText;

    [Header("PC 테스트 설정")]
    [Tooltip(
        "체크하면 게임 시작 후 지정한 시간이 지나면 " +
        "자동으로 직업 선택 화면을 표시합니다."
    )]
    [SerializeField]
    private bool useAutomaticTestTransition = true;

    [Tooltip("연결 중 화면을 보여줄 시간입니다.")]
    [SerializeField, Min(0f)]
    private float introDuration = 2f;

    [Header("로딩 연출 설정")]
    [Tooltip("로딩 점이 바뀌는 시간 간격입니다.")]
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
            introRoutine =
                StartCoroutine(IntroSequenceRoutine());
        }
    }

    /// <summary>
    /// 안내 화면만 활성화하고 로딩 글자 연출을 시작한다.
    /// </summary>
    private void ShowIntroScreen()
    {
        hasOpenedJobSelection = false;
        isShowingLoading = true;

        if (lobbyIntroPanel != null)
        {
            lobbyIntroPanel.SetActive(true);
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

    /// <summary>
    /// 제목 뒤의 점 개수를 반복해서 변경한다.
    /// </summary>
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
                    $"꿈나라 수호자 연결 중{dots}";
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

    /// <summary>
    /// 로딩 글자 애니메이션을 시작한다.
    /// </summary>
    private void StartLoadingTextAnimation()
    {
        StopLoadingTextAnimation();

        isShowingLoading = true;

        loadingTextRoutine =
            StartCoroutine(LoadingTextRoutine());
    }

    /// <summary>
    /// 로딩 글자 애니메이션을 중지한다.
    /// </summary>
    private void StopLoadingTextAnimation()
    {
        isShowingLoading = false;

        if (loadingTextRoutine != null)
        {
            StopCoroutine(loadingTextRoutine);
            loadingTextRoutine = null;
        }
    }

    /// <summary>
    /// PC 테스트용으로 일정 시간 기다린 뒤
    /// 연결 완료 문구를 보여주고 직업 선택 화면으로 전환한다.
    /// </summary>
    private IEnumerator IntroSequenceRoutine()
    {
        yield return new WaitForSeconds(
            introDuration
        );

        StopLoadingTextAnimation();

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

        yield return new WaitForSeconds(0.8f);

        ShowJobSelectionScreen();
    }

    /// <summary>
    /// 안내 화면을 닫고 직업 선택 화면과
    /// 플레이어 상태 목록을 표시한다.
    ///
    /// 최종 네트워크 버전에서는 연결 완료 시
    /// 네트워크 담당 코드가 이 함수를 호출하면 된다.
    /// </summary>
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

    /// <summary>
    /// 연결 상태를 다시 안내 화면으로 되돌린다.
    /// 연결이 끊겼을 때 사용할 수 있다.
    /// </summary>
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