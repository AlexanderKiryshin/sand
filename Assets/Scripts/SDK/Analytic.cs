using System;
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
    public static void TutorialStep(string tutorialId, int step, string stepId, string status, string reason = "")
        => Session.TutorialStep(tutorialId, step, stepId, status, reason);
    private static int LevelNumber(string index) => int.TryParse(index, out int value) ? value + 1 : 0;
    public static void AdShown(string progress, string placement, string adType)
    {
        int level = LevelNumber(progress);
        Track(adType == "interstitial" ? "interstitial_shown" : "rewarded_shown", new Dictionary<string, object>
        {
            { "adType", adType },
            { "placement", placement },
            { "result", "shown" },
            { "level", level },
            { "level_id", "level_" + level.ToString("000") },
            { "progressMarker", level }
        });
    }

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
        int level = LevelNumber(progress);
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "bonusType", bonus },
            { "booster_type", bonus },
            { "level", level },
            { "level_id", "level_" + level.ToString("000") },
            { "level_booster", "level_" + level.ToString("000") + ":" + bonus },
            { "progressMarker", level }
        };
        Track("bonus_used", eventParameters);
        if (bonus == "magnet" || bonus == "transparent" || bonus == "shuffle")
            Track("booster_" + bonus + "_used", eventParameters);
    }

    public static void GoldChanged(string level, float value)
    {
        ResourceChanged(level, "gold", value);
    }

    public static void ConveyorExpanded(string progress)
    {
        int level = LevelNumber(progress);
        Track("conveyor_expanded", new Dictionary<string, object>
        {
            { "level", level },
            { "level_id", "level_" + level.ToString("000") },
            { "placement", "conveyor_slot" }
        });
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

    public static void CosmeticSelected(string cosmeticId, string category, int index)
        => CosmeticEvent("cosmetic_selected", cosmeticId, category, index, null, null);

    public static void CosmeticAcquired(string cosmeticId, string category, int index, string source, string productId)
        => CosmeticEvent("cosmetic_acquired", cosmeticId, category, index, source, productId);

    private static void CosmeticEvent(string name, string cosmeticId, string category, int index, string source, string productId)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["cosmetic_id"] = cosmeticId,
                ["cosmetic_category"] = category,
                ["cosmetic_index"] = index
            };
            if (!string.IsNullOrEmpty(source)) data["acquisition"] = source;
            if (!string.IsNullOrEmpty(productId)) data["product_id"] = productId;
            Track(name, data);
        }
        catch (Exception) { Debug.LogWarning("Cosmetic analytics unavailable."); }
    }

    public static void BuyInApp(string level, string name)
    {
        Dictionary<string, object> eventParameters = new Dictionary<string, object>
        {
            { "name", name },
            { "product_id", name },
            { "result", "success" },
            { "level", LevelNumber(level) }
        };
        Track("buy_inapp", eventParameters);
        string productEvent;
        switch (name)
        {
            case "premium_kit_75": productEvent = "purchase_premium_kit"; break;
            // Preserve configured analytics goals while reporting the new product IDs in event parameters.
            case "gold_800": productEvent = "purchase_gold_300"; break;
            case "gold_1500": productEvent = "purchase_gold_700"; break;
            case "gold_3000": productEvent = "purchase_gold_1200"; break;
            case "removeAd": productEvent = "purchase_remove_ads"; break;
            default: return;
        }
        Track(productEvent, eventParameters);
    }
}
