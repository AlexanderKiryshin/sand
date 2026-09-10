using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SandRenderer : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private int pixelsPerCell = 16;
    [SerializeField] private int subGrainsPerAxis = 1;
    [SerializeField] [Range(0f, 1f)] private float grainStrength = 0.12f;

    public int DisplayScale { get { return pixelsPerCell; } }
    public int SubGrainsPerAxis { get { return subGrainsPerAxis; } }
    public float GrainStrength { get { return grainStrength; } }

    public float SubGrainScreenFraction
    {
        get { return 1f / subGrainsPerAxis; }
    }

    private SandGrid grid;
    private Texture2D texture;
    private Color32[] pixelBuffer;
    private Color32[] uploadScratch;
    private int pixelsPerSimCell;
    private int textureWidth;
    private int textureHeight;
    private bool introPourActive;
    private Sprite runtimeSprite;

    public bool IsIntroPourActive { get { return introPourActive; } }
    private static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

    public void Initialize(SandGrid sandGrid)
    {
        ReleaseTexture();
        grid = sandGrid;
        subGrainsPerAxis = grid.SubGrainsPerAxis;
        pixelsPerCell = Mathf.Max(subGrainsPerAxis, pixelsPerCell);
        pixelsPerSimCell = pixelsPerCell / subGrainsPerAxis;

        textureWidth = grid.Width * pixelsPerSimCell;
        textureHeight = grid.Height * pixelsPerSimCell;
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        pixelBuffer = new Color32[textureWidth * textureHeight];

        if (targetImage != null)
        {
            targetImage.texture = texture;
        }

        if (targetSpriteRenderer != null)
        {
            runtimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureWidth, textureHeight),
                new Vector2(0.5f, 0.5f),
                pixelsPerSimCell);
            targetSpriteRenderer.sprite = runtimeSprite;
        }

        RebuildPixelBuffer();
        ClearTexture();
        grid.ClearDirty();
    }

    public void RefreshFull()
    {
        if (grid == null || texture == null)
        {
            return;
        }

        CompleteIntroPour();
        RebuildPixelBuffer();
        ApplyTextureFull();
        grid.ClearDirty();
    }

    public void RefreshDirty()
    {
        if (grid == null || texture == null)
        {
            return;
        }

        if (introPourActive)
        {
            grid.ClearDirty();
            return;
        }

        List<int> dirty = grid.TakeDirtyIndices();
        if (dirty.Count == 0)
        {
            return;
        }

        int gridWidth = grid.Width;
        int gridHeight = grid.Height;
        int pixelMinX = textureWidth;
        int pixelMinY = textureHeight;
        int pixelMaxX = -1;
        int pixelMaxY = -1;

        for (int i = 0; i < dirty.Count; i++)
        {
            int index = dirty[i];
            int x = index % gridWidth;
            int y = index / gridWidth;
            WriteSimCell(x, y, grid.GetCell(x, y));

            int cellOriginX = x * pixelsPerSimCell;
            int displayRow = gridHeight - 1 - y;
            int cellOriginY = displayRow * pixelsPerSimCell;
            int cellMaxX = cellOriginX + pixelsPerSimCell - 1;
            int cellMaxY = cellOriginY + pixelsPerSimCell - 1;

            if (cellOriginX < pixelMinX)
            {
                pixelMinX = cellOriginX;
            }

            if (cellOriginY < pixelMinY)
            {
                pixelMinY = cellOriginY;
            }

            if (cellMaxX > pixelMaxX)
            {
                pixelMaxX = cellMaxX;
            }

            if (cellMaxY > pixelMaxY)
            {
                pixelMaxY = cellMaxY;
            }
        }

        if (pixelMaxX < pixelMinX || pixelMaxY < pixelMinY)
        {
            grid.ClearDirty();
            return;
        }

        int dirtyPixelCount = (pixelMaxX - pixelMinX + 1) * (pixelMaxY - pixelMinY + 1);
        if (dirtyPixelCount >= pixelBuffer.Length / 2)
        {
            ApplyTextureFull();
        }
        else
        {
            ApplyTextureRegion(pixelMinX, pixelMinY, pixelMaxX, pixelMaxY);
        }

        grid.ClearDirty();
    }

    private void WriteSimCell(int x, int y, byte colorIndex)
    {
        int gridHeight = grid.Height;
        int cellOriginX = x * pixelsPerSimCell;
        int displayRow = gridHeight - 1 - y;
        int cellOriginY = displayRow * pixelsPerSimCell;
        Color32 fillColor = colorIndex == 0
            ? ClearPixel
            : grid.GetDisplayColor(x, y);

        for (int py = 0; py < pixelsPerSimCell; py++)
        {
            int rowOffset = (cellOriginY + py) * textureWidth + cellOriginX;
            for (int px = 0; px < pixelsPerSimCell; px++)
            {
                // Small, restrained facets keep the painting legible while giving sand depth.
                float light = py == pixelsPerSimCell - 1 ? 1.035f : py == 0 ? 0.965f : 1f;
                pixelBuffer[rowOffset + px] = colorIndex == 0 ? ClearPixel : new Color32(
                    (byte)Mathf.Min(255, fillColor.r * light),
                    (byte)Mathf.Min(255, fillColor.g * light),
                    (byte)Mathf.Min(255, fillColor.b * light), fillColor.a);
            }
        }
    }

    private void ApplyTextureFull()
    {
        texture.SetPixels32(pixelBuffer);
        texture.Apply(false);
    }

    private void ApplyTextureRegion(int pixelMinX, int pixelMinY, int pixelMaxX, int pixelMaxY)
    {
        int regionWidth = pixelMaxX - pixelMinX + 1;
        int regionHeight = pixelMaxY - pixelMinY + 1;
        int required = regionWidth * regionHeight;
        if (uploadScratch == null || uploadScratch.Length < required)
        {
            uploadScratch = new Color32[required];
        }

        int dst = 0;
        for (int py = pixelMinY; py <= pixelMaxY; py++)
        {
            int rowStart = py * textureWidth + pixelMinX;
            Array.Copy(pixelBuffer, rowStart, uploadScratch, dst, regionWidth);
            dst += regionWidth;
        }

        texture.SetPixels32(pixelMinX, pixelMinY, regionWidth, regionHeight, uploadScratch);
        texture.Apply(false);
    }

    public void PrepareForIntro()
    {
        CompleteIntroPour();

        if (grid == null || texture == null)
        {
            return;
        }

        RebuildPixelBuffer();
        ClearTexture();
        grid.ClearDirty();
        introPourActive = true;
    }

    public void RevealLevelBlock(int levelX, int levelY, int stride)
    {
        if (grid == null || texture == null || stride < 1)
        {
            return;
        }

        int sub = grid.SubGrainsPerAxis;
        int levelWidth = grid.LevelWidth;
        int levelHeight = grid.LevelHeight;
        int pixelMinX = textureWidth;
        int pixelMinY = textureHeight;
        int pixelMaxX = -1;
        int pixelMaxY = -1;

        for (int blockY = 0; blockY < stride; blockY++)
        {
            int ly = levelY + blockY;
            if (ly < 0 || ly >= levelHeight)
            {
                continue;
            }

            for (int blockX = 0; blockX < stride; blockX++)
            {
                int lx = levelX + blockX;
                if (lx < 0 || lx >= levelWidth)
                {
                    continue;
                }

                int originX = lx * sub;
                int originY = ly * sub;
                for (int sy = 0; sy < sub; sy++)
                {
                    for (int sx = 0; sx < sub; sx++)
                    {
                        int simX = originX + sx;
                        int simY = originY + sy;
                        if (grid.GetCell(simX, simY) == 0)
                        {
                            continue;
                        }

                        WriteSimCell(simX, simY, grid.GetCell(simX, simY));

                        int cellOriginX = simX * pixelsPerSimCell;
                        int displayRow = grid.Height - 1 - simY;
                        int cellOriginY = displayRow * pixelsPerSimCell;
                        int cellMaxX = cellOriginX + pixelsPerSimCell - 1;
                        int cellMaxY = cellOriginY + pixelsPerSimCell - 1;

                        if (cellOriginX < pixelMinX)
                        {
                            pixelMinX = cellOriginX;
                        }

                        if (cellOriginY < pixelMinY)
                        {
                            pixelMinY = cellOriginY;
                        }

                        if (cellMaxX > pixelMaxX)
                        {
                            pixelMaxX = cellMaxX;
                        }

                        if (cellMaxY > pixelMaxY)
                        {
                            pixelMaxY = cellMaxY;
                        }
                    }
                }
            }
        }

        if (pixelMaxX < pixelMinX || pixelMaxY < pixelMinY)
        {
            return;
        }

        int dirtyPixelCount = (pixelMaxX - pixelMinX + 1) * (pixelMaxY - pixelMinY + 1);
        if (dirtyPixelCount >= pixelBuffer.Length / 2)
        {
            ApplyTextureFull();
        }
        else
        {
            ApplyTextureRegion(pixelMinX, pixelMinY, pixelMaxX, pixelMaxY);
        }
    }

    public void PlayIntroPour(float duration = 1.2f)
    {
        // The dedicated animator owns the animated reveal. Without it, display a complete board.
        RefreshFull();
    }

    private void OnDestroy() { ReleaseTexture(); }

    private void ReleaseTexture()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (texture != null) Destroy(texture);
        runtimeSprite = null;
        texture = null;
    }

    public void CompleteIntroPour()
    {
        if (!introPourActive)
        {
            return;
        }

        introPourActive = false;
        ApplyTextureFull();
    }

    private void RebuildPixelBuffer()
    {
        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = ClearPixel;
        }

        int gridWidth = grid.Width;
        int gridHeight = grid.Height;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                WriteSimCell(x, y, grid.GetCell(x, y));
            }
        }
    }

    private void ClearTexture()
    {
        if (uploadScratch == null || uploadScratch.Length < pixelBuffer.Length)
        {
            uploadScratch = new Color32[pixelBuffer.Length];
        }

        Array.Clear(uploadScratch, 0, pixelBuffer.Length);
        texture.SetPixels32(uploadScratch);
        texture.Apply(false);
    }
}
