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
    public int LevelCount { get { return levelDatabase != null ? levelDatabase.LevelCount : 0; } }
    public int CampaignLevelIndex => GetDisplayNumber(currentLevelIndex) - 1;
    public bool HasNextLevelInCampaign => currentLevelIndex + 1 < LevelCount && IsSeasonal(currentLevelIndex) == IsSeasonal(currentLevelIndex + 1);

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
        int ordinaryCount = 0, winterCount = 0;
        for (int i = 0; i < LevelCount; i++) { if (IsSeasonal(i)) winterCount++; else ordinaryCount++; }
        ProgressSaveService.Initialize(ordinaryCount);
        WinterProgress.Initialize(winterCount);
        int completed = ProgressSaveService.HighestCompletedIndex;
        int following = completed + 1;
        while (IsSeasonal(following)) following++;
        if(completed >= 0 && following < LevelCount && following > ProgressSaveService.HighestUnlockedIndex)
            ProgressSaveService.MarkLevelCompleted(completed, following);

        SandEconomyHud economyHud = FindObjectOfType<SandEconomyHud>();
        if (economyHud != null)
        {
            economyHud.Bootstrap(this);
        }

        int resume = ProgressSaveService.GetResumeLevelIndex();
        while (IsSeasonal(resume)) resume++;
        if (GameProgressStore.GetInt("sand_selected_campaign", 0) == 1)
            for (int i = 0; i < LevelCount; i++)
                if (IsSeasonal(i) && levelDatabase.GetLevel(i).seasonalNumber == WinterProgress.Resume + 1) { resume = i; break; }
        LoadLevel(resume);
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.W) || levelLoader == null || levelLoader.CurrentLevel == null
            || CampaignGallery.IsOpen || Time.timeScale <= 0f)
        {
            return;
        }

        Analytic.AbandonLevel("debug_skip");
        if (IsSeasonal(currentLevelIndex))
        {
            WinterProgress.Complete(levelDatabase.GetLevel(currentLevelIndex).seasonalNumber - 1);
        }
        else
        {
            int next = currentLevelIndex + 1;
            while (IsSeasonal(next)) next++;
            ProgressSaveService.MarkLevelCompleted(currentLevelIndex, next);
        }

        LoadNextLevel();
    }
