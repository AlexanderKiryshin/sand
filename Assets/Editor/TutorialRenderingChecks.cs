using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TutorialRenderingChecks
{
    private const string Key = "Sand.TutorialRenderingChecks";
    static TutorialRenderingChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying
            && UnityEngine.Object.FindObjectOfType<TutorialRenderingRunner>() == null)
            new GameObject("TutorialRenderingRunner").AddComponent<TutorialRenderingRunner>();
    }
    public static void Finish(string report, bool passed)
    {
        File.WriteAllText("tutorial-rendering-result.txt", (passed ? "PASS\n" : "FAIL\n") + report);
        Debug.Log((passed ? "PASS\n" : "FAIL\n") + report);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}

public class TutorialRenderingRunner : MonoBehaviour
{
    private readonly List<string> errors = new List<string>();
    private void OnEnable() { Application.logMessageReceived += OnLog; }
    private void OnDisable() { Application.logMessageReceived -= OnLog; }
    private void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) errors.Add(message + "\n" + trace);
    }
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Time.timeScale = 1;
        yield return new WaitForSecondsRealtime(3);
        float deadline = Time.realtimeSinceStartup + 20;
        while (FindObjectOfType<LevelIntroSandAnimator>().IsPlaying && Time.realtimeSinceStartup < deadline)
        {
            Time.timeScale = 1;
            yield return null;
        }
        string report = "";
        try
        {
            var ui = FindObjectOfType<GameUI>();
            var tutorial = FindObjectOfType<TutorialController>();
            var inventory = FindObjectOfType<BucketInventoryUI>();
            var hint = (TextMeshProUGUI)typeof(GameUI).GetField("hintLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
            LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
            report += "Initial hint=" + hint.text + ", characters=" + hint.textInfo.characterCount + "\n";
            if (hint.textInfo.characterCount == 0) throw new Exception("First tutorial hint has no rendered characters");
            var first = inventory.GetFirstAccessibleBucket();
            var target = (Bucket)typeof(TutorialController).GetField("targetBucket", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tutorial);
            report += "First bucket=" + (first == null ? "null" : first.LayoutIndex.ToString()) + ", target=" + (target == null ? "null" : target.LayoutIndex.ToString()) + "\n";
            if (!tutorial.IsBucketInteractionAllowed(target)) throw new Exception("Target bucket is blocked");
            var views = inventory.GetComponentsInChildren<BucketView>();
            var targetView = Array.Find(views, view => view.Bucket == target);
            var targetButton = targetView.GetComponent<Button>();
            if (!targetButton.interactable) throw new Exception("Highlighted bucket button is disabled");
            targetButton.onClick.Invoke();
            if (!hint.text.Contains(CampaignText.Get("tutorial_watch"))) throw new Exception("Click did not advance to the second hint");
            var belt = FindObjectOfType<ConveyorBelt>();
            var beltView = FindObjectOfType<ConveyorBeltView>();
            var beltButton = Array.Find(beltView.GetComponentsInChildren<Button>(), button => button.name == "BeltBucket");
            if (beltButton == null) throw new Exception("Placed bucket has no belt visual");
            int occupiedBeforeClick = belt.OccupiedSlots;
            beltButton.onClick.Invoke();
            if (belt.OccupiedSlots != occupiedBeforeClick || target.State == BucketState.InInventory)
                throw new Exception("Click removed a bucket from the belt");
            report += "Belt click preserves the bucket and occupied slots\n";
            tutorial.HandleBucketFilled(target);
            if (hint.text != CampaignText.Get("tutorial_repeat")) throw new Exception("Full bucket did not advance to the third hint");
            for (int step = 1; step <= 3; step++)
            {
                ui.SetHintVisible(false);
                ui.SetTutorialHint(CampaignText.Get(step == 1 ? "tutorial_pick" : step == 2 ? "tutorial_watch" : "tutorial_repeat"), step);
                Canvas.ForceUpdateCanvases();
                report += "Step " + step + ": characters=" + hint.textInfo.characterCount + "\n";
                if (hint.textInfo.characterCount == 0) throw new Exception("Step " + step + " has no rendered text");
                if (!Array.Exists(hint.textInfo.characterInfo, character => character.isVisible))
                    throw new Exception("Step " + step + " has no visible glyphs");
            }
        }
        catch (Exception error) { errors.Add(error.ToString()); }
        TutorialRenderingChecks.Finish(report + string.Join("\n", errors), errors.Count == 0);
    }
}
