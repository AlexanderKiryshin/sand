using System.Collections.Generic;
using MirraGames.SDK;
using UnityEngine;

public static class Analytic
{
    public static void RewardedStarted(string progress, string placement)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "adType", "rewarded" },
            { "placement", placement },
            { "result", "start" },
            { "internetConnection", Application.internetReachability != NetworkReachability.NotReachable },
            { "progressMarker", progress }
        };
        MirraSDK.Analytics.Report("video_ads_available", eventParameters);
    }

    public static void RewardedWatched(string progress, string placement)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "adType", "rewarded" },
            { "placement", placement },
            { "result", "watched" },
            { "internetConnection", Application.internetReachability != NetworkReachability.NotReachable },
            { "progressMarker", progress }
        };
        MirraSDK.Analytics.Report("video_ads_success", eventParameters);
    }

    public static void BonusUsed(string progress, string bonus)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "bonusType", bonus },
            { "progressMarker", progress }
        };
        MirraSDK.Analytics.Report("bonus_used", eventParameters);
    }

    public static void LevelStarted(string level)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "level", level }
        };
        MirraSDK.Analytics.Report("level_started", eventParameters);
    }

    public static void LevelCompleted(string level)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "level", level }
        };
        MirraSDK.Analytics.Report("level_comleted", eventParameters);
    }

    public static void GoldChanged(string level, float value)
    {
        ResourceChanged(level, "gold", value);
    }

    public static void ResourceChanged(string level, string resource, float value)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "resource", resource },
            { "level", level },
            { "value", value }
        };
        MirraSDK.Analytics.Report("resource_changed", eventParameters);
    }

    public static void BuyInApp(string level, string name)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "name", name },
            { "level", level }
        };
        MirraSDK.Analytics.Report("buy_inapp", eventParameters);
    }
}
