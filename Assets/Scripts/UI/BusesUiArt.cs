using UnityEngine;

/// <summary>
/// Loads BUSES-ported UI art from Resources, with generated sand booster icons.
/// </summary>
public static class BusesUiArt
{
    private static Sprite goldIcon;
    private static Sprite starIcon;
    private static Sprite trophyIcon;
    private static Sprite storeIcon;
    private static Sprite adIcon;
    private static Sprite lockIcon;
    private static Sprite closeIcon;
    private static Sprite frameWhite;
    private static Sprite frameBlue;
    private static Sprite ribbonYellow;
    private static Sprite btnGreen;
    private static Sprite btnBlue;
    private static Sprite btnRed;
    private static Sprite magnetIcon;
    private static Sprite transparentIcon;
    private static Sprite shuffleIcon;
    private static Sprite removeAdsIcon;

    public static Sprite Gold => goldIcon ?? (goldIcon = Load("UI/BusesIcons/Icon_Gold"));
    public static Sprite Star => starIcon ?? (starIcon = Load("UI/BusesIcons/Icon_ImageIcon_Star01_m"));
    public static Sprite Trophy => trophyIcon ?? (trophyIcon = Load("UI/BusesIcons/Icon_Trophy"));
    public static Sprite Store => storeIcon ?? (storeIcon = Load("UI/BusesIcons/Pictoicon_Store"));
    public static Sprite Ad => adIcon ?? (adIcon = Load("UI/BusesIcons/Icon_ImageIcon_Ad_00_l"));
    public static Sprite Lock => lockIcon ?? (lockIcon = Load("UI/BusesIcons/Icon_Lock"));
    public static Sprite Close => closeIcon ?? (closeIcon = Load("UI/BusesIcons/Pictoicon_Close"));
    public static Sprite FrameWhite => frameWhite ?? (frameWhite = Load("UI/BusesFrames/Frame_BasicFrame_Square09_White"));
    public static Sprite FrameBlue => frameBlue ?? (frameBlue = Load("UI/BusesFrames/Frame_CardFrame03_Blue"));
    public static Sprite RibbonYellow => ribbonYellow ?? (ribbonYellow = Load("UI/BusesFrames/Label_TitleRibbon_Yellow"));
    public static Sprite BtnGreen => btnGreen ?? (btnGreen = Load("UI/BusesButtons/Btn_MainButton_Green"));
    public static Sprite BtnBlue => btnBlue ?? (btnBlue = Load("UI/BusesButtons/Btn_MainButton_Blue"));
    public static Sprite BtnRed => btnRed ?? (btnRed = Load("UI/BusesButtons/Btn_MainButton_Red"));
    public static Sprite Magnet => magnetIcon ?? (magnetIcon = LoadProcessedBooster("UI/Boosters/Booster_Magnet"));
    public static Sprite Transparent => transparentIcon ?? (transparentIcon = LoadProcessedBooster("UI/Boosters/Booster_Transparent"));
    public static Sprite Shuffle => shuffleIcon ?? (shuffleIcon = LoadProcessedBooster("UI/Boosters/Booster_Shuffle"));
    public static Sprite RemoveAds => removeAdsIcon ?? (removeAdsIcon = Load("UI/BusesIcons/Icon_Adremove"));

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

    private static Sprite LoadProcessedBooster(string resourcePath)
    {
        Sprite processed = UiSpriteUtility.LoadProcessedIcon(resourcePath);
        if (processed != null)
        {
            return processed;
        }

        return Load(resourcePath);
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