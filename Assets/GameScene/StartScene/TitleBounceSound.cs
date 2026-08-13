using UnityEngine;

public class TitleBounceSound : MonoBehaviour
{
    [Header("오디오 소스")]
    [SerializeField]
    private AudioSource audioSource;


    [Header("튕김 효과음")]
    [SerializeField]
    private AudioClip bounceSound1;

    [SerializeField]
    private AudioClip bounceSound2;

    [SerializeField]
    private AudioClip bounceSound3;

    [SerializeField]
    private AudioClip bounceSound4;


    [Header("효과음 볼륨")]
    [SerializeField, Range(0f, 1f)]
    private float volume = 0.7f;


    // 첫 번째 큰 튕김
    public void PlayBounce1()
    {
        PlaySound(bounceSound1);
    }


    // 두 번째 튕김
    public void PlayBounce2()
    {
        PlaySound(bounceSound2);
    }


    // 세 번째 튕김
    public void PlayBounce3()
    {
        PlaySound(bounceSound3);
    }


    // 마지막 작은 튕김
    public void PlayBounce4()
    {
        PlaySound(bounceSound4);
    }


    // 실제 효과음 재생
    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            clip,
            volume
        );
    }
}