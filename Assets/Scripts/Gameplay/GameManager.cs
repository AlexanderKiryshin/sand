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
    private int currentLevelIndex;
    private GameState gameState = GameState.Playing;

    public GameState State { get { return gameState; } }
    public int CurrentLevelIndex { get { return currentLevelIndex; } }
    public BucketInventory Inventory { get { return bucketInventory; } }

    private void Awake()
    {
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
        LoadLevel(0);
    }

    private void EnsureConveyorView()
    {
        ApplyRuntimeLayout();

        if (FindObjectOfType<SandPourAnimator>() == null)
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

            polish.AddComponent<SandPourAnimator>();
        }

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

    private static void ApplyRuntimeLayout()
    {
        SetAnchor("SandPanel", 0.62f, new Vector2(280f, 280f));
        SetAnchor("ConveyorPanel", 0.38f, new Vector2(400f, 72f));
        SetAnchor("BucketInventory", 0.14f, new Vector2(110f, 110f));
    }

    private static void SetAnchor(string objectName, float anchorY, Vector2 size)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null)
        {
            return;
        }

        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.sizeDelta = size;
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
    }

    public void LoadLevel(int index)
    {
        if (levelDatabase == null || levelDatabase.LevelCount == 0)
        {
            Debug.LogError("LevelDatabase is empty.");
            return;
        }

        currentLevelIndex = Mathf.Clamp(index, 0, levelDatabase.LevelCount - 1);
        gameState = GameState.Playing;

        LevelData level = levelLoader.LoadLevel(currentLevelIndex);
        if (level == null)
        {
            return;
        }

        bucketInventory.BuildFromLevel(
            level,
            sandRenderer != null ? sandRenderer.SubGrainsPerAxis : 1);

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

        HandleGridChanged();
        HandleInventoryChanged();
        HandleConveyorChanged();
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

        LoadLevel(nextIndex);
    }

    public bool TryPlaceBucket(Bucket bucket)
    {
        if (gameState != GameState.Playing || conveyorBelt == null)
        {
            return false;
        }

        if (!bucketInventory.TryTakeBucket(bucket, conveyorBelt))
        {
            return false;
        }

        if (sandSimulator != null)
        {
            sandSimulator.Wake();
        }

        return true;
    }

    private void HandleGridChanged()
    {
        if (sandRenderer != null)
        {
            sandRenderer.RefreshDirty();
        }

        if (winConditionChecker != null)
        {
            winConditionChecker.CheckConditions();
        }
    }

    private void HandleInventoryChanged()
    {
        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Refresh();
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
        if (gameUI != null)
        {
            gameUI.ShowLevelComplete();
        }
    }
}
