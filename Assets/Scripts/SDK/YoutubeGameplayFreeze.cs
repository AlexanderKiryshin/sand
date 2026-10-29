using MirraGames.SDK;
using UnityEngine;

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
        if (next && !frozen) previousScale = Time.timeScale;
        if (next != frozen) MirraSDK.Time.Scale = next ? 0 : previousScale;
        frozen = next;
        MirraSDK.Audio.Pause = frozen;
    }
}
