using MirraGames.SDK;

public static class GameSdkReadiness
{
    public static bool IsReady
    {
        get
        {
            try
            {
                return YoutubePlatform.IsPlayer
                    ? YoutubePlatform.Ready
                    : MirraSDK.IsInitialized;
            }
            catch
            {
                return false;
            }
        }
    }
}
