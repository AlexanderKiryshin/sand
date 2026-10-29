using MirraGames.SDK;
using UnityEngine;

public class GameIsReady : MonoBehaviour
{
    private System.Collections.IEnumerator Start()
    {
        yield return new WaitUntil(() => GameSdkReadiness.IsReady);

        if (YoutubePlatform.IsPlayer)
        {
            // GameManager.Start has built the level; wait for its intro to become interactive.
            yield return null;
            var intro = FindObjectOfType<LevelIntroSandAnimator>();
            while (intro != null && intro.IsPlaying) yield return null;
            yield return new WaitForEndOfFrame();
            YoutubePlatform.GameReady();
            yield break;
        }
        MirraSDK.Analytics.GameIsReady();
        MirraSDK.Analytics.GameplayStart();
    }
}
