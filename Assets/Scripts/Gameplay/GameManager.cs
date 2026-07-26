using UnityEngine;

public enum GameState
{
    Playing,
    Paused,
    LevelComplete,
    LevelFailed
}

public class GameManager : MonoBehaviour
{
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private FallingSandSimulator sandSimulator;
    [SerializeField] private SandRenderer sandRenderer;
    [SerializeField] private ConveyorBelt conveyorBelt;
    [SerializeField] private SandCollector sandCollector;
    [SerializeField] private WinConditionChecker winConditionChecker;
    [SerializeField] private GameUI gameUI;
    [SerializeField] private BucketInventoryUI bucketInventoryUI;

    private BucketInventory bucketInventory;
    private TutorialController tutorialController;
    private int currentLevelIndex;
    private GameState gameState = GameState.Playing;
    private bool sandRenderPending;
    private LevelData runtimeLevelInstance;

    public GameState State { get { return gameState; } }
    public int CurrentLevelIndex { get { return currentLevelIndex; } }
    public BucketInventory Inventory { get { return bucketInventory; } }

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        bucketInventory = new BucketInventory();
    }

    private void Start()
    {
        if (levelDatabase == null || levelDatabase.LevelCount == 0)
        {
            levelDatabase = DefaultLevelFactory.CreateDatabaseWithHeartLevel();
        }

        BindEvents();
        EnsureConveyorView();
        ResolveTutorialController();
        ProgressSaveService.Initialize(levelDatabase.LevelCount);
        LoadLevel(ProgressSaveService.GetResumeLevelIndex());
    }

    private void LateUpdate()
    {
        if (!sandRenderPending || sandRenderer == null)
        {
            return;
        }

        sandRenderPending = false;
        sandRenderer.RefreshDirty();
    }

    private void EnsureConveyorView()
    {
        ApplyRuntimeLayout();
        EnsurePolishComponents();

        if (FindObjectOfType<ConveyorBeltView>() != null)
        {
            return;
        }

        GameObject conveyorPanel = GameObject.Find("ConveyorPanel");
        if (conveyorPanel != null)
        {
            conveyorPanel.AddComponent<ConveyorBeltView>();
        }
    }

    private static void EnsurePolishComponents()
    {
        GameObject polish = GameObject.Find("Polish");
        if (polish == null)
        {
            polish = new GameObject("Polish");
        }

        if (FindObjectOfType<SandConveyorMapper>() == null)
        {
            polish.AddComponent<SandConveyorMapper>();
        }

        if (FindObjectOfType<SandPourAnimator>() == null)
        {
            polish.AddComponent<SandPourAnimator>();
        }

        if (FindObjectOfType<LevelIntroSandAnimator>() == null)
        {
            polish.AddComponent<LevelIntroSandAnimator>();
        }

        if (FindObjectOfType<TutorialController>() == null)
        {
            polish.AddComponent<TutorialController>();
        }
    }

    private static void ApplyRuntimeLayout()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        MobilePortraitLayout layout = canvas.GetComponent<MobilePortraitLayout>();
        if (layout == null)
        {
            layout = canvas.gameObject.AddComponent<MobilePortraitLayout>();
        }

        layout.ApplyLayout();
    }

    private void BindEvents()
    {
        if (sandSimulator != null)
        {
            sandSimulator.OnGridChanged += HandleGridChanged;
        }

        if (conveyorBelt != null)
        {
            conveyorBelt.OnConveyorChanged += HandleConveyorChanged;
            conveyorBelt.OnBucketFilled += HandleBucketFilled;
            conveyorBelt.OnBucketReturned += HandleBucketReturned;
        }

        if (winConditionChecker != null)
        {
            winConditionChecker.Initialize(levelLoader, conveyorBelt, bucketInventory);
            winConditionChecker.OnLevelComplete += HandleLevelComplete;
        }

        bucketInventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void OnDestroy()
    {
        if (sandSimulator != null)
        {
            sandSimulator.OnGridChanged -= HandleGridChanged;
        }

        if (conveyorBelt != null)
        {
            conveyorBelt.OnConveyorChanged -= HandleConveyorChanged;
            conveyorBelt.OnBucketFilled -= HandleBucketFilled;
            conveyorBelt.OnBucketReturned -= HandleBucketReturned;
        }

        if (winConditionChecker != null)
        {
            winConditionChecker.OnLevelComplete -= HandleLevelComplete;
        }

        bucketInventory.OnInventoryChanged -= HandleInventoryChanged;
        ClearRuntimeLevelInstance();
    }

    public void LoadLevel(int index)
    {
        if (levelDatabase == null || levelDatabase.LevelCount == 0)
        {
            Debug.LogError("LevelDatabase is empty.");
            return;
        }

        int maxIndex = levelDatabase.LevelCount - 1;
        index = Mathf.Clamp(index, 0, maxIndex);
        if (!ProgressSaveService.IsLevelUnlocked(index))
        {
            index = Mathf.Clamp(ProgressSaveService.HighestUnlockedIndex, 0, maxIndex);
        }

        currentLevelIndex = index;
        ProgressSaveService.SetCurrentLevel(currentLevelIndex);
        gameState = GameState.Playing;
        sandRenderPending = false;

        LevelData level = levelLoader.LoadLevel(currentLevelIndex);
        if (level == null)
        {
            return;
        }

        LevelSegmentRules bucketSettings = levelDatabase != null
            ? levelDatabase.GetBucketSettingsForLevel(currentLevelIndex, level)
            : null;
        level = PrepareRuntimeLevel(level, bucketSettings);

        bucketInventory.BuildFromLevel(
            level,
            sandRenderer != null ? sandRenderer.SubGrainsPerAxis : 1,
            bucketSettings);

        bucketInventory.TryDispenseAll(level);

        if (conveyorBelt != null)
        {
            conveyorBelt.Configure(bucketInventory.TotalBucketCount);
        }

        if (gameUI != null)
        {
            gameUI.Bind(this, conveyorBelt, level);
        }

        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Bind(bucketInventory, conveyorBelt, level, this);
        }

        if (sandRenderer != null)
        {
            LevelIntroSandAnimator introAnimator = FindObjectOfType<LevelIntroSandAnimator>();
            if (introAnimator != null)
            {
                introAnimator.PlayIntro(level, sandRenderer, sandSimulator);
            }
            else
            {
                sandRenderer.PlayIntroPour(1.2f);
            }
        }

        ResolveTutorialController();
        if (tutorialController != null)
        {
            tutorialController.HandleLevelLoaded(this, gameUI, bucketInventoryUI, conveyorBelt);
        }

        HandleInventoryChanged();
        HandleConveyorChanged();

        if (winConditionChecker != null)
        {
            winConditionChecker.CheckConditions();
        }
    }

    public void RestartLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    public void LoadNextLevel()
    {
        if (levelDatabase == null)
        {
            return;
        }

        int nextIndex = currentLevelIndex + 1;
        if (nextIndex >= levelDatabase.LevelCount)
        {
            nextIndex = 0;
        }

        if (!ProgressSaveService.IsLevelUnlocked(nextIndex))
        {
            nextIndex = Mathf.Clamp(ProgressSaveService.HighestUnlockedIndex, 0, levelDatabase.LevelCount - 1);
        }

        LoadLevel(nextIndex);
    }

    public bool IsLevelUnlocked(int levelIndex)
    {
        return ProgressSaveService.IsLevelUnlocked(levelIndex);
    }

    public bool IsLevelCompleted(int levelIndex)
    {
        return ProgressSaveService.IsLevelCompleted(levelIndex);
    }

    public bool TryPlaceBucket(Bucket bucket)
    {
        if (gameState != GameState.Playing || conveyorBelt == null)
        {
            return false;
        }

        if (!bucketInventory.TryTakeBucket(bucket, conveyorBelt, levelLoader != null ? levelLoader.CurrentLevel : null))
        {
            return false;
        }

        if (sandSimulator != null)
        {
            sandSimulator.Wake();
        }

        if (tutorialController != null)
        {
            tutorialController.HandleBucketPlaced(bucket);
        }

        return true;
    }

    public bool IsBucketInteractionAllowed(Bucket bucket)
    {
        return tutorialController == null || tutorialController.IsBucketInteractionAllowed(bucket);
    }

    private void HandleGridChanged()
    {
        sandRenderPending = true;

        if (winConditionChecker != null)
        {
            winConditionChecker.CheckConditions();
        }
    }

    private void HandleInventoryChanged()
    {
        LevelData level = levelLoader != null ? levelLoader.CurrentLevel : null;
        if (level != null)
        {
            bucketInventory.TryDispenseAll(level);
        }

        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Refresh();
        }

        if (conveyorBelt != null)
        {
            conveyorBelt.SetAllBucketsDeployed(bucketInventory.CountRemaining() == 0);
        }

        if (winConditionChecker != null)
        {
            winConditionChecker.CheckConditions();
        }
    }

    private void HandleConveyorChanged()
    {
        if (sandSimulator != null)
        {
            sandSimulator.Wake();
        }

        if (gameUI != null)
        {
            gameUI.RefreshConveyor();
        }
    }

    private void HandleBucketFilled(Bucket bucket)
    {
        if (winConditionChecker != null)
        {
            winConditionChecker.CheckConditions();
        }
    }

    private void HandleBucketReturned(Bucket bucket)
    {
        if (bucketInventory != null)
        {
            bucketInventory.ReturnBucket(bucket);
        }
    }

    private void HandleLevelComplete()
    {
        if (gameState == GameState.LevelComplete)
        {
            return;
        }

        gameState = GameState.LevelComplete;
        ProgressSaveService.MarkLevelCompleted(currentLevelIndex);
        if (gameUI != null)
        {
            gameUI.ShowLevelComplete();
        }
    }

    private void ResolveTutorialController()
    {
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<TutorialController>();
        }
    }

    private LevelData PrepareRuntimeLevel(LevelData levelAsset, LevelSegmentRules bucketSettings)
    {
        ClearRuntimeLevelInstance();

        if (levelAsset == null
            || bucketSettings == null
            || bucketSettings.fillMode != BucketLayoutFillMode.RandomComponents)
        {
            return levelAsset;
        }

        runtimeLevelInstance = Instantiate(levelAsset);
        runtimeLevelInstance.name = levelAsset.name + " (Runtime)";
        LevelBucketLayoutGenerator.Apply(runtimeLevelInstance, bucketSettings);
        if (levelLoader != null)
        {
            levelLoader.SetCurrentLevel(runtimeLevelInstance);
        }

        return runtimeLevelInstance;
    }

    private void ClearRuntimeLevelInstance()
    {
        if (runtimeLevelInstance == null)
        {
            return;
        }

        Destroy(runtimeLevelInstance);
        runtimeLevelInstance = null;
    }
}
