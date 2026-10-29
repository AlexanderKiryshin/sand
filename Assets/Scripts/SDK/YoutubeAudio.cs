using MirraGames.SDK;

// Adapted from BUSES youtube: update Mirra's volume cache so resume cannot undo mute.
public static class YoutubeAudio
{
    public static void Apply(bool enabled) { MirraSDK.Audio.Volume = enabled ? 1f : 0f; }
}
