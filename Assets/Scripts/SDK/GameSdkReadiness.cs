using MirraGames.SDK;

public static class GameSdkReadiness
{
    public static bool IsReady
    {
        get
        {
            try
            {
                // MirraSDK includes its platform bootstrap in the provider readiness check.
                return MirraSDK.IsInitialized;
            }
            catch
            {
                return false;
            }
        }
    }
}
