using UnityEngine;

public class GameAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    private ConveyorBelt conveyor;
    private WinConditionChecker winChecker;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        conveyor = FindObjectOfType<ConveyorBelt>();
        winChecker = FindObjectOfType<WinConditionChecker>();

        if (conveyor != null)
        {
            conveyor.OnBucketFilled += HandleBucketFilled;
        }

        if (winChecker != null)
        {
            winChecker.OnLevelComplete += HandleLevelComplete;
        }
    }

    private void OnDestroy()
    {
        if (conveyor != null)
        {
            conveyor.OnBucketFilled -= HandleBucketFilled;
        }

        if (winChecker != null)
        {
            winChecker.OnLevelComplete -= HandleLevelComplete;
        }
    }

    public void PlayTap()
    {
        PlayTone(660f, 0.05f);
    }

    private void HandleBucketFilled(Bucket bucket)
    {
        PlayTone(440f, 0.08f);
    }

    private void HandleLevelComplete()
    {
        PlayTone(880f, 0.15f);
    }

    private void PlayTone(float frequency, float duration)
    {
        if (audioSource == null)
        {
            return;
        }

        int sampleRate = 44100;
        int sampleLength = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleLength];
        for (int i = 0; i < sampleLength; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (t / duration);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.2f;
        }

        AudioClip clip = AudioClip.Create("Tone", sampleLength, 1, sampleRate, false);
        clip.SetData(samples, 0);
        audioSource.PlayOneShot(clip);
    }
}
