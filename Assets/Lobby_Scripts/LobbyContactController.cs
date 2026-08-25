using System.Collections;
using System;
using TMPro;
using UnityEngine;

public class LobbyContactController : MonoBehaviour
{
    public static bool IsJobDialoguePhase { get; private set; }

    [Header("로비 UI 연결")]
    [SerializeField]
    private GameObject contactDialoguePanel;

    [SerializeField]
    private TMP_Text contactMessageText;

    [SerializeField]
    private GameObject jobSelectPanel;

    [SerializeField]
    private GameObject jobDescriptionPanel;

    [Header("오디오 연결")]
    [SerializeField]
    private AudioSource lobbyBGM;

    [SerializeField]
    private AudioSource contactAudioSource;

    [SerializeField]
    private AudioSource jobSelectOpenAudioSource;

    [Header("대화 음성 효과")]
    [SerializeField]
    private AudioSource dialogueVoiceAudioSource;

    [SerializeField]
    private AudioClip[] dialogueVoiceClips;

    [SerializeField, Min(1)]
    private int voiceCharacterStep = 2;

    [SerializeField]
    private float voicePitchMin = 1.5f;

    [SerializeField]
    private float voicePitchMax = 1.9f;

    [SerializeField, Range(0f, 1f)]
    private float voiceVolume = 0.5f;

    [Header("연락 연출 설정")]
    [SerializeField, Min(0f)]
    private float contactStartDelay = 1f;

    [SerializeField, Range(0f, 1f)]
    private float duckVolumeRatio = 0.4f;

    [SerializeField, Min(0f)]
    private float bgmFadeDuration = 0.4f;

    [SerializeField, Min(1)]
    private int contactRepeatCount = 3;

    [SerializeField, Min(0f)]
    private float contactRepeatGap = 0.35f;

    [SerializeField, Min(0f)]
    private float postContactDelay = 0.8f;

    [Header("브리핑 설정")]
    [SerializeField, Min(0f)]
    private float dialogueAppearDelay = 5f;

    [SerializeField, Min(0f)]
    private float typingSpeed = 0.055f;

    [SerializeField, Min(0.1f)]
    private float dialogueInterval = 1.7f;

    [SerializeField, TextArea(2, 4)]
    private string[] dialogueLines =
    {
        "드디어 연결됐어!",
        "너희가 꾸던 꿈은 사실 진짜 존재하는 세계야.",
        "그곳은 지금 악몽에 오염되고 있어.",
        "오염된 장난감들이 현실로 넘어오려고 해.",
        "이대로 두면 현실까지 침공당할 거야.",
        "먼저, 함께 싸울 직업을 선택해줘!"
    };

    private Coroutine contactRoutine;
    private Coroutine bgmRestoreRoutine;
    private bool hasStarted;
    private Action onDialogueAppeared;
    private ToyFriendDialogueHUD sharedDialogueHud;

    private void Awake()
    {
        IsJobDialoguePhase = false;
        sharedDialogueHud = ToyFriendDialogueHUD.GetOrCreate();
        if (sharedDialogueHud != null)
        {
            contactMessageText = sharedDialogueHud.DialogueText;
        }

        if (dialogueVoiceAudioSource != null)
        {
            dialogueVoiceAudioSource.playOnAwake = false;
        }

        dialogueLines = new[]
        {
            "드디어 연결됐어!",
            "너희가 꾸던 꿈은 사실 진짜 존재하는 세계야.",
            "그곳은 지금 악몽에 오염되고 있어.",
            "오염된 장난감들이 현실로 넘어오려고 해.",
            "이대로 두면 현실까지 침공당할 거야.",
            "먼저, 함께 싸울 직업을 선택해줘!"
        };

        SetInitialUIState();
    }

    public void BeginContactSequence()
    {
        BeginContactSequence(null);
    }

