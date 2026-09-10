using System;
using System.Collections.Generic;
using MirraGames.SDK;
using UnityEngine;

public class AdManager : MonoBehaviour
{
    public static AdManager instance;

    private string placement;
    private string progress;
    private RewardedAttempt rewardedAttempt;
    private float requestStartedAt;
    private Action onInterstitialClosed;
    private bool isWindowOpen;
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

    public bool CanStartRewarded(bool requireAdView = false)
    {
        if (rewardedAttempt != null && !rewardedAttempt.IsSettled) return false;
        if (rewardedMinInterval > 0f && Time.unscaledTime - lastRewardedTime < rewardedMinInterval)
        {
            return false;
        }

        if (!requireAdView && GameProgressStore.GetBool("no_ads"))
        {
            return true;
        }

        try
        {
            return MirraSDK.Ads.IsRewardedReady;
        }
        catch
        {
            return false;
        }
    }

    public void StartRewarded(string progressMarker, string placementName, Action onSuccess, Action onNotReady, bool requireAdView = false)
    {
        if (!CanStartRewarded(requireAdView)) { onNotReady?.Invoke(); return; }
        progress = progressMarker;
        placement = placementName;
        RewardedAttempt attempt = null;
        attempt = new RewardedAttempt(() =>
        {
            if (rewardedAttempt == attempt) rewardedAttempt = null;
            RestoreTimeScale();
            Analytic.RewardedWatched(progressMarker, placementName);
            onSuccess?.Invoke();
        }, () =>
        {
            if (rewardedAttempt == attempt) rewardedAttempt = null;
            RestoreTimeScale();
            onNotReady?.Invoke();
        });
        rewardedAttempt = attempt;
        requestStartedAt = Time.realtimeSinceStartup;
        lastRewardedTime = Time.unscaledTime;
        Analytic.RewardedStarted(progressMarker, placementName);
        if (!requireAdView && GameProgressStore.GetBool("no_ads")) { attempt.Complete(true); return; }
        try
        {
            // Installed SDK signature is (onOpen, onClose(isSuccess), tag).
            MirraSDK.Ads.InvokeRewarded(null, watched => attempt.Complete(watched), placementName);
        }
        catch { attempt.Complete(false); }
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
        if (rewardedAttempt != null && Time.realtimeSinceStartup - requestStartedAt > 180)
            rewardedAttempt.Complete(false);
        if (disablePeriodicAds || isWindowOpen)
        {
            return;
        }
    }
}
