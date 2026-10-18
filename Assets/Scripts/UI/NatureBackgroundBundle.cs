using UnityEngine;
using System.Linq;
using MirraGames.SDK.Common;

public static class NatureBackgroundBundle
{
    public const string ProductTag = "sand_background_nature_bundle";
    public const string LastShownKey = "sand_nature_bundle_last_shown";
    public static readonly int[] Backgrounds = CosmeticBundleCatalog.Nature.Backgrounds;
    public static bool TrySavings(ProductData bundle,ProductData[] individual,out string regularPrice,out int percent,CosmeticBundle definition=null)
    {
        definition=definition??CosmeticBundleCatalog.Nature;
        var backgrounds=definition.Backgrounds;
        regularPrice=null;percent=0;
        if(bundle==null || bundle.Tag!=definition.Tag || !ValidPrice(bundle) || individual==null || individual.Length!=backgrounds.Length)return false;
        float total=0;
        for(int i=0;i<individual.Length;i++)
        {
            var product=individual[i];
            if(product==null || product.Tag!=ThemeCatalog.ProductTag(backgrounds[i],ThemeCatalog.Part.Background) || product.Currency!=bundle.Currency || !ValidPrice(product))return false;
            total+=product.PriceFloat;
        }
        if(float.IsInfinity(total) || total<=bundle.PriceFloat)return false;
        percent=Mathf.FloorToInt((1-bundle.PriceFloat/total)*100);
        if(percent<1)return false;
        regularPrice=total.ToString("0.##")+" "+bundle.Currency;return true;
    }
    private static bool ValidPrice(ProductData product) => product.PriceFloat>0 && !float.IsNaN(product.PriceFloat) && !float.IsInfinity(product.PriceFloat) && !string.IsNullOrEmpty(product.Currency);
    public static bool IsOwned
    {
        get { foreach(int id in Backgrounds) if(!ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background))return false;return true; }
    }
    public static bool ShouldOffer(int levelNumber,bool seasonal)
        => !seasonal && levelNumber>=50 && (levelNumber-50)%20==0
            && levelNumber>GameProgressStore.GetInt(LastShownKey,0) && CosmeticBundleCatalog.All.Any(bundle=>!bundle.IsOwned);
    public static void MarkShown(int levelNumber)
    {
        GameProgressStore.SetInt(LastShownKey,Mathf.Max(levelNumber,GameProgressStore.GetInt(LastShownKey,0)));
        GameProgressStore.Save();
    }
    public static void Grant()
    {
        foreach(int id in Backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+id,true);
        GameProgressStore.Save();ThemeCatalog.Refresh();
    }
}
