using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneController : MonoBehaviour
{
    [SerializeField]
    private string lobbySceneName = "LobbyScene";

    [SerializeField]
    private Image fadeImage;

    [SerializeField]
    private float fadeDuration = 1.0f;

    private bool isStarting;

    private void Awake()
    {
        Color color = fadeImage.color;
        color.a = 0f;
        fadeImage.color = color;
    }

    public void StartGame()
    {
        if (isStarting)
            return;

        isStarting = true;

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        color.a = 0f;
        fadeImage.color = color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            color.a = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = color;

            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;

        yield return null;

        SceneManager.LoadScene(lobbySceneName);
    }
}