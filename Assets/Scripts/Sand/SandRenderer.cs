using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SandRenderer : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private int pixelsPerCell = 16;
    [SerializeField] private int subGrainsPerAxis = 4;
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
    private static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

    public void Initialize(SandGrid sandGrid)
    {
        grid = sandGrid;
        subGrainsPerAxis = grid.SubGrainsPerAxis;
        pixelsPerCell = Mathf.Max(subGrainsPerAxis, pixelsPerCell);
        pixelsPerSimCell = pixelsPerCell / subGrainsPerAxis;

        textureWidth = grid.Width * pixelsPerSimCell;
        textureHeight = grid.Height * pixelsPerSimCell;
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        pixelBuffer = new Color32[textureWidth * textureHeight];

        DisableStaticGrainOverlay();

        if (targetImage != null)
        {
            targetImage.texture = texture;
        }

        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureWidth, textureHeight),
                new Vector2(0.5f, 0.5f),
                pixelsPerSimCell);
        }

        RefreshFull();
    }

    public void RefreshFull()
    {
        if (grid == null || texture == null)
        {
            return;
        }

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

        ApplyTextureFull();
        grid.ClearDirty();
    }

    public void RefreshDirty()
    {
        if (grid == null || texture == null)
        {
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
                pixelBuffer[rowOffset + px] = fillColor;
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

    private static void DisableStaticGrainOverlay()
    {
        SandGrainOverlay overlay = FindObjectOfType<SandGrainOverlay>();
        if (overlay != null)
        {
            overlay.enabled = false;
        }
    }
}
