using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Real portrait UI clicks and transitions in a disposable editor profile.</summary>
[InitializeOnLoad]
public static class CampaignMechanicsVisualChecks
{
    private const string Key = "Sand.MechanicsPreview";
    private const string Folder = "tasks/previews/mechanics-v5";
    private static IEnumerator<double> steps;
    private static double next;
    private static readonly List<string> errors = new List<string>();
    private static GameManager Manager => UnityEngine.Object.FindObjectOfType<GameManager>();
    private static LevelData Level => UnityEngine.Object.FindObjectOfType<LevelLoader>().CurrentLevel;
    private static ConveyorBelt Belt => UnityEngine.Object.FindObjectOfType<ConveyorBelt>();
    private static FallingSandSimulator Sim => UnityEngine.Object.FindObjectOfType<FallingSandSimulator>();

    static CampaignMechanicsVisualChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message); };
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false))
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/campaign-mechanics-visual")) return;
            File.Delete("Temp/campaign-mechanics-visual");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) { File.WriteAllText("tasks/mechanics-visual-status.txt", "Save scene before capture."); return; }
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            CampaignVisualChecks.SetPortraitGameView();
            SessionState.SetBool("Sand.CampaignPreview", true);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        Time.timeScale = 1;
        if (steps == null) { steps = Sequence(); next = EditorApplication.timeSinceStartup + 7; }
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (!steps.MoveNext())
            {
                Require(errors.Count == 0, "Runtime errors: " + string.Join("\n", errors));
                File.WriteAllText("tasks/mechanics-visual-status.txt", "PASS " + DateTime.Now.ToString("O") + ": real pointer routing, mechanic transitions, restart, shop, ad and victory presentation. Controlled fills isolate UI transitions; physics tested separately.");
                Stop(); return;
            }
            next = EditorApplication.timeSinceStartup + steps.Current;
        }
        catch (Exception error) { File.WriteAllText("tasks/mechanics-visual-status.txt", error.ToString()); Debug.LogException(error); Stop(); }
    }

    private static void Stop()
    {
        SessionState.SetBool("Sand.CampaignPreview", false); SessionState.SetBool(Key, false);
        EditorApplication.isPlaying = false; steps = null;
    }

    private static IEnumerator<double> Sequence()
    {
        LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
        GameProgressStore.SetBool("sand_tutorial_v2_complete", true);
        GameProgressStore.SetInt("sand_progress_highest_unlocked", 99); ProgressSaveService.Load();
        foreach (BoosterType booster in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(booster);
        foreach (int number in new[] { 12, 18, 25, 28, 38, 48, 50, 58, 68, 80 })
        {
            Manager.LoadLevel(number - 1); yield return 1;
            while (UnityEngine.Object.FindObjectOfType<LevelIntroSandAnimator>().IsPlaying) yield return .5;
            Sim.SetSimulationPaused(true); Belt.SetMovementPaused(true); yield return .5;
            Require(UnityEngine.Object.FindObjectsOfType<BucketView>().All(v => v.GetComponentsInChildren<BucketMechanicVisual>().Length == 1), "One mechanic visual per bucket");
            Capture(number + "-start");
            if (number == 12)
            {
                Require(CampaignProgression.IsLessonStart(11), "Feeder lesson level");
                int seed = Level.campaignSeed; CampaignVisualChecks.PressButton("ResetButton"); yield return 1;
                while (UnityEngine.Object.FindObjectOfType<LevelIntroSandAnimator>().IsPlaying) yield return .5;
                Require(Level.campaignSeed != seed, "Restart changes layout");
                Require(UnityEngine.Object.FindObjectOfType<GameUI>() != null, "Hint UI available after restart");
                Capture("12-restart-hint-again");
            }
            else if (number == 50)
            {
                var unknown = UnityEngine.Object.FindObjectsOfType<BucketView>().Where(v => v.Bucket.IsMystery).ToArray();
                Require(unknown.Length > 0, "Mystery buckets visible");
                Require(unknown.All(v => v.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "?" && t.color == Color.white)), "White mystery marks");
                Require(unknown.All(v =>
                {
                    return v.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                        .Any(i => i.rectTransform != null && i.rectTransform.localScale.x < 0.95f);
                }), "Mystery buckets render smaller");
            }
            else if (number == 18)
            {
                int frost = CampaignProgression.FrostMovesFor(18);
                for (int i = 0; i < frost; i++)
                {
                    Dispatch(FirstAccessible()); yield return .65;
                    Require(Manager.Inventory.Buckets.All(b => b.FrostMoves <= frost - 1 - i), "One ice decrement per successful dispatch");
                    Capture("18-ice-" + (frost - 1 - i));
                }
            }
            else if (number == 58)
            {
                Require(Manager.Inventory.CrateBucketCount > 0 && !Manager.Inventory.CratesOpen, "Crate row starts closed");
                Dispatch(FirstAccessible()); yield return .65;
                Require(Manager.Inventory.CratesOpen, "First successful click opens crates"); Capture("58-crates-open");
                Dispatch(FirstAccessible()); yield return .65;
                Require(!Manager.Inventory.CratesOpen, "Second successful click closes crates"); Capture("58-crates-closed");
            }
            else if (number == 68)
            {
                var pair = Manager.Inventory.Buckets.First(b => b.SwapPartner != null); byte before = pair.ColorIndex;
                Require(pair.LayoutIndex / LevelData.BucketLayoutColumns
                    != pair.SwapPartner.LayoutIndex / LevelData.BucketLayoutColumns,
                    "Swap pair sits on different rows");
                Dispatch(FirstAccessible(b => b.SwapPartner == null)); yield return .65;
                Require(pair.SwapPartner.ColorIndex == before && pair.ColorIndex != before, "Color pair swaps visibly"); Capture("68-colors-swapped");
            }
            else if (number == 80)
            {
                InventoryLoopRoute.Bind(Level);
                var before = Manager.Inventory.Buckets.ToDictionary(b => b, b => b.LayoutIndex);
                Dispatch(FirstAccessible()); yield return .13; Capture("80-loop-mid-move"); yield return .6;
                foreach (var bucket in Manager.Inventory.Buckets)
                    if (before.TryGetValue(bucket, out int old)) Require(bucket.LayoutIndex == InventoryLoopRoute.Next(old), "Clockwise route advances one slot");
                Capture("80-loop-after");
            }
            else
            {
                Bucket target = Manager.Inventory.Buckets.First(b => number == 25 ? b.HasSecondSection : number == 28 ? b.KeyId != 0 : number == 38 ? b.LinkedPartner != null : b.SlotCost == 2);
                int guard = 0;
                while (!Manager.Inventory.IsBucketAccessible(target, Level) && guard++ < 30)
                {
                    Dispatch(FirstAccessible()); FinishBelt(); yield return .7;
                }
                Require(Manager.Inventory.IsBucketAccessible(target, Level), "Special bucket reachable via ordinary moves");
                if (number == 25)
                    Require(Manager.Inventory.Buckets.Count(b => b.HasSecondSection) >= 2, "Level 25 has two 1|2 buckets");
                Dispatch(target); yield return .7;
                if (number == 25)
                {
                    while (target.ColorIndex == target.FirstColor) Require(target.TryAddGrain(target.FirstColor), "Fill first section");
                    yield return .5; Require(!target.IsFull, "Second section still needed"); Capture("25-second-section");
                }
                if (number == 28)
                {
                    Require(Manager.Inventory.Buckets.Any(b => Manager.Inventory.IsGateClosed(b)), "Gates remain locked before key fills");
                    FinishBelt(); yield return .7; Require(!Manager.Inventory.Buckets.Any(b => Manager.Inventory.IsGateClosed(b)), "Filled key opens gates"); Capture("28-gates-open");
                }
                if (number == 38)
                {
                    Require(Belt.OccupiedSlots == 2, "Linked dispatch consumes two slots");
                    Require(UnityEngine.Object.FindObjectsOfType<LinkedBucketLinkGraphic>().Length > 0, "Linked pair uses two-tone line");
                    BoosterMechanics.TryFillConveyorBucket(target, Sim, Belt); yield return .7;
                    Require(Belt.OccupiedSlots == 2, "Full first partner stays on belt"); Capture("38-one-full-waiting");
                    BoosterMechanics.TryFillConveyorBucket(target.LinkedPartner, Sim, Belt); yield return .7;
                    Require(Belt.OccupiedSlots == 0, "Both full linked buckets leave");
                }
                if (number == 48) { Require(Belt.OccupiedSlots == 2, "Large bucket costs two slots"); Capture("48-large-two-slots"); }
            }
            yield return 1;
        }
        CampaignVisualChecks.PressButton("ShopBtn"); yield return .8; Capture("shop-golden-coins");
        CampaignVisualChecks.PressButton("CloseShop"); yield return .5;
        typeof(SandEconomyHud).GetMethod("OpenBuyPopup", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(SandEconomyHud.instance, new object[] { BoosterType.Magnet });
        yield return .7; Capture("rewarded-booster-popup");
        CampaignVisualChecks.PressButton(UnityEngine.Object.FindObjectsOfType<Button>().First(b => b.name == "CloseBtn" && b.transform.parent?.parent?.name == "BoosterBuyPopup"));
        yield return .5;
        SandEconomyHud.instance.ShowLevelComplete(); yield return 1.5; Capture("victory-golden-coins-ad");
        Require(UnityEngine.Object.FindObjectsOfType<Image>().Where(i => i.name == "Coins").All(i => i.sprite == BusesUiArt.Gold), "Shared victory and HUD coin art");
        yield return .6;
    }

    private static Bucket FirstAccessible(Func<Bucket, bool> extra = null)
        => Manager.Inventory.Buckets.First(b => Manager.Inventory.IsBucketAccessible(b, Level) && (extra == null || extra(b)));
    private static void Dispatch(Bucket bucket)
    {
        int before = Belt.OccupiedSlots;
        CampaignVisualChecks.PressBucket(bucket);
        Require(Belt.OccupiedSlots == before + bucket.SlotCost + (bucket.LinkedPartner != null ? bucket.LinkedPartner.SlotCost : 0), "Pointer click dispatches correct slot cost");
    }
    private static void FinishBelt()
    {
        foreach (var entry in Belt.Entries.ToArray()) BoosterMechanics.TryFillConveyorBucket(entry.Bucket, Sim, Belt);
        Belt.UpdateBelt(.01f);
    }
    private static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + name + ".png"));
        Debug.Log("MECHANICS_CAPTURE: " + name);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
