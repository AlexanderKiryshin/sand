using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class GameSceneBuilder
{
    [MenuItem("Window/Sand/Build Game Scene")]
    public static void BuildGameScene()
    {
        string levelPath = "Assets/Levels/Level_01_Heart.asset";
        if (!File.Exists(levelPath))
        {
            SampleLevelGenerator.GenerateHeartLevel();
        }

        LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        if (database == null)
        {
            Debug.LogError("LevelDatabase was not created.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        ConfigureCamera();

        GameObject systems = new GameObject("GameSystems");
        LevelLoader levelLoader = systems.AddComponent<LevelLoader>();
        FallingSandSimulator sandSimulator = systems.AddComponent<FallingSandSimulator>();
        SetPrivateField(sandSimulator, "ticksPerSecond", 32f);
        SetPrivateField(sandSimulator, "maxCollectPerTick", 6);
        SetPrivateField(sandSimulator, "straightTicksBeforeDiagonal", 2);
        SetPrivateField(sandSimulator, "collectRowCount", 8);
        SandCollector sandCollector = systems.AddComponent<SandCollector>();
        ConveyorBelt conveyorBelt = systems.AddComponent<ConveyorBelt>();
        ConveyorSettings conveyorSettings = EnsureConveyorSettings();
        WinConditionChecker winChecker = systems.AddComponent<WinConditionChecker>();
        GameManager gameManager = systems.AddComponent<GameManager>();

        GameObject sandDisplay = new GameObject("SandDisplay");
        SandRenderer sandRenderer = sandDisplay.AddComponent<SandRenderer>();

        Canvas canvas = CreateCanvas();
        RawImage sandImage = CreateSandDisplay(canvas.transform);
        SetPrivateField(sandRenderer, "targetImage", sandImage);
        SetPrivateField(sandRenderer, "pixelsPerCell", 16);
        SetPrivateField(sandRenderer, "subGrainsPerAxis", 4);

        GameUI gameUI = CreateGameUI(canvas.transform);
        BucketInventoryUI inventoryUI = CreateInventoryUI(canvas.transform);
        BucketView bucketPrefab = CreateBucketPrefab();

        SetPrivateField(levelLoader, "levelDatabase", database);
        SetPrivateField(levelLoader, "sandSimulator", sandSimulator);
        SetPrivateField(levelLoader, "sandRenderer", sandRenderer);
        SetPrivateField(levelLoader, "sandCollector", sandCollector);
        SetPrivateField(levelLoader, "conveyorBelt", conveyorBelt);

        SetPrivateField(sandCollector, "conveyorBelt", conveyorBelt);
        SetPrivateField(conveyorBelt, "settings", conveyorSettings);

        SetPrivateField(gameManager, "levelDatabase", database);
        SetPrivateField(gameManager, "levelLoader", levelLoader);
        SetPrivateField(gameManager, "sandSimulator", sandSimulator);
        SetPrivateField(gameManager, "sandRenderer", sandRenderer);
        SetPrivateField(gameManager, "conveyorBelt", conveyorBelt);
        SetPrivateField(gameManager, "sandCollector", sandCollector);
        SetPrivateField(gameManager, "winConditionChecker", winChecker);
        SetPrivateField(gameManager, "gameUI", gameUI);
        SetPrivateField(gameManager, "bucketInventoryUI", inventoryUI);

        SetPrivateField(inventoryUI, "bucketViewPrefab", bucketPrefab);

        GameObject polish = new GameObject("Polish");
        polish.transform.SetParent(systems.transform);
        polish.AddComponent<SandEffects>();
        polish.AddComponent<SandConveyorMapper>();
        polish.AddComponent<SandPourAnimator>();
        polish.AddComponent<GameAudio>();
        LevelSelectUI levelSelect = polish.AddComponent<LevelSelectUI>();
        SetPrivateField(levelSelect, "gameManager", gameManager);
        SetPrivateField(levelSelect, "levelDatabase", database);

        string scenePath = "Assets/Scenes/GameScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("Game scene built at " + scenePath);
    }

    [MenuItem("Window/Sand/Ensure Conveyor Settings")]
    public static ConveyorSettings EnsureConveyorSettingsAsset()
    {
        return EnsureConveyorSettings();
    }

    private static ConveyorSettings EnsureConveyorSettings()
    {
        const string path = "Assets/Settings/ConveyorSettings.asset";
        ConveyorSettings settings = AssetDatabase.LoadAssetAtPath<ConveyorSettings>(path);
        if (settings != null)
        {
            return settings;
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        settings = ScriptableObject.CreateInstance<ConveyorSettings>();
        AssetDatabase.CreateAsset(settings, path);
        return settings;
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.75f, 0.85f, 0.95f);
    }

    private static RawImage CreateSandDisplay(Transform canvas)
    {
        GameObject panel = CreateUIObject("SandPanel", canvas);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.62f);
        rect.anchorMax = new Vector2(0.5f, 0.62f);
        rect.sizeDelta = new Vector2(280f, 280f);
        rect.anchoredPosition = Vector2.zero;

        Image frame = panel.AddComponent<Image>();
        frame.color = new Color(0.92f, 0.94f, 0.97f);

        GameObject imageObject = CreateUIObject("SandImage", panel.transform);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        Stretch(imageRect, 12f);

        RawImage rawImage = imageObject.AddComponent<RawImage>();

        return rawImage;
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();

        return canvas;
    }

    private static GameUI CreateGameUI(Transform canvas)
    {
        GameObject header = CreateUIObject("Header", canvas);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 120f);
        headerRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI levelLabel = CreateText(header.transform, "LevelLabel", "Level 1", 42, TextAlignmentOptions.Center);
        RectTransform levelRect = levelLabel.rectTransform;
        levelRect.anchorMin = new Vector2(0.5f, 0.5f);
        levelRect.anchorMax = new Vector2(0.5f, 0.5f);
        levelRect.anchoredPosition = Vector2.zero;

        GameObject conveyorPanel = CreateUIObject("ConveyorPanel", canvas);
        RectTransform conveyorRect = conveyorPanel.GetComponent<RectTransform>();
        conveyorRect.anchorMin = new Vector2(0.5f, 0.38f);
        conveyorRect.anchorMax = new Vector2(0.5f, 0.38f);
        conveyorRect.sizeDelta = new Vector2(400f, 72f);
        conveyorPanel.AddComponent<ConveyorBeltView>();

        TextMeshProUGUI conveyorLabel = CreateText(conveyorPanel.transform, "ConveyorLabel", "0/5", 28, TextAlignmentOptions.Center);
        Stretch(conveyorLabel.rectTransform, 0f);

        GameObject hint = CreateUIObject("Hint", canvas);
        RectTransform hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.08f);
        hintRect.anchorMax = new Vector2(0.5f, 0.08f);
        hintRect.sizeDelta = new Vector2(700f, 80f);
        Image hintBackground = hint.AddComponent<Image>();
        hintBackground.color = new Color(1f, 0.95f, 0.75f);
        TextMeshProUGUI hintLabel = CreateText(hint.transform, "HintLabel", "Tap on bucket to collect sand", 28, TextAlignmentOptions.Center);
        Stretch(hintLabel.rectTransform, 8f);

        GameObject completePanel = CreateUIObject("LevelCompletePanel", canvas);
        RectTransform completeRect = completePanel.GetComponent<RectTransform>();
        Stretch(completeRect, 0f);
        Image completeBackground = completePanel.AddComponent<Image>();
        completeBackground.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject dialog = CreateUIObject("Dialog", completePanel.transform);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(500f, 280f);
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        Image dialogBackground = dialog.AddComponent<Image>();
        dialogBackground.color = Color.white;

        TextMeshProUGUI completeText = CreateText(dialog.transform, "CompleteText", "Level Complete!", 36, TextAlignmentOptions.Center);
        completeText.rectTransform.anchoredPosition = new Vector2(0f, 60f);

        Button nextButton = CreateButton(dialog.transform, "NextButton", "Next Level", new Vector2(0f, -20f));
        Button resetButton = CreateButton(header.transform, "ResetButton", "Reset", new Vector2(-420f, -20f));

        GameObject uiRoot = CreateUIObject("GameUI", canvas);
        GameUI gameUI = uiRoot.AddComponent<GameUI>();
        SetPrivateField(gameUI, "levelLabel", levelLabel);
        SetPrivateField(gameUI, "conveyorLabel", conveyorLabel);
        SetPrivateField(gameUI, "hintLabel", hintLabel);
        SetPrivateField(gameUI, "levelCompletePanel", completePanel);
        SetPrivateField(gameUI, "resetButton", resetButton);
        SetPrivateField(gameUI, "nextLevelButton", nextButton);

        completePanel.SetActive(false);
        return gameUI;
    }

    private static BucketInventoryUI CreateInventoryUI(Transform canvas)
    {
        GameObject inventory = CreateUIObject("BucketInventory", canvas);
        RectTransform rect = inventory.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.14f);
        rect.anchorMax = new Vector2(0.5f, 0.14f);
        rect.sizeDelta = new Vector2(220f, 220f);

        GridLayoutGroup grid = inventory.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(36f, 36f);
        grid.spacing = new Vector2(4f, 4f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = LevelData.BucketLayoutColumns;

        BucketInventoryUI inventoryUI = inventory.AddComponent<BucketInventoryUI>();
        SetPrivateField(inventoryUI, "bucketContainer", rect);
        SetPrivateField(inventoryUI, "gridLayout", grid);
        return inventoryUI;
    }

    private static BucketView CreateBucketPrefab()
    {
        string prefabFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject bucketObject = CreateUIObject("BucketView", null);
        RectTransform rect = bucketObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(44f, 44f);

        Image image = bucketObject.AddComponent<Image>();
        image.color = Color.white;
        Button button = bucketObject.AddComponent<Button>();

        BucketView view = bucketObject.AddComponent<BucketView>();
        SetPrivateField(view, "bucketImage", image);
        SetPrivateField(view, "button", button);

        string prefabPath = Path.Combine(prefabFolder, "BucketView.prefab");
        PrefabUtility.SaveAsPrefabAsset(bucketObject, prefabPath);
        Object.DestroyImmediate(bucketObject);
        return AssetDatabase.LoadAssetAtPath<BucketView>(prefabPath);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }

        return obj;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUIObject(name, parent);
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.black;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject obj = CreateUIObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 60f);
        rect.anchoredPosition = anchoredPosition;

        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.3f, 0.65f, 0.95f);
        Button button = obj.AddComponent<Button>();

        TextMeshProUGUI text = CreateText(obj.transform, "Label", label, 24, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f);
        text.color = Color.white;

        return button;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static void SetPrivateField(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetPrivateField(Object target, string fieldName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
        {
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetPrivateField(Object target, string fieldName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
        {
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
