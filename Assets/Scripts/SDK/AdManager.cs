using System;
using System.Collections.Generic;
using MirraGames.SDK;
using UnityEngine;

public class AdManager : MonoBehaviour
{
    public static AdManager instance;

    private string placement;
    private string progress;
    private Action onRewarded;
    private Action onNonReady;
    private Action onInterstitialClosed;
    private bool isWindowOpen;
    private bool rewardedSuccessHandled;
    private float lastRewardedTime = -999f;

    [SerializeField] private float rewardedMinInterval;
    [SerializeField] private bool disablePeriodicAds = true;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public bool CanStartRewarded()
    {
        if (rewardedMinInterval > 0f && Time.unscaledTime - lastRewardedTime < rewardedMinInterval)
        {
            return false;
        }

        if (GameProgressStore.GetBool("no_ads"))
        {
            return true;
        }

        try
        {
            return MirraSDK.Ads.IsRewardedReady;
        }
        catch
        {
            return true;
        }
    }

    public void StartRewarded(string progressMarker, string placementName, Action onSuccess, Action onNotReady)
    {
        rewardedSuccessHandled = false;
        progress = progressMarker;
        placement = placementName;
        onRewarded = onSuccess;
        onNonReady = onNotReady;

        if (rewardedMinInterval > 0f && Time.unscaledTime - lastRewardedTime < rewardedMinInterval)
        {
            HandleRewardedError(true);
            return;
        }

        if (GameProgressStore.GetBool("no_ads"))
        {
            lastRewardedTime = Time.unscaledTime;
            Analytic.RewardedStarted(progress, placement);
            HandleRewardedSuccess();
            return;
        }

        try
        {
            if (MirraSDK.Ads.IsRewardedReady)
            {
                lastRewardedTime = Time.unscaledTime;
                Analytic.RewardedStarted(progress, placement);
                MirraSDK.Ads.InvokeRewarded(HandleRewardedSuccess, HandleRewardedError, placement);
            }
            else
            {
                Analytic.RewardedStarted(progress, placement);
                HandleRewardedError(true);
            }
        }
        catch
        {
            // Editor / fallback providers: grant reward so flows remain testable.
            lastRewardedTime = Time.unscaledTime;
            HandleRewardedSuccess();
        }
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        onInterstitialClosed = onClosed;

        if (GameProgressStore.GetBool("no_ads"))
        {
            HandleInterstitialClosed();
            return;
        }

        try
        {
            if (MirraSDK.Ads.IsInterstitialReady)
            {
                MirraSDK.Ads.InvokeInterstitial(HandleInterstitialClosed);
                Dictionary<string, object> placements = new Dictionary<string, object>
                {
                    { "placement", "interstitial" }
                };
                MirraSDK.Analytics.Report("video_ads_started", placements);
            }
            else
            {
                HandleInterstitialClosed();
            }
        }
        catch
        {
            HandleInterstitialClosed();
        }
    }

    public void OnWindowEnabled(bool enabled)
    {
        isWindowOpen = enabled;
    }

    public bool IsWindowOpen => isWindowOpen;

    private void HandleRewardedSuccess()
    {
        rewardedSuccessHandled = true;
        onRewarded?.Invoke();
        Analytic.RewardedWatched(progress, placement);
        RestoreTimeScale();
    }

    private void HandleRewardedError(bool error)
    {
        if (error && !rewardedSuccessHandled)
        {
            onNonReady?.Invoke();
            RestoreTimeScale();
        }
    }

    private void HandleInterstitialClosed()
    {
        RestoreTimeScale();
        Action cb = onInterstitialClosed;
        onInterstitialClosed = null;
        cb?.Invoke();
    }

    private static void RestoreTimeScale()
    {
        try
        {
            MirraSDK.Time.Scale = 1f;
        }
        catch
        {
            // ignored
        }

        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (disablePeriodicAds || isWindowOpen)
        {
            return;
        }
    }
}
