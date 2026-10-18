using System.Collections.Generic;
using UnityEngine;

public enum GameUiSymbol { Restart, Magnet, Fill, Shuffle, Lock, Shop, Coins, Bundle, NoAds, Close, Key, Link, Settings, Skip, Video, Levels }

/// <summary>Bold, cached UI pictograms with consistent stroke weight, independent of font glyph coverage.</summary>
public static class GameUiSymbols
{
    private static readonly Dictionary<GameUiSymbol, Sprite> Cache = new Dictionary<GameUiSymbol, Sprite>();
    public static Sprite Booster(BoosterType type) => Get(type == BoosterType.Magnet ? GameUiSymbol.Magnet : type == BoosterType.Shuffle ? GameUiSymbol.Shuffle : GameUiSymbol.Fill);

    public static Sprite Get(GameUiSymbol symbol)
    {
        if (symbol == GameUiSymbol.Coins) return BusesUiArt.Gold;
        Sprite illustration = GameBoosterArt.Get(symbol);
        if (illustration != null) return illustration;
        if (Cache.TryGetValue(symbol, out Sprite sprite) && sprite != null) return sprite;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = symbol.ToString() };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + .5f) / size, (y + .5f) / size);
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(-Distance(symbol, p) * size));
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        Cache[symbol] = sprite;
        return sprite;
    }

    private static float Distance(GameUiSymbol symbol, Vector2 p)
    {
        float d = 10;
        switch (symbol)
        {
            case GameUiSymbol.Skip:
                d = Line(p, .18f, .23f, .43f, .50f);
                d = Mathf.Min(d, Line(p, .43f, .50f, .18f, .77f));
                d = Mathf.Min(d, Line(p, .57f, .23f, .82f, .50f));
                return Mathf.Min(d, Line(p, .82f, .50f, .57f, .77f));
            case GameUiSymbol.Video:
                d = Mathf.Abs(Box(p, .5f, .5f, .37f, .28f)) - .035f;
                d = Mathf.Min(d, Line(p, .42f, .34f, .65f, .50f));
                d = Mathf.Min(d, Line(p, .65f, .50f, .42f, .66f));
                return Mathf.Min(d, Line(p, .42f, .66f, .42f, .34f));
            case GameUiSymbol.Levels:
                d = Box(p, .29f, .29f, .14f, .14f);
                d = Mathf.Min(d, Box(p, .71f, .29f, .14f, .14f));
                d = Mathf.Min(d, Box(p, .29f, .71f, .14f, .14f));
                return Mathf.Min(d, Box(p, .71f, .71f, .14f, .14f));
            case GameUiSymbol.Settings:
                Vector2 centered = p - new Vector2(.5f, .5f);
                float angle = Mathf.Atan2(centered.y, centered.x);
                float outer = .30f + .045f * Mathf.Cos(angle * 8f);
                return Mathf.Max(centered.magnitude - outer, .12f - centered.magnitude);
            case GameUiSymbol.Key:
                d = Mathf.Abs((p - new Vector2(.32f, .69f)).magnitude - .18f) - .055f;
                d = Mathf.Min(d, Line(p, .44f, .56f, .78f, .22f));
                d = Mathf.Min(d, Line(p, .63f, .35f, .73f, .45f));
                return Mathf.Min(d, Line(p, .74f, .24f, .84f, .34f));
            case GameUiSymbol.Link:
                d = Mathf.Abs((p - new Vector2(.34f, .62f)).magnitude - .18f) - .05f;
                d = Mathf.Min(d, Mathf.Abs((p - new Vector2(.66f, .38f)).magnitude - .18f) - .05f);
                return Mathf.Min(d, Line(p, .37f, .59f, .63f, .41f));
            case GameUiSymbol.Restart:
                d = Mathf.Abs((p - new Vector2(.5f, .49f)).magnitude - .29f) - .045f;
                if (p.x > .55f && p.y > .60f) d = 10;
                d = Mathf.Min(d, Line(p, .76f, .78f, .77f, .55f));
                return Mathf.Min(d, Line(p, .77f, .55f, .55f, .61f));
            case GameUiSymbol.Magnet:
                if (p.y <= .51f) d = Mathf.Abs((p - new Vector2(.5f, .51f)).magnitude - .26f) - .085f;
                d = Mathf.Min(d, Box(p, .24f, .63f, .085f, .20f));
                return Mathf.Min(d, Box(p, .76f, .63f, .085f, .20f));
            case GameUiSymbol.Fill:
                d = Line(p, .22f, .58f, .30f, .19f);
                d = Mathf.Min(d, Line(p, .30f, .19f, .71f, .19f));
                d = Mathf.Min(d, Line(p, .71f, .19f, .79f, .58f));
                d = Mathf.Min(d, Line(p, .22f, .58f, .36f, .58f));
                d = Mathf.Min(d, Line(p, .65f, .58f, .79f, .58f));
                d = Mathf.Min(d, Line(p, .50f, .87f, .50f, .43f));
                d = Mathf.Min(d, Line(p, .36f, .57f, .50f, .43f));
                return Mathf.Min(d, Line(p, .64f, .57f, .50f, .43f));
            case GameUiSymbol.Shuffle:
                d = Line(p, .18f, .25f, .32f, .25f);
                d = Mathf.Min(d, Line(p, .32f, .25f, .68f, .75f));
                d = Mathf.Min(d, Line(p, .68f, .75f, .85f, .75f));
                d = Mathf.Min(d, Line(p, .18f, .75f, .32f, .75f));
                d = Mathf.Min(d, Line(p, .32f, .75f, .68f, .25f));
                d = Mathf.Min(d, Line(p, .68f, .25f, .85f, .25f));
                d = Mathf.Min(d, Line(p, .71f, .88f, .85f, .75f));
                d = Mathf.Min(d, Line(p, .71f, .62f, .85f, .75f));
                d = Mathf.Min(d, Line(p, .71f, .38f, .85f, .25f));
                return Mathf.Min(d, Line(p, .71f, .12f, .85f, .25f));
            case GameUiSymbol.Lock:
                d = Box(p, .5f, .36f, .26f, .20f);
                if ((p - new Vector2(.5f, .38f)).magnitude < .043f) d = 10;
                if (p.y > .51f) d = Mathf.Min(d, Mathf.Abs((p - new Vector2(.5f, .59f)).magnitude - .17f) - .055f);
                return d;
            case GameUiSymbol.Shop:
                // Supermarket cart: handle, tapered wire basket, chassis and two wheels.
                d = Line(p, .10f, .83f, .23f, .83f);
                d = Mathf.Min(d, Line(p, .23f, .83f, .36f, .33f));
                d = Mathf.Min(d, Line(p, .27f, .72f, .89f, .72f));
                d = Mathf.Min(d, Line(p, .89f, .72f, .80f, .44f));
                d = Mathf.Min(d, Line(p, .80f, .44f, .34f, .44f));
                d = Mathf.Min(d, Line(p, .36f, .33f, .80f, .33f));
                d = Mathf.Min(d, Line(p, .43f, .70f, .47f, .46f));
                d = Mathf.Min(d, Line(p, .62f, .70f, .62f, .46f));
                d = Mathf.Min(d, Line(p, .31f, .58f, .84f, .58f));
                d = Mathf.Min(d, (p - new Vector2(.42f, .19f)).magnitude - .068f);
                return Mathf.Min(d, (p - new Vector2(.75f, .19f)).magnitude - .068f);
            case GameUiSymbol.Coins:
                d = Mathf.Abs((p - new Vector2(.43f, .53f)).magnitude - .27f) - .042f;
                if (p.x > .57f || p.y < .26f) d = Mathf.Min(d, Mathf.Abs((p - new Vector2(.60f, .38f)).magnitude - .25f) - .042f);
                return Mathf.Min(d, Line(p, .43f, .39f, .43f, .67f));
            case GameUiSymbol.Bundle:
                d = Mathf.Abs(Box(p, .5f, .41f, .31f, .26f)) - .04f;
                d = Mathf.Min(d, Line(p, .50f, .15f, .50f, .85f));
                d = Mathf.Min(d, Line(p, .17f, .66f, .83f, .66f));
                d = Mathf.Min(d, Line(p, .50f, .74f, .29f, .86f));
                return Mathf.Min(d, Line(p, .50f, .74f, .71f, .86f));
            case GameUiSymbol.NoAds:
                d = Mathf.Abs(Box(p, .5f, .5f, .33f, .23f)) - .034f;
                return Mathf.Min(d, Line(p, .19f, .14f, .82f, .87f));
            default:
                return Mathf.Min(Line(p, .27f, .27f, .73f, .73f), Line(p, .27f, .73f, .73f, .27f));
        }
    }

    private static float Box(Vector2 p, float x, float y, float w, float h) => Mathf.Max(Mathf.Abs(p.x - x) - w, Mathf.Abs(p.y - y) - h);
    private static float Line(Vector2 p, float ax, float ay, float bx, float by)
    {
        Vector2 a = new Vector2(ax, ay), v = new Vector2(bx - ax, by - ay);
        return (p - a - v * Mathf.Clamp01(Vector2.Dot(p - a, v) / v.sqrMagnitude)).magnitude - .037f;
    }
}
