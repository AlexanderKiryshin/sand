using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs the actual sand/collector/belt code in an empty editor scene, without player saves or SDK.</summary>
public static class CampaignSimulationChecks
{
    [MenuItem("Window/Sand/Campaign/Run Simulation Checks")]
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in batch mode; this check owns an empty temporary scene.");
        RunCore();
    }
    public static void RunFrom38() => RunCore(37);
    public static void RunFrom58() => RunCore(57);
    public static void RunBefore58() => RunCore(0, 56);
    public static void Diagnose50() => RunCore(49, 49, 8000);
    public static void RunAfterAccessFix() => RunCore(accessFixSubset: true);
    public static void RunQueueProgression() => RunCore(queueProgressionSubset: true);

    public static void RunInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before simulation checks.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save modified scenes before simulation checks.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try { RunCore(); }
        finally { RestoreSetup(setup); }
    }

    public static void RunReworkInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before simulation checks.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save modified scenes before simulation checks.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try { RunCore(0, 1199, reworkSubset: true); }
        finally { RestoreSetup(setup); }
    }

    private static void RestoreSetup(SceneSetup[] setup)
    {
        // A nested check may start in an unnamed temporary scene. Unity cannot
        // restore its empty path; the outer check restores the saved scene.
        if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
            EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    public static void CheckSceneRestore()
    {
        var original = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var temporary = EditorSceneManager.GetSceneManagerSetup();
            RestoreSetup(temporary);
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) throw new InvalidOperationException("Temporary scene restoration failed.");
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var saved = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RestoreSetup(saved);
            active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != "Assets/Scenes/GameScene.unity" || !active.isLoaded) throw new InvalidOperationException("Saved scene restoration failed.");
        }
        finally { RestoreSetup(original); }
        File.WriteAllText("tasks/campaign-rework-scene-restore.txt", "PASS: unnamed temporary scene safely retained; outer saved scene restored.");
    }

    private static void RunCore(int firstIndex = 0, int lastIndex = 299, int maximumTicks = 12000, bool accessFixSubset = false, bool queueProgressionSubset = false, bool reworkSubset = false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConveyorPlacementChecks.Run();
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var report = new List<string> { "# Sand simulation checks", "", "Actual FallingSandSimulator, SandCollector, ConveyorBelt and BucketInventory; one grain per level cell for a bounded CPU check. No boosters, no player progress, no SDK.", "", "Level | Simulation ticks | Sand left | Buckets left", "--- | --- | --- | ---" };
        int[] samples = { 0, 1, 2, 11, 13, 15, 17, 19, 21, 23, 25, 27, 37, 47, 49, 57, 67, 79, 89, 99 };
        if (database.LevelCount >= 300) samples = samples.Concat(new[] { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 124, 149, 174, 199, 224, 249, 274, 299 }).ToArray();
        if (reworkSubset) samples = new[] { 0, 1, 2, 3, 4, 12, 299, 300, 399, 599, 799, 999, 1000, 1037, 1049, 1057, 1079, 1099, 1199 };
        samples = samples.Where(i => i >= firstIndex && i <= lastIndex).ToArray();
        if (queueProgressionSubset)
            samples = samples.Where(i => new[] { 17, 19, 25, 27, 47, 57, 67, 79, 103, 111, 199, 299 }.Contains(i)).ToArray();
        if (accessFixSubset)
            samples = samples.Where(i => i >= 111 || (database.levels[i].campaignTemplateVersion > 0 && database.levels[i].keyGates))
                .OrderBy(i => i == 47 ? 0 : i == 111 ? 1 : i < 111 ? 3 : 2).ToArray();
        foreach (int index in samples)
        {
            // Different mechanic combinations need different legal orders of play.
            // A bounded greedy policy timing out is not a proof that the puzzle is impossible.
            for (int strategy = 0; strategy < 3; strategy++)
            {
            var root = new GameObject("SimulationCheck");
            LevelData level = CampaignRunFactory.Create(database.levels[index], index);
            if (level.campaignTemplateVersion > 0) level.campaignSeed = database.levels[index].campaignSeed;
            Debug.Log($"SIMULATION_LEVEL_START: {index + 1}, seed {level.campaignSeed}, strategy {strategy}");
            LevelSegmentRules rules = level.bucketSettingsOverride;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                Rect("SandImage", Rect("SandPanel", root.transform, 640), 640);
                Rect("ConveyorPanel", root.transform, 680);
                root.AddComponent<SandConveyorMapper>().EnsureReferences();
                var loader = root.AddComponent<LevelLoader>();
                loader.SetCurrentLevel(level);
                var belt = root.AddComponent<ConveyorBelt>();
                ConveyorBelt.ConfigureGeometry(680, 72);
                var collector = root.AddComponent<SandCollector>();
                collector.Initialize(belt);
                var simulator = root.AddComponent<FallingSandSimulator>();
                var grid = new SandGrid(level, 1);
                simulator.Initialize(grid, collector);
                var inventory = new BucketInventory();
                UnityEngine.Random.InitState(level.campaignSeed);
                inventory.BuildFromLevel(level, 1, rules);
                belt.Configure(inventory.TotalBucketCount);
                belt.ConfigureLevel(level);
                belt.OnBucketReturned += inventory.ReturnBucket;
                belt.OnBucketFilled += inventory.HandleBucketFilled;
                var budget = typeof(FallingSandSimulator).GetField("collectBudgetThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                var recalled = new Dictionary<Bucket, int>();
                var dispatched = new Dictionary<Bucket, int>();
                int decisions = 0;
                int lastCount = grid.CountRemaining();
                int stalled = 0;
                int ticks;
                for (ticks = 0; ticks < maximumTicks && (grid.CountRemaining() > 0 || inventory.CountRemaining() > 0 || belt.Entries.Count > 0); ticks++)
                {
                    inventory.TryDispenseAll(level);
                    if (ticks % 8 == 0 && belt.CanAcceptBucket())
                    {
                        var demand = new int[256];
                        for (int x = 0; x < grid.Width; x++)
                            for (int y = grid.Height - 1; y >= Mathf.Max(0, grid.Height - 8); y--)
                                demand[grid.GetCell(x, y)]++;
                        var feederExits = new HashSet<int>(inventory.ConveyorSlots.Where(s => s.RemainingCount > 0)
                            .Select(s => ConveyorDirectionUtility.GetTargetLayoutIndex(s.LayoutIndex, s.Direction)));
                        var candidates = inventory.Buckets.Where(b => inventory.IsBucketAccessible(b, level))
                            .OrderByDescending(b => demand[b.ColorIndex] - (belt.HasBucketWithColor(b.ColorIndex) ? 1000 : 0)
                                + (strategy == 1 && feederExits.Contains(level.loopInventory ? InventoryLoopRoute.Next(b.LayoutIndex) : b.LayoutIndex) ? 600 : 0)
                                + (strategy == 2 ? (decisions - (dispatched.TryGetValue(b, out int turn) ? turn : 0)) * 100 : 0)
                                + (b.KeyId != 0 ? 300 : 0) + (inventory.IsCrateCovered(b) ? 80 : 0)
                                - (recalled.TryGetValue(b, out int until) && ticks < until ? 2000 : 0)).ToArray();
                        foreach (var candidate in candidates)
                            if (inventory.TryTakeBucket(candidate, belt, level)) { dispatched[candidate] = ++decisions; break; }
                    }
                    belt.UpdateBelt(1f / 32f);
                    simulator.Wake();
                    budget.SetValue(simulator, FallingSandSimulator.CollectionPerFrame);
                    simulator.SimulateTick();
                    int remaining = grid.CountRemaining();
                    stalled = remaining == lastCount ? stalled + 1 : 0;
                    lastCount = remaining;
                    if (stalled >= 240 && belt.Entries.Count > 0)
                    {
                        foreach (var entry in belt.Entries.ToArray())
                        {
                            recalled[entry.Bucket] = ticks + 600;
                            belt.TryRecallBucket(entry.Bucket);
                        }
                        stalled = 0;
                    }
                }
                if (grid.CountRemaining() != 0 || inventory.CountRemaining() != 0 || belt.Entries.Count != 0)
                {
                    if (strategy < 2)
                    {
                        Debug.Log($"SIMULATION_STRATEGY_RETRY: level {index + 1}, strategy {strategy}, sand {grid.CountRemaining()}");
                        continue;
                    }
                    var histogram = new int[256];
                    for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Height; y++) histogram[grid.GetCell(x, y)]++;
                    var all = CampaignIterationChecks.AllBuckets(inventory).Concat(belt.Entries.Select(e => e.Bucket)).ToArray();
                    Debug.Log("SIMULATION_DIAGNOSTICS: colors " + string.Join(",", Enumerable.Range(1, level.palette.Length).Select(c => $"{c}:sand{histogram[c]}/demand{all.Sum(b => b.RemainingForColor((byte)c))}"))
                        + " BELT " + string.Join(",", belt.Entries.Select(e => $"{e.Bucket.ColorIndex}:{e.Bucket.CurrentFill}/{e.Bucket.Capacity}:section{e.Bucket.SecondColor}:cost{e.Bucket.SlotCost}:link{e.Bucket.LinkedPartner?.ColorIndex}:x{e.PositionX}:pending{e.PendingRemoval}"))
                        + " FEEDERS " + string.Join(",", inventory.ConveyorSlots.Select(s => $"{s.LayoutIndex}:count{s.RemainingCount}")));
                    throw new InvalidOperationException($"Simulation incomplete on level {index + 1}, seed {level.campaignSeed}: sand={grid.CountRemaining()}, inventory={inventory.CountRemaining()}, belt={belt.Entries.Count}. Inventory: " + string.Join(",", inventory.Buckets.Select(b => $"{b.LayoutIndex}:{b.ColorIndex}/{b.RemainingCapacity}:key{b.KeyId}:gate{inventory.IsGateClosed(b)}:link{b.LinkedPartner?.LayoutIndex}:accessible{inventory.IsBucketAccessible(b, level)}")));
                }
                report.Add($"{index + 1} | {ticks} | 0 | 0");
                File.WriteAllLines("tasks/campaign-simulation.md", report);
                Debug.Log($"SIMULATION_LEVEL_PASS: {index + 1}, {ticks} ticks");
                break;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(rules);
                UnityEngine.Object.DestroyImmediate(level);
                UnityEngine.Random.state = randomState;
            }
            }
        }
        report.Add($"\nPASS: all {samples.Length} sampled random levels completed through real sand simulation without paid boosters at one grain per cell and the production emission budget. This check does not measure device FPS.");
        File.WriteAllLines("tasks/campaign-simulation.md", report);
        Debug.Log("CAMPAIGN_SIMULATION_PASS");
    }

    private static Transform Rect(string name, Transform parent, float width)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        ((RectTransform)obj.transform).sizeDelta = new Vector2(width, width);
        return obj.transform;
    }
}
