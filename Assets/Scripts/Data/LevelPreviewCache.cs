using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Preview textures owned by a gallery page or inspector; dispose when it closes.</summary>
public sealed class LevelPreviewCache : IDisposable
{
    private sealed class Entry
    {
        public int width;
        public int height;
        public byte[] cells;
        public Color32[] palette;
        public Texture2D texture;

        public bool Matches(LevelData level, byte[] currentCells)
        {
            if (texture == null || width != level.gridWidth || height != level.gridHeight
                || cells.Length != currentCells.Length || palette.Length != level.palette.Length) return false;
            for (int i = 0; i < cells.Length; i++) if (cells[i] != currentCells[i]) return false;
            for (int i = 0; i < palette.Length; i++)
            {
                Color32 a = palette[i], b = level.palette[i];
                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) return false;
            }
            return true;
        }
    }

    private readonly Dictionary<LevelData, Entry> entries = new Dictionary<LevelData, Entry>();

    public Texture2D Get(LevelData level)
    {
        if (level == null) return null;
        byte[] cells = level.cells;
        if (level.gridWidth <= 0 || level.gridHeight <= 0 || cells.Length != (long)level.gridWidth * level.gridHeight
            || level.palette == null || level.palette.Length == 0) return null;
        if (entries.TryGetValue(level, out Entry entry))
        {
            if (entry.Matches(level, cells)) return entry.texture;
            Release(entry.texture);
        }

        // Point filtering repeats each logical pixel without storing a 4x/8x enlarged PNG.
        Texture2D texture = ColorQuantizer.BuildPreviewTexture(level.gridWidth, level.gridHeight, cells, level.palette, 1);
        texture.name = level.name + " (Generated Preview)";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply(false, true);
        entries[level] = new Entry { width = level.gridWidth, height = level.gridHeight,
            cells = (byte[])cells.Clone(), palette = (Color32[])level.palette.Clone(), texture = texture };
        return texture;
    }

    public void Clear()
    {
        foreach (Entry entry in entries.Values) Release(entry.texture);
        entries.Clear();
    }

    public void Dispose() { Clear(); }

    private static void Release(Texture2D texture)
    {
        if (texture == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
        else UnityEngine.Object.DestroyImmediate(texture);
    }
}
