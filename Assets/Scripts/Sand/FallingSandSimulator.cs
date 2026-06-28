using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class FallingSandSimulator : MonoBehaviour
{
    // Collection lookup v4 — no IsMatchingCollectionColumn / CanCollectAt in prewarm.
    [SerializeField] private float ticksPerSecond = 32f;
    [SerializeField] private int maxCollectPerTick = 6;
    [SerializeField] private int collectRowCount = 8;
    [Tooltip("Сколько тиков падения строго вниз между тиками, где разрешена диагональ (влево/вправо). 2 = два тика вниз, один с диагональю.")]
    [SerializeField] [Min(0)] private int straightTicksBeforeDiagonal = 2;

    private SandGrid grid;
    private SandCollector collector;
    private ConveyorBelt conveyor;
    private float tickTimer;
    private bool isSleeping;
    private int fallTickCounter;
    private bool scanLeftToRight = true;
    private bool allowDiagonalFallThisTick;
    private readonly List<int> activeCollectLevelColumns = new List<int>(8);
    private bool[] movedThisTick;
    private bool[] collectLookup;
    private bool[] levelAddedScratch;
    private int collectLookupLevelWidth;
    private int collectCacheFrame = -1;
    private int drainStartRow = -1;
    private int subGrainsPerAxis = 1;

    public event Action OnGridChanged;

    public SandGrid Grid { get { return grid; } }

    public void Initialize(SandGrid sandGrid, SandCollector sandCollector)
    {
        grid = sandGrid;
        collector = sandCollector;
        conveyor = FindObjectOfType<ConveyorBelt>();
        subGrainsPerAxis = grid.SubGrainsPerAxis;
        movedThisTick = new bool[grid.Width * grid.Height];
        drainStartRow = ComputeDrainStartRow();
        EnsureCollectLookup(grid.LevelWidth);
        tickTimer = 0f;
        isSleeping = false;
        fallTickCounter = 0;
    }

    public void Wake()
    {
        isSleeping = false;
        InvalidateCollectCache();
    }

    private void FixedUpdate()
    {
        if (grid == null)
        {
            return;
        }

        if (isSleeping && !ShouldWake())
        {
            return;
        }

        tickTimer += Time.fixedDeltaTime;
        float tickInterval = 1f / Mathf.Max(1f, ticksPerSecond);
        while (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            bool changed = SimulateTick();
            if (changed)
            {
                isSleeping = false;
            }
            else if (!ShouldStayAwake())
            {
                isSleeping = true;
            }
        }
    }

    private bool ShouldWake()
    {
        return ShouldStayAwake();
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

        return HasPossibleCollection();
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
                for (int y = bottomRow; y >= drainStartRow; y--)
                {
                    byte color = grid.GetCell(x, y);
                    if (color == 0)
                    {
                        continue;
                    }

                    if (!collectLookup[color * levelWidth + levelX])
                    {
                        continue;
                    }

                    if (IsSettledGrain(x, y) && !WasMovedThisTick(x, y))
                    {
                        return true;
                    }
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
        ClearMovedThisTick();
        grid.RecalculateSandBounds();
        EnsureCollectCacheFresh();
        allowDiagonalFallThisTick = ShouldAllowDiagonalFall();

        bool changed = SimulateFallPass();
        if (ProcessCollectionUnderBucket())
        {
            changed = true;
            grid.RecalculateSandBounds();
        }

        if (conveyor != null)
        {
            conveyor.EndSandSimTick(grid.LevelWidth);
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
        if (straightTicksBeforeDiagonal <= 0)
        {
            return true;
        }

        int cycle = straightTicksBeforeDiagonal + 1;
        return fallTickCounter % cycle == straightTicksBeforeDiagonal;
    }

    private void ClearMovedThisTick()
    {
        if (movedThisTick == null || movedThisTick.Length != grid.Width * grid.Height)
        {
            movedThisTick = new bool[grid.Width * grid.Height];
            return;
        }

        Array.Clear(movedThisTick, 0, movedThisTick.Length);
    }

    private void MarkMovedThisTick(int x, int y)
    {
        movedThisTick[y * grid.Width + x] = true;
    }

    private bool WasMovedThisTick(int x, int y)
    {
        return movedThisTick[y * grid.Width + x];
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
                grid.MoveGrain(x, y, x, writeY);
                MarkMovedThisTick(x, writeY);
                changed = true;
            }

            writeY--;
        }

        return changed;
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
        for (int y = bottomRow; y >= topRow; y--)
        {
            if (scanLeftToRight)
            {
                for (int x = 0; x < width; x++)
                {
                    if (TryMoveGrainDown(x, y))
                    {
                        changed = true;
                    }
                }
            }
            else
            {
                for (int x = width - 1; x >= 0; x--)
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
        byte color = grid.GetCell(x, y);
        if (color == 0)
        {
            return false;
        }

        int belowY = y + 1;
        if (belowY >= grid.Height)
        {
            return false;
        }

        if (grid.IsEmpty(x, belowY))
        {
            grid.MoveGrain(x, y, x, belowY);
            MarkMovedThisTick(x, belowY);
            return true;
        }

        if (!allowDiagonalFallThisTick)
        {
            return false;
        }

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
        if (!grid.IsInside(targetX, belowY) || !grid.IsEmpty(targetX, belowY))
        {
            return false;
        }

        grid.MoveGrain(x, y, targetX, belowY);
        MarkMovedThisTick(targetX, belowY);
        return true;
    }

    private bool IsSettledGrain(int x, int y)
    {
        int belowY = y + 1;
        return belowY >= grid.Height || !grid.IsEmpty(x, belowY);
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

        int collectedThisTick = 0;
        int levelWidth = collectLookupLevelWidth;
        for (int columnIndex = 0; columnIndex < activeCollectLevelColumns.Count; columnIndex++)
        {
            int levelX = activeCollectLevelColumns[columnIndex];
            int simXStart = levelX * subGrainsPerAxis;
            for (int sx = 0; sx < subGrainsPerAxis; sx++)
            {
                int x = simXStart + sx;
                for (int y = bottomRow; y >= drainStartRow; y--)
                {
                    if (collectedThisTick >= maxCollectPerTick)
                    {
                        Profiler.EndSample();
                        return changed;
                    }

                    byte color = grid.GetCell(x, y);
                    if (color == 0 || !collectLookup[color * levelWidth + levelX])
                    {
                        continue;
                    }

                    if (!IsSettledGrain(x, y) || WasMovedThisTick(x, y))
                    {
                        continue;
                    }

                    if (collector.TryCollectGrain(color, x, grid.LevelWidth, grid.SubGrainsPerAxis))
                    {
                        grid.SetCell(x, y, 0);
                        grid.EnqueueVacancy(x, y);
                        CollapseColumn(x);
                        changed = true;
                        collectedThisTick++;
                    }
                }
            }
        }

        Profiler.EndSample();
        return changed;
    }
}
