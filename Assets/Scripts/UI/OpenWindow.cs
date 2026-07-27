using MirraGames.SDK;
using UnityEngine;

public class OpenWindow : MonoBehaviour
{
    public void Open()
    {
        MirraSDK.Analytics.GameplayStop();
        gameObject.SetActive(true);
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }
    }

    public void Close()
    {
        MirraSDK.Analytics.GameplayStart();
        gameObject.SetActive(false);
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }
    }
}
