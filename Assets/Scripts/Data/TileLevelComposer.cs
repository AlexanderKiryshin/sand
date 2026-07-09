using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Собирает уровень из тайлов. Каждый тайл — отдельный рисунок со своей палитрой (ImageProcessor).
/// </summary>
public static class TileLevelComposer
{
    [Serializable]
    public struct TileInput
    {
        public Color32[] pixels;
        public int sourceWidth;
        public int sourceHeight;
        public int tileX;
        public int tileY;
        public int colorCount;
        public bool autoColorCount;
        public byte alphaThreshold;
        public bool autoBackgroundColor;
        public Color32 backgroundColor;
        public SandColorPalette targetPalette;
    }

    public struct Settings
    {
        public int tilesX;
        public int tilesY;
        public int tileCellWidth;
        public int tileCellHeight;
        public int defaultColorCount;
        public bool defaultAutoColorCount;
        public byte alphaThreshold;
        public bool defaultAutoBackgroundColor;
        public Color32 defaultBackgroundColor;
        public SandColorPalette defaultTargetPalette;
    }

    public struct TileProcessResult
    {
        public int tileX;
        public int tileY;
        public ImageProcessor.Result result;
    }

    public struct ComposeResult
    {
        public ImageProcessor.Result level;
        public TileProcessResult[] tiles;
    }

    private static readonly Regex TileNamePattern = new Regex(
        @"(?:^|[_\-])(\d+)[_\-](\d+)(?:[_\-\.]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseTilePosition(string fileNameWithoutExtension, out int tileX, out int tileY)
    {
        tileX = 0;
        tileY = 0;
        if (string.IsNullOrEmpty(fileNameWithoutExtension))
        {
            return false;
        }

        Match match = TileNamePattern.Match(fileNameWithoutExtension);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out tileX) || !int.TryParse(match.Groups[2].Value, out tileY))
        {
            tileX = 0;
            tileY = 0;
            return false;
        }

