// Run from Assets/Editor in the isolated validation project; no player saves or SDK.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AllLevelPlayabilityChecks
{
    [Serializable] public class Case { public int index; public string path; }
    [Serializable] public class Cases { public Case[] cases; }
    [Serializable] public class Result
    {
        public int index, number, seed, strategy, ticks, sand, inventory, belt, decisions;
        public int originalBucketLimit, bucketLimit, initialBuckets;
        public bool winter, passed, supplyOrder;
        public string reason, diagnostics;
        public List<string> moves = new List<string>();
    }
    [Serializable] public class SceneSettings {
        public float ticksPerSecond, sandImageWidth, conveyorWidth, bucketWidth;
        public int maxTicksPerFrame, maxCollectPerTick, maxCollectPerFrame, collectRowCount, straightTicksBeforeDiagonal;
    }
    static float candidateCapacityFactor = 1f;
    static bool forceSupplyOrder;
    static SceneSettings settings;
    static string output;
    static FieldInfo budget = typeof(FallingSandSimulator).GetField("collectBudgetThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Run()
    {
        output = Environment.GetEnvironmentVariable("SAND_PLAYABILITY_OUTPUT");
        Directory.CreateDirectory(output);
        settings = JsonUtility.FromJson<SceneSettings>(File.ReadAllText("scene-settings.json"));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayabilityMappingFixture.Validate();
        int first = IntEnv("SAND_PLAYABILITY_FIRST", 0), last = IntEnv("SAND_PLAYABILITY_LAST", 1199);
        int strategies = IntEnv("SAND_PLAYABILITY_STRATEGIES", 3);
        var cases = JsonUtility.FromJson<Cases>(File.ReadAllText("column-cases.json")).cases.Where(c => c.index >= first && c.index <= last).ToArray();
        int minimumStrategy = IntEnv("SAND_PLAYABILITY_STRATEGY_FIRST", 0);
        var results = new Dictionary<int, Result>();
        string checkpoint = Path.Combine(output, "attempts.jsonl");
        if (File.Exists(checkpoint))
            foreach (string line in File.ReadLines(checkpoint))
            {
                var saved = JsonUtility.FromJson<Result>(line);
                if (saved.index >= first && saved.index <= last && (!results.ContainsKey(saved.index) || !results[saved.index].passed))
                    results[saved.index] = saved;
            }
        foreach (var item in cases)
        {
            if (results.TryGetValue(item.index, out var savedResult) && (savedResult.passed || savedResult.strategy >= strategies - 1)) continue;
            var source = AssetDatabase.LoadAssetAtPath<LevelData>(item.path);
            for (int strategy = minimumStrategy; strategy < strategies; strategy++)
            {
                var result = Trial(source, item.index, strategy);
                results[item.index] = result;
                File.AppendAllText(Path.Combine(output, "attempts.jsonl"), JsonUtility.ToJson(result) + "\n");
                if (result.passed) break;
            }
            var current = results[item.index];
            Debug.Log("PLAYABILITY_LEVEL: " + item.index + " " + (current.passed ? "PASS" : "UNRESOLVED") + " strategy " + current.strategy + " ticks " + current.ticks + " sand " + current.sand);
            File.WriteAllText(Path.Combine(output, "progress.json"), "{\"checked\":" + results.Count + ",\"total\":" + cases.Length + ",\"passed\":" + results.Values.Count(r => r.passed) + ",\"unresolved\":" + results.Values.Count(r => !r.passed) + ",\"lastIndex\":" + item.index + "}");
        }
        int failures = results.Values.Count(r => !r.passed);
        File.WriteAllText(Path.Combine(output, "status.txt"), (failures == 0 ? "PASS" : "UNRESOLVED") + ": " + results.Count + " levels checked; " + results.Values.Count(r => r.passed) + " completed; " + failures + " need deeper search. Actual sand, collection, conveyor and inventory; one grain per cell (production scene setting), five belt slots, no boosters, no artificial filling or recalls. Layout seeds are recorded in attempts.jsonl. A bounded strategy failure does not prove impossibility.");
        EditorApplication.Exit(failures == 0 ? 0 : 2);
    }
    static int IntEnv(string key, int fallback) { return int.TryParse(Environment.GetEnvironmentVariable(key), out int value) ? value : fallback; }

    static Result Trial(LevelData source, int index, int strategy)
    {
        var result = new Result { index = index, number = source.seasonalNumber > 0 ? source.seasonalNumber : index + 1, winter = source.seasonalNumber > 0, strategy = strategy };
        var random = UnityEngine.Random.state;
        var level = CampaignRunFactory.Create(source, index);
        var root = new GameObject("Full sand playability");
        try
        {
            int expectedSeed = unchecked(7319 + index * 104729);
            if (level.bucketSettingsOverride.verifiedLayoutSeed != 0)
            {
                if (level.campaignSeed != expectedSeed) throw new InvalidOperationException("Pinned runtime seed differs from witness seed");
            }
            // Unpinned production levels use a fresh seed on every run.
            else level.campaignSeed = unchecked(expectedSeed + IntEnv("SAND_PLAYABILITY_SEED_OFFSET", 0));
            result.seed = level.campaignSeed;
            if (forceSupplyOrder) level.bucketSettingsOverride.matchBucketOrderToSand = true;
            result.supplyOrder = level.bucketSettingsOverride.matchBucketOrderToSand;
            result.originalBucketLimit = level.bucketSettingsOverride.maxGrainsPerBucket;
            level.bucketSettingsOverride.maxGrainsPerBucket = candidateCapacityFactor == 1f ? result.originalBucketLimit : Mathf.Max(16, Mathf.RoundToInt(result.originalBucketLimit * candidateCapacityFactor));
            result.bucketLimit = level.bucketSettingsOverride.maxGrainsPerBucket;
            Rect("SandImage", Rect("SandPanel", root.transform, settings.sandImageWidth + 24), settings.sandImageWidth);
            Rect("ConveyorPanel", root.transform, settings.conveyorWidth);
            root.AddComponent<SandConveyorMapper>().EnsureReferences();
            root.AddComponent<LevelLoader>().SetCurrentLevel(level);
            var belt = root.AddComponent<ConveyorBelt>();
            ConveyorBelt.ConfigureGeometry(settings.conveyorWidth, settings.bucketWidth);
            var collector = root.AddComponent<SandCollector>(); collector.Initialize(belt);
            var simulator = root.AddComponent<FallingSandSimulator>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FallingSandSimulator).GetField("ticksPerSecond", flags).SetValue(simulator, settings.ticksPerSecond);
            foreach (string field in new[] {"maxTicksPerFrame", "maxCollectPerTick", "maxCollectPerFrame", "collectRowCount", "straightTicksBeforeDiagonal"})
                typeof(FallingSandSimulator).GetField(field, flags).SetValue(simulator, typeof(SceneSettings).GetField(field).GetValue(settings));
            var grid = new SandGrid(level, 1); simulator.Initialize(grid, collector);
            var inventory = new BucketInventory();
            UnityEngine.Random.InitState(level.campaignSeed);
            inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
            belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
            belt.OnBucketReturned += inventory.ReturnBucket;
            belt.OnBucketFilled += inventory.HandleBucketFilled;
            var initial = inventory.Buckets.Concat(inventory.ReservedBuckets).Concat(inventory.ConveyorSlots.SelectMany(s => s.ReservedBuckets)).ToArray();
            result.initialBuckets = initial.Length;
            foreach (byte color in level.GetColorsUsedInLevel())
                if (initial.Sum(b => b.RemainingForColor(color)) != grid.CountRemainingForColor(color))
                    throw new InvalidOperationException("Initial color budget mismatch: " + color);
            if (level.sectionBuckets && !initial.Any(b => b.HasSecondSection)) throw new InvalidOperationException("Missing section mechanic");
            if (level.largeBuckets && !initial.Any(b => b.SlotCost == 2)) throw new InvalidOperationException("Missing large mechanic");
            if (level.linkedBuckets && !initial.Any(b => b.LinkedPartner != null)) throw new InvalidOperationException("Missing linked mechanic");
            if (level.swappingColors && !initial.Any(b => b.SwapPartner != null)) throw new InvalidOperationException("Missing swap mechanic");
            if (level.keyGates && (!initial.Any(b => b.KeyId != 0) || !initial.Any(inventory.IsGateClosed))) throw new InvalidOperationException("Missing key mechanic");
            if (level.alternatingCrates && inventory.CrateBucketCount == 0) throw new InvalidOperationException("Missing crate mechanic");
            int lastCount = grid.CountRemaining(), stalled = 0, ticks;
            for (ticks = 0; ticks < 12000 && (grid.CountRemaining() > 0 || inventory.CountRemaining() > 0 || belt.Entries.Count > 0); ticks++)
            {
                inventory.TryDispenseAll(level);
                if (ticks % 8 == 0 && belt.CanAcceptBucket() && (strategy < 3 || belt.Entries.Count == 0 || stalled >= (strategy % 3 == 0 ? 64 : strategy % 3 == 1 ? 128 : 256)))
                {
                    var demand = new int[256];
                    for (int x = 0; x < grid.Width; x++)
                        for (int y = grid.Height - 1; y >= Mathf.Max(0, grid.Height - settings.collectRowCount); y--)
                            demand[grid.GetCell(x, y)]++;
                    var exits = new HashSet<int>(inventory.ConveyorSlots.Where(s => s.RemainingCount > 0).Select(s => ConveyorDirectionUtility.GetTargetLayoutIndex(s.LayoutIndex, s.Direction)));
                    var candidates = inventory.Buckets.Where(b => inventory.IsBucketAccessible(b, level))
                        .OrderByDescending(b => Score(b, strategy, demand, grid, belt, inventory, level, exits, result.decisions)).ToArray();
                    foreach (var candidate in candidates)
                    {
                        if (strategy == 1 && belt.OccupiedSlots >= 2 && demand[candidate.ColorIndex] == 0 && candidate.KeyId == 0) continue;
                        int position = candidate.LayoutIndex;
                        if (inventory.TryTakeBucket(candidate, belt, level))
                        {
                            result.moves.Add(ticks + ":" + position + ":" + candidate.ColorIndex + ":" + candidate.Capacity);
                            result.decisions++; break;
                        }
                    }
                }
                belt.SetAllBucketsDeployed(inventory.CountRemaining() == 0);
                belt.UpdateBelt(1f / settings.ticksPerSecond);
                simulator.Wake(); budget.SetValue(simulator, settings.maxCollectPerFrame); simulator.SimulateTick();
                int remaining = grid.CountRemaining();
                stalled = remaining == lastCount ? stalled + 1 : 0; lastCount = remaining;
                if (stalled >= 768) { result.reason = "No collected grains for 768 ticks"; break; }
            }
            result.ticks = ticks; result.sand = grid.CountRemaining(); result.inventory = inventory.CountRemaining(); result.belt = belt.Entries.Count;
            result.passed = result.sand == 0 && result.inventory == 0 && result.belt == 0;
            if (!result.passed)
            {
                result.reason = result.reason ?? "Tick limit";
                var all = inventory.Buckets.Concat(inventory.ReservedBuckets).Concat(inventory.ConveyorSlots.SelectMany(s => s.ReservedBuckets)).Concat(belt.Entries.Select(e => e.Bucket)).ToArray();
                result.diagnostics = "colors=" + string.Join(",", Enumerable.Range(1, level.palette.Length).Select(c => c + ":sand" + grid.CountRemainingForColor((byte)c) + "/demand" + all.Sum(b => b.RemainingForColor((byte)c))))
                    + " inventory=" + string.Join(",", inventory.Buckets.Select(b => b.LayoutIndex + ":" + b.ColorIndex + "/" + b.RemainingCapacity + ":key" + b.KeyId + ":gate" + inventory.IsGateClosed(b) + ":link" + b.LinkedPartner?.LayoutIndex + ":open" + inventory.IsBucketAccessible(b, level)))
                    + " belt=" + string.Join(",", belt.Entries.Select(e => e.Bucket.ColorIndex + ":" + e.Bucket.CurrentFill + "/" + e.Bucket.Capacity + ":second" + e.Bucket.SecondColor + ":cost" + e.Bucket.SlotCost + ":link" + e.Bucket.LinkedPartner?.ColorIndex));
            }
        }
        catch (Exception error) { result.passed = false; result.reason = error.ToString(); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride);
            UnityEngine.Object.DestroyImmediate(level);
            UnityEngine.Random.state = random;
        }
        return result;
    }


    [Serializable] public class RepairCase { public int index, firstStrategy; public string path; }
    [Serializable] public class RepairCases { public RepairCase[] cases; }

    public static void RunRepairCandidates()
    {
        forceSupplyOrder = true;
        output = Environment.GetEnvironmentVariable("SAND_PLAYABILITY_OUTPUT");
        Directory.CreateDirectory(output);
        settings = JsonUtility.FromJson<SceneSettings>(File.ReadAllText("scene-settings.json"));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayabilityMappingFixture.Validate();
        var cases = JsonUtility.FromJson<RepairCases>(File.ReadAllText("repair-cases.json")).cases;
        var latest = new Dictionary<int, Result>();
        string checkpoint = Path.Combine(output, "candidates.jsonl");
        if (File.Exists(checkpoint))
            foreach (string line in File.ReadLines(checkpoint))
            {
                var saved = JsonUtility.FromJson<Result>(line);
                if (!latest.ContainsKey(saved.index) || !latest[saved.index].passed) latest[saved.index] = saved;
            }
        foreach (var item in cases)
        {
            if (latest.TryGetValue(item.index, out var saved) && saved.passed) continue;
            var source = AssetDatabase.LoadAssetAtPath<LevelData>(item.path);
            bool passed = false;
            foreach (float factor in new[] { 1f, .85f, .7f, .55f, .4f, .25f, .16f })
            {
                candidateCapacityFactor = factor;
                var strategies = factor == 1f ? new[] { 0, 1, 2, 3, 4, 5 } : new[] { 0, 1, 3 };
                foreach (int strategy in strategies)
                {
                    var result = Trial(source, item.index, strategy);
                    File.AppendAllText(checkpoint, JsonUtility.ToJson(result) + "\n");
                    latest[item.index] = result;
                    if (result.passed) { passed = true; break; }
                }
                if (passed) break;
            }
            var current = latest[item.index];
            Debug.Log("REPAIR_CANDIDATE: " + item.index + " " + (current.passed ? "PASS" : "UNRESOLVED") + " cap " + current.originalBucketLimit + "->" + current.bucketLimit + " buckets " + current.initialBuckets);
            File.WriteAllText(Path.Combine(output, "progress.json"), "{\"checked\":" + latest.Count + ",\"total\":" + cases.Length + ",\"passed\":" + latest.Values.Count(v => v.passed) + ",\"unresolved\":" + latest.Values.Count(v => !v.passed) + ",\"lastIndex\":" + item.index + "}");
        }
        candidateCapacityFactor = 1f;
        int failures = latest.Values.Count(v => !v.passed);
        File.WriteAllText(Path.Combine(output, "status.txt"), (failures == 0 ? "PASS" : "UNRESOLVED") + ": " + latest.Count + " candidates checked, " + latest.Values.Count(v => v.passed) + " completed, " + failures + " unresolved. Candidate capacity changes are in-memory only; game assets are not written.");
        EditorApplication.Exit(failures == 0 ? 0 : 2);
    }

    [Serializable] public class VerifiedCase { public int index; public string path; public Result witness; }
    [Serializable] public class VerifiedCases { public VerifiedCase[] cases; }

    public static void RunVerifiedRoutes()
    {
        forceSupplyOrder = false;
        candidateCapacityFactor = 1f;
        output = Environment.GetEnvironmentVariable("SAND_PLAYABILITY_OUTPUT");
        Directory.CreateDirectory(output);
        settings = JsonUtility.FromJson<SceneSettings>(File.ReadAllText("scene-settings.json"));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayabilityMappingFixture.Validate();
        var cases = JsonUtility.FromJson<VerifiedCases>(File.ReadAllText("verified-cases.json")).cases;
        int failures = 0, checkedCount = 0;
        foreach (var item in cases)
        {
            var source = AssetDatabase.LoadAssetAtPath<LevelData>(item.path);
            var result = Trial(source, item.index, item.witness.strategy);
            var witness = item.witness;
            bool same = result.passed && result.seed == witness.seed && result.ticks == witness.ticks
                && result.moves.SequenceEqual(witness.moves);
            if (!same) { failures++; result.passed = false; result.reason = "Verified route changed: " + result.reason; }
            File.AppendAllText(Path.Combine(output, "verified.jsonl"), JsonUtility.ToJson(result) + "\n");
            checkedCount++;
            File.WriteAllText(Path.Combine(output, "progress.json"), "{\"checked\":" + checkedCount + ",\"total\":" + cases.Length + ",\"passed\":" + (checkedCount-failures) + ",\"unresolved\":" + failures + "}");
            Debug.Log("VERIFIED_ROUTE: " + item.index + " " + (same ? "PASS" : "FAIL"));
        }
        File.WriteAllText(Path.Combine(output, "status.txt"), (failures == 0 ? "PASS" : "FAIL") + ": " + checkedCount + " routes replayed against integrated assets; " + failures + " failures.");
        EditorApplication.Exit(failures == 0 ? 0 : 2);
    }

    static float Score(Bucket b, int strategy, int[] demand, SandGrid grid, ConveyorBelt belt, BucketInventory inventory, LevelData level, HashSet<int> exits, int decisions)
    {
        float score = demand[b.ColorIndex] - (belt.HasBucketWithColor(b.ColorIndex) ? 1000 : 0) + (b.KeyId != 0 ? 300 : 0) + (inventory.IsCrateCovered(b) ? 80 : 0);
        if (strategy == 1) score += demand[b.ColorIndex] * 300f / Mathf.Max(1, b.RemainingCapacity);
        if (strategy == 2 && exits.Contains(level.loopInventory ? InventoryLoopRoute.Next(b.LayoutIndex) : b.LayoutIndex)) score += 600;
        if (strategy >= 3 && strategy < 6) score += demand[b.ColorIndex] * 800f / Mathf.Max(1, b.RemainingCapacity);
        if (strategy >= 6) score += ((b.LayoutIndex * 31337 + decisions * 7919 + strategy * 104729) & 255) * (strategy < 9 ? .25f : 2f);
        if (strategy >= 12)
        {
            // Evaluate both linked cups and both sections against the actual outlet demand.
            // These strategies only choose legal moves; physics and capacities stay unchanged.
            score = 0;
            foreach (var cup in b.LinkedPartner == null ? new[] { b } : new[] { b, b.LinkedPartner })
            {
                foreach (byte color in cup.HasSecondSection ? new[] { cup.FirstColor, cup.SecondColor }.Distinct() : new[] { cup.FirstColor })
                {
                    int remaining = cup.RemainingForColor(color);
                    if (remaining <= 0) continue;
                    score += demand[color] * (strategy % 3 == 0 ? 1f : 300f / remaining);
                    if (belt.Entries.Any(e => e.Bucket.RemainingForColor(color) > 0)) score -= 1000;
                }
            }
            if (b.KeyId != 0) score += 300;
            if (strategy >= 15) score += ((b.LayoutIndex * 31337 + decisions * 7919 + strategy * 104729) & 255) * .25f;
        }
        return score;
    }
    static Transform Rect(string name, Transform parent, float size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        ((RectTransform)obj.transform).sizeDelta = new Vector2(size, size); return obj.transform;
    }
}

