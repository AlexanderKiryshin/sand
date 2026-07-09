using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LevelDataColorRecalculator
{
    public static bool RecalculateFromTexture(
        LevelData levelData,
        Texture2D sourceTexture,
        SandColorPalette targetPalette,
        int colorCount,
        bool autoColorCount,
        bool autoBackgroundColor,
        Color32 backgroundColor,
        int previewScale)
    {
        if (levelData == null)
        {
            return false;
        }

        if (levelData.usesTiles && levelData.tileSources != null && levelData.tileSources.Length > 0)
        {
            return RecalculateFromTiles(levelData, targetPalette, colorCount, autoColorCount, previewScale);
        }

        if (sourceTexture == null)
        {
            return false;
        }

        Texture2D readableTexture = EnsureReadable(sourceTexture);
        ImageProcessor.Settings settings = ImageProcessor.Settings.Create(
            levelData.gridWidth,
            levelData.gridHeight,
            colorCount);
        settings.autoColorCount = autoColorCount;
        settings.targetPalette = targetPalette;
        settings.autoBackgroundColor = autoBackgroundColor;
        settings.backgroundColor = backgroundColor;

        ImageProcessor.Result result = ImageProcessor.Process(readableTexture, settings);

        if (!ReferenceEquals(readableTexture, sourceTexture))
        {
            Object.DestroyImmediate(readableTexture);
        }

        ApplyResult(levelData, result, previewScale);
        return true;
    }

    public static bool RecalculateFromTiles(
        LevelData levelData,
        SandColorPalette targetPalette,
        int colorCount,
        bool autoColorCount,
        int previewScale)
    {
        if (levelData == null || !levelData.usesTiles || levelData.tileSources == null || levelData.tileSources.Length == 0)
        {
            return false;
        }

        TileLevelComposer.Settings settings = new TileLevelComposer.Settings
        {
            tilesX = Mathf.Max(1, levelData.tilesX),
            tilesY = Mathf.Max(1, levelData.tilesY),
            tileCellWidth = Mathf.Max(1, levelData.tileCellWidth),
            tileCellHeight = Mathf.Max(1, levelData.tileCellHeight),
            defaultColorCount = colorCount,
            defaultAutoColorCount = autoColorCount,
            alphaThreshold = 128,
            defaultAutoBackgroundColor = true,
            defaultBackgroundColor = new Color32(255, 255, 255, 255),
            defaultTargetPalette = targetPalette
        };

        string spriteSheetPath = GetSpriteSheetPath(levelData);
        if (levelData.usesUnitySpriteSheet && !string.IsNullOrEmpty(spriteSheetPath))
        {
            if (!UnitySpriteTileLoader.TryLoadTiles(
                    spriteSheetPath,
                    out UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles,
                    out UnitySpriteTileLoader.GridInfo grid,
                    out string error))
            {
                Debug.LogWarning("Recalculate from tiles failed: " + error);
                return false;
            }

            settings.tilesX = grid.tilesX;
            settings.tilesY = grid.tilesY;
            List<TileLevelComposer.TileInput> tileInputs = BuildTileInputsFromSprites(
                levelData,
                spriteTiles,
                targetPalette,
                colorCount,
                autoColorCount);
            if (tileInputs.Count == 0)
            {
                return false;
            }

            TileLevelComposer.ComposeResult composeResult = TileLevelComposer.Compose(tileInputs, settings);
            UpdateTileSourcePalettes(levelData, composeResult.tiles);
            ApplyResult(levelData, composeResult.level, previewScale);
            return true;
        }

        List<TileLevelComposer.TileInput> legacyTileInputs = BuildLegacyPerFileTileInputs(
            levelData,
            targetPalette,
            colorCount,
            autoColorCount);
        if (legacyTileInputs.Count == 0)
        {
            return false;
        }

        TileLevelComposer.ComposeResult legacyResult = TileLevelComposer.Compose(legacyTileInputs, settings);
        UpdateTileSourcePalettes(levelData, legacyResult.tiles);
        ApplyResult(levelData, legacyResult.level, previewScale);
        return true;
    }

    private static string GetSpriteSheetPath(LevelData levelData)
    {
        if (!string.IsNullOrEmpty(levelData.spriteSheetAssetPath))
        {
            return levelData.spriteSheetAssetPath;
        }

        if (levelData.tileSources != null && levelData.tileSources.Length > 0)
        {
            return levelData.tileSources[0].assetPath;
        }

        return string.Empty;
    }

    private static List<TileLevelComposer.TileInput> BuildTileInputsFromSprites(
        LevelData levelData,
        UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles,
        SandColorPalette targetPalette,
        int defaultColorCount,
        bool defaultAutoColorCount)
    {
        List<TileLevelComposer.TileInput> inputs = new List<TileLevelComposer.TileInput>(spriteTiles.Length);
        for (int i = 0; i < spriteTiles.Length; i++)
        {
            UnitySpriteTileLoader.SpriteTileInfo spriteTile = spriteTiles[i];
            TileSourceData stored = FindTileSource(levelData, spriteTile.spriteName, spriteTile.tileX, spriteTile.tileY);
            SandColorPalette tilePalette = ResolveTilePalette(stored, targetPalette);
            int tileColorCount = stored.colorCount > 0 ? stored.colorCount : defaultColorCount;
            bool tileAutoColorCount = levelData.usesUnitySpriteSheet
                ? stored.autoColorCount
                : defaultAutoColorCount;

            inputs.Add(new TileLevelComposer.TileInput
            {
                pixels = spriteTile.pixels,
                sourceWidth = spriteTile.pixelWidth,
                sourceHeight = spriteTile.pixelHeight,
                tileX = spriteTile.tileX,
                tileY = spriteTile.tileY,
                colorCount = tileColorCount,
                autoColorCount = tileAutoColorCount,
                alphaThreshold = 128,
                autoBackgroundColor = stored.autoBackgroundColor,
                backgroundColor = stored.backgroundColor,
                targetPalette = tilePalette
            });
        }

        return inputs;
    }

    private static List<TileLevelComposer.TileInput> BuildLegacyPerFileTileInputs(
        LevelData levelData,
        SandColorPalette targetPalette,
        int colorCount,
        bool defaultAutoColorCount)
    {
        List<TileLevelComposer.TileInput> tileInputs = new List<TileLevelComposer.TileInput>(levelData.tileSources.Length);
        for (int i = 0; i < levelData.tileSources.Length; i++)
        {
            TileSourceData source = levelData.tileSources[i];
            if (string.IsNullOrEmpty(source.assetPath))
            {
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source.assetPath);
            if (texture == null)
            {
                continue;
            }

            SandColorPalette tilePalette = ResolveTilePalette(source, targetPalette);
            int tileColorCount = source.colorCount > 0 ? source.colorCount : colorCount;
            bool tileAutoColorCount = defaultAutoColorCount;

            Texture2D readableTexture = EnsureReadable(texture);
            tileInputs.Add(new TileLevelComposer.TileInput
            {
                pixels = readableTexture.GetPixels32(),
                sourceWidth = readableTexture.width,
                sourceHeight = readableTexture.height,
                tileX = source.tileX,
                tileY = source.tileY,
                colorCount = tileColorCount,
                autoColorCount = tileAutoColorCount,
                alphaThreshold = 128,
                autoBackgroundColor = source.autoBackgroundColor,
                backgroundColor = source.backgroundColor,
                targetPalette = tilePalette
            });

            if (!ReferenceEquals(readableTexture, texture))
            {
                Object.DestroyImmediate(readableTexture);
            }
        }

        return tileInputs;
    }

    private static TileSourceData FindTileSource(LevelData levelData, string spriteName, int tileX, int tileY)
    {
        TileSourceData fallback = default;
        bool hasFallback = false;

        for (int i = 0; i < levelData.tileSources.Length; i++)
        {
            TileSourceData source = levelData.tileSources[i];
            if (!string.IsNullOrEmpty(spriteName) && source.spriteName == spriteName)
            {
                return source;
            }

            if (source.tileX == tileX && source.tileY == tileY)
            {
                fallback = source;
                hasFallback = true;
            }
        }

        return hasFallback ? fallback : levelData.tileSources[0];
    }

    private static SandColorPalette ResolveTilePalette(TileSourceData source, SandColorPalette targetPalette)
    {
        if (!string.IsNullOrEmpty(source.targetPaletteAssetPath))
        {
            SandColorPalette storedPalette = AssetDatabase.LoadAssetAtPath<SandColorPalette>(source.targetPaletteAssetPath);
            if (storedPalette != null)
            {
                return storedPalette;
            }
        }

        return targetPalette;
    }

    private static void UpdateTileSourcePalettes(LevelData levelData, TileLevelComposer.TileProcessResult[] tileResults)
    {
        if (levelData.tileSources == null || tileResults == null)
        {
            return;
        }

        for (int i = 0; i < tileResults.Length; i++)
        {
            TileLevelComposer.TileProcessResult tileResult = tileResults[i];
            for (int s = 0; s < levelData.tileSources.Length; s++)
            {
                TileSourceData source = levelData.tileSources[s];
                if (source.tileX == tileResult.tileX && source.tileY == tileResult.tileY)
                {
                    source.tilePalette = tileResult.result.palette != null
                        ? (Color32[])tileResult.result.palette.Clone()
                        : new Color32[0];
                    levelData.tileSources[s] = source;
                    break;
                }
            }
        }
    }

    public static bool RemapToPalette(LevelData levelData, SandColorPalette targetPalette, int colorCount)
    {
        if (levelData == null || targetPalette == null || levelData.cells == null || levelData.cells.Length == 0)
        {
            return false;
        }

        int expected = levelData.gridWidth * levelData.gridHeight;
        if (levelData.cells.Length != expected)
        {
            return false;
        }

        Color32[] sourceColors = targetPalette.GetColors();
        if (sourceColors.Length == 0)
        {
            return false;
        }

        Color32[] pixels = new Color32[levelData.cells.Length];
        bool[] isPadding = new bool[levelData.cells.Length];
        for (int i = 0; i < levelData.cells.Length; i++)
        {
            byte colorIndex = levelData.cells[i];
            if (colorIndex == 0)
            {
                isPadding[i] = true;
                pixels[i] = new Color32(0, 0, 0, 0);
            }
            else
            {
                isPadding[i] = false;
                pixels[i] = levelData.GetPaletteColor(colorIndex);
            }
        }

        colorCount = Mathf.Clamp(colorCount, 1, SandColorPalette.MaxColorCount);
        Color32[] newPalette = ColorQuantizer.SelectPaletteFromSource(
            pixels,
            isPadding,
            sourceColors,
            colorCount,
            128);

        if (newPalette.Length == 0)
        {
            return false;
        }

        byte[] newCells = new byte[levelData.cells.Length];
        for (int i = 0; i < levelData.cells.Length; i++)
        {
            if (isPadding[i])
            {
                newCells[i] = 0;
                continue;
            }

            newCells[i] = (byte)(ColorQuantizer.FindNearestPaletteIndex(pixels[i], newPalette) + 1);
        }

        levelData.palette = newPalette;
        levelData.cells = newCells;
        LevelEditorUtility.ApplyBucketLayoutRules(levelData);
        return true;
    }

    private static void ApplyResult(LevelData levelData, ImageProcessor.Result result, int previewScale)
    {
        levelData.palette = result.palette;
        levelData.cells = result.cellIndices;
        LevelEditorUtility.ApplyBucketLayoutRules(levelData);

        if (levelData.previewTexture != null)
        {
            Object.DestroyImmediate(levelData.previewTexture, true);
        }

        previewScale = Mathf.Clamp(previewScale, 1, 16);
        levelData.previewTexture = ImageProcessor.BuildTexture(result, previewScale);
    }

    private static Texture2D EnsureReadable(Texture2D texture)
    {
        try
        {
            texture.GetPixel(0, 0);
            return texture;
        }
        catch
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, renderTexture);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;

            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            return copy;
        }
    }
}
