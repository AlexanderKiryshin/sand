using MirraGames.SDK;
using UnityEngine;
using YTGameSDK;

// Adapted from BUSES, without ForceResume or the grace period that ignored SDK pause.
public static class YoutubeGameplayFreeze
{
    public static bool PlatformPaused { get; private set; }
    private static bool adPending, frozen;
    private static float previousScale;
    public static void PauseFromSdk() { PlatformPaused = true; Apply(); }
    public static void ResumeFromSdk() { PlatformPaused = false; Apply(); }
    public static void SetAdPending(bool value) { adPending = value; Apply(); }
    private static void Apply()
    {
        bool next = PlatformPaused || adPending;
        bool resuming = frozen && !next;
        if (next && !frozen) previousScale = Time.timeScale;
        if (next != frozen) MirraSDK.Time.Scale = next ? 0 : previousScale;
        frozen = next;
        MirraSDK.Audio.Pause = frozen;
        if (resuming)
        {
            // BUSES rechecks the YouTube audio setting when gameplay resumes.
            var wrapper = Object.FindObjectOfType<YTGameWrapper>();
            if (wrapper != null) YoutubeAudio.Apply(wrapper.IsYTGameAudioEnabled());
        }
    }
}
