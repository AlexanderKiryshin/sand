using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Читает тайлы из Unity Sprite Sheet (Texture Type: Sprite, Mode: Multiple).
/// Позиция тайла в сетке определяется по rect спрайта в атласе.
/// </summary>
public static class UnitySpriteTileLoader
{
    public struct SpriteTileInfo
    {
        public string spriteName;
        public int tileX;
        public int tileY;
        public int pixelWidth;
        public int pixelHeight;
        public Color32[] pixels;
    }

    public struct GridInfo
    {
        public int tilesX;
        public int tilesY;
        public int spriteCount;
    }

    public static bool TryLoadTiles(
        string textureAssetPath,
        out SpriteTileInfo[] tiles,
        out GridInfo grid,
        out string errorMessage)
    {
        tiles = new SpriteTileInfo[0];
        grid = new GridInfo();
        errorMessage = null;

        if (string.IsNullOrEmpty(textureAssetPath))
        {
            errorMessage = "Sprite sheet path is empty.";
            return false;
        }

        TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
        if (importer == null)
        {
            errorMessage = "Asset is not a texture: " + textureAssetPath;
            return false;
        }

        if (importer.textureType != TextureImporterType.Sprite)
        {
            errorMessage =
                "Texture must be Sprite (2D and UI).\n"
                + "Select the texture, set Texture Type to Sprite (2D and UI), Sprite Mode to Multiple, "
                + "then slice tiles in the Sprite Editor.";
            return false;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            errorMessage =
                "Sprite Mode must be Multiple.\n"
                + "Open Sprite Editor and slice the image into tiles.";
            return false;
        }

        Sprite[] sprites = LoadSprites(textureAssetPath);
        if (sprites.Length == 0)
        {
            errorMessage =
                "No sub-sprites found on this texture.\n"
                + "Open Sprite Editor and slice the image into tiles.";
            return false;
        }

        List<List<Sprite>> rows = GroupSpritesIntoRows(sprites);
        List<SpriteTileInfo> tileList = new List<SpriteTileInfo>(sprites.Length);
        int tilesX = 0;

        for (int tileY = 0; tileY < rows.Count; tileY++)
        {
            List<Sprite> row = rows[tileY];
            tilesX = Mathf.Max(tilesX, row.Count);

            for (int tileX = 0; tileX < row.Count; tileX++)
            {
                Sprite sprite = row[tileX];
                if (!TryGetSpritePixels(sprite, out Color32[] pixels, out int width, out int height))
                {
                    errorMessage = "Could not read pixels for sprite: " + sprite.name
                        + "\nEnable Read/Write on the texture import settings.";
                    return false;
                }

                tileList.Add(new SpriteTileInfo
                {
                    spriteName = sprite.name,
                    tileX = tileX,
                    tileY = tileY,
                    pixelWidth = width,
                    pixelHeight = height,
                    pixels = pixels
                });
            }
        }

        tiles = tileList.ToArray();
        grid = new GridInfo
        {
            tilesX = Mathf.Max(1, tilesX),
            tilesY = Mathf.Max(1, rows.Count),
            spriteCount = tiles.Length
        };
        return true;
    }

    public static bool TryFindSprite(string textureAssetPath, string spriteName, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(textureAssetPath) || string.IsNullOrEmpty(spriteName))
        {
            return false;
        }

        Sprite[] sprites = LoadSprites(textureAssetPath);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i].name == spriteName)
            {
                sprite = sprites[i];
                return true;
            }
        }

        return false;
    }

    public static List<TileLevelComposer.TileInput> BuildTileInputs(
        SpriteTileInfo[] spriteTiles,
        int colorCount,
        bool autoColorCount,
        byte alphaThreshold,
        bool autoBackgroundColor,
        Color32 backgroundColor,
        SandColorPalette targetPalette)
    {
        List<TileLevelComposer.TileInput> inputs = new List<TileLevelComposer.TileInput>(spriteTiles.Length);
        for (int i = 0; i < spriteTiles.Length; i++)
        {
            SpriteTileInfo info = spriteTiles[i];
            inputs.Add(new TileLevelComposer.TileInput
            {
                pixels = info.pixels,
                sourceWidth = info.pixelWidth,
                sourceHeight = info.pixelHeight,
                tileX = info.tileX,
                tileY = info.tileY,
                colorCount = colorCount,
                autoColorCount = autoColorCount,
                alphaThreshold = alphaThreshold,
                autoBackgroundColor = autoBackgroundColor,
                backgroundColor = backgroundColor,
                targetPalette = targetPalette
            });
        }

        return inputs;
    }

    private static Sprite[] LoadSprites(string textureAssetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(textureAssetPath);
        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static List<List<Sprite>> GroupSpritesIntoRows(Sprite[] sprites)
    {
        List<Sprite> sorted = new List<Sprite>(sprites);
        sorted.Sort(CompareSpritesForGrid);

        List<List<Sprite>> rows = new List<List<Sprite>>();
        for (int i = 0; i < sorted.Count; i++)
        {
            Sprite sprite = sorted[i];
            if (rows.Count == 0)
            {
                rows.Add(new List<Sprite> { sprite });
                continue;
            }

            List<Sprite> lastRow = rows[rows.Count - 1];
            float rowY = lastRow[0].textureRect.y;
            float spriteY = sprite.textureRect.y;
            float rowTolerance = Mathf.Max(1f, lastRow[0].textureRect.height * 0.25f);
            if (Mathf.Abs(spriteY - rowY) <= rowTolerance)
            {
                lastRow.Add(sprite);
            }
            else
            {
                rows.Add(new List<Sprite> { sprite });
            }
        }

        for (int r = 0; r < rows.Count; r++)
        {
            rows[r].Sort((Sprite a, Sprite b) => a.textureRect.x.CompareTo(b.textureRect.x));
        }

        return rows;
    }

    private static int CompareSpritesForGrid(Sprite a, Sprite b)
    {
        int yCompare = b.textureRect.y.CompareTo(a.textureRect.y);
        if (yCompare != 0)
        {
            return yCompare;
        }

        return a.textureRect.x.CompareTo(b.textureRect.x);
    }

    public static bool TryGetSpritePixels(Sprite sprite, out Color32[] pixels, out int width, out int height)
    {
        pixels = null;
        width = 0;
        height = 0;
        if (sprite == null || sprite.texture == null)
        {
            return false;
        }

        Texture2D readableTexture = EnsureReadable(sprite.texture);
        Rect rect = sprite.textureRect;
        int x = Mathf.FloorToInt(rect.x);
        int y = Mathf.FloorToInt(rect.y);
        width = Mathf.FloorToInt(rect.width);
        height = Mathf.FloorToInt(rect.height);

        if (width <= 0 || height <= 0)
        {
            if (!ReferenceEquals(readableTexture, sprite.texture))
            {
                UnityEngine.Object.DestroyImmediate(readableTexture);
            }

            return false;
        }

        try
        {
            Color[] raw = readableTexture.GetPixels(x, y, width, height);
            pixels = new Color32[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                pixels[i] = raw[i];
            }
        }
        catch
        {
            if (!ReferenceEquals(readableTexture, sprite.texture))
            {
                UnityEngine.Object.DestroyImmediate(readableTexture);
            }

            return false;
        }

        if (!ReferenceEquals(readableTexture, sprite.texture))
        {
            UnityEngine.Object.DestroyImmediate(readableTexture);
        }

        return true;
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
