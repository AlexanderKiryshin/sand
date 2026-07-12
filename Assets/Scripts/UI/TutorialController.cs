using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    [SerializeField] private bool tutorialOnFirstLevelOnly = true;
    [SerializeField] private float ringPulseScale = 0.12f;
    [SerializeField] private float ringPulseSpeed = 3.2f;
    [SerializeField] private float handTapSpeed = 4.5f;
    [SerializeField] private float handSizeMultiplier = 1.15f;

    private GameManager gameManager;
    private GameUI gameUI;
    private BucketInventoryUI bucketInventoryUI;
    private LevelIntroSandAnimator introAnimator;
    private Canvas canvas;
    private RectTransform overlayRoot;
    private RectTransform highlightRingRect;
    private Image highlightRingImage;
    private RectTransform handRect;
    private Image handImage;
    private Bucket targetBucket;
    private bool tutorialEnabled;
    private bool stepActive;
    private bool completed;
    private float animationTime;

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
                bucketInventoryUI.ClearTutorialFocus();
            }

            SetTutorialVisualsVisible(false);
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

        Vector2 bucketCenter;
        float bucketSize;
        if (!bucketInventoryUI.TryGetBucketCanvasPosition(targetBucket, out bucketCenter, out bucketSize))
        {
            SetTutorialVisualsVisible(false);
            return;
        }

        EnsureOverlay();
        SetTutorialVisualsVisible(true);
        UpdateHand(bucketCenter, bucketSize);
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
                gameUI.SetHintVisible(false);
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
            gameUI.SetHintVisible(false);
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
            gameUI.SetControlsInteractable(false);
            gameUI.SetHintVisible(false);
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
            gameUI.SetHintVisible(false);
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
            bucketInventoryUI.ClearTutorialFocus();
        }

        SetTutorialVisualsVisible(false);
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
        EnsureHighlightRing();
        EnsureHand();
    }

    private void EnsureHighlightRing()
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (highlightRingRect == null)
        {
            Transform existing = overlayRoot.Find("TutorialHighlight");
            if (existing != null)
            {
                highlightRingRect = existing.GetComponent<RectTransform>();
            }
        }

        if (highlightRingRect == null)
        {
            GameObject ringObject = new GameObject("TutorialHighlight", typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(overlayRoot, false);
            highlightRingRect = ringObject.GetComponent<RectTransform>();
            highlightRingRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRingRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRingRect.pivot = new Vector2(0.5f, 0.5f);

            highlightRingImage = ringObject.GetComponent<Image>();
            highlightRingImage.sprite = CreateRingSprite();
            highlightRingImage.color = new Color(1f, 0.92f, 0.2f, 0.95f);
            highlightRingImage.raycastTarget = false;
        }
        else if (highlightRingImage == null)
        {
            highlightRingImage = highlightRingRect.GetComponent<Image>();
        }
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
            handRect.pivot = TutorialHandSprite.Pivot;
            handRect.localRotation = Quaternion.Euler(0f, 0f, -4f);

            handImage = handObject.GetComponent<Image>();
            handImage.sprite = TutorialHandSprite.Get();
            handImage.color = Color.white;
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }
        else if (handImage == null)
        {
            handImage = handRect.GetComponent<Image>();
        }

        if (handImage != null)
        {
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }
    }

    private void UpdateHighlightRing(Vector2 bucketCenter, float bucketSize)
    {
        if (highlightRingRect == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(animationTime * ringPulseSpeed) * ringPulseScale;
        float ringSize = bucketSize * 1.42f * pulse;
        highlightRingRect.anchoredPosition = bucketCenter;
        highlightRingRect.sizeDelta = new Vector2(ringSize, ringSize);

        if (highlightRingImage != null)
        {
            float alphaPulse = 0.72f + Mathf.Sin(animationTime * ringPulseSpeed * 1.2f) * 0.2f;
            highlightRingImage.color = new Color(1f, 0.9f, 0.15f, alphaPulse);
        }
    }

    private void UpdateHand(Vector2 bucketCenter, float bucketSize)
    {
        if (handRect == null)
        {
            return;
        }

        handRect.pivot = TutorialHandSprite.Pivot;

        float handSize = bucketSize * handSizeMultiplier;
        handRect.sizeDelta = new Vector2(handSize, handSize);

        float tap = (Mathf.Sin(animationTime * handTapSpeed) + 1f) * 0.5f;
        Vector2 fingerTarget = bucketCenter + new Vector2(-bucketSize * 0.02f, bucketSize * 0.01f);
        Vector2 restOffset = new Vector2(handSize * 0.1f, handSize * 0.32f);
        Vector2 pressedOffset = new Vector2(handSize * 0.03f, handSize * 0.06f);
        handRect.anchoredPosition = Vector2.Lerp(fingerTarget + restOffset, fingerTarget + pressedOffset, tap);

        float scalePulse = 1f - tap * 0.04f;
        handRect.localScale = new Vector3(scalePulse, scalePulse, 1f);
    }

    private void SetTutorialVisualsVisible(bool visible)
    {
        if (highlightRingRect != null)
        {
            highlightRingRect.gameObject.SetActive(false);
        }

        if (handRect != null)
        {
            handRect.gameObject.SetActive(visible);
        }
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.5f - 1f;
        float innerRadius = outerRadius - 7f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float ring = Mathf.Clamp01(outerRadius - distance) * Mathf.Clamp01(distance - innerRadius);
                float softOuter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((outerRadius - distance) / 2f));
                float softInner = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - innerRadius) / 2f));
                float alpha = ring * softOuter * softInner;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
