using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opt-in editor-only presentation, using an isolated save profile.</summary>
[InitializeOnLoad]
public static class MarketingPreview
{
    public const string Key = "Sand.MarketingPreview";
    const string Request = "Library/marketing-lake-preview-request";
    static MarketingPreview()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetBool(Key, false);
        };
    }
    [MenuItem("Window/Sand/Marketing/Open Lake Gameplay")]
    public static void Open()
    {
        if (!EditorApplication.isPlaying)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameScene.unity")
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the current scene before opening the gameplay preview.");
                EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            }
        }
        SessionState.SetBool(Key, true);
        CampaignVisualChecks.SetPortraitGameView();
        if (EditorApplication.isPlaying)
        {
            if (UnityEngine.Object.FindObjectOfType<MarketingLakePreviewRunner>() == null)
                new GameObject("MarketingLakePreview").AddComponent<MarketingLakePreviewRunner>();
        }
        else EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(Request);
            try { Open(); }
            catch (Exception error) { File.WriteAllText("Temp/marketing-lake-preview-status.txt", "FAIL\n" + error); }
        }
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying
            && UnityEngine.Object.FindObjectOfType<MarketingLakePreviewRunner>() == null)
            new GameObject("MarketingLakePreview").AddComponent<MarketingLakePreviewRunner>();
    }
}

public sealed class MarketingLakePreviewRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        Application.runInBackground = true;
        for (int i = 0; i < 10 || FindObjectOfType<GameManager>() == null; i++) yield return null;
        var manager = FindObjectOfType<GameManager>();
        if (!GameProgressStore.IsPreview) throw new InvalidOperationException("Expected an isolated preview save.");
        LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 999);
        ProgressSaveService.Load();
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(type);
        ThemeCatalog.Unlock(27, ThemeCatalog.Part.Background);
        if (!ThemeCatalog.Select(27, ThemeCatalog.Part.Background)) throw new InvalidOperationException("Could not select the lake background.");
        manager.LoadLevel(8);
        SandEconomyHud.instance.SetSkipLevelButtonVisible(false);
        var intro = FindObjectOfType<LevelIntroSandAnimator>();
        while (intro != null && intro.IsPlaying) { Time.timeScale = 1; yield return null; }
        FindObjectOfType<TutorialController>().HandleLevelCompleted();
        FindObjectOfType<GameUI>().SetHintVisible(false);
        Time.timeScale = 1;
        yield return null;
        if (manager.CurrentLevelIndex != 8 || ThemeCatalog.SelectedBackground != 27)
            throw new InvalidOperationException("Preview configuration changed unexpectedly.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/marketing-lake-preview-status.txt", "PASS\nLevel 9, lake background (27), tutorial closed. Isolated editor profile.\n");
        Debug.Log("MARKETING_LAKE_PREVIEW_READY: level 9, background lake, no tutorial.");
    }
}
