// Run from Assets/Editor in the disposable validation project.
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
using TMPro;

[InitializeOnLoad]
public static class ProgressionVisualChecks
{
    const string Key = "Sand.ProgressionVisualChecks";
    static ProgressionVisualChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && UnityEngine.Object.FindObjectOfType<ProgressionVisualRunner>() == null)
            new GameObject("ProgressionVisualRunner").AddComponent<ProgressionVisualRunner>();
    }
    public static void Finish(bool success, string message)
    {
        string folder = Environment.GetEnvironmentVariable("SAND_PROGRESSION_OUTPUT");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ui-status.txt"), (success ? "PASS\n" : "FAIL\n") + message);
        SessionState.SetBool(Key, false); SessionState.SetBool("Sand.BuildSizeChecks", false);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
public sealed class ProgressionVisualRunner : MonoBehaviour
{
    readonly List<string> errors = new List<string>();
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string trace, LogType type)
    { if ((type == LogType.Error || type == LogType.Exception) && !trace.Contains("UnityEditor.Search.")) errors.Add(message); }
    IEnumerator Start()
    {
        var steps = Check();
        while (true)
        {
            object step;
            try { if (!steps.MoveNext()) break; step = steps.Current; }
            catch (Exception error) { ProgressionVisualChecks.Finish(false, error.ToString()); yield break; }
            yield return step;
        }
        ProgressionVisualChecks.Finish(errors.Count == 0, "Level 180 tutorial plus 6/8/all-stock challenges and real moves verified; full-stock screenshots captured in RU/EN; winter tutorials remain disabled.\n" + string.Join("\n", errors));
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    IEnumerator Check()
    {
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Time.captureFramerate = 30;
        for (int i = 0; i < 110; i++) yield return null;
        Require(GameProgressStore.IsPreview, "Expected an isolated profile.");
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 999); ProgressSaveService.Load();
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(type);
        var manager = FindObjectOfType<GameManager>();
        var loader = FindObjectOfType<LevelLoader>();
        var canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        string output = Environment.GetEnvironmentVariable("SAND_PROGRESSION_OUTPUT");
        var capture = typeof(CollectionSelectionCheckRunner).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
        ThemeCatalog.Unlock(24, ThemeCatalog.Part.Background); ThemeCatalog.Select(24, ThemeCatalog.Part.Background);
        ThemeCatalog.Unlock(21, ThemeCatalog.Part.Conveyor); ThemeCatalog.Select(21, ThemeCatalog.Part.Conveyor);
        foreach (string language in new[] { "RU", "EN" })
        {
            Time.timeScale = 1;
            LocalizationManager.Instance.SetLanguage(language == "RU" ? MirraGames.SDK.Common.LanguageType.Russian : MirraGames.SDK.Common.LanguageType.English);
            manager.LoadLevel(179);
            for (int i = 0; i < 110; i++) { Time.timeScale = 1; yield return null; }
            Require(loader.CurrentLevel.bucketLayout.All(c => c != 254 && c != 255), "Obstacle visible on level 180.");
            var group = manager.Inventory.Buckets.Where(b => b.ColorSwapGroup != null).ToArray();
            Require(group.Length == 4, "Four marked buckets not visible.");
            var hint = canvas.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hint");
            Require(hint.gameObject.activeInHierarchy, "The 180 tutorial did not open.");
            Require(hint.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == CampaignText.Get("lesson_swap_group")), "Wrong tutorial text.");
            if (language == "EN") Require(FindObjectsOfType<TextMeshProUGUI>().All(t => !t.text.Any(c => c >= '\u0400' && c <= '\u04ff')), "Russian text remains in EN.");
            Time.timeScale = 0;
            capture.Invoke(null, new object[] { canvas, Path.Combine(output, "level-180-tutorial-" + language + ".png"), 1440, 2560 });
            hint.Find("HintClose").GetComponent<Button>().onClick.Invoke();
            Require(!hint.gameObject.activeSelf, "Tutorial close did not work.");
            Time.timeScale = 1;
            var before = group.Select(b => b.ColorIndex + ":" + b.Capacity).OrderBy(s => s).ToArray();
            var colors = group.Select(b => b.ColorIndex).ToArray();
            var next = manager.Inventory.Buckets.First(b => b.ColorSwapGroup == null && b.LinkedPartner == null && manager.Inventory.IsBucketAccessible(b, loader.CurrentLevel));
            Require(manager.TryPlaceBucket(next), "Ordinary move failed on 180.");
            Require(before.SequenceEqual(group.Select(b => b.ColorIndex + ":" + b.Capacity).OrderBy(s => s)), "Visible group shuffle changed sand budget.");
            Require(!colors.SequenceEqual(group.Select(b => b.ColorIndex)), "Visible marked colors did not change.");
            for (int i = 0; i < 25; i++) yield return null;
            Time.timeScale = 0;
            capture.Invoke(null, new object[] { canvas, Path.Combine(output, "level-180-after-move-" + language + ".png"), 1440, 2560 });
        }
        foreach (int number in new[] { 186, 190, 200 })
        foreach (string language in number == 200 ? new[] { "RU", "EN" } : new[] { "RU" })
        {
            Time.timeScale = 1;
            LocalizationManager.Instance.SetLanguage(language == "RU" ? MirraGames.SDK.Common.LanguageType.Russian : MirraGames.SDK.Common.LanguageType.English);
            manager.LoadLevel(number - 1);
            for (int i = 0; i < 110; i++) { Time.timeScale = 1; yield return null; }
            var all = manager.Inventory.Buckets.Concat(manager.Inventory.ReservedBuckets).ToArray();
            var group = all.Where(b => b.ColorSwapGroup != null).ToArray();
            Require(group.Length == CampaignProgression.SwapBucketCount(number, all.Length), "Wrong visible challenge count at " + number);
            if (number == 200) Require(group.Length == all.Length, "Unmarked cup in full-stock challenge.");
            var hint = canvas.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hint");
            Require(!hint.gameObject.activeInHierarchy, "Unexpected tutorial at " + number);
            Time.timeScale = 0;
            capture.Invoke(null, new object[] { canvas, Path.Combine(output, "level-" + number + "-" + language + ".png"), 1440, 2560 });
            var before = group.Select(b => b.ColorIndex + ":" + b.Capacity).OrderBy(v => v).ToArray();
            var colors = group.Select(b => b.ColorIndex).ToArray();
            var next = manager.Inventory.Buckets.First(b => manager.Inventory.IsBucketAccessible(b, loader.CurrentLevel));
            Time.timeScale = 1;
            Require(manager.TryPlaceBucket(next), "Challenge move failed at " + number);
            Require(before.SequenceEqual(group.Select(b => b.ColorIndex + ":" + b.Capacity).OrderBy(v => v)), "Challenge move lost sand at " + number);
            Require(!colors.SequenceEqual(group.Select(b => b.ColorIndex)), "Challenge colors stayed unchanged at " + number);
            for (int i = 0; i < 25; i++) yield return null;
            if (number == 200)
            {
                Time.timeScale = 0;
                capture.Invoke(null, new object[] { canvas, Path.Combine(output, "level-200-after-move-" + language + ".png"), 1440, 2560 });
            }
        }
        Time.timeScale = 1; manager.LoadLevel(1179);
        for (int i = 0; i < 110; i++) { Time.timeScale = 1; yield return null; }
        var winterHint = canvas.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hint");
        Require(!winterHint.gameObject.activeInHierarchy, "Winter tutorial unexpectedly enabled.");
        for (int number = 3; number <= 14; number++)
        {
            if (number == 12) continue;
            manager.LoadLevel(number - 1); yield return null;
            Require(!winterHint.gameObject.activeInHierarchy, "Removed obstacle lesson still opens at " + number);
        }
    }
}
