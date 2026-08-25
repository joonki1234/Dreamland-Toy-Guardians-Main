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

    [Header("ToyFriend Dialogue Duck")]
    [SerializeField, Range(0f, 1f)]
    private float dialogueDuckRatio = 0.55f;

    [SerializeField, Min(0.05f)]
    private float dialogueDuckFadeDuration = 0.3f;

    private Coroutine _crossfadeRoutine;
    private Coroutine _duckRoutine;
    private bool _dialogueDucked;

    private float TargetMusicVolume =>
        musicVolume * (_dialogueDucked ? dialogueDuckRatio : 1f);

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

    public void BeginToyFriendDialogueDuck()
    {
        SetDialogueDucked(true);
    }

    public void EndToyFriendDialogueDuck()
    {
        SetDialogueDucked(false);
    }

    private void SetDialogueDucked(bool ducked)
    {
        if (musicSource == null || !isActiveAndEnabled)
        {
            return;
        }

        _dialogueDucked = ducked;

        if (_duckRoutine != null)
        {
            StopCoroutine(_duckRoutine);
            _duckRoutine = null;
        }

        // 크로스페이드가 진행 중이면 그 루틴이 매 프레임 TargetMusicVolume을
        // 참조하므로 별도 볼륨 루틴과 경쟁시키지 않습니다.
        if (_crossfadeRoutine != null)
        {
            return;
        }

        _duckRoutine = StartCoroutine(FadeVolumeRoutine(TargetMusicVolume));
    }

    private IEnumerator FadeVolumeRoutine(float targetVolume)
    {
        if (musicSource == null)
        {
            _duckRoutine = null;
            yield break;
        }

        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < dialogueDuckFadeDuration)
        {
            if (musicSource == null)
            {
                _duckRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                Mathf.Clamp01(elapsed / dialogueDuckFadeDuration));
            yield return null;
        }

        if (musicSource != null)
        {
            musicSource.volume = targetVolume;
        }
        _duckRoutine = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || musicSource == null || musicSource.clip == clip)
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

        while (t < half && musicSource != null && musicSource.isPlaying)
        {
            if (musicSource == null)
            {
                _crossfadeRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / half);
            yield return null;
        }

        if (musicSource == null)
        {
            _crossfadeRoutine = null;
            yield break;
        }

        musicSource.clip = newClip;
        musicSource.volume = 0f;
        musicSource.Play();

        t = 0f;

        while (t < half)
        {
            if (musicSource == null)
            {
                _crossfadeRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, TargetMusicVolume, t / half);
            yield return null;
        }

        if (musicSource != null)
        {
            musicSource.volume = TargetMusicVolume;
        }
        _crossfadeRoutine = null;
    }

    private void OnDisable()
    {
        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
            _crossfadeRoutine = null;
        }

        if (_duckRoutine != null)
        {
            StopCoroutine(_duckRoutine);
            _duckRoutine = null;
        }

        _dialogueDucked = false;
    }
}
