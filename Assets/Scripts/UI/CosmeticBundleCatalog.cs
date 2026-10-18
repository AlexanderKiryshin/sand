using System.Linq;
using UnityEngine;

public sealed class CosmeticBundle
{
    public readonly string Tag,TitleKey;
    public readonly int[] Backgrounds;
    public CosmeticBundle(string tag,string title,params int[] backgrounds){Tag=tag;TitleKey=title;Backgrounds=backgrounds;}
    public bool IsOwned => Backgrounds.All(id=>ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background));
    public void GrantPurchased()
    {
        var newlyOwned = Backgrounds.Where(id => !ThemeCatalog.IsUnlocked(id, ThemeCatalog.Part.Background)).ToArray();
        Grant();
        foreach (int id in newlyOwned)
            Analytic.CosmeticAcquired(ThemeCatalog.NameKey(id, ThemeCatalog.Part.Background), "background", id, "bundle_purchase", Tag);
    }
    public void Grant()
    {
        foreach(int id in Backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+id,true);
        GameProgressStore.Save();ThemeCatalog.Refresh();
    }
}
public static class CosmeticBundleCatalog
{
    public static readonly CosmeticBundle Nature=new CosmeticBundle("sand_background_nature_bundle","nature_bundle_title",27,24,29);
    public static CosmeticBundle PickUnowned()
    {
        var available = All.Where(bundle => !bundle.IsOwned).ToArray();
        return available.Length == 0 ? null : available[Random.Range(0, available.Length)];
    }
    public static readonly CosmeticBundle[] All={
        Nature,
        new CosmeticBundle("sand_background_botanical_bundle","bundle_botanical",12,21,28),
        new CosmeticBundle("sand_background_travel_bundle","bundle_travel",13,15,25),
        new CosmeticBundle("sand_background_cozy_bundle","bundle_cozy",14,17,30),
        new CosmeticBundle("sand_background_artist_bundle","bundle_artist",16,20,23),
        new CosmeticBundle("sand_background_material_bundle","bundle_material",18,19,22)
    };
}
