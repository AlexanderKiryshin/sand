public enum BoosterType
{
    Magnet = 0,
    Transparent = 1,
    Shuffle = 2
}

public static class BoosterCatalog
{
    public const int StartCharges = 2;
    public const int BuyPriceCoins = 90;
    public const int AdGrantCharges = 1;

    public static int GetUnlockLevelIndex(BoosterType type)
    {
        switch (type)
        {
            case BoosterType.Magnet:
                return 5;
            case BoosterType.Transparent:
                return 7;
            case BoosterType.Shuffle:
                return 9;
            default:
                return int.MaxValue;
        }
    }

    public static string GetSaveKey(BoosterType type)
    {
        switch (type)
        {
            case BoosterType.Magnet:
                return "MagnetBooster";
            case BoosterType.Transparent:
                return "TransparentBooster";
            case BoosterType.Shuffle:
                return "ShuffleBooster";
            default:
                return "UnknownBooster";
        }
    }

    public static string GetTutorialSeenKey(BoosterType type)
    {
        return GetSaveKey(type) + "TutorialSeen";
    }

    public static string GetGrantedKey(BoosterType type)
    {
        return GetSaveKey(type) + "Granted";
    }

    public static string GetLocalizationKey(BoosterType type)
    {
        switch (type)
        {
            case BoosterType.Magnet:
                return "magnet";
            case BoosterType.Transparent:
                return "transparent";
            case BoosterType.Shuffle:
                return "shuffle";
            default:
                return "magnet";
        }
    }

    public static string GetTutorialHintKey(BoosterType type)
    {
        switch (type)
        {
            case BoosterType.Magnet:
                return "tutorial_magnet";
            case BoosterType.Transparent:
                return "tutorial_transparent";
            case BoosterType.Shuffle:
                return "tutorial_shuffle";
            default:
                return "tutorial_magnet";
        }
    }

    public static string GetAdPlacement(BoosterType type)
    {
        return GetLocalizationKey(type);
    }
}