#endif

    private void LateUpdate()
    {
        // A cup enters at the rear/outlet first, then advances after the current
        // movement finishes, without requiring another player action.
        if (gameState == GameState.Playing && bucketInventory != null && bucketInventory.NeedsSettling
            && levelLoader != null && (bucketInventoryUI == null || !bucketInventoryUI.IsMoving))
            bucketInventory.TryDispenseAll(levelLoader.CurrentLevel);

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

        if (FindObjectOfType<LevelPresentation>() == null) polish.AddComponent<LevelPresentation>();
        if (FindObjectOfType<GameAudio>() == null) polish.AddComponent<GameAudio>();
        if (FindObjectOfType<AudioSettingsMenu>() == null) polish.AddComponent<AudioSettingsMenu>();

        if (FindObjectOfType<SandEconomyHud>() == null)
        {
            polish.AddComponent<SandEconomyHud>();
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
        if (!IsLevelUnlocked(index))
        {
            if (IsSeasonal(index))
            {
                int number = WinterProgress.HighestUnlocked + 1;
                for (int i = 0; i < LevelCount; i++)
                    if (IsSeasonal(i) && levelDatabase.GetLevel(i).seasonalNumber == number) { index = i; break; }
            }
            else index = Mathf.Clamp(ProgressSaveService.HighestUnlockedIndex, 0, maxIndex);
        }

        GameIsReady.SetLevelPlaying(false);
        currentLevelIndex = index;
        StopCoroutine(nameof(PresentLevelComplete));
        FindObjectOfType<SandPourAnimator>()?.ClearPour();
        GameAudio.Instance?.ResetLevelAudio();
        // Keep the saved resume point when jumping to a level through the Editor override.
        if (!GameDebugSettings.UnlockAllLevels || IsLevelUnlockedByProgress(currentLevelIndex))
        {
            if (IsSeasonal(currentLevelIndex)) WinterProgress.SetCurrent(levelDatabase.GetLevel(currentLevelIndex).seasonalNumber - 1);
            else ProgressSaveService.SetCurrentLevel(currentLevelIndex);
            GameProgressStore.SetInt("sand_selected_campaign", IsSeasonal(currentLevelIndex) ? 1 : 0);
            GameProgressStore.Save();
        }
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
        if (level.campaignLevel) bucketSettings = level.bucketSettingsOverride;

        Analytic.BeginLevel(currentLevelIndex, level, IsLevelCompleted(currentLevelIndex));

        Random.State randomState = Random.state;
        if (level.campaignLevel) Random.InitState(level.campaignSeed);
        try
        {
            bucketInventory.BuildFromLevel(level,
                sandRenderer != null ? sandRenderer.SubGrainsPerAxis : 1, bucketSettings);
        }
        finally { Random.state = randomState; }

        bucketInventory.TryDispenseAll(level);

        if (conveyorBelt != null)
        {
            conveyorBelt.Configure(bucketInventory.TotalBucketCount);
            conveyorBelt.ConfigureLevel(level);
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

        SandEconomyHud economyHud = FindObjectOfType<SandEconomyHud>();
        if (economyHud != null)
        {
            economyHud.OnLevelLoaded(CampaignLevelIndex);
        }

        HandleInventoryChanged();
        HandleConveyorChanged();

        GameIsReady.SetLevelPlaying(true);
        if (winConditionChecker != null)
        {
            winConditionChecker.CheckConditions();
        }
    }

    public void RestartLevel()
    {
        Analytic.RestartLevel();
        LoadLevel(currentLevelIndex);
    }

    public void LoadNextLevel()
    {
        if (levelDatabase == null)
        {
            return;
        }

        int nextIndex = currentLevelIndex + 1;
        if (IsSeasonal(currentLevelIndex))
        {
            if (IsSeasonal(nextIndex)) LoadLevel(nextIndex);
            else OpenCollection();
            return;
        }
        while (IsSeasonal(nextIndex)) nextIndex++;
        if (nextIndex >= levelDatabase.LevelCount)
        {
            OpenCollection();
            return;
        }

        if (!IsLevelUnlocked(nextIndex))
        {
            nextIndex = Mathf.Clamp(ProgressSaveService.HighestUnlockedIndex, 0, levelDatabase.LevelCount - 1);
        }

        LoadLevel(nextIndex);
    }

    public void OpenCollection() { CampaignGallery.Show(levelDatabase, this); }

    public bool IsLevelUnlocked(int levelIndex)
    {
#if UNITY_EDITOR
        if (GameDebugSettings.UnlockAllLevels)
        {
            return levelDatabase != null && levelDatabase.GetLevel(levelIndex) != null;
        }
#endif
        return IsLevelUnlockedByProgress(levelIndex);
    }

    private bool IsLevelUnlockedByProgress(int levelIndex)
    {
        return IsSeasonal(levelIndex) ? WinterProgress.IsUnlocked(levelDatabase.GetLevel(levelIndex).seasonalNumber - 1) : ProgressSaveService.IsLevelUnlocked(levelIndex);
    }

    public bool IsLevelCompleted(int levelIndex)
    {
        return IsSeasonal(levelIndex) ? WinterProgress.IsCompleted(levelDatabase.GetLevel(levelIndex).seasonalNumber - 1)
            : ProgressSaveService.IsLevelCompleted(levelIndex);
    }

    public bool IsSeasonal(int index) => levelDatabase != null && levelDatabase.GetLevel(index) != null && levelDatabase.GetLevel(index).seasonalNumber > 0;
    public string LevelCaption => CampaignText.Get("level") + " " + GetDisplayNumber(currentLevelIndex);
    public int GetDisplayNumber(int index)
    {
        if(IsSeasonal(index)) return levelDatabase.GetLevel(index).seasonalNumber;
        int number=0;
        for(int i=0;i<=index;i++) if(!IsSeasonal(i)) number++;
        return number;
    }

    public bool TryPlaceBucket(Bucket bucket)
    {
        if (gameState != GameState.Playing || conveyorBelt == null || !IsBucketInteractionAllowed(bucket))
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

        Analytic.BucketPlaced();
        if (tutorialController != null)
        {
            tutorialController.HandleBucketPlaced(bucket);
        }

        return true;
    }

    public bool IsBucketInteractionAllowed(Bucket bucket)
    {
        if (SandEconomyHud.instance != null && SandEconomyHud.instance.BlocksInventoryForTutorial)
        {
            return false;
        }

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
        bucketInventory?.HandleBucketFilled(bucket);
        if (tutorialController != null) tutorialController.HandleBucketFilled(bucket);
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
        GameIsReady.SetLevelPlaying(false);
        Analytic.CompleteLevel();
        if (tutorialController != null) tutorialController.HandleLevelCompleted();
        if (IsSeasonal(currentLevelIndex))
        {
            WinterProgress.Complete(levelDatabase.GetLevel(currentLevelIndex).seasonalNumber - 1);
        }
        else
        {
            int next = currentLevelIndex + 1;
            while (IsSeasonal(next)) next++;
            ProgressSaveService.MarkLevelCompleted(currentLevelIndex, next);
        }
        StartCoroutine(nameof(PresentLevelComplete));
    }

    private System.Collections.IEnumerator PresentLevelComplete()
    {
        // Let the last bucket finish its exit before covering the belt with rewards.
        yield return new WaitForSeconds(BucketCompletionAnimation.Duration);
        SandEconomyHud economyHud = FindObjectOfType<SandEconomyHud>();
        if (economyHud != null)
        {
            economyHud.ShowLevelComplete();
        }
        else if (gameUI != null)
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

        if (levelAsset != null && levelAsset.campaignLevel)
        {
            runtimeLevelInstance = CampaignRunFactory.Create(levelAsset, currentLevelIndex);
            levelLoader.SetCurrentLevel(runtimeLevelInstance);
            return runtimeLevelInstance;
        }

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

        if (runtimeLevelInstance.campaignLevel) Destroy(runtimeLevelInstance.bucketSettingsOverride);
        Destroy(runtimeLevelInstance);
        runtimeLevelInstance = null;
    }
}
