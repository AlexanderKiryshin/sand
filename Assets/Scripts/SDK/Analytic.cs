using System.Collections.Generic;
using UnityEngine;

public static class Analytic
{
    private static LevelAnalyticsSession session;
    private static LevelAnalyticsSession Session => session ?? (session = new LevelAnalyticsSession(WebGameAnalytics.Report, () => Time.realtimeSinceStartupAsDouble));
    public static void ResetSession() { session = null; }
    public static void BeginLevel(int index, LevelData level, bool replay)
    {
        var mechanics = new List<string>();
        if (level.alternatingCrates) mechanics.Add("crates");
        if (level.loopInventory) mechanics.Add("loop");
        if (level.frostMoves > 0) mechanics.Add("ice");
        if (level.sectionBuckets) mechanics.Add("sections");
        if (level.keyGates) mechanics.Add("keys");
        if (level.linkedBuckets) mechanics.Add("linked");
        if (level.largeBuckets) mechanics.Add("large");
        if (level.swappingColors) mechanics.Add("swap");
        if (level.mysteryBuckets) mechanics.Add("mystery");
        Session.Begin(index, level.displayTitle, level.artTheme, string.Join(",", mechanics), replay);
    }
    public static void CompleteLevel() => Session.Complete();
    public static void RestartLevel() => Session.Restart();
    public static void AbandonLevel(string reason) => Session.Abandon(reason);
    public static void BucketPlaced() => Session.Move();
    public static void Track(string name, Dictionary<string, object> data = null) => Session.Track(name, data);
    private static int LevelNumber(string index) => int.TryParse(index, out int value) ? value + 1 : 0;
    public static void RewardedStarted(string progress, string placement)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "adType", "rewarded" },
            { "placement", placement },
            { "result", "start" },
            { "internetConnection", Application.internetReachability != NetworkReachability.NotReachable },
            { "progressMarker", LevelNumber(progress) }
        };
        Track("video_ads_available", eventParameters);
    }

    public static void RewardedWatched(string progress, string placement)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "adType", "rewarded" },
            { "placement", placement },
            { "result", "watched" },
            { "internetConnection", Application.internetReachability != NetworkReachability.NotReachable },
            { "progressMarker", LevelNumber(progress) }
        };
        Track("video_ads_success", eventParameters);
    }

    public static void BonusUsed(string progress, string bonus)
    {
        Session.Booster();
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "bonusType", bonus },
            { "progressMarker", LevelNumber(progress) }
        };
        Track("bonus_used", eventParameters);
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
            { "level", LevelNumber(level) },
            { "value", value }
        };
        Track("resource_changed", eventParameters);
    }

    public static void BuyInApp(string level, string name)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "name", name },
            { "level", LevelNumber(level) }
        };
        Track("buy_inapp", eventParameters);
    }
}
