using System;
using System.Collections.Generic;
using UnityEngine;

public class SandGrid
{
    private byte[] cells;
    private Color32[] displayColors;
    private readonly int width;
    private readonly int height;
    private readonly int levelWidth;
    private readonly int levelHeight;
    private readonly int subGrainsPerAxis;
    private readonly Color32[] palette;
    private readonly bool[] dirtyFlags;
    private readonly List<int> dirtyIndices;
    private readonly List<int> vacancyQueue;
    private readonly bool[] vacancyQueued;
    private readonly int[] columnTopY;
    private readonly bool[] columnHasSand;
    private int sandMinY = int.MaxValue;
    private int sandMaxY = -1;
    private bool boundsNeedRecalc;
    private int remainingCount;
    private readonly int[] remainingByColor = new int[256];

    public int Width { get { return width; } }
    public int Height { get { return height; } }
    public int LevelWidth { get { return levelWidth; } }
    public int LevelHeight { get { return levelHeight; } }
    public int SubGrainsPerAxis { get { return subGrainsPerAxis; } }
    public Color32[] Palette { get { return palette; } }
    public int SandMinY { get { return sandMinY; } }
    public int SandMaxY { get { return sandMaxY; } }
    public int VacancyCount { get { return vacancyQueue.Count; } }

    public SandGrid(LevelData levelData, int grainsPerAxis)
    {
        levelWidth = levelData.gridWidth;
        levelHeight = levelData.gridHeight;
        subGrainsPerAxis = Mathf.Max(1, grainsPerAxis);
        width = levelWidth * subGrainsPerAxis;
        height = levelHeight * subGrainsPerAxis;
        palette = levelData.palette;
        cells = new byte[width * height];
        displayColors = new Color32[width * height];
        dirtyFlags = new bool[width * height];
        dirtyIndices = new List<int>(256);
        vacancyQueue = new List<int>(256);
        vacancyQueued = new bool[width * height];
        columnTopY = new int[width];
        columnHasSand = new bool[width];

        for (int x = 0; x < width; x++)
        {
            columnTopY[x] = height;
        }

        for (int ly = 0; ly < levelHeight; ly++)
        {
            for (int lx = 0; lx < levelWidth; lx++)
            {
                byte colorIndex = levelData.cells[ly * levelWidth + lx];
                if (colorIndex == 0)
                {
                    continue;
                }

                int originX = lx * subGrainsPerAxis;
                int originY = ly * subGrainsPerAxis;
                for (int sy = 0; sy < subGrainsPerAxis; sy++)
                {
                    for (int sx = 0; sx < subGrainsPerAxis; sx++)
                    {
                        int x = originX + sx;
                        int y = originY + sy;
                        cells[y * width + x] = colorIndex;
                        remainingCount++;
                        remainingByColor[colorIndex]++;
                        UpdateSandBounds(y);
                        NotifySandAt(x, y);
                    }
                }
            }
        }
    }

    public void BakeDisplayColors(float grainStrength)
    {
        SandGrainColorBaker.Bake(this, grainStrength);
    }

