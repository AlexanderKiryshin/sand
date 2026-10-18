// Copy into Assets/Editor of the disposable capture project; never included in players.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class MarketingCapture
{
    const string Key = "Sand.MarketingCapture";
    static MarketingCapture() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying
            && UnityEngine.Object.FindObjectOfType<MarketingCaptureRunner>() == null)
            new GameObject("MarketingCaptureRunner").AddComponent<MarketingCaptureRunner>();
    }
    public static void Finish(bool success, string message)
    {
        string output = Environment.GetEnvironmentVariable("SAND_CAPTURE_OUTPUT");
        File.WriteAllText(Path.Combine(output, "capture-status.txt"), (success ? "PASS\n" : "FAIL\n") + message);
        SessionState.SetBool(Key, false);
        SessionState.SetBool("Sand.BuildSizeChecks", false);
        Time.captureFramerate = 0;
        EditorApplication.Exit(success ? 0 : 1);
    }
}

public sealed class MarketingCaptureRunner : MonoBehaviour
{
    const int Width = 1080, Height = 1920, Fps = 30, FramesPerShot = 330;
    readonly List<string> errors = new List<string>();
    readonly List<string> events = new List<string>();
    string output;
    Process encoder;
    Process wideEncoder;
    RenderTexture target;
    RenderTexture wideTarget;
    Texture2D pixels;
    Texture2D widePixels;
    Camera camera;
    Canvas canvas;
    int globalFrame;
    int shot;
    int shotFrame;
    GameManager manager;
    LevelLoader loader;
    ConveyorBelt belt;

    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }
    void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            if (trace.Contains("UnityEditor.Search.")) return;
            errors.Add(message + "\n" + trace);
        }
    }
    IEnumerator Start()
    {
        output = Environment.GetEnvironmentVariable("SAND_CAPTURE_OUTPUT");
        Directory.CreateDirectory(output);
        IEnumerator sequence = Capture();
        while (true)
        {
            object step;
            try
            {
                if (!sequence.MoveNext()) break;
                step = sequence.Current;
            }
            catch (Exception error)
            {
                try { encoder?.StandardInput.Close(); wideEncoder?.StandardInput.Close(); } catch { }
                MarketingCapture.Finish(false, error + "\n" + string.Join("\n", errors));
                yield break;
            }
            yield return step;
        }
        MarketingCapture.Finish(errors.Count == 0, "Captured real gameplay in native portrait and landscape at 30 fps. Disposable save profile.\n" + string.Join("\n", errors));
    }
    IEnumerator Capture()
    {
        if (Environment.GetEnvironmentVariable("SAND_CAPTURE_SCREENSHOTS") == "1")
        {
            IEnumerator screenshots = CaptureScreenshots();
            while (screenshots.MoveNext()) yield return screenshots.Current;
            yield break;
        }
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        Time.captureFramerate = Fps;
        for (int i = 0; i < 100; i++) yield return null;
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Capture needs a graphics device.");
        if (!GameProgressStore.IsPreview) throw new InvalidOperationException("Isolated save profile is required.");
        manager = FindObjectOfType<GameManager>();
        loader = FindObjectOfType<LevelLoader>();
        belt = FindObjectOfType<ConveyorBelt>();
        string languageCode = Environment.GetEnvironmentVariable("SAND_CAPTURE_LANGUAGE") == "en" ? "en" : "ru";
        var language = languageCode == "en" ? MirraGames.SDK.Common.LanguageType.English : MirraGames.SDK.Common.LanguageType.Russian;
        LocalizationManager.Instance.SetLanguage(language);
        GameProgressStore.SetBool("sand_tutorial_v2_complete", true);
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 999);
        ProgressSaveService.Load();
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(type);
        camera = Camera.main;
        target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        target.Create();
        pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        wideTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        wideTarget.Create();
        widePixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        belt.OnBucketFilled += b => events.Add((globalFrame / (float)Fps).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",filled," + shot);
        var intro = FindObjectOfType<LevelIntroSandAnimator>();
        int[] levels = { 0, 6, 37, 1000 };
        int[] backgrounds = { 44, 38, 40, 48 };
        int frameCount = FramesPerShot;
        if (int.TryParse(Environment.GetEnvironmentVariable("SAND_CAPTURE_LEVEL"), out int singleLevel))
        {
            levels = new[] { singleLevel };
            backgrounds = new[] { 27 };
            frameCount = 900;
        }
        File.WriteAllText(Path.Combine(output, "capture-config.json"),
            "{\"fps\":30,\"language\":\"" + languageCode + "\",\"frames_per_shot\":" + frameCount + ",\"levels\":[" + string.Join(",", levels.Select(i => i + 1))
                + "],\"backgrounds\":[" + string.Join(",", backgrounds) + "]}");
        for (shot = 0; shot < levels.Length; shot++)
        {
            Time.timeScale = 1;
            ThemeCatalog.Unlock(backgrounds[shot], ThemeCatalog.Part.Background);
            ThemeCatalog.Select(backgrounds[shot], ThemeCatalog.Part.Background);
            manager.LoadLevel(levels[shot]);
            // The game is unchanged; hide the editor-only skip button in the recorded UI.
            SandEconomyHud.instance.SetSkipLevelButtonVisible(false);
            for (int i = 0; i < 95; i++) { Time.timeScale = 1; yield return null; }
            if (intro != null && intro.IsPlaying) throw new InvalidOperationException("Intro not settled.");
            // Finish only tutorial presentation, then close the normal in-game hint.
            // This does not complete the level or alter sand, buckets, or movement.
            FindObjectOfType<TutorialController>().HandleLevelCompleted();
            FindObjectOfType<GameUI>().SetHintVisible(false);
            FindObjectOfType<MobilePortraitLayout>().ApplyLayout();
            Canvas.ForceUpdateCanvases();
            if (LocalizationManager.Instance.GetCurrentLanguage() != language)
                throw new InvalidOperationException("Capture language changed unexpectedly.");
            encoder = OpenEncoder(shot, Width, Height, "source-");
            wideEncoder = OpenEncoder(shot, 1920, 1080, "wide-source-");
            for (shotFrame = 0; shotFrame < frameCount; shotFrame++, globalFrame++)
            {
                Time.timeScale = 1;
                if (shotFrame >= 12 && shotFrame % 22 == 12 && manager.State == GameState.Playing) PlayMove();
                // Demonstrate the real color filter briefly, returning to the full painting.
                if (shot == 1 && shotFrame == 45) FindObjectOfType<ColorFocusPanel>().Select(FindObjectOfType<ColorFocusPanel>().RedColor);
                if (shot == 1 && shotFrame == 85) FindObjectOfType<ColorFocusPanel>().Select(0);
                int grainsBefore = loader.CurrentGrid.CountRemaining();
                yield return null;
                int grainsCollected = grainsBefore - loader.CurrentGrid.CountRemaining();
                if (grainsCollected > 0) events.Add((globalFrame / (float)Fps).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",flow," + shot);
                // Stop before the first rendered frame of the actual victory window.
                Transform victory = canvas.transform.Find("EconomyLevelCompletePanel");
                if (victory != null && victory.gameObject.activeInHierarchy) break;
                RenderFrame(target, pixels, encoder, Width, Height);
                RenderFrame(wideTarget, widePixels, wideEncoder, 1920, 1080);
                if (shotFrame % 60 == 0)
                {
                    File.WriteAllText(Path.Combine(output, "capture-progress.txt"), "shot=" + shot + " frame=" + shotFrame + " grains=" + loader.CurrentGrid.CountRemaining() + " filled=" + belt.FilledBuckets);
                    File.WriteAllBytes(Path.Combine(output, "shot-" + shot + "-" + shotFrame + ".png"), pixels.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(output, "wide-shot-" + shot + "-" + shotFrame + ".png"), widePixels.EncodeToPNG());
                }
            }
            encoder.StandardInput.Close();
            wideEncoder.StandardInput.Close();
            encoder.WaitForExit();
            wideEncoder.WaitForExit();
            if (encoder.ExitCode != 0) throw new InvalidOperationException("FFmpeg failed: " + encoder.StandardError.ReadToEnd());
            if (wideEncoder.ExitCode != 0) throw new InvalidOperationException("Wide FFmpeg failed: " + wideEncoder.StandardError.ReadToEnd());
            encoder.Dispose(); encoder = null;
            wideEncoder.Dispose(); wideEncoder = null;
            File.WriteAllLines(Path.Combine(output, "events.csv"), events);
            File.WriteAllText(Path.Combine(output, "capture-timing.json"),
                "{\"rendered_frames\":" + shotFrame + ",\"fps\":30,\"victory_window_included\":false}");
            Debug.Log("MARKETING_SHOT_COMPLETE: " + shot);
        }
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release(); Destroy(target); Destroy(pixels);
        wideTarget.Release(); Destroy(wideTarget); Destroy(widePixels);
    }
    void RenderFrame(RenderTexture texture, Texture2D readback, Process sink, int width, int height)
    {
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance, previousScale = canvas.scaleFactor;
        try
        {
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.scaleFactor = height / MobilePortraitLayout.ReferenceHeight;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            readback.Apply(false);
            if (sink != null)
            {
                byte[] data = readback.GetRawTextureData();
                sink.StandardInput.BaseStream.Write(data, 0, data.Length);
            }
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = null;
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            canvas.scaleFactor = previousScale;
            Canvas.ForceUpdateCanvases();
        }
    }
    [Serializable]
    sealed class ScreenshotRecord
    {
        public string language, scene, campaign, background, conveyor, casings, portrait, landscape;
        public int level, databaseIndex, backgroundIndex, conveyorIndex, casingsIndex, remainingGrains, occupiedSlots;
        public bool tutorialHintVisible;
        public string[] visibleLabels;
    }
    [Serializable]
    sealed class ScreenshotReport
    {
        public int portraitWidth = 1440, portraitHeight = 2560, landscapeWidth = 2560, landscapeHeight = 1440;
        public ScreenshotRecord[] screenshots;
    }
    IEnumerator CaptureScreenshots()
    {
        const int photoWidth = 1440, photoHeight = 2560;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        Time.captureFramerate = Fps;
        for (int i = 0; i < 100; i++) yield return null;
        if (!GameProgressStore.IsPreview) throw new InvalidOperationException("Screenshots require an isolated save profile.");
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Screenshots require a graphics device.");
        manager = FindObjectOfType<GameManager>();
        loader = FindObjectOfType<LevelLoader>();
        belt = FindObjectOfType<ConveyorBelt>();
        var simulator = FindObjectOfType<FallingSandSimulator>();
        var inventory = FindObjectOfType<BucketInventoryUI>();
        var intro = FindObjectOfType<LevelIntroSandAnimator>();
        camera = Camera.main;
        canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        Transform screenshotHint = canvas.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hint");
        target = new RenderTexture(photoWidth, photoHeight, 24, RenderTextureFormat.ARGB32);
        wideTarget = new RenderTexture(photoHeight, photoWidth, 24, RenderTextureFormat.ARGB32);
        target.Create(); wideTarget.Create();
        pixels = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
        widePixels = new Texture2D(photoHeight, photoWidth, TextureFormat.RGB24, false);
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 999);
        ProgressSaveService.Load();
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(type);
        int[] levels = { 0, 4, 6, 8, 37, 1000 };
        int[] backgrounds = { 24, 40, 28, 27, 29, 30 };
        int[] conveyors = { 21, 10, 15, 16, 9, 13 };
        int[] casings = { 12, 10, 15, 16, 9, 13 };
        string[] names = { "level-001-forest", "level-005-blue-calm", "level-007-tropical", "level-009-lake", "level-038-autumn", "winter-001-snow-forest" };
        var records = new List<ScreenshotRecord>();
        Directory.CreateDirectory(Path.Combine(output, "RU"));
        Directory.CreateDirectory(Path.Combine(output, "EN"));
        for (shot = 0; shot < levels.Length; shot++)
        {
            Time.timeScale = 1;
            simulator.SetSimulationPaused(false);
            belt.SetMovementPaused(false);
            LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
            ThemeCatalog.Unlock(backgrounds[shot], ThemeCatalog.Part.Background);
            if (!ThemeCatalog.Select(backgrounds[shot], ThemeCatalog.Part.Background)) throw new InvalidOperationException("Background selection failed.");
            ThemeCatalog.Unlock(conveyors[shot], ThemeCatalog.Part.Conveyor);
            ThemeCatalog.Unlock(casings[shot], ThemeCatalog.Part.Casings);
            if (!ThemeCatalog.Select(conveyors[shot], ThemeCatalog.Part.Conveyor)
                || !ThemeCatalog.Select(casings[shot], ThemeCatalog.Part.Casings))
                throw new InvalidOperationException("Custom conveyor/casing selection failed.");
            manager.LoadLevel(levels[shot]);
            if (manager.CurrentLevelIndex != levels[shot]) throw new InvalidOperationException("Wrong screenshot level.");
            SandEconomyHud.instance.SetSkipLevelButtonVisible(false);
            for (int frame = 0; frame < 95; frame++) { Time.timeScale = 1; yield return null; }
            if (intro != null && intro.IsPlaying) throw new InvalidOperationException("Screenshot intro is not settled.");
            FindObjectOfType<TutorialController>().HandleLevelCompleted();
            FindObjectOfType<TutorialController>().HandleHintDismissed();
            FindObjectOfType<GameUI>().SetHintVisible(false);
            FindObjectOfType<MobilePortraitLayout>().ApplyLayout();
            PlayMove();
            for (int frame = 0; frame < 12; frame++) yield return null;
            PlayMove();
            for (int frame = 0; frame < 18; frame++) yield return null;
            for (int frame = 0; inventory.IsMoving && frame < 60; frame++) yield return null;
            simulator.SetSimulationPaused(true);
            belt.SetMovementPaused(true);
            Time.timeScale = 0;
            int grains = loader.CurrentGrid.CountRemaining();
            foreach (string language in new[] { "RU", "EN" })
            {
                var locale = language == "RU" ? MirraGames.SDK.Common.LanguageType.Russian : MirraGames.SDK.Common.LanguageType.English;
                LocalizationManager.Instance.SetLanguage(locale);
                yield return null;
                FindObjectOfType<TutorialController>().HandleHintDismissed();
                FindObjectOfType<GameUI>().SetHintVisible(false);
                Canvas.ForceUpdateCanvases();
                if (screenshotHint.gameObject.activeInHierarchy) throw new InvalidOperationException("Tutorial hint is visible in a screenshot.");
                if (LocalizationManager.Instance.GetCurrentLanguage() != locale || loader.CurrentGrid.CountRemaining() != grains)
                    throw new InvalidOperationException("Localization changed the captured gameplay state.");
                if (ThemeCatalog.SelectedBackground != backgrounds[shot] || ThemeCatalog.Selected != conveyors[shot]
                    || ThemeCatalog.SelectedCasings != casings[shot])
                    throw new InvalidOperationException("Screenshot cosmetic selection changed unexpectedly.");
                string[] labels = FindObjectsOfType<TMPro.TextMeshProUGUI>()
                    .Where(t => t.isActiveAndEnabled && t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                    .Select(t => t.text).ToArray();
                if (language == "EN" && labels.Any(t => t.Any(c => c >= '\u0400' && c <= '\u04ff')))
                    throw new InvalidOperationException("Russian text remains in an English screenshot: " + string.Join(" / ", labels));
                Transform victory = canvas.transform.Find("EconomyLevelCompletePanel");
                if (victory != null && victory.gameObject.activeInHierarchy) throw new InvalidOperationException("Victory UI is visible.");
                string portrait = language + "/" + names[shot] + "-9x16.png";
                string landscape = language + "/" + names[shot] + "-16x9.png";
                RenderFrame(target, pixels, null, photoWidth, photoHeight);
                File.WriteAllBytes(Path.Combine(output, portrait), pixels.EncodeToPNG());
                RenderFrame(wideTarget, widePixels, null, photoHeight, photoWidth);
                File.WriteAllBytes(Path.Combine(output, landscape), widePixels.EncodeToPNG());
                records.Add(new ScreenshotRecord {
                    language = language, scene = names[shot], campaign = manager.IsSeasonal(levels[shot]) ? "winter" : "regular",
                    level = manager.CampaignLevelIndex + 1, databaseIndex = levels[shot], backgroundIndex = backgrounds[shot],
                    conveyorIndex = conveyors[shot], casingsIndex = casings[shot],
                    conveyor = ThemeCatalog.Names[conveyors[shot]], casings = ThemeCatalog.Names[casings[shot]],
                    background = ThemeCatalog.Names[backgrounds[shot]], remainingGrains = grains, occupiedSlots = belt.OccupiedSlots,
                    portrait = portrait, landscape = landscape, visibleLabels = labels,
                    tutorialHintVisible = screenshotHint.gameObject.activeInHierarchy
                });
                File.WriteAllText(Path.Combine(output, "capture-progress.txt"), "Captured " + language + " " + names[shot] + "; PNG files=" + records.Count * 2);
            }
        }
        File.WriteAllText(Path.Combine(output, "screenshots.json"), JsonUtility.ToJson(new ScreenshotReport { screenshots = records.ToArray() }, true));
        RenderTexture.active = null; camera.targetTexture = null;
        target.Release(); wideTarget.Release(); Destroy(target); Destroy(wideTarget); Destroy(pixels); Destroy(widePixels);
    }
    Process OpenEncoder(int number, int width, int height, string prefix)
    {
        string path = Path.Combine(output, prefix + number + ".mp4");
        return Process.Start(new ProcessStartInfo {
            FileName = Environment.GetEnvironmentVariable("SAND_CAPTURE_FFMPEG"),
            Arguments = "-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size " + width + "x" + height
                + " -framerate " + Fps + " -i pipe:0 -vf vflip -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -an -movflags +faststart \"" + path + "\"",
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true
        });
    }
    void PlayMove()
    {
        if (belt.FreeSlots <= 0 || FindObjectOfType<BucketInventoryUI>().IsMoving) return;
        var grid = loader.CurrentGrid;
        var demand = new int[256];
        for (int x = 0; x < grid.Width; x++)
        {
            int found = 0;
            for (int y = grid.Height - 1; y >= 0 && found < 12; y--)
            {
                byte color = grid.GetCell(x, y);
                if (color != 0) { demand[color] += 12 - found; found++; }
            }
        }
        var candidates = manager.Inventory.Buckets
            .Where(b => manager.Inventory.IsBucketAccessible(b, loader.CurrentLevel)
                && b.SlotCost + (b.LinkedPartner != null ? b.LinkedPartner.SlotCost : 0) <= belt.FreeSlots)
            .OrderByDescending(b => demand[b.ColorIndex] / (1 + belt.Entries.Count(e => e.Bucket.ColorIndex == b.ColorIndex))
                + (shot == 2 && b.LinkedPartner != null ? 100000 : 0)).ToArray();
        foreach (var bucket in candidates)
        {
            if (!manager.TryPlaceBucket(bucket)) continue;
            events.Add((globalFrame / (float)Fps).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",tap," + shot);
            GameAudio.Instance?.PlayTap();
            break;
        }
    }
}
