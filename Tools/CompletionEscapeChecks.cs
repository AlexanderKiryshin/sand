// Copy to Assets/Editor in a disposable project; run CompletionEscapeChecks.Run.
// Exclude BuildSizeSmokeChecks.cs there: both use its isolated in-memory profile flag.
// Exercises the actual C# completion handler in Play Mode with Mirra's editor ads.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MirraGames.SDK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CompletionEscapeChecks
{
    const string Key = "Sand.CompletionEscapeChecks";
    static CompletionEscapeChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        CampaignVisualChecks.SetPortraitGameView();
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying &&
            UnityEngine.Object.FindObjectOfType<CompletionEscapeRunner>() == null)
            new GameObject("CompletionEscapeRunner").AddComponent<CompletionEscapeRunner>();
    }
    public static void Finish(bool success, string message)
    {
        string output = Environment.GetEnvironmentVariable("SAND_ESC_RESULTS") ?? "Temp/CompletionEscapeChecks";
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "status.txt"), (success ? "PASS\n" : "FAIL\n") + message);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

public sealed class CompletionEscapeRunner : MonoBehaviour
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> passed = new List<string>();
    readonly List<string> errors = new List<string>();
    SandEconomyHud hud;
    GameManager game;
    GameObject panel;
    object ads;
    Stack<Action<bool>> rewarded, interstitial;
    int escapeFrames;
    void Update() { if (Input.GetKeyDown(KeyCode.Escape)) escapeFrames++; }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string trace, LogType type)
    {
        // The unrelated Yandex editor update checker can fail offline during Play Mode.
        if ((type == LogType.Error || type == LogType.Exception) && !trace.Contains("YG.EditorScr.Server"))
            errors.Add(message);
    }
    static void Call(object target, string method) { target.GetType().GetMethod(method, Hidden).Invoke(target, null); }
    void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        passed.Add(message);
        Debug.Log("COMPLETION_ESC_PASS " + message);
    }
    void CloseAd(string element, string button)
    {
        var view = (VisualElement)ads.GetType().GetField(element, Hidden).GetValue(ads);
        var control = view.Q<Button>(button);
        using (var click = ClickEvent.GetPooled()) { click.target = control; control.SendEvent(click); }
    }
    IEnumerator Win()
    {
        // A synthetic win may be requested while the level intro/tutorial is paused.
        var intro = FindObjectOfType<LevelIntroSandAnimator>();
        while (intro != null && intro.IsPlaying) { Time.timeScale = 1; yield return null; }
        Time.timeScale = 1;
        Call(game, "HandleLevelComplete");
        float limit = Time.realtimeSinceStartup + 10;
        while (!panel.activeSelf && Time.realtimeSinceStartup < limit) { Time.timeScale = 1; yield return null; }
        if (!panel.activeSelf) throw new InvalidOperationException("Victory window did not appear.");
    }
    IEnumerator Escape()
    {
        int before = escapeFrames;
        EditorGUIUtility.QueueGameViewInputEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
        yield return null;
        yield return null;
        EditorGUIUtility.QueueGameViewInputEvent(new Event { type = EventType.KeyUp, keyCode = KeyCode.Escape });
        yield return null;
        if (escapeFrames == before)
        {
            Debug.Log("COMPLETION_ESC_INPUT: batch Game View did not deliver keyboard input; invoking the C# handler directly.");
            Call(hud, "CloseCompleteOnEscape");
        }
    }
    IEnumerator Start()
    {
        Application.runInBackground = true;
        var steps = new Stack<IEnumerator>();
        steps.Push(Check());
        while (steps.Count > 0)
        {
            object step;
            try
            {
                if (!steps.Peek().MoveNext()) { steps.Pop(); continue; }
                step = steps.Peek().Current;
                if (step is IEnumerator nested) { steps.Push(nested); continue; }
            }
            catch (Exception error) { CompletionEscapeChecks.Finish(false, error + "\n" + string.Join("\n", errors)); yield break; }
            yield return step;
        }
        CompletionEscapeChecks.Finish(errors.Count == 0, string.Join("\n", passed) + "\n" + string.Join("\n", errors));
    }
    IEnumerator Check()
    {
        for (int i = 0; i < 120; i++) yield return null;
        Require(GameProgressStore.IsPreview && MirraSDK.IsInitialized, "Isolated Play Mode profile is ready");
        game = FindObjectOfType<GameManager>();
        hud = FindObjectOfType<SandEconomyHud>();
        panel = (GameObject)typeof(SandEconomyHud).GetField("completePanel", Hidden).GetValue(hud);
        ads = MirraSDK.Ads;
        Require(ads.GetType().Name == "PrototypeAds", "Only editor ad callbacks are used");
        rewarded = (Stack<Action<bool>>)ads.GetType().GetField("rewardedCloseCallbacks", Hidden).GetValue(ads);
        interstitial = (Stack<Action<bool>>)ads.GetType().GetField("interstitialCloseCallbacks", Hidden).GetValue(ads);
        var coins = CoinManager.instance;
        int before = coins.Coins, level = game.CurrentLevelIndex;
        yield return Escape();
        Require(coins.Coins == before && game.CurrentLevelIndex == level, "Escape with no victory window does nothing");
        yield return Win();
        yield return Escape();
        Require(!panel.activeSelf && coins.Coins == before + 50 && game.CurrentLevelIndex == level + 1 &&
            game.State == GameState.Playing && !AdManager.instance.IsWindowOpen,
            "Escape closes victory, grants 50 coins and advances to a playable level" +
            " [panel=" + panel.activeSelf + ", coins=" + coins.Coins + ", before=" + before +
            ", level=" + game.CurrentLevelIndex + ", previous=" + level + ", state=" + game.State +
            ", window=" + AdManager.instance.IsWindowOpen + ", keyboardFrames=" + escapeFrames + "]");
        Require(rewarded.Count == 0 && interstitial.Count == 0 && GameProgressStore.GetInt("Coins") == coins.Coins,
            "Escape saves the standard reward without requesting ads");
        yield return Escape(); Call(hud, "OnCompleteDouble"); Call(hud, "OnCompleteContinue");
        Require(coins.Coins == before + 50 && game.CurrentLevelIndex == level + 1 && rewarded.Count == 0 && interstitial.Count == 0,
            "Repeated dismissal and stale button calls cannot grant a second reward");

        foreach (bool success in new[] { false, true })
        {
            yield return Win();
            before = coins.Coins; level = game.CurrentLevelIndex;
            Call(hud, "OnCompleteDouble");
            yield return Escape(); Call(hud, "OnCompleteDouble");
            Require(panel.activeSelf && coins.Coins == before && game.CurrentLevelIndex == level && rewarded.Count == 1,
                "Pending rewarded blocks Escape and repeated reward requests (success=" + success + ")");
            var callback = rewarded.Peek();
            CloseAd("rewardedElement", success ? "Success" : "Close");
            callback(true); callback(false);
            if (!success) yield return Escape();
            Require(!panel.activeSelf && game.CurrentLevelIndex == level + 1 && coins.Coins == before + (success ? 100 : 50),
                "Reward cancellation/success and duplicate callbacks leave exactly one reward (success=" + success + ")");
        }

        yield return Win(); before = coins.Coins; level = game.CurrentLevelIndex;
        Call(hud, "OnCompleteContinue"); yield return Escape();
        Require(panel.activeSelf && coins.Coins == before && interstitial.Count == 1,
            "Normal Continue retains its existing interstitial; Escape cannot interrupt a pending continuation");
        CloseAd("interstitialElement", "Close");
        Require(!panel.activeSelf && coins.Coins == before + 50 && game.CurrentLevelIndex == level + 1,
            "Normal Continue still advances with one standard reward");

        for (int i = 0; i < 199; i++) WinterProgress.Complete(i);
        game.LoadLevel(game.LevelCount - 1);
        yield return Win(); before = coins.Coins;
        yield return Escape();
        Require(!panel.activeSelf && CampaignGallery.IsOpen && coins.Coins == before + 50,
            "Escape on the final level closes victory and opens the collection");
    }
}
