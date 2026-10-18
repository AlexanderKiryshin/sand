using MirraGames.SDK;
using UnityEngine;

public class GameIsReady : MonoBehaviour
{
    private System.Collections.IEnumerator Start()
    {
        yield return new WaitUntil(() => GameSdkReadiness.IsReady);

        MirraSDK.Analytics.GameIsReady();
        MirraSDK.Analytics.GameplayStart();
    }
}
