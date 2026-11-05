using MirraGames.SDK;

// Adapted from BUSES youtube: update Mirra's volume cache so resume cannot undo mute.
public static class YoutubeAudio
{
    private static bool ytAudioEnabled = true;

    public static bool IsYtAudioEnabled => ytAudioEnabled;

    public static void Apply(bool enabled)
    {
        ytAudioEnabled = enabled;
        MirraSDK.Audio.Volume = enabled ? 1f : 0f;
    }
}
