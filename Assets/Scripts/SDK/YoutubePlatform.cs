using System;
using System.Collections;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using UnityEngine;
using YTGameSDK;

/// <summary>Game adapter over Google's YTGameWrapper, following the BUSES integration.</summary>
public sealed class YoutubePlatform : MonoBehaviour
{
    public static bool IsPlayer
    {
        get {
#if YOUTUBE_PLAYABLES && UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
    public static bool PurchasesAllowed
    {
        get {
#if YOUTUBE_PLAYABLES
            return false;
#else
            return true;
#endif
        }
    }
    private static YoutubePlatform instance;
    private static readonly YoutubeCloudProgress cloud = new YoutubeCloudProgress();
    private YTGameWrapper wrapper;
    private bool languageLoaded, signaled;
    public static bool Ready => instance != null && cloud.Loaded && instance.languageLoaded && MirraSDK.IsInitialized;
    public static LanguageType Language { get; private set; } = LanguageType.English;
    public static bool HasKey(string key) => cloud.HasKey(key);
    public static double Get(string key, double fallback) => cloud.Get(key, fallback);
    public static void Set(string key, double value) => cloud.Set(key, value);
    public static void Save() => cloud.RequestSave();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!IsPlayer || instance != null) return;
        var root = new GameObject("YTGameWrapper");
        DontDestroyOnLoad(root);
        root.AddComponent<YTGameWrapper>();
        instance = root.AddComponent<YoutubePlatform>();
    }
    private IEnumerator Start()
    {
        wrapper = GetComponent<YTGameWrapper>();
        cloud.Status = wrapper.ShowStatus;
        wrapper.SetOnAudioEnabledChangeCallback(YoutubeAudio.Apply);
        YoutubeAudio.Apply(wrapper.IsYTGameAudioEnabled());
        wrapper.SetOnPauseCallback(() => {
            cloud.Flush(wrapper.SendGameSaveData, true);
            YoutubeGameplayFreeze.PauseFromSdk();
        });
        wrapper.SetOnResumeCallback(YoutubeGameplayFreeze.ResumeFromSdk);
        while (wrapper.IsPlatformPaused()) yield return null;
        wrapper.LoadLanguage(tag => {
            string code = (tag ?? "en").ToLowerInvariant().Split('-')[0];
            Language = code == "ru" ? LanguageType.Russian : code == "tr" ? LanguageType.Turkish :
                code == "de" ? LanguageType.German : LanguageType.English;
            languageLoaded = true;
        });
        wrapper.LoadGameSaveData(cloud.CompleteLoad);
    }
    private void Update()
    {
        if (wrapper != null) cloud.Tick(wrapper, YoutubeGameplayFreeze.PlatformPaused || wrapper.IsPlatformPaused());
    }
    public static void GameReady()
    {
        if (!Ready || instance.signaled) return;
        instance.signaled = true;
        instance.wrapper.SendGameIsReady();
    }
    public static bool AdAvailable(bool rewarded) => Ready && !YoutubeGameplayFreeze.PlatformPaused &&
        !instance.wrapper.IsAdPending && instance.wrapper.InPlayablesEnv();
    public static void RequestAd(string placement, bool rewarded, Action<bool> callback)
    {
        if (!AdAvailable(rewarded)) { callback?.Invoke(false); return; }
        var input = UnityEngine.EventSystems.EventSystem.current;
        bool enabled = input != null && input.enabled;
        if (input != null) input.enabled = false;
        YoutubeGameplayFreeze.SetAdPending(true);
        void Complete(bool earned)
        {
            YoutubeGameplayFreeze.SetAdPending(false);
            try { callback?.Invoke(earned); }
            finally { if (input != null) input.enabled = enabled; }
        }
        if (rewarded) instance.wrapper.RequestRewardedAd(placement, earned => Complete(earned));
        else instance.wrapper.RequestInterstitialAd(_ => Complete(false));
    }
}
