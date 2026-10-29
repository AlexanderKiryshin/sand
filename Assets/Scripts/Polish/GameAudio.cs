using System;
using UnityEngine;

/// <summary>Separate effects, sand and music voices; all clips are loaded once.</summary>
public class GameAudio : MonoBehaviour
{
    private const string SoundsKey = "sand_audio_sounds";
    private const string MusicKey = "sand_audio_music";
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = .65f;
    [SerializeField, Range(0f, 1f)] private float sandVolume = .42f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = .16f;

    public static GameAudio Instance { get; private set; }
    public bool SoundsEnabled { get; private set; }
    public bool MusicEnabled { get; private set; }
    public event Action SettingsChanged;

    private AudioSource sandSource, musicSource;
    private AudioClip tapClip, filledClip, completeClip;
    private ConveyorBelt conveyor;
    private SandCollector collector;
    private SandPourAnimator pourAnimator;
    private LevelIntroSandAnimator introAnimator;
    private WinConditionChecker winChecker;
    private bool applicationPaused, applicationUnfocused;
    private float lastGrainTime = -10f, lastTapTime = -10f, lastFilledTime = -10f;
    private float completionDelay = -1f;
    private int grainsThisFrame;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        SoundsEnabled = ReadPreference(SoundsKey);
        MusicEnabled = ReadPreference(MusicKey);
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(audioSource, false);
        sandSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(sandSource, true);
        ConfigureSource(musicSource, true);
        musicSource.priority = 128;
        tapClip = Resources.Load<AudioClip>("Audio/BucketTap");
        filledClip = Resources.Load<AudioClip>("Audio/BucketFilled");
        completeClip = Resources.Load<AudioClip>("Audio/LevelComplete");
        sandSource.clip = Resources.Load<AudioClip>("Audio/SandPour");
        musicSource.clip = Resources.Load<AudioClip>("Audio/SandGarden");
        ApplySettings();
    }

    private void Start()
    {
        conveyor = FindObjectOfType<ConveyorBelt>();
        collector = FindObjectOfType<SandCollector>();
        pourAnimator = FindObjectOfType<SandPourAnimator>();
        introAnimator = FindObjectOfType<LevelIntroSandAnimator>();
        winChecker = FindObjectOfType<WinConditionChecker>();
        if (conveyor != null) conveyor.OnBucketFilled += HandleBucketFilled;
        if (collector != null) collector.OnGrainCollected += HandleGrainCollected;
        if (winChecker != null) winChecker.OnLevelComplete += HandleLevelComplete;
    }

    private static void ConfigureSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.ignoreListenerPause = false;
    }

    private void Update()
    {
        bool suspended = applicationPaused || applicationUnfocused;
        audioSource.mute = !SoundsEnabled || suspended;
        sandSource.mute = !SoundsEnabled || suspended;
        musicSource.mute = !MusicEnabled || suspended;

        // One softly varying loop covers any number of grains and overlapping streams.
        bool pouring = Time.timeScale > 0f && (Time.time - lastGrainTime < .12f
            || (pourAnimator != null && pourAnimator.ActiveGrainCount > 0)
            || (introAnimator != null && introAnimator.IsPlaying));
        float intensity = Mathf.Lerp(.48f, 1f, Mathf.Clamp01(grainsThisFrame / 24f));
        float target = SoundsEnabled && !suspended && pouring ? sandVolume * intensity : 0f;
        sandSource.volume = Mathf.MoveTowards(sandSource.volume, target, Time.unscaledDeltaTime * (target > sandSource.volume ? 2f : 3f));
        if (sandSource.volume > .001f && !sandSource.isPlaying && !AudioListener.pause) sandSource.Play();
        else if (sandSource.volume <= .001f && sandSource.isPlaying) sandSource.Stop();
        grainsThisFrame = 0;

        float musicTarget = MusicEnabled && !suspended ? musicVolume : 0f;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicTarget, Time.unscaledDeltaTime * musicVolume / 6f);
        if (musicTarget > 0f && !musicSource.isPlaying && !AudioListener.pause) musicSource.Play();
        if (completionDelay >= 0f)
        {
            completionDelay -= Time.deltaTime;
            if (completionDelay < 0f) PlayEffect(completeClip, .85f);
        }
    }

    public void SetSoundsEnabled(bool value)
    {
        SoundsEnabled = value;
        SavePreference(SoundsKey, value);
        ApplySettings();
        SettingsChanged?.Invoke();
    }

    public void SetMusicEnabled(bool value)
    {
        MusicEnabled = value;
        SavePreference(MusicKey, value);
        ApplySettings();
        SettingsChanged?.Invoke();
    }

    private void ApplySettings()
    {
        bool suspended = applicationPaused || applicationUnfocused;
        audioSource.volume = effectsVolume;
        audioSource.mute = !SoundsEnabled || suspended;
        sandSource.mute = !SoundsEnabled || suspended;
        musicSource.mute = !MusicEnabled || suspended;
        if (!SoundsEnabled || suspended)
        {
            audioSource.Stop();
            sandSource.Stop();
            sandSource.volume = 0f;
        }
        if (!MusicEnabled || suspended)
        {
            musicSource.Pause();
            musicSource.volume = 0f;
        }
        else musicSource.UnPause();
    }

    // Audio preferences belong to this device and must be ready before SDK/cloud initialization.
    private static bool ReadPreference(string key) => GameProgressStore.IsPreview || YoutubePlatform.IsPlayer
        ? GameProgressStore.GetBool(key, true) : PlayerPrefs.GetInt(key, 1) != 0;

    private static void SavePreference(string key, bool value)
    {
        if (GameProgressStore.IsPreview || YoutubePlatform.IsPlayer) { GameProgressStore.SetBool(key, value); GameProgressStore.Save(); return; }
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ResetLevelAudio()
    {
        lastGrainTime = -10f;
        grainsThisFrame = 0;
        completionDelay = -1f;
        audioSource.Stop();
        sandSource.Stop();
        sandSource.volume = 0f;
    }

    public void PlayTap()
    {
        if (Time.unscaledTime - lastTapTime < .035f) return;
        lastTapTime = Time.unscaledTime;
        PlayEffect(tapClip);
    }

    private void HandleGrainCollected(byte color, int column)
    {
        lastGrainTime = Time.time;
        grainsThisFrame++;
    }

    private void HandleBucketFilled(Bucket bucket)
    {
        // Linked pairs finish together; avoid doubling the same sound in one frame.
        if (Time.unscaledTime - lastFilledTime < .06f) return;
        lastFilledTime = Time.unscaledTime;
        PlayEffect(filledClip);
    }

    private void HandleLevelComplete()
    {
        if (completionDelay < 0f) completionDelay = BucketCompletionAnimation.Duration;
    }

    private void PlayEffect(AudioClip clip, float volume = 1f)
    {
        if (SoundsEnabled && !applicationPaused && !applicationUnfocused && clip != null && isActiveAndEnabled)
            audioSource.PlayOneShot(clip, volume);
    }

    private void OnApplicationFocus(bool focused) { if (YoutubePlatform.IsPlayer) return; applicationUnfocused = !focused; if (sandSource != null) ApplySettings(); }
    private void OnApplicationPause(bool paused) { if (YoutubePlatform.IsPlayer) return; applicationPaused = paused; if (sandSource != null) ApplySettings(); }
    private void OnDisable()
    {
        if (audioSource != null) audioSource.Stop();
        if (sandSource != null) sandSource.Stop();
        if (musicSource != null) musicSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (conveyor != null) conveyor.OnBucketFilled -= HandleBucketFilled;
        if (collector != null) collector.OnGrainCollected -= HandleGrainCollected;
        if (winChecker != null) winChecker.OnLevelComplete -= HandleLevelComplete;
        if (sandSource != null) Destroy(sandSource);
        if (musicSource != null) Destroy(musicSource);
    }
}
