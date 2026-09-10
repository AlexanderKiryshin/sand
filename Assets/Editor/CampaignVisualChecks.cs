using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opt-in editor smoke capture with a disposable profile. Does not run in players.</summary>
[InitializeOnLoad]
public static class CampaignVisualChecks
{
    private const string Key = "Sand.CampaignPreview";
    private const string Request = "Temp/campaign-visual-request";
    private static double next;
    private static int stage;
    private static bool languagePrepared;
    private static double nextStepReady;
    private static readonly System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();

    static CampaignVisualChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Exception || type == LogType.Error)) errors.Add(message);
        };
    }

    [MenuItem("Window/Sand/Campaign/Capture Game Preview")]
    public static void Run()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling)
        {
            File.WriteAllText(Request, "capture after compile");
            return;
        }
        var textSettings = Resources.Load<TMPro.TMP_Settings>("TMP Settings");
        if (textSettings == null || new SerializedObject(textSettings).FindProperty("assetVersion").stringValue != "2")
        {
            string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly).resolvedPath;
            AssetDatabase.importPackageCompleted += ResumeAfterTextImport;
            AssetDatabase.ImportPackage(Path.Combine(package, "Package Resources/TMP Essential Resources.unitypackage"), false);
            return;
        }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene before starting preview captures.");
        if (File.Exists("Temp/campaign-density-check"))
        {
            File.Delete("Temp/campaign-density-check");
            CampaignBuilder.UpdateRules();
            CampaignSimulationChecks.RunInEditor();
        }
        if (File.Exists("Temp/campaign-simulation-only"))
        {
            File.Delete("Temp/campaign-simulation-only");
            CampaignSimulationChecks.RunInEditor();
        }
        Directory.CreateDirectory("tasks/previews");
        next = 0;
        stage = 0;
        nextStepReady = 0;
        languagePrepared = false;
        errors.Clear();
        BucketPresentationObservations.Reset();
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        SetPortraitGameView();
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (SessionState.GetBool("Sand.MechanicsPreview", false)) return;
        if (File.Exists("Temp/campaign-visual-reset"))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.isPlaying = false;
                return;
            }
            File.Delete("Temp/campaign-visual-reset");
            Run();
            return;
        }
        if (!SessionState.GetBool(Key, false))
        {
            if (!EditorApplication.isCompiling && File.Exists(Request)) { File.Delete(Request); Run(); }
            return;
        }
        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.SetBool(Key, false);
            return;
        }
        // This disposable editor capture may lose OS focus while its screenshots
        // are inspected. Keep its clock running; production focus handling is untouched.
        Application.runInBackground = true;
        if (Time.timeScale == 0) Time.timeScale = 1;
        if (next == 0) next = EditorApplication.timeSinceStartup + 7;
        try
        {
            BucketPresentationObservations.Observe(Capture);
            if (EditorApplication.timeSinceStartup < next) return;
            GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (manager == null) throw new InvalidOperationException("GameManager did not start.");
            SandGrid grid = UnityEngine.Object.FindObjectOfType<LevelLoader>().CurrentGrid;
            if (grid.SubGrainsPerAxis != 1 || grid.Width != grid.LevelWidth || grid.Height != grid.LevelHeight)
                throw new InvalidOperationException("Physical density was not restored to one grain per level cell.");
            if (!languagePrepared)
            {
                LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
                manager.RestartLevel();
                languagePrepared = true;
                next = EditorApplication.timeSinceStartup + 4;
                return;
            }
            var intro = UnityEngine.Object.FindObjectOfType<LevelIntroSandAnimator>();
            if ((stage == 0 || stage == 7 || stage == 22) && intro != null && intro.IsPlaying) return;
            if (stage == 3)
            {
                var tutorial = UnityEngine.Object.FindObjectOfType<TutorialController>();
                int phase = (int)typeof(TutorialController).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tutorial);
                if (phase != 2) return;
                if (nextStepReady == 0) nextStepReady = EditorApplication.timeSinceStartup + 1;
                if (EditorApplication.timeSinceStartup < nextStepReady) return;
            }
            switch (stage++)
            {
                case 0:
                    Debug.Log($"CAMPAIGN_DENSITY_PASS: {grid.Width}x{grid.Height} = {grid.Width * grid.Height} physical cells, subGrainsPerAxis={grid.SubGrainsPerAxis}");
                    if (UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>().Any(i => i.name.StartsWith("Slot_")))
                        throw new InvalidOperationException("Ordinary inventory cell backgrounds are still visible.");
                    Capture("01-tutorial"); break;
                case 1:
                    PressBucket(UnityEngine.Object.FindObjectOfType<BucketInventoryUI>().GetFirstAccessibleBucket());
                    break;
                case 2: Capture("02-sand-flow"); break;
                case 3: Capture("06-tutorial-next"); break;
                case 4:
                    PressBucket(UnityEngine.Object.FindObjectOfType<BucketInventoryUI>().GetFirstAccessibleBucket());
                    if (!GameProgressStore.GetBool("sand_tutorial_v2_complete", false))
                        throw new InvalidOperationException("Tutorial did not finish after the second real move.");
                    break;
                case 5:
                    Capture("07-tutorial-complete");
                    Debug.Log("CAMPAIGN_TUTORIAL_PASS: pick, watch, next move and completion verified through real actions.");
                    break;
                case 6:
                    GameProgressStore.SetBool("sand_tutorial_v2_complete", true);
                    GameProgressStore.SetInt("sand_progress_highest_unlocked", 99);
                    ProgressSaveService.Load();
                    foreach (BoosterType booster in Enum.GetValues(typeof(BoosterType))) BoosterService.instance.MarkTutorialSeen(booster);
                    manager.LoadLevel(CampaignProgression.IceLevel - 1);
                    break;
                case 7:
                    Capture("03-new-painting");
                    if (!UnityEngine.Object.FindObjectsOfType<IceShellGraphic>().Any(s => s.Layers > 0))
                        throw new InvalidOperationException("Frozen bucket has no visible ice shell.");
                    foreach (var shell in UnityEngine.Object.FindObjectsOfType<IceShellGraphic>())
                    {
                        var mesh = shell.canvasRenderer.GetMesh();
                        int vertices = mesh != null ? mesh.vertexCount : 0;
                        if (vertices == 0) throw new InvalidOperationException("Ice shell produced no drawable mesh.");
                        Debug.Log($"CAMPAIGN_ICE_MESH: {vertices} vertices, layers={shell.Layers}");
                    }
                    break;
                case 8: manager.OpenCollection(); break;
                case 9: Capture("04-collection"); break;
                case 10:
                    UnityEngine.Object.FindObjectOfType<CampaignGallery>().SendMessage("Close");
                    for (int move = 0; move < CampaignProgression.IntroFrostMoves; move++)
                    {
                        var waiting = manager.Inventory.Buckets.First(b => !b.IsFrozen && manager.Inventory.IsBucketAccessible(b, UnityEngine.Object.FindObjectOfType<LevelLoader>().CurrentLevel));
                        if (!manager.TryPlaceBucket(waiting)) throw new InvalidOperationException("Could not perform thawing move.");
                    }
                    break;
                case 11:
                    if (UnityEngine.Object.FindObjectsOfType<IceShellGraphic>().Any(s => s.Layers > 0))
                        throw new InvalidOperationException("Ice shell remained after the final thawing move.");
                    Capture("05-thawed-buckets");
                    Debug.Log("CAMPAIGN_ICE_VISUAL_PASS: frozen shell visible; a real move thaws the ice and removes the shell.");
                    break;
                case 12:
                    for (int i = 0; i < 2; i++)
                    {
                        var candidate = UnityEngine.Object.FindObjectOfType<BucketInventoryUI>().GetFirstAccessibleBucket();
                        if (candidate == null || !manager.TryPlaceBucket(candidate)) break;
                    }
                    Capture("18-visible-batch-spawn");
                    break;
                case 13: Capture("08-conveyor-active"); break;
                case 14:
                    PressButton("ShopBtn");
                    break;
                case 15: Capture("13-shop-top"); break;
                case 16:
                    UnityEngine.Object.FindObjectOfType<RewardedShop>().GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = 0;
                    break;
                case 17: Capture("14-shop-bottom"); break;
                case 18:
                    PressButton("CloseShop");
                    if (UnityEngine.Object.FindObjectOfType<RewardedShop>() != null) throw new InvalidOperationException("Shop did not close.");
                    Debug.Log("SHOP_VISUAL_PASS: opened, scrolled and closed without purchases.");
                    break;
                case 19:
                    PressButton("Booster_Magnet");
                    break;
                case 20:
                    if (BoosterService.instance.ActiveTargetMode != BoosterType.Magnet) throw new InvalidOperationException("Booster button did not activate its existing target mode.");
                    Capture("15-booster-selected");
                    break;
                case 21:
                    BoosterService.instance.CancelTargetMode();
                    PressButton("ResetButton");
                    break;
                case 22:
                    if (manager.CurrentLevelIndex != CampaignProgression.IceLevel - 1 || grid.CountRemaining() != UnityEngine.Object.FindObjectOfType<LevelLoader>().CurrentLevel.CountTotalLevelCells())
                        throw new InvalidOperationException("Restart button did not restore the current painting.");
                    Capture("16-restart-and-controls");
                    Debug.Log("GAME_CONTROLS_PASS: booster activation/cancel and real restart preserve existing behavior.");
                    break;
                default:
                    BucketPresentationObservations.AssertComplete();
                    if (errors.Count > 0) throw new InvalidOperationException("Runtime errors during preview: " + string.Join("\n", errors));
                    File.WriteAllText("tasks/campaign-visual.md", "# Editor visual smoke check\n\nPortrait game captures in `tasks/previews`. Uses an in-memory profile; no player progress changes.\n\n" +
                        (errors.Count == 0 ? "PASS: no runtime errors during capture.\n" : "ERRORS:\n" + string.Join("\n", errors)));
                    Debug.Log("CAMPAIGN_VISUAL_CAPTURE_COMPLETE");
                    EditorApplication.isPlaying = false;
                    return;
            }
            next = EditorApplication.timeSinceStartup + 4;
        }
        catch (Exception exception)
        {
            File.WriteAllText("tasks/campaign-visual-error.txt", exception.ToString());
            Debug.LogException(exception);
            EditorApplication.isPlaying = false;
        }
    }

    private static void ResumeAfterTextImport(string packageName)
    {
        AssetDatabase.importPackageCompleted -= ResumeAfterTextImport;
        EditorApplication.delayCall += Run;
    }

    private static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("tasks/previews/" + name + ".png"));
        Debug.Log("CAMPAIGN_CAPTURE: " + name + " " + Screen.width + "x" + Screen.height);
    }

    public static void PressBucket(Bucket bucket)
    {
        var view = UnityEngine.Object.FindObjectsOfType<BucketView>().First(v => v.Bucket == bucket);
        PressButton(view.GetComponent<UnityEngine.UI.Button>());
    }

    public static void PressButton(string name)
        => PressButton(UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().First(b => b.name == name));

    public static void PressButton(UnityEngine.UI.Button button)
    {
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events == null || !button.IsInteractable()) throw new InvalidOperationException("Control is not ready for a pointer click: " + button.name);
        RectTransform rect = (RectTransform)button.transform;
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new UnityEngine.EventSystems.PointerEventData(events)
        {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
        };
        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(pointer, hits);
        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() != button)
            throw new InvalidOperationException("A UI layer blocks the control: " + button.name + "; hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Debug.Log("UI_POINTER_PASS: " + button.name);
    }

    public static void SetPortraitGameView()
    {
        Assembly editor = typeof(Editor).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        object sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
        Type sizeType = editor.GetType("UnityEditor.GameViewSize");
        Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
        object size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            new object[] { Enum.Parse(kindType, "FixedResolution"), 540, 960, "Campaign QA portrait" }, null);
        int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        int customs = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        Type viewType = editor.GetType("UnityEditor.GameView");
        EditorWindow view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, builtins + customs);
        view.Focus();
    }
}

