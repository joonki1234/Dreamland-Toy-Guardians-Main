using System.Collections;
using UnityEngine;

/// <summary>
/// 동물의숲(Animal Crossing) 캐릭터처럼, 대사가 나오는 동안 알파벳/숫자별로 잘게
/// 쪼개진 짧은 음절 사운드를 빠르게 무작위로 재생해서 "중얼중얼" 말하는 느낌을 낸다.
/// 실제 텍스트 글자와 음절을 1:1로 맞추지는 않고, 그냥 리듬감 있게 무작위 재생한다.
///
/// 클립은 Assets/Audio/Resources/Voice/Animalese/&lt;voiceName&gt;/ 폴더에서
/// Resources.LoadAll로 불러온다 (voiceName 예: robot, default, deep, squeaky, tired, tired_alt).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AnimaleseVoicePlayer : MonoBehaviour
{
    [Tooltip("비워두면 voiceName의 Resources 폴더를 사용합니다. Lobby ToyFriend와 통일할 때는 Lobby의 클립을 직접 연결합니다.")]
    [SerializeField]
    private AudioClip[] voiceClips;

    [Tooltip("Assets/Audio/Resources/Voice/Animalese/ 아래 폴더 이름")]
    [SerializeField]
    private string voiceName = "robot";

    [Tooltip("음절 사이 간격(초). 낮을수록 더 빠르게 중얼거린다.")]
    [SerializeField, Min(0.02f)]
    private float syllableInterval = 0.08f;

    [Tooltip("음절마다 피치를 살짝 무작위로 바꿔서 단조롭지 않게 한다.")]
    [SerializeField]
    private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    [SerializeField, Range(0f, 1f)]
    private float volume = 1f;

    [Tooltip(
        "0이면 완전 2D(거리와 무관하게 항상 또렷하게 들림), 1이면 완전 3D(거리에 따라 작아짐). " +
        "대사는 거리와 상관없이 잘 들려야 해서 기본값을 낮게 둔다.")]
    [SerializeField, Range(0f, 1f)]
    private float spatialBlend = 0.15f;

    private AudioSource _audioSource;
    private AudioClip[] _clips;
    private Coroutine _routine;

    [SerializeField, Min(1)]
    private int characterStep = 2;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = spatialBlend;
        _audioSource.priority = 0; // 다른 효과음에 묻히지 않도록 최우선 순위
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = 3f;
        _audioSource.maxDistance = 25f;

        LoadClips();
    }

    private void LoadClips()
    {
        _clips = voiceClips != null && voiceClips.Length > 0
            ? voiceClips
            : Resources.LoadAll<AudioClip>("Voice/Animalese/" + voiceName);

        if (_clips == null || _clips.Length == 0)
        {
            Debug.LogWarning(
                $"[AnimaleseVoicePlayer] '{voiceName}' 보이스 클립을 찾을 수 없습니다. " +
                $"Assets/Audio/Resources/Voice/Animalese/{voiceName}/ 폴더를 확인하세요.");
        }
    }

    /// <summary>지정한 시간(초) 동안 무작위 음절을 빠르게 재생한다.</summary>
    public void PlayBabbleForDuration(float duration)
    {
        StopBabble();

        if (_clips == null || _clips.Length == 0)
        {
            return;
        }

        _routine = StartCoroutine(BabbleRoutine(duration));
    }

    /// <summary>LobbyContactController와 같은 방식으로 유효 문자 두 글자마다 한 음절을 재생합니다.</summary>
    public void PlayForText(string text, float characterInterval)
    {
        StopBabble();

        if (_clips == null || _clips.Length == 0 || string.IsNullOrEmpty(text))
        {
            return;
        }

        _routine = StartCoroutine(TextRoutine(text, characterInterval));
    }

    public void StopBabble()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    /// <summary>런타임에 보이스를 바꾸고 싶을 때 사용한다 (직업/캐릭터별로 다른 목소리 등).</summary>
    public void SetVoice(string newVoiceName)
    {
        voiceName = newVoiceName;
        LoadClips();
    }

    private IEnumerator BabbleRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_audioSource == null)
            {
                _routine = null;
                yield break;
            }

            AudioClip clip = _clips[Random.Range(0, _clips.Length)];

            _audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            _audioSource.volume = volume;
            _audioSource.PlayOneShot(clip);

            float wait = Mathf.Max(0.02f, syllableInterval);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        _routine = null;
    }

    private IEnumerator TextRoutine(string text, float characterInterval)
    {
        int spokenCharacterCount = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (!char.IsWhiteSpace(character) &&
                character != ',' && character != '.' &&
                character != '!' && character != '?' && character != '…')
            {
                spokenCharacterCount++;

                if (spokenCharacterCount % Mathf.Max(1, characterStep) == 0)
                {
                    PlaySyllable();
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.001f, characterInterval));

            if (character == ',')
            {
                yield return new WaitForSeconds(0.14f);
            }
            else if (character == '.' || character == '!' || character == '?')
            {
                yield return new WaitForSeconds(0.25f);
            }
        }

        _routine = null;
    }

    private void PlaySyllable()
    {
        if (_audioSource == null || _clips == null || _clips.Length == 0)
        {
            return;
        }

        AudioClip clip = null;
        int startIndex = Random.Range(0, _clips.Length);

        for (int i = 0; i < _clips.Length; i++)
        {
            AudioClip candidate = _clips[(startIndex + i) % _clips.Length];
            if (candidate != null)
            {
                clip = candidate;
                break;
            }
        }

        if (clip == null)
        {
            return;
        }

        _audioSource.pitch = Random.Range(
            Mathf.Min(pitchRange.x, pitchRange.y),
            Mathf.Max(pitchRange.x, pitchRange.y));
        // LobbyContactController의 AudioSource(volume 1) + PlayOneShot(volume 0.5)와
        // 최종 게인이 같도록 source volume은 1로 두고 volumeScale만 적용합니다.
        _audioSource.volume = 1f;
        _audioSource.PlayOneShot(clip, volume);
    }

    private void OnDisable()
    {
        StopBabble();
    }
}
