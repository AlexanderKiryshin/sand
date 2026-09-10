using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelIntroSandAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform pourLayer;
    [SerializeField] private float introDuration = 1.5f;
    [SerializeField] private int introGrainBudget = 1500;
    [SerializeField] private int poolSize = 512;
    [SerializeField] private int maxActiveGrains = 320;
    [SerializeField] private float fallDuration = 0.42f;
    [SerializeField] [Range(0f, 0.5f)] private float fallDurationSpread = 0.25f;
    [SerializeField] private Vector2 startJitter = new Vector2(8f, 18f);
    [SerializeField] private float grainSizeScale = 1f;
    [SerializeField] private float minGrainSize = 1f;

    private readonly List<FallingGrain> activeGrains = new List<FallingGrain>();
    private readonly Queue<FallingGrain> pool = new Queue<FallingGrain>();
    private readonly List<SpawnRequest> pendingSpawns = new List<SpawnRequest>();

    private SandConveyorMapper mapper;
    private SandRenderer sandRenderer;
    private FallingSandSimulator sandSimulator;
    private Canvas canvas;
    private float introElapsed;
    private int nextSpawnIndex;
    private int revealStride = 1;
    private bool isPlaying;

    public bool IsPlaying { get { return isPlaying; } }
    public event Action OnIntroFinished;

    private struct SpawnRequest
    {
        public int levelX;
        public int levelY;
        public byte colorIndex;
        public float spawnAt;
        public bool spawnVisibleGrain;
    }

    private class FallingGrain
    {
        public RectTransform rect;
        public Image image;
        public Vector3 startWorld;
        public Vector3 endWorld;
        public int levelX;
        public int levelY;
        public float elapsed;
        public float duration;
    }

    private void Awake()
    {
        mapper = FindObjectOfType<SandConveyorMapper>();
        sandRenderer = FindObjectOfType<SandRenderer>();
        sandSimulator = FindObjectOfType<FallingSandSimulator>();
        canvas = FindObjectOfType<Canvas>();

        if (mapper == null && canvas != null)
        {
            mapper = canvas.gameObject.AddComponent<SandConveyorMapper>();
        }

        EnsurePourLayer();
        BuildPool();
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        introElapsed += Time.deltaTime;
        SpawnDueGrains();

        for (int i = activeGrains.Count - 1; i >= 0; i--)
        {
            FallingGrain grain = activeGrains[i];
            grain.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(grain.elapsed / grain.duration);
            float eased = t * t;
            grain.rect.position = Vector3.Lerp(grain.startWorld, grain.endWorld, eased);

            if (t < 1f)
            {
                continue;
            }

            sandRenderer.RevealLevelBlock(grain.levelX, grain.levelY, revealStride);
            grain.rect.gameObject.SetActive(false);
            activeGrains.RemoveAt(i);
            pool.Enqueue(grain);
        }

        if (nextSpawnIndex >= pendingSpawns.Count && activeGrains.Count == 0)
        {
            FinishIntro();
        }
    }

    public void PlayIntro(LevelData level, SandRenderer renderer, FallingSandSimulator simulator)
    {
        if (level == null || renderer == null || mapper == null)
        {
            return;
        }

        StopIntro();

        sandRenderer = renderer;
        sandSimulator = simulator;
        sandRenderer.PrepareForIntro();
        if (sandSimulator != null)
        {
            sandSimulator.SetSimulationPaused(true);
        }

        EnsurePourLayer();
        BuildSpawnQueue(level);
        introElapsed = 0f;
        nextSpawnIndex = 0;
        BringPourLayerToFront();
        isPlaying = pendingSpawns.Count > 0;
        if (!isPlaying)
        {
            FinishIntro();
        }
    }

    public void StopIntro()
    {
        if (!isPlaying && activeGrains.Count == 0 && pendingSpawns.Count == 0)
        {
            return;
        }

        isPlaying = false;
        pendingSpawns.Clear();
        nextSpawnIndex = 0;

        for (int i = activeGrains.Count - 1; i >= 0; i--)
        {
            FallingGrain grain = activeGrains[i];
            if (grain.rect != null)
            {
                grain.rect.gameObject.SetActive(false);
            }

            pool.Enqueue(grain);
        }

        activeGrains.Clear();

        if (sandRenderer != null)
        {
            sandRenderer.CompleteIntroPour();
        }

        if (sandSimulator != null)
        {
            sandSimulator.SetSimulationPaused(false);
        }
    }

    private void BuildSpawnQueue(LevelData level)
    {
        pendingSpawns.Clear();
        revealStride = 1;

        int levelWidth = level.gridWidth;
        int levelHeight = level.gridHeight;
        int anchorCount = 0;
        for (int ly = 0; ly < levelHeight; ly++)
        {
            for (int lx = 0; lx < levelWidth; lx++)
            {
                if (level.cells[ly * levelWidth + lx] == 0)
                {
                    continue;
                }

                anchorCount++;
            }
        }

        if (anchorCount == 0)
        {
            return;
        }

        int targetVisibleGrains = Mathf.Max(1, introGrainBudget);
        if (anchorCount > targetVisibleGrains)
        {
            revealStride = Mathf.Max(1, Mathf.CeilToInt((float)anchorCount / targetVisibleGrains));
        }

        for (int ly = 0; ly < levelHeight; ly += revealStride)
        {
            for (int lx = 0; lx < levelWidth; lx += revealStride)
            {
                byte colorIndex = level.cells[ly * levelWidth + lx];
                if (colorIndex == 0)
                {
                    continue;
                }

                int rowFromBottom = levelHeight - 1 - ly;
                float rowPhase = levelHeight > 1 ? (float)rowFromBottom / (levelHeight - 1) : 0f;
                float spawnAt = rowPhase * introDuration * 0.92f + UnityEngine.Random.Range(0f, 0.04f);
                pendingSpawns.Add(new SpawnRequest
                {
                    levelX = lx,
                    levelY = ly,
                    colorIndex = colorIndex,
                    spawnAt = spawnAt,
                    spawnVisibleGrain = true
                });
            }
        }

        pendingSpawns.Sort((a, b) =>
        {
            int bySpawn = a.spawnAt.CompareTo(b.spawnAt);
            if (bySpawn != 0)
            {
                return bySpawn;
            }

            if (a.levelY != b.levelY)
            {
                return b.levelY.CompareTo(a.levelY);
            }

            return a.levelX.CompareTo(b.levelX);
        });
    }

    private void SpawnDueGrains()
    {
        mapper.EnsureReferences();

        while (nextSpawnIndex < pendingSpawns.Count
            && pendingSpawns[nextSpawnIndex].spawnAt <= introElapsed
            && activeGrains.Count < maxActiveGrains)
        {
            SpawnRequest request = pendingSpawns[nextSpawnIndex];
            nextSpawnIndex++;

            if (request.spawnVisibleGrain)
            {
                SpawnVisibleGrain(request);
            }
            else
            {
                sandRenderer.RevealLevelBlock(request.levelX, request.levelY, revealStride);
            }
        }
    }

    private void SpawnVisibleGrain(SpawnRequest request)
    {
        LevelLoader loader = FindObjectOfType<LevelLoader>();
        if (loader == null || loader.CurrentLevel == null)
        {
            sandRenderer.RevealLevelBlock(request.levelX, request.levelY, revealStride);
            return;
        }

        LevelData level = loader.CurrentLevel;
        Vector3 endWorld = mapper.GetLevelCellWorldPosition(
            request.levelX,
            request.levelY,
            level.gridWidth,
            level.gridHeight);
        if (endWorld == Vector3.zero)
        {
            sandRenderer.RevealLevelBlock(request.levelX, request.levelY, revealStride);
            return;
        }

        Vector3 startWorld = mapper.GetLevelPourStartWorldPosition(
            request.levelX,
            level.gridWidth,
            level.gridHeight);
        startWorld += new Vector3(
            UnityEngine.Random.Range(-startJitter.x, startJitter.x),
            UnityEngine.Random.Range(0f, startJitter.y),
            0f);

        FallingGrain grain = pool.Count > 0 ? pool.Dequeue() : CreateGrain();
        grain.rect.sizeDelta = GetGrainSize(level.gridWidth);
        grain.levelX = request.levelX;
        grain.levelY = request.levelY;
        grain.startWorld = startWorld;
        grain.endWorld = endWorld;
        grain.elapsed = UnityEngine.Random.Range(0f, fallDuration * 0.08f);
        float durationFactor = 1f + UnityEngine.Random.Range(-fallDurationSpread, fallDurationSpread);
        grain.duration = Mathf.Max(0.12f, fallDuration * durationFactor);
        grain.image.color = SandDisplayColors.FromPalette(level.GetPaletteColor(request.colorIndex));
        grain.rect.gameObject.SetActive(true);
        grain.rect.position = grain.startWorld;
        grain.rect.SetAsLastSibling();
        activeGrains.Add(grain);
    }

    private Vector2 GetGrainSize(int gridWidth)
    {
        float size = 4f;
        if (mapper != null && gridWidth > 0)
        {
            size = mapper.GetCellScreenSize(gridWidth);
            if (sandRenderer != null && sandRenderer.SubGrainsPerAxis > 0)
            {
                size *= sandRenderer.SubGrainScreenFraction;
            }
        }

        size *= grainSizeScale;
        if (revealStride > 1)
        {
            size *= revealStride;
        }
        size = Mathf.Max(minGrainSize, size);
        return new Vector2(size, size);
    }

    private void FinishIntro()
    {
        isPlaying = false;
        pendingSpawns.Clear();
        nextSpawnIndex = 0;

        if (sandRenderer != null)
        {
            sandRenderer.CompleteIntroPour();
        }

        if (sandSimulator != null)
        {
            sandSimulator.SetSimulationPaused(false);
        }

        if (OnIntroFinished != null)
        {
            OnIntroFinished();
        }
    }

    private void EnsurePourLayer()
    {
        RectTransform parent = ResolvePourParent();
        if (parent == null)
        {
            return;
        }

        if (pourLayer == null)
        {
            Transform existingOnPanel = parent.Find("SandIntroPourLayer");
            if (existingOnPanel != null)
            {
                pourLayer = existingOnPanel.GetComponent<RectTransform>();
            }
        }

        if (pourLayer == null)
        {
            GameObject layerObject = new GameObject("SandIntroPourLayer", typeof(RectTransform));
            layerObject.transform.SetParent(parent, false);
            pourLayer = layerObject.GetComponent<RectTransform>();
            StretchPourLayer(pourLayer);
        }
        else if (pourLayer.parent != parent)
        {
            pourLayer.SetParent(parent, true);
            StretchPourLayer(pourLayer);
        }

        BringPourLayerToFront();
    }

    private RectTransform ResolvePourParent()
    {
        if (mapper != null)
        {
            mapper.EnsureReferences();
            RectTransform sandPanel = mapper.SandPanelRect;
            if (sandPanel != null)
            {
                return sandPanel;
            }
        }

        return canvas != null ? canvas.transform as RectTransform : null;
    }

    private static void StretchPourLayer(RectTransform layer)
    {
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.pivot = new Vector2(0.5f, 0.5f);
    }

    private void BringPourLayerToFront()
    {
        if (pourLayer != null)
        {
            pourLayer.SetAsLastSibling();
        }
    }

    private void BuildPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            pool.Enqueue(CreateGrain());
        }
    }

    private FallingGrain CreateGrain()
    {
        GameObject grainObject = new GameObject("IntroGrain", typeof(RectTransform));
        grainObject.transform.SetParent(pourLayer, false);

        RectTransform rect = grainObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = grainObject.AddComponent<Image>();
        image.sprite = SandUiSprite.White;
        image.raycastTarget = false;
        grainObject.SetActive(false);

        return new FallingGrain
        {
            rect = rect,
            image = image
        };
    }
}
