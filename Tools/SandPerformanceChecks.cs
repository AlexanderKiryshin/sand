// Run SandPerformanceChecks.Run in the disposable project prepared by prepare_sand_performance.py.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SandPerformanceChecks
{
    const string Key = "Sand.PerformanceChecks";
    public static string Output => Environment.GetEnvironmentVariable("SAND_PERF_OUTPUT") ?? "Temp/SandPerformance";
    static SandPerformanceChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        SessionState.SetBool(Key, true);
        int width = int.Parse(Environment.GetEnvironmentVariable("SAND_PERF_WIDTH") ?? "1920");
        int height = int.Parse(Environment.GetEnvironmentVariable("SAND_PERF_HEIGHT") ?? "1080");
        ShopLayoutChecks.SetResolution(width, height);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && UnityEngine.Object.FindObjectOfType<SandPerformanceRunner>() == null)
            new GameObject("SandPerformanceRunner").AddComponent<SandPerformanceRunner>();
    }
    public static void Finish(bool success, string report)
    {
        File.WriteAllText(Path.Combine(Output, "status.txt"), (success ? "PASS\n" : "FAIL\n") + report);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

public sealed class SandPerformanceRunner : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    readonly List<string> snapshots = new List<string>();
    GameManager game;
    LevelLoader loader;
    SandRenderer renderer;
    ConveyorBelt belt;
    bool withinBudget = true;
    IEnumerator Start()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Time.captureFramerate = 60;
        var steps = Check();
        while (true)
        {
            object step;
            try { if (!steps.MoveNext()) break; step = steps.Current; }
            catch (Exception error) { SandPerformanceChecks.Finish(false, string.Join("\n", report) + "\n" + error); yield break; }
            yield return step;
        }
        File.WriteAllLines(Path.Combine(SandPerformanceChecks.Output, "snapshots.csv"), snapshots);
        string baseline = Environment.GetEnvironmentVariable("SAND_PERF_BASELINE");
        if (!string.IsNullOrEmpty(baseline))
        {
            var expected = File.ReadAllLines(baseline);
            if (!expected.SequenceEqual(snapshots))
            {
                SandPerformanceChecks.Finish(false, string.Join("\n", report) + "\nVisual/physics snapshots differ from baseline.");
                yield break;
            }
            report.Add("Exact texture, particle and simulation parity: " + (snapshots.Count - 1) + " snapshots.");
        }
        SandPerformanceChecks.Finish(withinBudget, string.Join("\n", report));
    }
    IEnumerator Check()
    {
        for (int i = 0; i < 120; i++) { Time.timeScale = 1; yield return null; }
        Require(GameProgressStore.IsPreview, "Expected isolated progress.");
        GameProgressStore.SetBool("sand_tutorial_v2_complete", true);
        var tutorial = FindObjectOfType<TutorialController>();
        tutorial.HandleLevelCompleted(); tutorial.HandleHintDismissed(); tutorial.enabled = false;
        FindObjectOfType<GameUI>().SetHintVisible(false);
        game = FindObjectOfType<GameManager>(); loader = FindObjectOfType<LevelLoader>();
        renderer = FindObjectOfType<SandRenderer>(); belt = FindObjectOfType<ConveyorBelt>();
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 299);
        GameProgressStore.SetInt("sand_progress_highest_completed", 299);
        ProgressSaveService.Load();
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            GameProgressStore.SetBool(BoosterCatalog.GetTutorialSeenKey(type), true);
        string timings = Path.Combine(SandPerformanceChecks.Output, "timings.csv");
        File.WriteAllText(timings, "phase,method,frames,calls,max_calls_per_frame,mean_ms,p95_ms,max_ms\n");
        snapshots.Add("phase,frame,texture_hash,grid_hash,particles_hash,remaining,active_pour");
        string requestedLevels = Environment.GetEnvironmentVariable("SAND_PERF_LEVELS");
        int[] levels = string.IsNullOrEmpty(requestedLevels) ? new[] { 0, 14, 149 } : requestedLevels.Split(',').Select(int.Parse).ToArray();
        foreach (int levelIndex in levels)
        {
            Time.timeScale = 1;
            UnityEngine.Random.InitState(78123 + levelIndex);
            game.LoadLevel(levelIndex);
            Require(game.CurrentLevelIndex == levelIndex, "Requested level was locked or substituted.");
            SandPerformanceProbe.Begin();
            string phase = "intro-" + (levelIndex + 1);
            for (int frame = 0; frame < 150; frame++)
            {
                Time.timeScale = 1;
                yield return new WaitForEndOfFrame();
                if (frame % 15 == 0 || frame == 149) Snapshot(phase, frame);
                if (frame == 60) ScreenCapture.CaptureScreenshot(Path.Combine(SandPerformanceChecks.Output, phase + ".png"));
                yield return null;
            }
            double introP95 = SandPerformanceProbe.End(phase, timings);
            withinBudget &= introP95 < 16.667;
            Require(!FindObjectOfType<LevelIntroSandAnimator>().IsPlaying, "Intro did not finish.");
            report.Add(phase + " tracked CPU p95 ms=" + introP95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            int initialSand = loader.CurrentGrid.CountRemaining();
            phase = "pour-" + (levelIndex + 1);
            UnityEngine.Random.InitState(95123 + levelIndex);
            SandPerformanceProbe.Begin();
            for (int frame = 0; frame < 480; frame++)
            {
                Time.timeScale = 1;
                if (frame % 16 == 0 && belt.CanAcceptBucket()) PlaceBucket();
                yield return new WaitForEndOfFrame();
                if (frame % 60 == 0 || frame == 479) Snapshot(phase, frame);
                if (frame == 180) ScreenCapture.CaptureScreenshot(Path.Combine(SandPerformanceChecks.Output, phase + ".png"));
                yield return null;
            }
            double pourP95 = SandPerformanceProbe.End(phase, timings);
            withinBudget &= pourP95 < 16.667;
            CheckCollectionLookup();
            int collected = initialSand - loader.CurrentGrid.CountRemaining();
            Require(collected > 0, "No real sand collection during " + phase);
            report.Add(phase + " tracked CPU p95 ms=" + pourP95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ", collected=" + collected);
            File.WriteAllLines(Path.Combine(SandPerformanceChecks.Output, "progress.txt"), report);
        }
    }
    void PlaceBucket()
    {
        var grid = loader.CurrentGrid;
        var demand = new int[256];
        for (int x = 0; x < grid.Width; x++)
            for (int y = grid.Height - 1; y >= Math.Max(0, grid.Height - 8); y--) demand[grid.GetCell(x, y)]++;
        var candidates = game.Inventory.Buckets.Where(bucket => game.Inventory.IsBucketAccessible(bucket, loader.CurrentLevel))
            .OrderByDescending(bucket => demand[bucket.ColorIndex] - (belt.HasBucketWithColor(bucket.ColorIndex) ? 1000 : 0)).ToArray();
        foreach (var candidate in candidates) if (game.TryPlaceBucket(candidate)) break;
    }
    void CheckCollectionLookup()
    {
        var method = typeof(ConveyorBelt).GetMethod("FindBucketForColumnBaseline");
        var baseline = (Func<int, int, byte, ConveyorBelt.BeltEntry>)Delegate.CreateDelegate(
            typeof(Func<int, int, byte, ConveyorBelt.BeltEntry>), belt, method);
        int comparisons = 0, width = loader.CurrentGrid.LevelWidth;
        foreach (var moving in belt.Entries)
        {
            float previousX = moving.PositionX;
            try
            {
                for (int position = 0; position <= 16; position++)
                {
                    moving.PositionX = Mathf.Lerp(ConveyorBelt.ExitX - 10, ConveyorBelt.EntryX + 10, position / 16f);
                    for (int x = 0; x < width; x++)
                        for (byte color = 1; color <= loader.CurrentLevel.palette.Length; color++)
                        {
                            Require(ReferenceEquals(baseline(x, width, color), belt.FindBucketForColumn(x, width, color)),
                                "Collection target changed at x=" + x + ", color=" + color + ", position=" + moving.PositionX);
                            comparisons++;
                        }
                }
            }
            finally { moving.PositionX = previousX; }
        }
        Require(comparisons > 0, "No collection geometry comparisons.");
        report.Add("Collection target parity: " + comparisons + " positions/colors matched.");
    }
    void Snapshot(string phase, int frame)
    {
        var texture = (Texture2D)renderer.DisplayRect.GetComponent<RawImage>().texture;
        ulong textureHash = 14695981039346656037UL, gridHash = textureHash, particleHash = textureHash;
        foreach (var color in texture.GetPixels32())
        { Hash(ref textureHash, color.r); Hash(ref textureHash, color.g); Hash(ref textureHash, color.b); Hash(ref textureHash, color.a); }
        var grid = loader.CurrentGrid;
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++) Hash(ref gridHash, grid.GetCell(x, y));
        foreach (string layerName in new[] { "SandIntroPourLayer", "SandCollectPourLayer" })
        {
            var layer = GameObject.Find(layerName);
            if (layer == null) continue;
            foreach (var img in layer.GetComponentsInChildren<Image>())
            {
                Vector3 position = img.rectTransform.position;
                HashInt(ref particleHash, Mathf.RoundToInt(position.x * 1000)); HashInt(ref particleHash, Mathf.RoundToInt(position.y * 1000));
                Color32 color = img.color; Hash(ref particleHash, color.r); Hash(ref particleHash, color.g); Hash(ref particleHash, color.b);
                HashInt(ref particleHash, Mathf.RoundToInt(img.rectTransform.rect.width * 1000));
            }
        }
        snapshots.Add(phase + "," + frame + "," + textureHash + "," + gridHash + "," + particleHash + "," + grid.CountRemaining() + "," + FindObjectOfType<SandPourAnimator>().ActiveGrainCount);
    }
    static void Hash(ref ulong value, byte part) { unchecked { value = (value ^ part) * 1099511628211UL; } }
    static void HashInt(ref ulong value, int part) { for (int i = 0; i < 4; i++) Hash(ref value, (byte)(part >> (8 * i))); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
