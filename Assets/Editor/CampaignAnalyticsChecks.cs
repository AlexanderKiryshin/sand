using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameAnalyticsSDK;
using UnityEngine;

public static class CampaignAnalyticsChecks
{
    public static void Run()
    {
        double now = 10;
        var events = new List<(string name, Dictionary<string, object> data)>();
        var session = new LevelAnalyticsSession((name, data) => events.Add((name, data)), () => now);
        session.Begin(0, "First", "wildlife", "", false); session.Move(); session.Booster(); now = 20;
        session.Complete(); session.Complete(); session.Abandon("quit");
        Require(events.Count(e => e.name == "level_started") == 1 && events.Count(e => e.name == "level_completed") == 1, "One start and one victory per attempt.");
        Require(!events.Any(e => e.name == "level_abandoned"), "Leaving a won level is not a failure.");
        var complete = events.Last().data;
        Require((int)complete["level"] == 1 && (double)complete["duration_seconds"] == 10 && (int)complete["moves"] == 1 && (int)complete["boosters"] == 1, "One-based number, elapsed time and counters.");
        session.Begin(299, "Last", "fantasy", "crates", false); string attempt = (string)events.Last().data["attempt_id"];
        now = 30; session.Restart(); session.Begin(299, "Last", "fantasy", "crates", false);
        Require((int)events.Last().data["level"] == 300 && (int)events.Last().data["attempt"] == 2 && (string)events.Last().data["attempt_id"] != attempt, "Restart starts a new attempt on level 300.");
        Require(events.Count(e => e.name == "level_abandoned") == 1 && events.Count(e => e.name == "level_restarted") == 1, "Restart terminates previous attempt once.");
        session.Begin(11, "Another", "people", "", true);
        Require(events.Count(e => e.name == "level_abandoned") == 2, "Changing unfinished level records abandonment.");
        session.Track("bonus_used", new Dictionary<string, object> { ["bonusType"] = "magnet" });
        session.Fail("test_failure"); session.Fail("test_failure");
        Require(events.Count(e => e.name == "level_failed") == 1, "Duplicate failure callbacks are ignored.");
        Require(WebGameAnalytics.ProgressionFor("level_started") == GAProgressionStatus.Start
            && WebGameAnalytics.ProgressionFor("level_completed") == GAProgressionStatus.Complete
            && WebGameAnalytics.ProgressionFor("level_abandoned") == GAProgressionStatus.Fail
            && WebGameAnalytics.ProgressionFor("bonus_used") == null, "GameAnalytics progression uses stable start/complete/fail IDs.");
        Directory.CreateDirectory("tasks");
        File.WriteAllText("tasks/analytics-validation.md", "PASS: one-based levels 1 and 300, unique attempt IDs, completion deduplication, restart and abandonment, counters and elapsed time, GameAnalytics progression mapping. Production network calls are disabled in Editor.\n");
        Debug.Log("ANALYTICS_CHECKS_PASS");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
