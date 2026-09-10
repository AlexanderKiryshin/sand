using UnityEngine;

/// <summary>Four full-color illustrations share one imported, cached texture.</summary>
public static class GameBoosterArt
{
    private static Texture2D atlas;
    private static readonly Sprite[] Sprites = new Sprite[4];

    public static Sprite Get(GameUiSymbol symbol)
    {
        int index = symbol == GameUiSymbol.Magnet ? 0 : symbol == GameUiSymbol.Fill ? 1
            : symbol == GameUiSymbol.Shuffle ? 2 : symbol == GameUiSymbol.Lock ? 3 : -1;
        if (index < 0) return null;
        if (Sprites[index] != null) return Sprites[index];
        if (atlas == null) atlas = Resources.Load<Texture2D>("UI/BoosterAtlas");
        if (atlas == null) return null;
        int width = atlas.width / 2, height = atlas.height / 2;
        var rect = new Rect(index % 2 * width, (1 - index / 2) * height, width, height);
        Sprites[index] = Sprite.Create(atlas, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        Sprites[index].name = "Illustrated" + symbol;
        return Sprites[index];
    }
}
