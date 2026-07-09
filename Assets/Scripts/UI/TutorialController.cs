using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    [SerializeField] private bool tutorialOnFirstLevelOnly = true;
    [SerializeField] private float bucketBounceHeight = 10f;
    [SerializeField] private float bucketBounceSpeed = 5f;
    [SerializeField] private Vector2 handOffset = new Vector2(44f, 54f);
    [SerializeField] private float handBobHeight = 8f;
    [SerializeField] private float handBobSpeed = 3.4f;
    [SerializeField] private float handScalePulse = 0.06f;

    private GameManager gameManager;
    private GameUI gameUI;
    private BucketInventoryUI bucketInventoryUI;
    private LevelIntroSandAnimator introAnimator;
    private Canvas canvas;
    private RectTransform overlayRoot;
    private RectTransform handRect;
    private Image handImage;
    private Bucket targetBucket;
    private bool tutorialEnabled;
    private bool stepActive;
    private bool completed;
    private float animationTime;

    private const string FirstStepText = "Tap the highlighted bucket to send it onto the conveyor";

    private void Awake()
    {
        canvas = FindObjectOfType<Canvas>();
        EnsureOverlay();
    }

    private void Update()
    {
        if (!stepActive || completed)
        {
            if (bucketInventoryUI != null)
            {
                bucketInventoryUI.ClearTutorialBounce();
            }

            SetHandVisible(false);
            return;
        }

        if (targetBucket == null && bucketInventoryUI != null)
        {
            targetBucket = bucketInventoryUI.GetFirstAccessibleBucket();
        }

        if (targetBucket == null || bucketInventoryUI == null)
        {
            CompleteTutorial();
            return;
        }

        animationTime += Time.deltaTime;
        float bounce = Mathf.Sin(animationTime * bucketBounceSpeed) * bucketBounceHeight;
        bucketInventoryUI.SetTutorialBounce(targetBucket, Mathf.Max(0f, bounce));

        Vector2 targetCanvasPosition;
        if (!bucketInventoryUI.TryGetBucketCanvasPosition(targetBucket, out targetCanvasPosition))
        {
            SetHandVisible(false);
            return;
        }

        EnsureOverlay();
        SetHandVisible(true);
        if (handRect == null)
        {
            return;
        }

        float bob = Mathf.Sin(animationTime * handBobSpeed) * handBobHeight;
        handRect.anchoredPosition = targetCanvasPosition + handOffset + new Vector2(0f, bob);

        float pulse = 1f + Mathf.Sin(animationTime * handBobSpeed * 1.3f) * handScalePulse;
        handRect.localScale = new Vector3(pulse, pulse, 1f);
    }

    private void OnDestroy()
    {
        if (introAnimator != null)
        {
            introAnimator.OnIntroFinished -= HandleIntroFinished;
        }
    }

    public void HandleLevelLoaded(
        GameManager manager,
        GameUI ui,
        BucketInventoryUI inventoryUI,
        ConveyorBelt conveyor)
    {
        gameManager = manager;
        gameUI = ui;
        bucketInventoryUI = inventoryUI;
        completed = false;
        stepActive = false;
        animationTime = 0f;
        targetBucket = null;

        tutorialEnabled = gameManager != null
            && (!tutorialOnFirstLevelOnly || gameManager.CurrentLevelIndex == 0);

        ResolveCanvas();
        EnsureOverlay();
        ResolveIntroAnimator();

        if (!tutorialEnabled)
        {
            DisableTutorialVisuals();
            if (gameUI != null)
            {
                gameUI.SetControlsInteractable(true);
                gameUI.SetHint("Tap a bucket of matching color to collect sand");
            }

            if (bucketInventoryUI != null)
            {
                bucketInventoryUI.Refresh();
            }

            return;
        }

        if (gameUI != null)
        {
            gameUI.SetControlsInteractable(false);
            gameUI.SetHint("Get ready...");
        }

        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Refresh();
        }

        if (introAnimator != null && introAnimator.IsPlaying)
        {
            return;
        }

        BeginFirstStep();
    }

    public bool IsBucketInteractionAllowed(Bucket bucket)
    {
        if (!tutorialEnabled || completed)
        {
            return true;
        }

        if (!stepActive)
        {
            return false;
        }

        return bucket != null && bucket == targetBucket;
    }

    public void HandleBucketPlaced(Bucket bucket)
    {
        if (!tutorialEnabled || completed || !stepActive)
        {
            return;
        }

        if (bucket == null || bucket != targetBucket)
        {
            return;
        }

        CompleteTutorial();
    }

    private void BeginFirstStep()
    {
        if (bucketInventoryUI == null)
        {
            return;
        }

        targetBucket = bucketInventoryUI.GetFirstAccessibleBucket();
        if (targetBucket == null)
        {
            CompleteTutorial();
            return;
        }

        stepActive = true;
        animationTime = 0f;

        if (gameUI != null)
        {
            gameUI.SetHint(FirstStepText);
            gameUI.SetControlsInteractable(false);
        }

        bucketInventoryUI.Refresh();
    }

    private void CompleteTutorial()
    {
        completed = true;
        stepActive = false;
        tutorialEnabled = false;
        targetBucket = null;

        DisableTutorialVisuals();

        if (gameUI != null)
        {
            gameUI.SetControlsInteractable(true);
            gameUI.SetHint("Tap a bucket of matching color to collect sand");
        }

        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Refresh();
        }
    }

    private void DisableTutorialVisuals()
    {
        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.ClearTutorialBounce();
        }

        SetHandVisible(false);
    }

    private void ResolveCanvas()
    {
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }
    }

    private void ResolveIntroAnimator()
    {
        if (introAnimator != null)
        {
            introAnimator.OnIntroFinished -= HandleIntroFinished;
        }

        introAnimator = FindObjectOfType<LevelIntroSandAnimator>();
        if (introAnimator != null)
        {
            introAnimator.OnIntroFinished += HandleIntroFinished;
        }
    }

    private void HandleIntroFinished()
    {
        if (!tutorialEnabled || completed || stepActive)
        {
            return;
        }

        BeginFirstStep();
    }

    private void EnsureOverlay()
    {
        ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        if (overlayRoot == null)
        {
            Transform existing = canvas.transform.Find("TutorialOverlay");
            if (existing != null)
            {
                overlayRoot = existing.GetComponent<RectTransform>();
            }
        }

        if (overlayRoot == null)
        {
            GameObject overlayObject = new GameObject("TutorialOverlay", typeof(RectTransform));
            overlayObject.transform.SetParent(canvas.transform, false);
            overlayRoot = overlayObject.GetComponent<RectTransform>();
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
        }

        overlayRoot.SetAsLastSibling();
        EnsureHand();
    }

    private void EnsureHand()
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (handRect == null)
        {
            Transform existing = overlayRoot.Find("TutorialHand");
            if (existing != null)
            {
                handRect = existing.GetComponent<RectTransform>();
            }
        }

        if (handRect == null)
        {
            GameObject handObject = new GameObject("TutorialHand", typeof(RectTransform), typeof(Image));
            handObject.transform.SetParent(overlayRoot, false);
            handRect = handObject.GetComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.5f, 0.5f);
            handRect.anchorMax = new Vector2(0.5f, 0.5f);
            handRect.pivot = new Vector2(0.2f, 0.8f);
            handRect.sizeDelta = new Vector2(72f, 72f);
            handRect.localRotation = Quaternion.Euler(0f, 0f, -18f);

            handImage = handObject.GetComponent<Image>();
            handImage.sprite = CreateHandSprite();
            handImage.color = new Color(1f, 0.96f, 0.88f, 1f);
            handImage.raycastTarget = false;

            Outline outline = handObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.28f);
            outline.effectDistance = new Vector2(3f, -3f);
        }
        else if (handImage == null)
        {
            handImage = handRect.GetComponent<Image>();
        }

        if (handImage != null)
        {
            handImage.raycastTarget = false;
        }
    }

    private void SetHandVisible(bool visible)
    {
        if (handRect != null)
        {
            handRect.gameObject.SetActive(visible);
        }
    }

    private static Sprite CreateHandSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        // Simple pointing hand silhouette for the tutorial indicator.
        FillRect(texture, 12, 6, 10, 14, fill);
        FillRect(texture, 17, 14, 5, 14, fill);
        FillRect(texture, 9, 10, 4, 10, fill);
        FillRect(texture, 8, 8, 4, 7, fill);
        FillRect(texture, 6, 6, 4, 6, fill);
        FillRect(texture, 5, 4, 16, 4, fill);
        FillRect(texture, 19, 10, 6, 3, fill);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.2f, 0.8f), 100f);
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }
}
