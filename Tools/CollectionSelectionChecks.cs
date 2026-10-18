// Copy to Assets/Editor in the isolated capture project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class CollectionSelectionChecks
{
    const string Key = "Sand.CollectionSelectionChecks";
    static CollectionSelectionChecks() { EditorApplication.update += Tick; }
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
            && UnityEngine.Object.FindObjectOfType<CollectionSelectionCheckRunner>() == null)
            new GameObject("CollectionSelectionCheckRunner").AddComponent<CollectionSelectionCheckRunner>();
    }
    public static void Finish(bool passed, string message)
    {
        string output = Environment.GetEnvironmentVariable("SAND_COLLECTION_CHECK_OUTPUT");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "status.txt"), (passed ? "PASS\n" : "FAIL\n") + message);
        SessionState.SetBool(Key, false);
        SessionState.SetBool("Sand.BuildSizeChecks", false);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
public sealed class CollectionSelectionCheckRunner : MonoBehaviour
{
    readonly List<string> errors = new List<string>();
    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }
    void OnLog(string message, string trace, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error) && !trace.Contains("UnityEditor.Search.")) errors.Add(message);
    }
    IEnumerator Start()
    {
        var sequence = Check();
        while (true)
        {
            object step;
            try { if (!sequence.MoveNext()) break; step = sequence.Current; }
            catch (Exception error) { CollectionSelectionChecks.Finish(false, error.ToString()); yield break; }
            yield return step;
        }
        CollectionSelectionChecks.Finish(errors.Count == 0, "Image selectors, RU/EN text, regular/winter grids, pagination, locks, level selection, pause restoration and preview cleanup verified.\n" + string.Join("\n", errors));
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    IEnumerator Check()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Time.captureFramerate = 30;
        for (int i = 0; i < 110; i++) yield return null;
        Require(GameProgressStore.IsPreview, "Expected an isolated profile.");
        var manager = FindObjectOfType<GameManager>();
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        var belt = FindObjectOfType<ConveyorBelt>();
        var sand = FindObjectOfType<FallingSandSimulator>();
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 0);
        GameProgressStore.SetInt("winter_highest_unlocked", 0);
        ProgressSaveService.Load();
        manager.LoadLevel(0);
        for (int i = 0; i < 100; i++) yield return null;
        FindObjectOfType<TutorialController>().HandleLevelCompleted();
        FindObjectOfType<TutorialController>().HandleHintDismissed();
        FindObjectOfType<GameUI>().SetHintVisible(false);
        string output = Environment.GetEnvironmentVariable("SAND_COLLECTION_CHECK_OUTPUT");
        Directory.CreateDirectory(output);
        foreach (string language in new[] { "RU", "EN" })
        {
            LocalizationManager.Instance.SetLanguage(language == "RU" ? MirraGames.SDK.Common.LanguageType.Russian : MirraGames.SDK.Common.LanguageType.English);
            float timeScale = Time.timeScale;
            bool beltPaused = belt.IsMovementPaused, sandPaused = sand.IsSimulationPaused;
            var gallery = CampaignGallery.Show(database, manager);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Require(CampaignGallery.IsOpen && Time.timeScale == 0 && belt.IsMovementPaused && sand.IsSimulationPaused, "Gallery must pause gameplay.");
            var panel = gallery.transform.Find("CollectionPanel");
            var regular = panel.Find("AllLevels").GetComponent<Button>();
            var winter = panel.Find("WinterLevels").GetComponent<Button>();
            Texture[] previews = new[] { regular, winter }.SelectMany(b => b.GetComponentsInChildren<RawImage>()).Select(i => i.texture).ToArray();
            Require(previews.Length == 6 && previews.All(t => t != null), "Six collection paintings must be visible.");
            Require(!regular.interactable && winter.interactable, "Regular collection must start selected.");
            CheckGrid(panel, database, manager, false);
            if (language == "EN") Require(gallery.GetComponentsInChildren<TextMeshProUGUI>().All(t => !t.text.Any(c => c >= '\u0400' && c <= '\u04ff')), "Cyrillic remains in EN.");
            Capture(canvas, Path.Combine(output, language + "-regular.png"), 1440, 2560);
            winter.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Require(regular.interactable && !winter.interactable && winter.transform.Find("SelectedOutline").gameObject.activeSelf, "Winter selection state is wrong.");
            Require(previews.All(t => t != null), "Changing collection released selector paintings.");
            CheckGrid(panel, database, manager, true);
            var cards = panel.Find("Paintings").GetComponentsInChildren<Button>();
            Require(cards[0].interactable && !cards[1].interactable, "Winter level locks changed.");
            Capture(canvas, Path.Combine(output, language + "-winter.png"), 1440, 2560);
            Capture(canvas, Path.Combine(output, language + "-winter-landscape.png"), 2560, 1440);
            panel.Find("Next").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Require(panel.Find("Page").GetComponent<TextMeshProUGUI>().text.StartsWith("2 / "), "Winter page did not advance.");
            CheckGrid(panel, database, manager, true);
            regular.onClick.Invoke();
            yield return null;
            Require(panel.Find("Page").GetComponent<TextMeshProUGUI>().text.StartsWith("1 / "), "Collection switch did not reset pagination.");
            CheckGrid(panel, database, manager, false);
            gallery.RefreshProgress();
            yield return null;
            Require(previews.All(t => t != null), "Progress refresh released selector paintings.");
            gallery.Close();
            Require(!CampaignGallery.IsOpen && Mathf.Approximately(Time.timeScale, timeScale) && belt.IsMovementPaused == beltPaused && sand.IsSimulationPaused == sandPaused, "Gallery did not restore pause state.");
            yield return null;
            Require(previews.All(t => t == null), "Selector previews leaked after close.");
        }
        var finalGallery = CampaignGallery.Show(database, manager);
        finalGallery.transform.Find("CollectionPanel/WinterLevels").GetComponent<Button>().onClick.Invoke();
        yield return null;
        finalGallery.transform.Find("CollectionPanel/Paintings").GetComponentsInChildren<Button>()[0].onClick.Invoke();
        Require(!CampaignGallery.IsOpen && manager.IsSeasonal(manager.CurrentLevelIndex), "Selecting a winter painting did not start its level.");
        yield return null;
        var reopened = CampaignGallery.Show(database, manager, true);
        yield return null;
        Require(!reopened.transform.Find("CollectionPanel/WinterLevels").GetComponent<Button>().interactable, "Opening from a winter level selected the wrong collection.");
        reopened.Close();
    }
    static void CheckGrid(Transform panel, LevelDatabase database, GameManager manager, bool winter)
    {
        var cards = panel.Find("Paintings").GetComponentsInChildren<Button>();
        Require(cards.Length == 20, "Expected 20 level cards.");
        foreach (var card in cards)
        {
            int index = int.Parse(card.name.Substring(6)) - 1;
            Require(manager.IsSeasonal(index) == winter, "Wrong campaign in grid.");
            Require(card.interactable == manager.IsLevelUnlocked(index), "Level lock state changed.");
        }
        int count = Enumerable.Range(0, database.LevelCount).Count(i => manager.IsSeasonal(i) == winter);
        string expected = string.Format(CampaignText.Get("collection_level_count"), count);
        Require(panel.Find(winter ? "WinterLevels/CollectionCount" : "AllLevels/CollectionCount").GetComponent<TextMeshProUGUI>().text == expected, "Wrong collection count.");
    }
    static void Capture(Canvas canvas, string path, int width, int height)
    {
        var camera = Camera.main;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderMode previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance, previousScale = canvas.scaleFactor;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.scaleFactor = height / MobilePortraitLayout.ReferenceHeight;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            pixels.Apply(false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            canvas.scaleFactor = previousScale;
            Canvas.ForceUpdateCanvases();
            target.Release();
            Destroy(target); Destroy(pixels);
        }
    }
}