        return tileX >= 0 && tileY >= 0;
    }

    public static void AutoDetectGridSize(IList<TileInput> tiles, out int tilesX, out int tilesY)
    {
        tilesX = 0;
        tilesY = 0;
        if (tiles == null || tiles.Count == 0)
        {
            return;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            TileInput tile = tiles[i];
            tilesX = Mathf.Max(tilesX, tile.tileX + 1);
            tilesY = Mathf.Max(tilesY, tile.tileY + 1);
        }
    }

    public static ImageProcessor.Result ProcessSingleTile(TileInput tile, Settings settings)
    {
        settings.tileCellWidth = Mathf.Clamp(settings.tileCellWidth, 1, LevelData.MaxTileCellDimension);
        settings.tileCellHeight = Mathf.Clamp(settings.tileCellHeight, 1, LevelData.MaxTileCellDimension);

        if (tile.pixels == null || tile.sourceWidth <= 0 || tile.sourceHeight <= 0)
        {
            return EmptyResult(settings.tileCellWidth, settings.tileCellHeight);
        }

        int colorCount = tile.colorCount > 0 ? tile.colorCount : settings.defaultColorCount;
        SandColorPalette palette = tile.targetPalette != null ? tile.targetPalette : settings.defaultTargetPalette;

        ImageProcessor.Settings imageSettings = ImageProcessor.Settings.Create(
            settings.tileCellWidth,
            settings.tileCellHeight,
            colorCount);
        imageSettings.alphaThreshold = tile.alphaThreshold > 0 ? tile.alphaThreshold : settings.alphaThreshold;
        imageSettings.targetPalette = palette;
        imageSettings.autoBackgroundColor = tile.autoBackgroundColor;
        imageSettings.backgroundColor = tile.backgroundColor;
        imageSettings.autoColorCount = tile.autoColorCount;

        return ImageProcessor.Process(tile.pixels, tile.sourceWidth, tile.sourceHeight, imageSettings);
    }

    public static ComposeResult Compose(IList<TileInput> tiles, Settings settings)
    {
        settings.tileCellWidth = Mathf.Clamp(settings.tileCellWidth, 1, LevelData.MaxTileCellDimension);
        settings.tileCellHeight = Mathf.Clamp(settings.tileCellHeight, 1, LevelData.MaxTileCellDimension);
        settings.tilesX = Mathf.Max(1, settings.tilesX);
        settings.tilesY = Mathf.Max(1, settings.tilesY);
        settings.defaultColorCount = Mathf.Clamp(settings.defaultColorCount, 1, SandColorPalette.MaxColorCount);
        settings.alphaThreshold = settings.alphaThreshold > 0 ? settings.alphaThreshold : (byte)128;

        if (tiles != null && tiles.Count > 0)
        {
            AutoDetectGridSize(tiles, out int detectedTilesX, out int detectedTilesY);
            settings.tilesX = Mathf.Max(settings.tilesX, detectedTilesX);
            settings.tilesY = Mathf.Max(settings.tilesY, detectedTilesY);
        }

        int gridWidth = settings.tilesX * settings.tileCellWidth;
        int gridHeight = settings.tilesY * settings.tileCellHeight;
        int cellCount = gridWidth * gridHeight;

        byte[] masterCells = new byte[cellCount];
        Color32[] masterQuantized = new Color32[cellCount];
        Color32 letterboxColor = new Color32(0, 0, 0, 0);
        List<Color32> globalPalette = new List<Color32>(SandColorPalette.MaxColorCount);
        List<TileProcessResult> tileResults = new List<TileProcessResult>(tiles != null ? tiles.Count : 0);

        if (tiles == null || tiles.Count == 0)
        {
            return new ComposeResult
            {
                level = new ImageProcessor.Result
                {
                    width = gridWidth,
                    height = gridHeight,
                    letterboxColor = letterboxColor,
                    resizedPixels = masterQuantized,
                    quantizedPixels = masterQuantized,
                    palette = new Color32[0],
                    cellIndices = masterCells
                },
                tiles = new TileProcessResult[0]
            };
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            TileInput tile = tiles[i];
            if (tile.pixels == null || tile.sourceWidth <= 0 || tile.sourceHeight <= 0)
            {
                continue;
            }

            if (tile.tileX < 0 || tile.tileY < 0 || tile.tileX >= settings.tilesX || tile.tileY >= settings.tilesY)
            {
                continue;
            }

            ImageProcessor.Result tileResult = ProcessSingleTile(tile, settings);
            tileResults.Add(new TileProcessResult
            {
                tileX = tile.tileX,
                tileY = tile.tileY,
                result = tileResult
            });

            byte[] localToGlobal = BuildLocalToGlobalRemap(tileResult.palette, globalPalette);
            int baseX = tile.tileX * settings.tileCellWidth;
            int baseY = tile.tileY * settings.tileCellHeight;

            for (int localY = 0; localY < settings.tileCellHeight; localY++)
            {
                for (int localX = 0; localX < settings.tileCellWidth; localX++)
                {
                    int localIndex = localY * settings.tileCellWidth + localX;
                    byte localColorIndex = tileResult.cellIndices[localIndex];
                    int globalX = baseX + localX;
                    int globalY = baseY + localY;
                    int globalIndex = globalY * gridWidth + globalX;

                    byte globalColorIndex = localColorIndex < localToGlobal.Length
                        ? localToGlobal[localColorIndex]
                        : (byte)0;
                    masterCells[globalIndex] = globalColorIndex;

                    if (globalColorIndex == 0)
                    {
                        masterQuantized[globalIndex] = letterboxColor;
                    }
                    else
                    {
                        masterQuantized[globalIndex] = globalPalette[globalColorIndex - 1];
                    }
                }
            }
        }

        Color32[] mergedPalette = globalPalette.ToArray();
        ColorQuantizer.FillEdgeCellsFromNeighbors(masterCells, gridWidth, gridHeight);
        for (int i = 0; i < masterCells.Length; i++)
        {
            byte colorIndex = masterCells[i];
            if (colorIndex == 0)
            {
                masterQuantized[i] = letterboxColor;
            }
            else
            {
                masterQuantized[i] = globalPalette[colorIndex - 1];
            }
        }

        return new ComposeResult
        {
            level = new ImageProcessor.Result
            {
                width = gridWidth,
                height = gridHeight,
                letterboxColor = letterboxColor,
                resizedPixels = masterQuantized,
                quantizedPixels = masterQuantized,
                palette = mergedPalette,
                cellIndices = masterCells
            },
            tiles = tileResults.ToArray()
        };
    }

    public static void MergeTileResult(
        ImageProcessor.Result tileResult,
        int tileX,
        int tileY,
        Settings settings,
        ref byte[] masterCells,
        ref Color32[] masterPalette)
    {
        if (tileResult.cellIndices == null || masterCells == null)
        {
            return;
        }

        settings.tileCellWidth = Mathf.Clamp(settings.tileCellWidth, 1, LevelData.MaxTileCellDimension);
        settings.tileCellHeight = Mathf.Clamp(settings.tileCellHeight, 1, LevelData.MaxTileCellDimension);
        settings.tilesX = Mathf.Max(1, settings.tilesX);
        settings.tilesY = Mathf.Max(1, settings.tilesY);

        if (tileX < 0 || tileY < 0 || tileX >= settings.tilesX || tileY >= settings.tilesY)
        {
            return;
        }

        int gridWidth = settings.tilesX * settings.tileCellWidth;
        int gridHeight = settings.tilesY * settings.tileCellHeight;
        if (masterCells.Length != gridWidth * gridHeight)
        {
            return;
        }

        if (masterPalette == null)
        {
            masterPalette = new Color32[0];
        }

        List<Color32> globalPalette = new List<Color32>(masterPalette);
        byte[] localToGlobal = BuildLocalToGlobalRemap(tileResult.palette, globalPalette);
        int baseX = tileX * settings.tileCellWidth;
        int baseY = tileY * settings.tileCellHeight;

        for (int localY = 0; localY < settings.tileCellHeight; localY++)
        {
            for (int localX = 0; localX < settings.tileCellWidth; localX++)
            {
                int localIndex = localY * settings.tileCellWidth + localX;
                byte localColorIndex = tileResult.cellIndices[localIndex];
                int globalX = baseX + localX;
                int globalY = baseY + localY;
                int globalIndex = globalY * gridWidth + globalX;

                masterCells[globalIndex] = localColorIndex < localToGlobal.Length
                    ? localToGlobal[localColorIndex]
                    : (byte)0;
            }
        }

        masterPalette = globalPalette.ToArray();
        ColorQuantizer.FillEdgeCellsFromNeighbors(masterCells, gridWidth, gridHeight);
    }

    public static Settings CreateSettings(
        int tilesX,
        int tilesY,
        int tileCellWidth,
        int tileCellHeight,
        int defaultColorCount,
        bool defaultAutoColorCount,
        byte alphaThreshold,
        bool defaultAutoBackgroundColor,
        Color32 defaultBackgroundColor,
        SandColorPalette defaultTargetPalette)
    {
        return new Settings
        {
            tilesX = tilesX,
            tilesY = tilesY,
            tileCellWidth = tileCellWidth,
            tileCellHeight = tileCellHeight,
            defaultColorCount = defaultColorCount,
            defaultAutoColorCount = defaultAutoColorCount,
            alphaThreshold = alphaThreshold,
            defaultAutoBackgroundColor = defaultAutoBackgroundColor,
            defaultBackgroundColor = defaultBackgroundColor,
            defaultTargetPalette = defaultTargetPalette
        };
    }

    public static TileInput CreateTileInput(
        Color32[] pixels,
        int sourceWidth,
        int sourceHeight,
        int tileX,
        int tileY,
        int colorCount,
        bool autoColorCount,
        byte alphaThreshold,
        bool autoBackgroundColor,
        Color32 backgroundColor,
        SandColorPalette targetPalette)
    {
        return new TileInput
        {
            pixels = pixels,
            sourceWidth = sourceWidth,
            sourceHeight = sourceHeight,
            tileX = tileX,
            tileY = tileY,
            colorCount = colorCount,
            autoColorCount = autoColorCount,
            alphaThreshold = alphaThreshold,
            autoBackgroundColor = autoBackgroundColor,
            backgroundColor = backgroundColor,
            targetPalette = targetPalette
        };
    }

    private static ImageProcessor.Result EmptyResult(int width, int height)
    {
        int cellCount = width * height;
        return new ImageProcessor.Result
        {
            width = width,
            height = height,
            letterboxColor = new Color32(0, 0, 0, 0),
            resizedPixels = new Color32[cellCount],
            quantizedPixels = new Color32[cellCount],
            palette = new Color32[0],
            cellIndices = new byte[cellCount]
        };
    }

    private static byte[] BuildLocalToGlobalRemap(Color32[] localPalette, List<Color32> globalPalette)
    {
        int maxLocalIndex = localPalette != null ? localPalette.Length : 0;
        byte[] remap = new byte[maxLocalIndex + 1];
        remap[0] = 0;

        if (localPalette == null)
        {
            return remap;
        }

        for (int i = 0; i < localPalette.Length; i++)
        {
            byte localIndex = (byte)(i + 1);
            remap[localIndex] = (byte)FindOrAddGlobalColor(globalPalette, localPalette[i]);
        }

        return remap;
    }

    private static int FindOrAddGlobalColor(List<Color32> globalPalette, Color32 color)
    {
        for (int i = 0; i < globalPalette.Count; i++)
        {
            if (ColorsEqual(globalPalette[i], color))
            {
                return i + 1;
            }
        }

        if (globalPalette.Count >= SandColorPalette.MaxColorCount)
        {
            return ColorQuantizer.FindNearestPaletteIndex(color, globalPalette.ToArray()) + 1;
        }

        globalPalette.Add(color);
        return globalPalette.Count;
    }

    private static bool ColorsEqual(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
