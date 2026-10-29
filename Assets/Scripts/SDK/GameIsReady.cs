using MirraGames.SDK;
using System.Collections.Generic;
using UnityEngine;

public class GameIsReady : MonoBehaviour
{
    private static readonly HashSet<object> openWindows = new HashSet<object>();
    private static bool levelLoaded;
    private static bool levelPlaying;
    private static bool readyReported;
    private static bool gameplayReported;

    private System.Collections.IEnumerator Start()
    {
        yield return new WaitUntil(() => GameSdkReadiness.IsReady && levelLoaded);

        if (!readyReported)
        {
            MirraSDK.Analytics.GameIsReady();
            readyReported = true;
        }
        RefreshGameplay();
    }

    public static void SetLevelPlaying(bool playing)
    {
        if (playing) levelLoaded = true;
        levelPlaying = playing;
        RefreshGameplay();
    }

    public static void SetWindowOpen(object window, bool open)
    {
        if (window == null) return;
        if (open) openWindows.Add(window);
        else openWindows.Remove(window);
        RefreshGameplay();
    }

    private static void RefreshGameplay()
    {
        if (!readyReported || !GameSdkReadiness.IsReady) return;
        bool playing = levelPlaying && openWindows.Count == 0;
        if (playing == gameplayReported) return;
        gameplayReported = playing;
        if (playing) MirraSDK.Analytics.GameplayStart();
        else MirraSDK.Analytics.GameplayStop();
    }

    private void LateUpdate() => RefreshGameplay();

    private void OnDestroy() => Reset();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        openWindows.Clear();
        levelLoaded = levelPlaying = readyReported = gameplayReported = false;
    }
}
