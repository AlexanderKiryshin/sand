using System;
using UnityEngine;

/// <summary>Runs against the actual C# cloud adapter and Unity JSON parser before the player build.</summary>
public static class YoutubePortChecks
{
    private static void Require(bool condition, string description)
    { if (!condition) throw new Exception(description); Debug.Log("YOUTUBE_CHECK_PASS " + description); }
    public static void Run()
    {
        const string empty = "{\"version\":2,\"entries\":[]}";
        var failed = new YoutubeCloudProgress(); failed.CompleteLoad(null);
        failed.CompleteLoad(empty);
        Require(failed.Loaded && !failed.Writable, "Failed load cannot be replaced by a late empty result");
        var invalid = new YoutubeCloudProgress(); invalid.CompleteLoad("{}");
        Require(!invalid.Writable, "Missing entries block writes");
        var duplicate = new YoutubeCloudProgress();
        duplicate.CompleteLoad("{\"version\":2,\"entries\":[{\"key\":\"a\",\"value\":\"1\"},{\"key\":\"a\",\"value\":\"2\"}]}");
        Require(!duplicate.Writable && !duplicate.HasKey("a"), "Duplicate keys reject load atomically");
        var state = new YoutubeCloudProgress(); state.CompleteLoad(empty); state.Set("Coins", 123);
        Action<string> ack = null; int writes = 0;
        Action<string, Action<string>> send = (json, callback) => { writes++; ack = callback; };
        state.Flush(send); state.Set("Coins", 223); state.Flush(send);
        Require(writes == 1 && state.Dirty, "Writes serialize while progress changes");
        ack("success"); Require(state.Dirty, "Old acknowledgment leaves newer progress dirty");
        state.Flush(send); ack("success"); ack("failed");
        Require(writes == 2 && !state.Dirty && state.Get("Coins", 0) == 223, "Newest acknowledgment clears dirty once");
        state.Set("Coins", 323); state.Flush(send); ack("timeout"); ack("success");
        state.Flush(send);
        Require(!state.Writable && state.Dirty && writes == 3, "Timed-out write cannot race a new write");
        var retry = new YoutubeCloudProgress(); retry.CompleteLoad(empty); retry.Set("Coins", 1);
        int attempts = 0;
        for (int i = 0; i < 8; i++) retry.Flush((json, callback) => { attempts++; callback("failed"); });
        Require(attempts == 3 && retry.Dirty, "Save failures have a bounded retry count");
        var restored = new YoutubeCloudProgress();
        restored.CompleteLoad("{\"version\":2,\"entries\":[{\"key\":\"Coins\",\"value\":\"123\"},{\"key\":\"flag\",\"value\":\"true\"}]}");
        Require(restored.Get("Coins", 0) == 123 && restored.Get("flag", 0) == 1, "BUSES key-value progress restores numeric and bool values");
    }
}
