using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class FallingSandSimulator : MonoBehaviour
{
    // Collection lookup v4 — no IsMatchingCollectionColumn / CanCollectAt in prewarm.
    [SerializeField] private float ticksPerSecond = 32f;
    [SerializeField] private int maxTicksPerFrame = 4;
    [SerializeField] private int maxCollectPerTick = 6;
    [SerializeField] private int maxCollectPerFrame = 32;
    [SerializeField] private int collectRowCount = 8;
    [Tooltip("Задержка бокового ссыпания при упоре снизу: 0 = сразу в стороны каждый тик; N = N тиков строго вниз между тиками с диагональю.")]
    [SerializeField] [Min(0)] private int straightTicksBeforeDiagonal = 0;

    private SandGrid grid;
    private SandCollector collector;
    private ConveyorBelt conveyor;
    private float tickTimer;
    private bool isSleeping;
    private int collectBudgetThisFrame;
    private int fallTickCounter;
    private bool scanLeftToRight = true;
    private bool allowDiagonalFallThisTick;
    private readonly List<int> activeCollectLevelColumns = new List<int>(8);
    private int[] movedStamp;
    private int currentTickStamp = 1;
    private bool[] collectLookup;
    private bool[] levelAddedScratch;
    private bool[] columnsToCollapse;
    private int collectLookupLevelWidth;
    private int collectCacheFrame = -1;
    private int drainStartRow = -1;
    private int subGrainsPerAxis = 1;
    private int gridHeight;
    private bool simulationPaused;

    public event Action OnGridChanged;

    public SandGrid Grid { get { return grid; } }
    public bool IsSimulationPaused { get { return simulationPaused; } }

    public void Initialize(SandGrid sandGrid, SandCollector sandCollector)
    {
        grid = sandGrid;
        collector = sandCollector;
        conveyor = FindObjectOfType<ConveyorBelt>();
        subGrainsPerAxis = grid.SubGrainsPerAxis;
        gridHeight = grid.Height;
        movedStamp = new int[grid.Width * grid.Height];
        columnsToCollapse = new bool[grid.Width];
        drainStartRow = ComputeDrainStartRow();
        EnsureCollectLookup(grid.LevelWidth);
        tickTimer = 0f;
        isSleeping = false;
        fallTickCounter = 0;
        currentTickStamp = 1;
    }

    public void Wake()
    {
        isSleeping = false;
        InvalidateCollectCache();
    }

    public void NotifyExternalGridEdit()
    {
        Wake();
        if (OnGridChanged != null)
        {
            OnGridChanged();
        }
    }

    public void SetSimulationPaused(bool paused)
    {
        simulationPaused = paused;
        if (!paused)
        {
            Wake();
        }
    }

    private void Update()
    {
        if (grid == null || simulationPaused)
        {
            return;
        }

        if (isSleeping && !ShouldStayAwake())
        {
            return;
        }

        isSleeping = false;
        collectBudgetThisFrame = maxCollectPerFrame;
        tickTimer += Time.deltaTime;
        float tickInterval = 1f / Mathf.Max(1f, ticksPerSecond);
        int ticksRun = 0;

        while (tickTimer >= tickInterval && ticksRun < maxTicksPerFrame)
        {
            if (collectBudgetThisFrame <= 0)
            {
                break;
            }

            tickTimer -= tickInterval;
            ticksRun++;
            InvalidateCollectCache();

            bool changed = SimulateTick();
            if (!changed && !ShouldStayAwake())
            {
                isSleeping = true;
                break;
            }
        }
    }

    private bool ShouldStayAwake()
    {
        if (grid.HasVacancies())
        {
            return true;
        }

        if (conveyor != null && conveyor.Entries.Count > 0 && grid.CountRemaining() > 0)
        {
            return true;
        }

        if (HasPossibleCollection())
        {
            return true;
        }

        return HasUnstableSand();
    }

    private bool HasUnstableSand()
    {
        int width = grid.Width;
        int topRow = grid.SandMinY;
        int bottomRow = grid.SandMaxY;
        if (bottomRow < topRow)
        {
            return false;
        }

        for (int x = 0; x < width; x++)
        {
            if (!grid.ColumnHasSand(x))
            {
                continue;
            }

            int columnTop = grid.GetColumnTopY(x);
            int startY = columnTop < topRow ? topRow : columnTop;
            for (int y = startY; y <= bottomRow; y++)
            {
                if (grid.IsEmptyUnchecked(x, y))
                {
                    continue;
                }

                int belowY = y + 1;
                if (belowY >= gridHeight)
                {
                    continue;
                }

                if (grid.IsEmptyUnchecked(x, belowY))
                {
                    return true;
                }

                if (x > 0 && grid.IsEmptyUnchecked(x - 1, belowY))
                {
                    return true;
                }

                if (x < width - 1 && grid.IsEmptyUnchecked(x + 1, belowY))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int ComputeDrainStartRow()
    {
        if (grid == null)
        {
            return 0;
        }

        return grid.Height - Mathf.Max(1, collectRowCount);
    }

    private void EnsureCollectLookup(int levelWidth)
    {
        if (collectLookup != null && collectLookupLevelWidth == levelWidth)
        {
            return;
        }

        collectLookupLevelWidth = levelWidth;
        collectLookup = new bool[256 * levelWidth];
        levelAddedScratch = new bool[levelWidth];
    }

    private void EnsureCollectCacheFresh()
    {
        if (collectCacheFrame == Time.frameCount)
        {
            return;
        }

        collectCacheFrame = Time.frameCount;
        PrewarmCollectColumnCache();
    }

    private void PrewarmCollectColumnCache()
    {
        Profiler.BeginSample("SandCollectionPrewarmV4");
        activeCollectLevelColumns.Clear();
        Array.Clear(collectLookup, 0, collectLookup.Length);

        if (conveyor == null || conveyor.Entries.Count == 0)
        {
            Profiler.EndSample();
            return;
        }

        int levelWidth = grid.LevelWidth;
        if (levelAddedScratch == null || levelAddedScratch.Length != levelWidth)
        {
            levelAddedScratch = new bool[levelWidth];
        }

        conveyor.FillSandCollectionLookup(
            collectLookup,
            levelWidth,
            activeCollectLevelColumns,
            levelAddedScratch);
        Profiler.EndSample();
    }

    private void InvalidateCollectCache()
    {
        collectCacheFrame = -1;
    }

    private bool HasPossibleCollection()
    {
        if (collector == null || conveyor == null || conveyor.Entries.Count == 0)
        {
            return false;
        }

        EnsureCollectCacheFresh();
        if (activeCollectLevelColumns.Count == 0)
        {
            return false;
        }

        int bottomRow = grid.SandMaxY;
        if (bottomRow < drainStartRow)
        {
            return false;
        }

        int levelWidth = collectLookupLevelWidth;
        for (int columnIndex = 0; columnIndex < activeCollectLevelColumns.Count; columnIndex++)
        {
            int levelX = activeCollectLevelColumns[columnIndex];
            int simXStart = levelX * subGrainsPerAxis;
            for (int sx = 0; sx < subGrainsPerAxis; sx++)
            {
                int x = simXStart + sx;
                int y = FindTopCollectableRow(x, levelX, levelWidth);
                if (y >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void NotifyGridChanged()
    {
        if (OnGridChanged != null)
        {
            OnGridChanged();
        }
    }

    public bool SimulateTick()
    {
        currentTickStamp++;
        if (currentTickStamp == int.MaxValue)
        {
            Array.Clear(movedStamp, 0, movedStamp.Length);
            currentTickStamp = 1;
        }

        grid.RecalculateSandBounds();
        EnsureCollectCacheFresh();
        allowDiagonalFallThisTick = ShouldAllowDiagonalFall();

        bool changed = SimulateFallPass();
        if (ProcessCollectionUnderBucket())
        {
            changed = true;
            grid.RecalculateSandBounds();
        }

        if (changed)
        {
            NotifyGridChanged();
        }

        fallTickCounter++;
        scanLeftToRight = !scanLeftToRight;
        return changed;
    }

    private bool ShouldAllowDiagonalFall()
    {
        return straightTicksBeforeDiagonal <= 0
            || fallTickCounter % (straightTicksBeforeDiagonal + 1) == straightTicksBeforeDiagonal;
    }

    private void MarkMovedThisTick(int x, int y)
    {
        movedStamp[y * grid.Width + x] = currentTickStamp;
    }

    private bool WasMovedThisTick(int x, int y)
    {
        return movedStamp[y * grid.Width + x] == currentTickStamp;
    }

    private bool CollapseColumn(int x)
    {
        int topRow = grid.SandMinY;
        int bottomRow = grid.SandMaxY;
        if (bottomRow < topRow)
        {
            return false;
        }

        bool changed = false;
        int writeY = bottomRow;
        for (int y = bottomRow; y >= topRow; y--)
        {
            byte color = grid.GetCell(x, y);
            if (color == 0)
            {
                continue;
            }

            if (y != writeY)
            {
                grid.MoveGrainUnchecked(x, y, x, writeY);
                MarkMovedThisTick(x, writeY);
                changed = true;
            }

            writeY--;
        }

        return changed;
    }

    private void FlushPendingColumnCollapses()
    {
        int width = grid.Width;
        for (int x = 0; x < width; x++)
        {
            if (!columnsToCollapse[x])
            {
                continue;
            }

            columnsToCollapse[x] = false;
            CollapseColumn(x);
        }
    }

    private bool SimulateFallPass()
    {
        int width = grid.Width;
        int topRow = grid.SandMinY;
        int bottomRow = grid.SandMaxY;
        if (bottomRow < topRow)
        {
            return false;
        }

        bool changed = false;
        if (scanLeftToRight)
        {
            for (int x = 0; x < width; x++)
            {
                if (!grid.ColumnHasSand(x))
                {
                    continue;
                }

                int columnTop = grid.GetColumnTopY(x);
                int startY = columnTop < topRow ? topRow : columnTop;
                for (int y = bottomRow; y >= startY; y--)
                {
                    if (TryMoveGrainDown(x, y))
                    {
                        changed = true;
                    }
                }
            }
        }
        else
        {
            for (int x = width - 1; x >= 0; x--)
            {
                if (!grid.ColumnHasSand(x))
                {
                    continue;
                }

                int columnTop = grid.GetColumnTopY(x);
                int startY = columnTop < topRow ? topRow : columnTop;
                for (int y = bottomRow; y >= startY; y--)
                {
                    if (TryMoveGrainDown(x, y))
                    {
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    private bool TryMoveGrainDown(int x, int y)
    {
        byte color = grid.GetCellUnchecked(x, y);
        if (color == 0)
        {
            return false;
        }

        int belowY = y + 1;
        if (belowY >= gridHeight)
        {
            return false;
        }

        if (grid.IsEmptyUnchecked(x, belowY))
        {
            grid.MoveGrainUnchecked(x, y, x, belowY);
            MarkMovedThisTick(x, belowY);
            return true;
        }

        if (straightTicksBeforeDiagonal > 0 && !allowDiagonalFallThisTick)
        {
            return false;
        }

        return TryMoveGrainSideways(x, y);
    }

    private bool TryMoveGrainSideways(int x, int y)
    {
        int firstDir = GetDiagonalDirection(x, y);
        if (TryMoveGrainDiagonal(x, y, firstDir))
        {
            return true;
        }

        return TryMoveGrainDiagonal(x, y, -firstDir);
    }

    private int GetDiagonalDirection(int x, int y)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663 ^ fallTickCounter * 83492791;
            int dir = (hash & 1) == 0 ? -1 : 1;
            return scanLeftToRight ? dir : -dir;
        }
    }

    private bool TryMoveGrainDiagonal(int x, int y, int dirX)
    {
        int belowY = y + 1;
        int targetX = x + dirX;
        if (targetX < 0 || targetX >= grid.Width || belowY >= gridHeight)
        {
            return false;
        }

        if (!grid.IsEmptyUnchecked(targetX, belowY))
        {
            return false;
        }

        grid.MoveGrainUnchecked(x, y, targetX, belowY);
        MarkMovedThisTick(targetX, belowY);
        return true;
    }

    private bool IsSettledGrain(int x, int y)
    {
        int belowY = y + 1;
        return belowY >= gridHeight || !grid.IsEmptyUnchecked(x, belowY);
    }

    private int FindTopCollectableRow(int x, int levelX, int levelWidth)
    {
        int bottomRow = grid.SandMaxY;
        if (bottomRow < drainStartRow)
        {
            return -1;
        }

        int topRow = Mathf.Max(drainStartRow, grid.SandMinY);
        for (int y = topRow; y <= bottomRow; y++)
        {
            byte color = grid.GetCell(x, y);
            if (color == 0 || !collectLookup[color * levelWidth + levelX])
            {
                continue;
            }

            if (IsSettledGrain(x, y) && !WasMovedThisTick(x, y))
            {
                return y;
            }
        }

        return -1;
    }

    private bool ProcessCollectionUnderBucket()
    {
        if (collector == null || conveyor == null || conveyor.Entries.Count == 0)
        {
            return false;
        }

        if (activeCollectLevelColumns.Count == 0)
        {
            return false;
        }

        Profiler.BeginSample("SandCollectionDrainV4");
        bool changed = false;
        int bottomRow = grid.SandMaxY;
        if (bottomRow < drainStartRow)
        {
            Profiler.EndSample();
            return false;
        }

        int collectLimit = Mathf.Min(maxCollectPerTick, collectBudgetThisFrame);
        if (collectLimit <= 0)
        {
            Profiler.EndSample();
            return false;
        }

        int collectedThisTick = 0;
        int levelWidth = collectLookupLevelWidth;

        while (collectedThisTick < collectLimit)
        {
            bool layerCollected = false;

            for (int columnIndex = 0; columnIndex < activeCollectLevelColumns.Count; columnIndex++)
            {
                int levelX = activeCollectLevelColumns[columnIndex];
                int simXStart = levelX * subGrainsPerAxis;
                for (int sx = 0; sx < subGrainsPerAxis; sx++)
                {
                    if (collectedThisTick >= collectLimit)
                    {
                        break;
                    }

                    int x = simXStart + sx;
                    int y = FindTopCollectableRow(x, levelX, levelWidth);
                    if (y < 0)
                    {
                        continue;
                    }

                    byte color = grid.GetCell(x, y);
                    if (collector.TryCollectGrain(color, x, grid.LevelWidth, grid.SubGrainsPerAxis))
                    {
                        grid.SetCell(x, y, 0);
                        grid.EnqueueVacancy(x, y);
                        columnsToCollapse[x] = true;
                        changed = true;
                        collectedThisTick++;
                        layerCollected = true;
                    }
                }
            }

            FlushPendingColumnCollapses();

            if (!layerCollected)
            {
                break;
            }
        }

        collectBudgetThisFrame -= collectedThisTick;
        Profiler.EndSample();
        return changed;
    }
}
