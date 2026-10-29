using UnityEngine;

/// <summary>
/// Loads BUSES-ported UI art from Resources, with generated sand booster icons.
/// </summary>
public static class BusesUiArt
{
    private static Sprite goldIcon;
    private static Sprite starIcon;
    private static Sprite adIcon;
    private static Sprite frameWhite;
    private static Sprite btnGreen;

    public static Sprite Gold => goldIcon ?? (goldIcon = Load("UI/BusesIcons/Icon_Gold"));
    public static Sprite Star => starIcon ?? (starIcon = Load("UI/BusesIcons/Icon_ImageIcon_Star01_m"));
    public static Sprite Ad => adIcon ?? (adIcon = Load("UI/BusesIcons/Icon_ImageIcon_Ad_00_l"));
    public static Sprite FrameWhite => frameWhite ?? (frameWhite = Load("UI/BusesFrames/Frame_BasicFrame_Square09_White"));
    public static Sprite BtnGreen => btnGreen ?? (btnGreen = Load("UI/BusesButtons/Btn_MainButton_Green"));
    public static Sprite Magnet => GameUiSymbols.Get(GameUiSymbol.Magnet);
    public static Sprite Transparent => GameUiSymbols.Get(GameUiSymbol.Fill);
    public static Sprite Shuffle => GameUiSymbols.Get(GameUiSymbol.Shuffle);

    public static Sprite GetBoosterIcon(BoosterType type)
    {
        switch (type)
        {
            case BoosterType.Magnet:
                return Magnet;
            case BoosterType.Transparent:
                return Transparent;
            case BoosterType.Shuffle:
                return Shuffle;
            default:
                return Magnet;
        }
    }

    private static Sprite Load(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }
}
