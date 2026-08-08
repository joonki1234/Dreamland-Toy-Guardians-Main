using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneController : MonoBehaviour
{
    [Header("이동할 로비 씬 이름")]
    [SerializeField]
    private string lobbySceneName = "LobbyScene";

    [Header("화면 전환 Fade 설정")]

    // 화면 전체를 덮는 검은색 Image
    // StartCanvas 안에 만든 FadeImage를 연결하면 된다.
    [SerializeField]
    private Image fadeImage;

    // 화면이 완전히 검게 변하는 데 걸리는 시간
    [SerializeField]
    private float fadeDuration = 1.0f;

    // START 버튼을 여러 번 눌렀을 때
    // 씬 전환이 중복 실행되는 것을 막기 위한 변수
    private bool isStarting;

    /// <summary>
    /// START 버튼을 눌렀을 때 호출되는 함수
    /// </summary>
    public void StartGame()
    {
        // 이미 시작 중이면 다시 실행하지 않는다.
        if (isStarting)
        {
            return;
        }

        isStarting = true;

        // Fade 연출 후 LobbyScene으로 이동한다.
        StartCoroutine(StartGameRoutine());
    }

    /// <summary>
    /// 화면을 천천히 검게 만든 뒤
    /// LobbyScene으로 이동하는 코루틴
    /// </summary>
    private IEnumerator StartGameRoutine()
    {
        float elapsedTime = 0f;

        // 현재 FadeImage의 색상을 가져온다.
        Color color = fadeImage.color;

        // fadeDuration 동안 Alpha 값을
        // 0 → 1로 천천히 증가시킨다.
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            // 현재 Fade 진행도를 0~1 사이 값으로 계산
            float alpha =
                Mathf.Clamp01(elapsedTime / fadeDuration);

            // Alpha 값만 변경해서
            // 검은 화면이 점점 진해지도록 만든다.
            color.a = alpha;

            fadeImage.color = color;

            // 다음 프레임까지 기다린다.
            yield return null;
        }

        // 마지막에는 완전히 검은 화면으로 확실히 맞춘다.
        color.a = 1f;
        fadeImage.color = color;

        // Fade가 끝난 뒤 Lobby Scene으로 이동
        SceneManager.LoadScene(lobbySceneName);
    }
}