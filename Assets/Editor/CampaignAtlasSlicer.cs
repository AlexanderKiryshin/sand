using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Find the actual seams in generated atlas grids before sampling their paintings.</summary>
public static class CampaignAtlasSlicer
{
    private sealed class Grid { public int[] x, y; }
    private static readonly Dictionary<Texture2D, Grid> Cache = new Dictionary<Texture2D, Grid>();
    public static void ClearCache() => Cache.Clear();

    public static RectInt TileRect(Texture2D atlas, int tile, int columns, int rows)
    {
        if (tile < 0 || tile >= columns * rows) throw new ArgumentOutOfRangeException(nameof(tile));
        if (!Cache.TryGetValue(atlas, out Grid grid))
        {
            Color32[] pixels = atlas.GetPixels32();
            grid = new Grid
            {
                x = FindSeams(pixels, atlas.width, atlas.height, columns, true),
                y = FindSeams(pixels, atlas.width, atlas.height, rows, false)
            };
            Cache.Add(atlas, grid);
        }
        int column = tile % columns;
        // Unity texture rows start at the bottom; atlas scene numbering starts at the top.
        int row = rows - 1 - tile / columns;
        const int inset = 2; // Exclude blended pixels on both sides of each seam.
        return new RectInt(grid.x[column] + inset, grid.y[row] + inset,
            grid.x[column + 1] - grid.x[column] - inset * 2,
            grid.y[row + 1] - grid.y[row] - inset * 2);
    }

    private static int[] FindSeams(Color32[] pixels, int width, int height, int count, bool vertical)
    {
        int length = vertical ? width : height;
        int across = vertical ? height : width;
        var seams = new int[count + 1];
        seams[count] = length;
        int radius = Mathf.Max(2, Mathf.CeilToInt(length / (float)count * .12f));
        for (int seam = 1; seam < count; seam++)
        {
            int expected = Mathf.RoundToInt(seam * length / (float)count);
            long bestScore = -1;
            for (int position = Mathf.Max(1, expected - radius); position <= Mathf.Min(length - 1, expected + radius); position++)
            {
                long score = 0;
                for (int cross = 0; cross < across; cross++)
                {
                    int index = vertical ? cross * width + position : position * width + cross;
                    Color32 a = pixels[index], b = pixels[index - (vertical ? 1 : width)];
                    score += Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
                }
                if (score > bestScore) { bestScore = score; seams[seam] = position; }
            }
        }
        return seams;
    }
}
