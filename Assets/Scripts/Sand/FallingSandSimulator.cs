using System;
using System.Collections.Generic;
using UnityEngine;

public class FallingSandSimulator : MonoBehaviour
{
    [SerializeField] private float ticksPerSecond = 24f;
    [SerializeField] private int maxCollectPerTick = 6;
    [SerializeField] private int collectRowCount = 8;

    private SandGrid grid;
    private SandCollector collector;
    private ConveyorBelt conveyor;
    private float tickTimer;
    private bool isSleeping;
    private int fallTickCounter;
    private bool scanLeftToRight = true;
    private readonly Dictionary<int, bool> collectColumnCache = new Dictionary<int, bool>();

    public event Action OnGridChanged;

    public SandGrid Grid { get { return grid; } }

    public void Initialize(SandGrid sandGrid, SandCollector sandCollector)
    {
        grid = sandGrid;
        collector = sandCollector;
        conveyor = FindObjectOfType<ConveyorBelt>();
        tickTimer = 0f;
        isSleeping = false;
        fallTickCounter = 0;
    }

    public void Wake()
    {
        isSleeping = false;
    }

    private void Update()
    {
        if (grid == null)
        {
            return;
        }

        if (isSleeping && !ShouldWake())
        {
            return;
        }

        tickTimer += Time.deltaTime;
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

        if (HasPossibleCollection())
        {
            return true;
        }

        if (conveyor != null && conveyor.Entries.Count > 0 && grid.CountRemaining() > 0)
        {
            return true;
        }

        return false;
    }

    private bool HasPossibleCollection()
    {
        if (collector == null || conveyor == null || conveyor.Entries.Count == 0)
        {
            return false;
        }

        int topRow = grid.SandMinY;
        int bottomRow = grid.SandMaxY;
        if (bottomRow < topRow)
        {
            return false;
        }

        for (int y = bottomRow; y >= topRow; y--)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                byte color = grid.GetCell(x, y);
                if (color != 0 && IsCollectibleGrain(x, y, color))
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
        collectColumnCache.Clear();
        grid.RecalculateSandBounds();

        bool changed = SimulateFallPass();
        if (ProcessCollectionUnderBucket())
        {
            changed = true;
        }

        if (changed)
        {
            NotifyGridChanged();
        }

        fallTickCounter++;
        scanLeftToRight = !scanLeftToRight;
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
            grid.SetCell(x, belowY, color);
            grid.SetCell(x, y, 0);
            return true;
        }

        if (IsMatchingCollectionColumn(x, color))
        {
            return false;
        }

        int firstDir = GetDiagonalDirection(x, y);
        if (TryMoveGrainDiagonal(x, y, color, firstDir))
        {
            return true;
        }

        return TryMoveGrainDiagonal(x, y, color, -firstDir);
    }

    private int GetDiagonalDirection(int x, int y)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663 ^ fallTickCounter * 83492791;
            return (hash & 1) == 0 ? -1 : 1;
        }
    }

    private bool TryMoveGrainDiagonal(int x, int y, byte color, int dirX)
    {
        int belowY = y + 1;
        int targetX = x + dirX;
        if (!grid.IsInside(targetX, belowY) || !grid.IsEmpty(targetX, belowY))
        {
            return false;
        }

        if (IsMatchingCollectionColumn(targetX, color))
        {
            return false;
        }

        grid.SetCell(targetX, belowY, color);
        grid.SetCell(x, y, 0);
        return true;
    }

    private bool IsMatchingCollectionColumn(int simX, byte color)
    {
        if (collector == null)
        {
            return false;
        }

        int levelX = grid.ToLevelX(simX);
        int cacheKey = (color << 8) | levelX;
        bool cached;
        if (collectColumnCache.TryGetValue(cacheKey, out cached))
        {
            return cached;
        }

        bool result = collector.CanCollectAt(levelX, grid.LevelWidth, color);
        collectColumnCache[cacheKey] = result;
        return result;
    }

    private bool IsCollectibleGrain(int x, int y, byte color)
    {
        if (!IsMatchingCollectionColumn(x, color))
        {
            return false;
        }

        int drainStart = grid.Height - Mathf.Max(1, collectRowCount);
        return y >= drainStart;
    }

    private bool ProcessCollectionUnderBucket()
    {
        if (collector == null || conveyor == null || conveyor.Entries.Count == 0)
        {
            return false;
        }

        bool changed = false;
        int width = grid.Width;
        int topRow = grid.SandMinY;
        int bottomRow = grid.SandMaxY;
        if (bottomRow < topRow)
        {
            return false;
        }

        int collectedThisTick = 0;
        for (int y = bottomRow; y >= topRow; y--)
        {
            for (int x = 0; x < width; x++)
            {
                if (collectedThisTick >= maxCollectPerTick)
                {
                    return changed;
                }

                byte color = grid.GetCell(x, y);
                if (color == 0 || !IsCollectibleGrain(x, y, color))
                {
                    continue;
                }

                if (collector.TryCollectGrain(color, x, grid.LevelWidth, grid.SubGrainsPerAxis))
                {
                    grid.SetCell(x, y, 0);
                    grid.EnqueueVacancy(x, y);
                    changed = true;
                    collectedThisTick++;
                }
            }
        }

        return changed;
    }
}
