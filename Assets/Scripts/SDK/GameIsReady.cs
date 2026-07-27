using MirraGames.SDK;
using UnityEngine;

public class GameIsReady : MonoBehaviour
{
    private void Start()
    {
        if (!MirraSDK.IsInitialized)
        {
            return;
        }

        MirraSDK.Analytics.GameIsReady();
        MirraSDK.Analytics.GameplayStart();
    }
}
