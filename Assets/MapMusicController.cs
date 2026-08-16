using System.Collections;
using UnityEngine;

/// <summary>
/// 맵 배경음악(탐험용)과 전투음악을 관리한다.
/// 씬이 시작되면 탐험용 BGM을 재생하고, 전투가 시작되면(GameFlowManager.StartWave1())
/// 전투음악으로 부드럽게 크로스페이드 전환한다.
///
/// explorationBgm / battleBgm을 Inspector에서 비워두면
/// Assets/Audio/Resources/Music/exploration_bgm.* , battle_bgm.* 을 자동으로 불러온다.
/// </summary>
public class MapMusicController : MonoBehaviour
{
    [SerializeField]
    private AudioSource musicSource;

    [SerializeField]
    private AudioClip explorationBgm;

    [SerializeField]
    private AudioClip battleBgm;

    [SerializeField, Range(0f, 1f)]
    private float musicVolume = 0.25f;

    [SerializeField, Min(0.1f)]
    private float crossfadeDuration = 1.5f;

    private Coroutine _crossfadeRoutine;

    private void Awake()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;

        if (explorationBgm == null)
        {
            explorationBgm = Resources.Load<AudioClip>("Music/exploration_bgm");
        }

        if (battleBgm == null)
        {
            battleBgm = Resources.Load<AudioClip>("Music/battle_bgm");
        }
    }

    private void Start()
    {
        PlayExploration();
    }

    public void PlayExploration()
    {
        PlayClip(explorationBgm);
    }

    public void PlayBattle()
    {
        PlayClip(battleBgm);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip)
        {
            return;
        }

        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
        }

        _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(clip));
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        float half = Mathf.Max(0.05f, crossfadeDuration * 0.5f);
        float startVolume = musicSource.volume;
        float t = 0f;

        while (t < half && musicSource.isPlaying)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / half);
            yield return null;
        }

        musicSource.clip = newClip;
        musicSource.volume = 0f;
        musicSource.Play();

        t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / half);
            yield return null;
        }

        musicSource.volume = musicVolume;
        _crossfadeRoutine = null;
    }
}