    public void BeginContactSequence(Action dialogueAppearedCallback)
    {
        if (hasStarted)
        {
            return;
        }

        SetInitialUIState();
        onDialogueAppeared = dialogueAppearedCallback;
        hasStarted = true;
        contactRoutine = StartCoroutine(ContactSequenceRoutine());
    }

    private void SetInitialUIState()
    {
        if (contactDialoguePanel != null)
        {
            contactDialoguePanel.SetActive(false);
        }

        if (jobDescriptionPanel != null)
        {
            jobDescriptionPanel.SetActive(false);
        }

        IsJobDialoguePhase = false;
        sharedDialogueHud?.Hide();

        if (jobSelectPanel != null)
        {
            jobSelectPanel.SetActive(false);
        }
    }

    private IEnumerator ContactSequenceRoutine()
    {
        if (contactStartDelay > 0f)
        {
            yield return new WaitForSeconds(contactStartDelay);
        }

        float originalBGMVolume =
            lobbyBGM != null
                ? lobbyBGM.volume
                : 0f;

        bool wasBGMPlaying =
            lobbyBGM != null &&
            lobbyBGM.isPlaying;

        if (wasBGMPlaying)
        {
            yield return FadeBGMVolume(
                originalBGMVolume,
                originalBGMVolume * duckVolumeRatio
            );
        }

        if (contactAudioSource != null)
        {
            int repeatCount =
                Mathf.Max(1, contactRepeatCount);

            for (int i = 0; i < repeatCount; i++)
            {
                contactAudioSource.Play();

                while (contactAudioSource != null && contactAudioSource.isPlaying)
                {
                    yield return null;
                }

                if (i < repeatCount - 1 &&
                    contactRepeatGap > 0f)
                {
                    yield return new WaitForSeconds(
                        contactRepeatGap
                    );
                }
            }
        }

        if (wasBGMPlaying)
        {
            bgmRestoreRoutine = StartCoroutine(
                RestoreBGMAfterContactAudio(originalBGMVolume)
            );
        }

        if (postContactDelay > 0f)
        {
            yield return new WaitForSeconds(postContactDelay);
        }

        if (sharedDialogueHud == null && contactDialoguePanel != null)
        {
            contactDialoguePanel.SetActive(true);
        }

        sharedDialogueHud?.Show(string.Empty);

        onDialogueAppeared?.Invoke();
        onDialogueAppeared = null;

        if (dialogueLines != null)
        {
            for (int i = 0; i < dialogueLines.Length; i++)
            {
                yield return TypeDialogueLine(dialogueLines[i]);

                yield return new WaitForSeconds(dialogueInterval);
            }
        }

        if (contactDialoguePanel != null)
        {
            contactDialoguePanel.SetActive(false);
        }

        sharedDialogueHud?.Hide();

        if (jobSelectPanel != null)
        {
            jobSelectPanel.SetActive(true);
        }

        // 기존 구조에서 JobDescriptionPanel이 활성화되던 바로 이 시점부터만
        // LobbySelectionController가 공통 DialogueText를 갱신할 수 있습니다.
        IsJobDialoguePhase = true;

        if (sharedDialogueHud == null && jobDescriptionPanel != null)
        {
            jobDescriptionPanel.SetActive(true);
        }

        sharedDialogueHud?.Show(contactMessageText != null ? contactMessageText.text : string.Empty);

        if (jobSelectOpenAudioSource != null)
        {
            jobSelectOpenAudioSource.Play();
        }

        contactRoutine = null;
    }

    private IEnumerator RestoreBGMAfterContactAudio(
        float originalBGMVolume)
    {
        if (contactAudioSource != null)
        {
            while (contactAudioSource != null && contactAudioSource.isPlaying)
            {
                yield return null;

                if (contactAudioSource == null)
                {
                    bgmRestoreRoutine = null;
                    yield break;
                }
            }
        }

        if (lobbyBGM == null)
        {
            bgmRestoreRoutine = null;
            yield break;
        }

        yield return FadeBGMVolume(
            lobbyBGM.volume,
            originalBGMVolume
        );

        bgmRestoreRoutine = null;
    }

