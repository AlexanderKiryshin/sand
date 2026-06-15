using UnityEngine;

public class SandConveyorMapper : MonoBehaviour
{
    [SerializeField] private RectTransform sandPanel;
    [SerializeField] private RectTransform sandImage;
    [SerializeField] private RectTransform beltRect;
    [SerializeField] private RectTransform bucketContainer;

    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        EnsureReferences();
    }

    public void EnsureReferences()
    {
        if (sandPanel == null)
        {
            GameObject panel = GameObject.Find("SandPanel");
            if (panel != null)
            {
                sandPanel = panel.GetComponent<RectTransform>();
            }
        }

        if (sandImage == null && sandPanel != null)
        {
            Transform image = sandPanel.Find("SandImage");
            if (image != null)
            {
                sandImage = image.GetComponent<RectTransform>();
            }
        }

        if (beltRect == null)
        {
            GameObject belt = GameObject.Find("ConveyorPanel");
            if (belt != null)
            {
                beltRect = belt.GetComponent<RectTransform>();
            }
        }

        if (bucketContainer == null && beltRect != null)
        {
            Transform container = beltRect.Find("ConveyorBuckets");
            if (container != null)
            {
                bucketContainer = container.GetComponent<RectTransform>();
            }
        }

        if (bucketContainer == null && beltRect != null)
        {
            bucketContainer = beltRect;
        }
    }

    public bool IsColumnUnderBucket(float bucketBeltX, int gridX, int gridWidth)
    {
        EnsureReferences();
        RectTransform beltSpace = bucketContainer != null ? bucketContainer : beltRect;

        if (sandImage == null || beltSpace == null)
        {
            return false;
        }

        float bucketSandX = GetBucketSandLocalX(bucketBeltX);
        float columnCenter = GetColumnLocalX(gridX, gridWidth);
        float threshold = GetColumnHalfWidth(gridWidth) * 0.85f;
        Rect rect = sandImage.rect;
        float contentLeft = rect.xMin + GetSandPadding();
        float contentRight = rect.xMax - GetSandPadding();
        float margin = GetColumnHalfWidth(gridWidth);
        bool inBounds = bucketSandX >= contentLeft - margin && bucketSandX <= contentRight + margin;
        bool aligned = Mathf.Abs(bucketSandX - columnCenter) <= threshold;

        if (!inBounds)
        {
            return false;
        }

        return aligned;
    }

    public float GetBucketSandLocalX(float bucketBeltX)
    {
        EnsureReferences();
        RectTransform beltSpace = bucketContainer != null ? bucketContainer : beltRect;
        if (beltSpace == null)
        {
            return 0f;
        }

        return BeltLocalXToSandLocalX(bucketBeltX, beltSpace);
    }

    public bool IsBucketOverSand(float bucketBeltX, int gridWidth)
    {
        EnsureReferences();
        if (sandImage == null || gridWidth <= 0)
        {
            return false;
        }

        float bucketSandX = GetBucketSandLocalX(bucketBeltX);
        Rect rect = sandImage.rect;
        float contentLeft = rect.xMin + GetSandPadding();
        float contentRight = rect.xMax - GetSandPadding();
        float margin = GetColumnHalfWidth(gridWidth);
        return bucketSandX >= contentLeft - margin && bucketSandX <= contentRight + margin;
    }

    public float GetColumnAlignmentError(float bucketBeltX, int gridX, int gridWidth)
    {
        float bucketSandX = GetBucketSandLocalX(bucketBeltX);
        float columnCenter = GetColumnLocalX(gridX, gridWidth);
        return Mathf.Abs(bucketSandX - columnCenter);
    }

    public Vector3 GetPourStartWorldPosition(int gridX, int gridWidth)
    {
        EnsureReferences();
        if (sandImage == null)
        {
            return Vector3.zero;
        }

        float localX = GetColumnLocalX(gridX, gridWidth);
        float localY = sandImage.rect.yMin + 1f;
        return sandImage.TransformPoint(new Vector3(localX, localY, 0f));
    }

    public float GetPourSpanWorldY(int gridX, int gridWidth, Vector3 bucketTargetWorld)
    {
        Vector3 start = GetPourStartWorldPosition(gridX, gridWidth);
        return start.y - bucketTargetWorld.y;
    }

    public Vector2 GetColumnCanvasPosition(int gridX, int gridWidth)
    {
        float localX = GetColumnLocalX(gridX, gridWidth);
        return SandLocalToCanvasPosition(new Vector2(localX, 0f));
    }

    public float GetCellScreenSize(int gridWidth)
    {
        EnsureReferences();
        if (sandImage == null || gridWidth <= 0)
        {
            return 8f;
        }

        float contentWidth = sandImage.rect.width - GetSandPadding() * 2f;
        return contentWidth / gridWidth;
    }

    private float GetColumnLocalX(int gridX, int gridWidth)
    {
        Rect rect = sandImage.rect;
        float contentLeft = rect.xMin + GetSandPadding();
        float contentWidth = rect.width - GetSandPadding() * 2f;
        float normalized = (gridX + 0.5f) / gridWidth;
        return contentLeft + normalized * contentWidth;
    }

    private float GetColumnHalfWidth(int gridWidth)
    {
        Rect rect = sandImage.rect;
        float contentWidth = rect.width - GetSandPadding() * 2f;
        return contentWidth / gridWidth * 0.5f;
    }

    private float GetSandPadding()
    {
        return 12f;
    }

    private float BeltLocalXToSandLocalX(float beltX, RectTransform beltSpace)
    {
        Vector3 bucketWorld = beltSpace.TransformPoint(new Vector3(beltX, 0f, 0f));
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 sandLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sandImage,
            RectTransformUtility.WorldToScreenPoint(eventCamera, bucketWorld),
            eventCamera,
            out sandLocal);
        return sandLocal.x;
    }

    private Vector2 SandLocalToCanvasPosition(Vector2 sandLocal)
    {
        Vector3 world = sandImage.TransformPoint(new Vector3(sandLocal.x, sandLocal.y, 0f));
        return WorldToCanvasLocal(world);
    }

    private Vector2 WorldToCanvasLocal(Vector3 world)
    {
        if (canvas == null)
        {
            return Vector2.zero;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, world);
        Vector2 canvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out canvasPos);
        return canvasPos;
    }
}
