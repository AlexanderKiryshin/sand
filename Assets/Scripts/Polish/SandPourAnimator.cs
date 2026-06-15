using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SandPourAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform pourLayer;
    [SerializeField] private int poolSize = 96;
    [SerializeField] private float pourDuration = 0.55f;
    [SerializeField] private Vector2 grainSize = new Vector2(4f, 4f);
    [SerializeField] private bool syncGrainSizeWithRenderer = true;
    [SerializeField] private float grainSizeScale = 1f;
    [SerializeField] private float minGrainSize = 1f;
    [SerializeField] private int grainsPerCollect = 1;

    private SandCollector collector;
    private ConveyorBelt conveyor;
    private ConveyorBeltView beltView;
    private SandConveyorMapper mapper;
    private Canvas canvas;
    private readonly List<PourGrain> activeGrains = new List<PourGrain>();
    private readonly Queue<PourGrain> pool = new Queue<PourGrain>();

    private class PourGrain
    {
        public RectTransform rect;
        public Image image;
        public Vector3 startWorld;
        public Vector3 endWorld;
        public Bucket targetBucket;
        public float elapsed;
        public float duration;
        public bool active;
    }

    private void Awake()
    {
        collector = FindObjectOfType<SandCollector>();
        conveyor = FindObjectOfType<ConveyorBelt>();
        beltView = FindObjectOfType<ConveyorBeltView>();
        mapper = FindObjectOfType<SandConveyorMapper>();
        canvas = FindObjectOfType<Canvas>();

        if (mapper == null && canvas != null)
        {
            mapper = canvas.gameObject.AddComponent<SandConveyorMapper>();
        }

        EnsurePourLayer();
        BuildPool();

        if (collector != null)
        {
            collector.OnGrainCollected += HandleGrainCollected;
        }
    }

    private void Start()
    {
        ApplyGrainSizeToPool();
    }

    private Vector2 GetGrainSize()
    {
        float size = grainSize.x;
        SandRenderer sandRenderer = FindObjectOfType<SandRenderer>();

        LevelLoader loader = FindObjectOfType<LevelLoader>();
        if (mapper != null && loader != null && loader.CurrentLevel != null)
        {
            mapper.EnsureReferences();
            float cellSize = mapper.GetCellScreenSize(loader.CurrentLevel.gridWidth);
            if (sandRenderer != null && sandRenderer.SubGrainsPerAxis > 0)
            {
                size = cellSize * sandRenderer.SubGrainScreenFraction;
            }
            else
            {
                size = cellSize;
            }
        }
        else if (syncGrainSizeWithRenderer && sandRenderer != null)
        {
            size = (float)sandRenderer.DisplayScale / sandRenderer.SubGrainsPerAxis;
        }

        size *= grainSizeScale;
        size = Mathf.Max(minGrainSize, size);
        grainSize = new Vector2(size, size);
        return grainSize;
    }

    private void ApplyGrainSizeToPool()
    {
        Vector2 size = GetGrainSize();
        foreach (PourGrain grain in pool)
        {
            if (grain.rect != null)
            {
                grain.rect.sizeDelta = size;
            }
        }

        for (int i = 0; i < activeGrains.Count; i++)
        {
            if (activeGrains[i].rect != null)
            {
                activeGrains[i].rect.sizeDelta = size;
            }
        }
    }

    private void OnDestroy()
    {
        if (collector != null)
        {
            collector.OnGrainCollected -= HandleGrainCollected;
        }
    }

    private void Update()
    {
        for (int i = activeGrains.Count - 1; i >= 0; i--)
        {
            PourGrain grain = activeGrains[i];
            if (grain.targetBucket != null && beltView != null)
            {
                beltView.TryGetBucketPourTargetWorldPosition(grain.targetBucket, out grain.endWorld);
            }

            grain.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(grain.elapsed / grain.duration);
            float eased = t * t;
            grain.rect.position = Vector3.Lerp(grain.startWorld, grain.endWorld, eased);

            if (t >= 1f)
            {
                grain.active = false;
                Bucket finishedBucket = grain.targetBucket;
                grain.targetBucket = null;
                grain.rect.gameObject.SetActive(false);
                activeGrains.RemoveAt(i);
                pool.Enqueue(grain);

                if (finishedBucket != null && conveyor != null)
                {
                    conveyor.NotifyPourFinished(finishedBucket);
                }
            }
        }
    }

    public bool HasActivePourTo(Bucket bucket)
    {
        if (bucket == null)
        {
            return false;
        }

        for (int i = 0; i < activeGrains.Count; i++)
        {
            if (activeGrains[i].targetBucket == bucket)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleGrainCollected(byte colorIndex, int simGridX)
    {
        LevelLoader loader = FindObjectOfType<LevelLoader>();
        if (loader == null || loader.CurrentLevel == null || pourLayer == null || mapper == null)
        {
            return;
        }

        SandGrid sandGrid = loader.CurrentGrid;
        int simGridWidth = sandGrid != null ? sandGrid.Width : loader.CurrentLevel.gridWidth;
        int levelGridWidth = loader.CurrentLevel.gridWidth;
        int levelGridX = sandGrid != null ? sandGrid.ToLevelX(simGridX) : simGridX;
        mapper.EnsureReferences();

        Vector3 startWorld = mapper.GetPourStartWorldPosition(simGridX, simGridWidth);
        if (startWorld == Vector3.zero)
        {
            return;
        }

        ConveyorBelt.BeltEntry bucketEntry = conveyor != null
            ? conveyor.FindBucketForColumn(levelGridX, levelGridWidth, colorIndex)
            : null;

        Vector3 endWorld = startWorld;
        Bucket targetBucket = null;
        if (bucketEntry != null && bucketEntry.Bucket != null && beltView != null
            && beltView.TryGetBucketPourTargetWorldPosition(bucketEntry.Bucket, out endWorld))
        {
            targetBucket = bucketEntry.Bucket;
        }
        else
        {
            endWorld = startWorld + Vector3.down * 140f;
        }

        Color32 color = SandDisplayColors.FromPalette(loader.CurrentLevel.GetPaletteColor(colorIndex));

        for (int i = 0; i < grainsPerCollect; i++)
        {
            SpawnPourGrain(startWorld, endWorld, color, targetBucket);
        }
    }

    private void SpawnPourGrain(Vector3 startWorld, Vector3 endWorld, Color32 color, Bucket targetBucket)
    {
        PourGrain grain = pool.Count > 0 ? pool.Dequeue() : CreateGrain();
        grain.rect.sizeDelta = GetGrainSize();
        grain.startWorld = startWorld;
        grain.endWorld = endWorld;
        grain.targetBucket = targetBucket;
        grain.elapsed = 0f;
        grain.duration = pourDuration;
        grain.active = true;
        grain.image.color = color;
        grain.rect.gameObject.SetActive(true);
        grain.rect.position = startWorld;
        grain.rect.SetAsLastSibling();
        activeGrains.Add(grain);
    }

    private void EnsurePourLayer()
    {
        if (pourLayer != null || canvas == null)
        {
            return;
        }

        GameObject layerObject = new GameObject("SandPourLayer", typeof(RectTransform));
        layerObject.transform.SetParent(canvas.transform, false);
        pourLayer = layerObject.GetComponent<RectTransform>();
        pourLayer.anchorMin = Vector2.zero;
        pourLayer.anchorMax = Vector2.one;
        pourLayer.offsetMin = Vector2.zero;
        pourLayer.offsetMax = Vector2.zero;
        pourLayer.SetAsLastSibling();
    }

    private void BuildPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            pool.Enqueue(CreateGrain());
        }
    }

    private PourGrain CreateGrain()
    {
        GameObject grainObject = new GameObject("PourGrain", typeof(RectTransform));
        grainObject.transform.SetParent(pourLayer, false);

        RectTransform rect = grainObject.GetComponent<RectTransform>();
        Vector2 size = GetGrainSize();
        rect.sizeDelta = size;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = grainObject.AddComponent<Image>();
        image.raycastTarget = false;
        grainObject.SetActive(false);

        return new PourGrain
        {
            rect = rect,
            image = image
        };
    }
}