    private IEnumerator TypeDialogueLine(string line)
    {
        if (contactMessageText == null)
        {
            yield break;
        }

        contactMessageText.text = line ?? string.Empty;
        contactMessageText.maxVisibleCharacters = 0;
        contactMessageText.ForceMeshUpdate();

        int characterCount =
            contactMessageText.textInfo.characterCount;

        int voiceCharacterCount = 0;

        for (int visibleCount = 1;
             visibleCount <= characterCount;
             visibleCount++)
        {
            contactMessageText.maxVisibleCharacters = visibleCount;

            if (typingSpeed > 0f)
            {
                yield return new WaitForSeconds(typingSpeed);
            }

            char visibleCharacter =
                contactMessageText.textInfo
                    .characterInfo[visibleCount - 1]
                    .character;

            if (ShouldPlayDialogueVoice(visibleCharacter))
            {
                voiceCharacterCount++;

                if (voiceCharacterCount %
                    Mathf.Max(1, voiceCharacterStep) == 0)
                {
                    PlayDialogueVoice();
                }
            }

            if (visibleCharacter == ',')
            {
                yield return new WaitForSeconds(0.14f);
            }
            else if (visibleCharacter == '.' ||
                     visibleCharacter == '!' ||
                     visibleCharacter == '?')
            {
                yield return new WaitForSeconds(0.25f);
            }
        }

        contactMessageText.maxVisibleCharacters = characterCount;
    }

    private bool ShouldPlayDialogueVoice(char character)
    {
        return
            !char.IsWhiteSpace(character) &&
            character != ',' &&
            character != '.' &&
            character != '!' &&
            character != '?' &&
            character != '…';
    }

    private void PlayDialogueVoice()
    {
        if (dialogueVoiceAudioSource == null ||
            dialogueVoiceClips == null ||
            dialogueVoiceClips.Length == 0)
        {
            return;
        }

        int startIndex =
            UnityEngine.Random.Range(
                0,
                dialogueVoiceClips.Length
            );

        AudioClip selectedClip = null;

        for (int i = 0; i < dialogueVoiceClips.Length; i++)
        {
            int clipIndex =
                (startIndex + i) % dialogueVoiceClips.Length;

            if (dialogueVoiceClips[clipIndex] != null)
            {
                selectedClip = dialogueVoiceClips[clipIndex];
                break;
            }
        }

        if (selectedClip == null)
        {
            return;
        }

        float minimumPitch =
            Mathf.Min(voicePitchMin, voicePitchMax);

        float maximumPitch =
            Mathf.Max(voicePitchMin, voicePitchMax);

        dialogueVoiceAudioSource.pitch =
            UnityEngine.Random.Range(
                minimumPitch,
                maximumPitch
            );

        dialogueVoiceAudioSource.PlayOneShot(
            selectedClip,
            voiceVolume
        );
    }

    private IEnumerator FadeBGMVolume(
        float startVolume,
        float targetVolume)
    {
        if (lobbyBGM == null)
        {
            yield break;
        }

        if (bgmFadeDuration <= 0f)
        {
            lobbyBGM.volume = targetVolume;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < bgmFadeDuration)
        {
            if (lobbyBGM == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            lobbyBGM.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                Mathf.Clamp01(elapsed / bgmFadeDuration)
            );

            yield return null;
        }

        if (lobbyBGM == null)
        {
            yield break;
        }

        lobbyBGM.volume = targetVolume;
    }

    private void OnDisable()
    {
        IsJobDialoguePhase = false;
        if (dialogueVoiceAudioSource != null)
        {
            dialogueVoiceAudioSource.Stop();
        }

        if (contactRoutine != null)
        {
            StopCoroutine(contactRoutine);
            contactRoutine = null;
        }

        if (bgmRestoreRoutine != null)
        {
            StopCoroutine(bgmRestoreRoutine);
            bgmRestoreRoutine = null;
        }
    }
}
