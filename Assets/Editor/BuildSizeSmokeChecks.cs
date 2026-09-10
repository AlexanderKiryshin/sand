using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run in a disposable project with graphics enabled, without -quit.
[InitializeOnLoad]
public static class BuildSizeSmokeChecks
{
    private const string Key = "Sand.BuildSizeChecks";
    static BuildSizeSmokeChecks() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetBool("Sand.CampaignPreview", false);
        SessionState.SetBool("Sand.AudioPolishChecks", false);
        SessionState.SetBool("Sand.MechanicsPreview", false);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying
            && UnityEngine.Object.FindObjectOfType<BuildSizeSmokeRunner>() == null)
            new GameObject("BuildSizeSmokeRunner").AddComponent<BuildSizeSmokeRunner>();
    }

    public static void Finish(bool passed, string report)
    {
        Directory.CreateDirectory("tasks");
        File.WriteAllText("tasks/build-size-smoke.md", (passed ? "PASS\n" : "FAIL\n") + report);
        SessionState.SetBool(Key, false);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}

public sealed class BuildSizeSmokeRunner : MonoBehaviour
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> editorIssues = new List<string>();
    private string report = "";
    private void OnEnable() { Application.logMessageReceived += OnLog; }
    private void OnDisable() { Application.logMessageReceived -= OnLog; }
    private void OnLog(string message, string trace, LogType type)
    {
        // The Search indexer can fail in disposable batch projects. Keep its
        // editor-only diagnostic visible without treating it as a player error.
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException:")
            && trace.Contains("UnityEditor.Search.SearchDatabase")
            && trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        {
            editorIssues.Add(message + "\n" + trace);
            return;
        }
        if (type == LogType.Exception || type == LogType.Error) errors.Add(message + "\n" + trace);
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        IEnumerator checks = Check();
        while (true)
        {
            object step;
            try
            {
                if (!checks.MoveNext()) break;
                step = checks.Current;
            }
            catch (Exception error)
            {
                BuildSizeSmokeChecks.Finish(false, report + error + "\n" + string.Join("\n", errors) + EditorDiagnostics());
                yield break;
            }
            yield return step;
        }
        BuildSizeSmokeChecks.Finish(errors.Count == 0, report + string.Join("\n", errors) + EditorDiagnostics());
    }

    private string EditorDiagnostics() => editorIssues.Count == 0 ? ""
        : "\nEditor Search diagnostics (outside player runtime):\n" + string.Join("\n", editorIssues);

    private IEnumerator Check()
    {
        yield return new WaitForSecondsRealtime(8);
        Time.timeScale = 1;
        var manager = FindObjectOfType<GameManager>();
        Require(manager != null && manager.LevelCount == 300, "Existing scene starts with 300 campaign levels.");
        Require(GameProgressStore.IsPreview, "Checks use an isolated in-memory save profile.");
        GameProgressStore.SetBool("sand_tutorial_v2_complete", true);
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 299);
        ProgressSaveService.Load();
        manager.LoadLevel(11);
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(type);
        yield return new WaitForSecondsRealtime(5);

        foreach (GameUiSymbol symbol in new[] { GameUiSymbol.Magnet, GameUiSymbol.Fill, GameUiSymbol.Shuffle, GameUiSymbol.Lock })
        {
            Sprite sprite = GameUiSymbols.Get(symbol);
            Require(sprite != null && sprite.texture.width == 512 && sprite.rect.width == 256,
                "Atlas sprite loads at 256x256: " + symbol);
        }
        Require(BusesUiArt.Magnet == GameUiSymbols.Get(GameUiSymbol.Magnet)
            && BusesUiArt.Transparent == GameUiSymbols.Get(GameUiSymbol.Fill)
            && BusesUiArt.Shuffle == GameUiSymbols.Get(GameUiSymbol.Shuffle), "Legacy booster accessors share the same atlas sprites.");
        Capture("game");
        Press("ShopBtn");
        yield return null;
        Require(FindObjectOfType<RewardedShop>() != null, "Shop opens after removing old booster images.");
        Capture("shop");
        Press("CloseShop");
        yield return null;
        Require(FindObjectOfType<RewardedShop>() == null, "Shop closes.");
        Press("Booster_Magnet");
        yield return null;
        Require(BoosterService.instance.ActiveTargetMode == BoosterType.Magnet, "Magnet button activates its target mode.");
        BoosterService.instance.CancelTargetMode();

        manager.OpenCollection();
        yield return null;
        var gallery = FindObjectOfType<CampaignGallery>();
        Require(gallery != null && gallery.GetComponentsInChildren<RawImage>().Count(i => i.texture != null) == 20,
            "Gallery creates 20 previews from compressed level data.");
        var previousTextures = gallery.GetComponentsInChildren<RawImage>().Select(i => i.texture).ToArray();
        gallery.GetComponentsInChildren<Button>().Single(b => b.name == "Next").onClick.Invoke();
        yield return null;
        Require(previousTextures.All(t => t == null), "Changing gallery pages releases old textures.");
        Require(gallery.GetComponentsInChildren<RawImage>().Count(i => i.texture != null) == 20, "Next gallery page has 20 generated previews.");
        Capture("gallery");
        gallery.Close();
        yield return null;

        Press("AudioSettingsButton");
        yield return null;
        var settings = FindObjectOfType<AudioSettingsMenu>();
        Require(settings.IsOpen, "Settings modal opens.");
        settings.Close();
        GameProgressStore.SetInt("sand_size_check", 123);
        Require(GameProgressStore.GetInt("sand_size_check") == 123, "Save API round trip succeeds in the preview profile.");
        manager.RestartLevel();
        yield return new WaitForSecondsRealtime(5);
        Require(manager.CurrentLevelIndex == 11 && manager.Inventory != null, "Level restart retains the selected level and inventory.");
        Require(errors.Count == 0, "No runtime errors during editor smoke checks.");
    }

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report += "- " + message + "\n";
        Debug.Log("BUILD_SIZE_SMOKE_PASS: " + message);
    }

    private static void Press(string name)
    {
        Button button = FindObjectsOfType<Button>().Single(b => b.name == name);
        if (!button.interactable) throw new InvalidOperationException("Button disabled: " + name);
        button.onClick.Invoke();
    }

    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Run visual checks with graphics enabled.");
        Canvas canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        Camera camera = Camera.main;
        RenderMode mode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousPlaneDistance = canvas.planeDistance;
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        var target = new RenderTexture(1080, 1920, 24);
        var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("tasks/previews/build-size");
            File.WriteAllBytes("tasks/previews/build-size/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            canvas.renderMode = mode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlaneDistance;
            Destroy(pixels); target.Release(); Destroy(target);
        }
    }
}