    public void PrepareDrainGap(int emptyRows)
    {
        emptyRows = Mathf.Clamp(emptyRows, 1, height / 4);
        for (int y = height - emptyRows; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SetCell(x, y, 0);
                EnqueueVacancy(x, y);
            }
        }
    }

    public byte GetCell(int x, int y)
    {
        if (!IsInside(x, y))
        {
            return 0;
        }

        return cells[y * width + x];
    }

    public byte GetCellUnchecked(int x, int y)
    {
        return cells[y * width + x];
    }

    public bool IsEmptyUnchecked(int x, int y)
    {
        return cells[y * width + x] == 0;
    }

    public bool ColumnHasSand(int x)
    {
        return columnHasSand[x];
    }

    public int GetColumnTopY(int x)
    {
        return columnTopY[x];
    }

    public void SetCell(int x, int y, byte value)
    {
        if (!IsInside(x, y))
        {
            return;
        }

        int index = y * width + x;
        byte previous = cells[index];
        if (previous == value)
        {
            return;
        }

        cells[index] = value;
        if (value == 0)
        {
            displayColors[index] = default;
            UpdateColumnAfterRemove(x, y);
        }
        else
        {
            NotifySandAt(x, y);
        }

        MarkDirty(index);

        if (previous != 0)
        {
            remainingCount--;
            remainingByColor[previous]--;
        }

        if (value != 0)
        {
            remainingCount++;
            remainingByColor[value]++;
            UpdateSandBounds(y);
            ClearVacancyQueued(index);
        }
        else if (y <= sandMinY || y >= sandMaxY)
        {
            boundsNeedRecalc = true;
        }
    }

    public void EnqueueVacancy(int x, int y)
    {
        if (!IsInside(x, y) || !IsEmpty(x, y))
        {
            return;
        }

        int index = y * width + x;
        if (vacancyQueued[index])
        {
            return;
        }

        vacancyQueued[index] = true;
        vacancyQueue.Add(index);
    }

    public bool TryDequeueLowestVacancy(out int x, out int y)
    {
        x = 0;
        y = 0;
        if (vacancyQueue.Count == 0)
        {
            return false;
        }

        int bestQueueIndex = -1;
        int bestCellIndex = -1;
        int bestY = -1;

        for (int i = 0; i < vacancyQueue.Count; i++)
        {
            int cellIndex = vacancyQueue[i];
            if (!vacancyQueued[cellIndex])
            {
                continue;
            }

            int candidateY = cellIndex / width;
            if (candidateY > bestY)
            {
                bestY = candidateY;
                bestCellIndex = cellIndex;
                bestQueueIndex = i;
            }
        }

        if (bestQueueIndex < 0)
        {
            vacancyQueue.Clear();
            return false;
        }

        vacancyQueue.RemoveAt(bestQueueIndex);
        vacancyQueued[bestCellIndex] = false;

        if (!IsEmpty(bestCellIndex % width, bestCellIndex / width))
        {
            return TryDequeueLowestVacancy(out x, out y);
        }

        x = bestCellIndex % width;
        y = bestCellIndex / width;
        return true;
    }

    public void ClearVacancies()
    {
        vacancyQueue.Clear();
        Array.Clear(vacancyQueued, 0, vacancyQueued.Length);
    }

    public bool HasVacancies()
    {
        return vacancyQueue.Count > 0;
    }

    public Color32 GetDisplayColor(int x, int y)
    {
        if (!IsInside(x, y))
        {
            return default;
        }

        return displayColors[y * width + x];
    }

    public Color32 GetDisplayColorUnchecked(int x, int y)
    {
        return displayColors[y * width + x];
    }

    public void SetDisplayColorSilent(int x, int y, Color32 color)
    {
        if (!IsInside(x, y))
        {
            return;
        }

        displayColors[y * width + x] = color;
    }

    public void MoveGrain(int fromX, int fromY, int toX, int toY)
    {
        if (!IsInside(fromX, fromY) || !IsInside(toX, toY))
        {
            return;
        }

        MoveGrainCore(fromX, fromY, toX, toY);
    }

    public void MoveGrainUnchecked(int fromX, int fromY, int toX, int toY)
    {
        MoveGrainCore(fromX, fromY, toX, toY);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public bool IsEmpty(int x, int y)
    {
        return GetCell(x, y) == 0;
    }

    public int ToLevelX(int simX)
    {
        return simX / subGrainsPerAxis;
    }

    public int ToLevelY(int simY)
    {
        return simY / subGrainsPerAxis;
    }

    public void RecalculateSandBounds()
    {
        if (!boundsNeedRecalc)
        {
            return;
        }

        sandMinY = int.MaxValue;
        sandMaxY = -1;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                if (cells[row * width + col] != 0)
                {
                    if (row < sandMinY)
                    {
                        sandMinY = row;
                    }

                    if (row > sandMaxY)
                    {
                        sandMaxY = row;
                    }
                }
            }
        }

        boundsNeedRecalc = false;
    }

    public List<int> TakeDirtyIndices()
    {
        return dirtyIndices;
    }

    public void ClearDirty()
    {
        for (int i = 0; i < dirtyIndices.Count; i++)
        {
            dirtyFlags[dirtyIndices[i]] = false;
        }

        dirtyIndices.Clear();
    }

    public int CountRemaining()
    {
        return remainingCount;
    }

    public int CountRemainingForColor(byte colorIndex) => remainingByColor[colorIndex];

    public byte[] GetCellsCopy()
    {
        byte[] copy = new byte[cells.Length];
        Array.Copy(cells, copy, cells.Length);
        return copy;
    }

    private void MoveGrainCore(int fromX, int fromY, int toX, int toY)
    {
        int fromIndex = fromY * width + fromX;
        int toIndex = toY * width + toX;
        byte color = cells[fromIndex];
        if (color == 0)
        {
            return;
        }

        Color32 display = displayColors[fromIndex];
        cells[fromIndex] = 0;
        displayColors[fromIndex] = default;
        MarkDirty(fromIndex);
        remainingCount--;
        UpdateColumnAfterRemove(fromX, fromY);

        byte previous = cells[toIndex];
        if (previous != 0) remainingByColor[previous]--;
        cells[toIndex] = color;
        displayColors[toIndex] = display;
        MarkDirty(toIndex);

        if (previous == 0)
        {
            remainingCount++;
        }

        NotifySandAt(toX, toY);
        UpdateSandBounds(toY);
        ClearVacancyQueued(toIndex);

        if (fromY <= sandMinY || fromY >= sandMaxY)
        {
            boundsNeedRecalc = true;
        }
    }

    private void NotifySandAt(int x, int y)
    {
        if (y < columnTopY[x])
        {
            columnTopY[x] = y;
        }

        columnHasSand[x] = true;
    }

    private void UpdateColumnAfterRemove(int x, int removedY)
    {
        if (!columnHasSand[x])
        {
            return;
        }

        if (removedY > columnTopY[x])
        {
            return;
        }

        RecalculateColumnTop(x);
    }

    private void RecalculateColumnTop(int x)
    {
        for (int y = 0; y < height; y++)
        {
            if (cells[y * width + x] != 0)
            {
                columnTopY[x] = y;
                columnHasSand[x] = true;
                return;
            }
        }

        columnTopY[x] = height;
        columnHasSand[x] = false;
    }

    private void MarkDirty(int index)
    {
        if (!dirtyFlags[index])
        {
            dirtyFlags[index] = true;
            dirtyIndices.Add(index);
        }
    }

    private void ClearVacancyQueued(int index)
    {
        if (vacancyQueued[index])
        {
            vacancyQueued[index] = false;
        }
    }

    private void UpdateSandBounds(int row)
    {
        if (row < sandMinY)
        {
            sandMinY = row;
        }

        if (row > sandMaxY)
        {
            sandMaxY = row;
        }
    }
}
