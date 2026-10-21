// Copy to Assets/Editor of the isolated inventory validation project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ColumnFrontAccessChecks
{
    [Serializable] public class Case { public int index; public string path; }
    [Serializable] public class Cases { public Case[] cases; }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("SAND_COLUMN_ACCESS_OUTPUT");
        Directory.CreateDirectory(output);
        var random = UnityEngine.Random.state;
        int runs = 0;
        try
        {
            CheckBlockedCup();
            foreach (var item in JsonUtility.FromJson<Cases>(File.ReadAllText("column-cases.json")).cases)
            {
                var source = AssetDatabase.LoadAssetAtPath<LevelData>(item.path);
                Require(source != null, "Missing source " + item.path);
                for (int run = 0; run < 3; run++)
                {
                    var level = CampaignRunFactory.Create(source, item.index);
                    var root = new GameObject("Column route check");
                    try
                    {
                        level.campaignSeed = unchecked(7319 + item.index * 104729 + run * 7919);
                        UnityEngine.Random.InitState(level.campaignSeed);
                        var stock = new BucketInventory();
                        stock.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                        var all = stock.Buckets.Concat(stock.ReservedBuckets).Concat(stock.ConveyorSlots.SelectMany(slot => slot.ReservedBuckets)).ToArray();
                        foreach (byte color in level.GetColorsUsedInLevel())
                            Require(all.Sum(b => b.RemainingForColor(color)) == level.CountLevelCellsForColor(color),
                                "Color budget mismatch at " + item.index + "/" + run);
                        var belt = root.AddComponent<ConveyorBelt>();
                        belt.Configure(stock.TotalBucketCount); belt.ConfigureLevel(level);
                        int moves = 0;
                        while (stock.CountRemaining() > 0 && moves++ < 500)
                        {
                            stock.TryDispenseAll(level);
                            foreach (var cup in stock.Buckets)
                                if (stock.IsBucketAccessible(cup, level))
                                    Require(BucketGridAccess.IsAccessible(cup.LayoutIndex, stock),
                                        "Side-column bypass at " + item.index + "/" + run);
                            var next = stock.Buckets.OrderByDescending(stock.IsCrateCovered).ThenByDescending(b => b.KeyId).ToArray()
                                .FirstOrDefault(b => stock.IsBucketAccessible(b, level) && stock.TryTakeBucket(b, belt, level));
                            Require(next != null, "Blocked dispatch route: " + item.path + ", index " + item.index + ", seed " + level.campaignSeed + ", remaining " + stock.CountRemaining());
                            foreach (var entry in belt.Entries) stock.HandleBucketFilled(entry.Bucket);
                            belt.ResetConveyor();
                        }
                        Require(stock.CountRemaining() == 0, "Incomplete route " + item.index + "/" + run);
                        runs++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                        UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride);
                        UnityEngine.Object.DestroyImmediate(level);
                    }
                }
                if ((item.index + 1) % 100 == 0) Debug.Log("COLUMN_ROUTES_PROGRESS: " + (item.index + 1) + ", runs " + runs);
            }
            File.WriteAllText(Path.Combine(output, "unity-status.txt"), "PASS: " + runs + " deterministic inventory routes; exact per-color budgets; no adjacent-column bypass; real dispatch, linked pairs, ice, keys, crates and loop inventory. Sand physics is not simulated.");
            Debug.Log("COLUMN_FRONT_ACCESS_PASS: " + runs);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "unity-status.txt"), "FAIL after " + runs + " runs\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally { UnityEngine.Random.state = random; }
    }

    static void CheckBlockedCup()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var root = new GameObject("Blocked column regression");
        try
        {
            level.EnsureBucketLayout();
            var stock = new BucketInventory();
            var front = new Bucket(1, 10, 0);
            var rear = new Bucket(2, 10, 5);
            var cups = (List<Bucket>)stock.Buckets;
            cups.AddRange(new[] { front, rear });
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(3);
            Require(!stock.IsBucketAccessible(rear, level) && !stock.TryTakeBucket(rear, belt, level),
                "A free adjacent column opened/dispatched the rear cup");
            Require(cups.Count == 2 && belt.Entries.Count == 0, "Blocked dispatch mutated the board");
            cups.Remove(front);
            Require(stock.IsBucketAccessible(rear, level) && stock.TryTakeBucket(rear, belt, level),
                "Clearing the own column did not enable dispatch");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
    }
}
