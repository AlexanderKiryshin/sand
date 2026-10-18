using MirraGames.SDK;
using UnityEngine;

public class OpenWindow : MonoBehaviour
{
    public void Open()
    {
        if (GameSdkReadiness.IsReady) MirraSDK.Analytics.GameplayStop();
        gameObject.SetActive(true);
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }
    }

    public void Close()
    {
        if (GameSdkReadiness.IsReady) MirraSDK.Analytics.GameplayStart();
        gameObject.SetActive(false);
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }
    }
}
