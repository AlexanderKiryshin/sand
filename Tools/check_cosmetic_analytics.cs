using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UnityEngine {
 public struct Color {public float r,g,b;public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;}public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t);}
 public struct Color32 {public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public static class Mathf {public static int Clamp(int n,int min,int max)=>Math.Max(min,Math.Min(max,n));public static int Max(int a,int b)=>Math.Max(a,b);public static int FloorToInt(float n)=>(int)Math.Floor(n);}
 public static class Random {private static System.Random random=new System.Random(7);public static int Range(int min,int max)=>random.Next(min,max);}
 public class CanvasGroup {public float alpha=1f;}
 public class Object {public static T[] FindObjectsOfType<T>()=>new T[0];}
 public static class Debug {public static void LogWarning(string text){}}
 public static class Time {public static double realtimeSinceStartupAsDouble;}
 public static class Application {public static NetworkReachability internetReachability;}
 public enum NetworkReachability {NotReachable,ReachableViaLocalAreaNetwork}
}
namespace MirraGames.SDK.Common {
 public class ProductData {public string Tag,Currency;public float PriceFloat;public ProductData(string tag,float price,string currency){Tag=tag;PriceFloat=price;Currency=currency;}public string GetFullPriceFloat()=>PriceFloat+" "+Currency;}
}
public static class GameProgressStore {
 private static Dictionary<string,int> values=new Dictionary<string,int>();
 public static int GetInt(string key,int fallback=0)=>values.TryGetValue(key,out int value)?value:fallback;
 public static bool GetBool(string key)=>GetInt(key)!=0;
 public static void SetInt(string key,int value)=>values[key]=value;
 public static void SetBool(string key,bool value)=>SetInt(key,value?1:0);
 public static void Save(){}public static void Clear()=>values.Clear();
}
public static class CampaignText {public static string Get(string key)=>key;}
public class ThemeBackdropGraphic {public void SetVerticesDirty(){}public void SetMaterialDirty(){}}
public class ConveyorSurfaceGraphic {public void SetVerticesDirty(){}}
public class ConveyorHoodGraphic {public void SetVerticesDirty(){}}
public class LevelData {public int gridWidth,gridHeight;public byte[] cells;public Color32[] palette;public bool alternatingCrates,loopInventory,sectionBuckets,keyGates,linkedBuckets,largeBuckets,swappingColors,mysteryBuckets;public int frostMoves;public string displayTitle,artTheme;}
public static class SandGrainColorBaker {public static void Bake(SandGrid grid,float strength){}}
public class SandRenderer {
 private SandGrid grid;
 public SandRenderer(SandGrid value){grid=value;}
REMAINING_COLOR_METHOD
}
public class PaletteAvailabilityVerification {
 private SandRenderer renderer;
 public CanvasGroup[] swatchVisibility;
 public PaletteAvailabilityVerification(SandGrid grid,int colors){renderer=new SandRenderer(grid);swatchVisibility=Enumerable.Range(0,colors+1).Select(_=>new CanvasGroup()).ToArray();}
PALETTE_REFRESH_METHOD
}
public static class WebGameAnalytics {
 public static bool Throw;
 public static List<KeyValuePair<string,Dictionary<string,object>>> Events=new List<KeyValuePair<string,Dictionary<string,object>>>();
 public static void Report(string name,Dictionary<string,object> data){if(Throw)throw new InvalidOperationException("test failure");Events.Add(new KeyValuePair<string,Dictionary<string,object>>(name,data));}
}
public class TutorialPaletteStub {public byte RedColor=7;}
public class TutorialUiStub {public void SetHintVisible(bool value){}public void Refresh(){}}
public class TutorialFilterVerification {
 public int FilterTutorialStep=1;private TutorialPaletteStub filterPanel=new TutorialPaletteStub();
 private TutorialUiStub gameUI=new TutorialUiStub(),bucketInventoryUI=new TutorialUiStub();
 private void ShowFilterHint(){}private void SetTutorialVisualsVisible(bool value){}
FILTER_HANDLER
}
public static class TutorialRedVerification { RED_METHOD }
public static class CosmeticChecks {
 static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 static int Count(string name)=>WebGameAnalytics.Events.Count(item=>item.Key==name);
 static void Reset(){GameProgressStore.Clear();WebGameAnalytics.Events.Clear();WebGameAnalytics.Throw=false;Analytic.ResetSession();}
 public static void Run(){
  Reset();
  Require(ThemeCatalog.Acquire(4,ThemeCatalog.Part.Casings,"coins"),"Casing purchase failed");
  Require(!ThemeCatalog.IsUnlocked(4,ThemeCatalog.Part.Conveyor),"Buying Ocean casings also unlocked the Ocean conveyor");
  Require(!ThemeCatalog.IsUnlocked(4,ThemeCatalog.Part.Background),"Buying Ocean casings also unlocked a background");
  Reset();Analytic.BeginLevel(49,new LevelData(),false);WebGameAnalytics.Events.Clear();
  Require(!ThemeCatalog.Select(12,ThemeCatalog.Part.Background),"Locked selection accepted");
  Require(Count("cosmetic_selected")==0,"Locked preview emitted selection");
  Require(ThemeCatalog.Acquire(4,ThemeCatalog.Part.Background,"coins"),"Coin acquisition failed");
  Require(Count("cosmetic_acquired")==1 && Count("cosmetic_selected")==1,"Coin item events missing");
  var acquired=WebGameAnalytics.Events.Single(item=>item.Key=="cosmetic_acquired").Value;
  Require((string)acquired["cosmetic_id"]=="theme_olive_marks" && (string)acquired["cosmetic_category"]=="background" && (string)acquired["acquisition"]=="coins" && (int)acquired["level"]==50,"Incorrect acquisition context");
  ThemeCatalog.Acquire(4,ThemeCatalog.Part.Background,"coins");
  Require(Count("cosmetic_acquired")==1 && Count("cosmetic_selected")==1,"Repeated acquisition/equip duplicated events");
  ThemeCatalog.Select(ThemeCatalog.DefaultBackground,ThemeCatalog.Part.Background);ThemeCatalog.Select(4,ThemeCatalog.Part.Background);
  Require(Count("cosmetic_selected")==3 && Count("cosmetic_acquired")==1,"Reapply should track selection only");
  foreach(string method in new[]{"rewarded","no_ads_bonus","purchase"}) {
   Reset();ThemeCatalog.Acquire(46,ThemeCatalog.Part.Background,method);
   Require(Count("cosmetic_acquired")==1 && (string)WebGameAnalytics.Events.First(item=>item.Key=="cosmetic_acquired").Value["acquisition"]==method,"Acquisition source missing");
  }
  Reset();ThemeCatalog.Unlock(12,ThemeCatalog.Part.Background);
  Require(Count("cosmetic_acquired")==0,"Restored ownership counted as purchase");
  ThemeCatalog.Select(12,ThemeCatalog.Part.Background);Require(Count("cosmetic_selected")==1,"Restored selection not tracked");
  Reset();CosmeticBundleCatalog.Nature.Grant();Require(WebGameAnalytics.Events.Count==0,"Bundle restore emitted new acquisition");
  Reset();ThemeCatalog.Unlock(24,ThemeCatalog.Part.Background);CosmeticBundleCatalog.Nature.GrantPurchased();CosmeticBundleCatalog.Nature.GrantPurchased();
  Require(Count("cosmetic_acquired")==2 && Count("cosmetic_selected")==0,"Bundle must track only newly owned art and not equip it");
  Require(WebGameAnalytics.Events.All(item=>(string)item.Value["product_id"]==CosmeticBundleCatalog.Nature.Tag && (string)item.Value["acquisition"]=="bundle_purchase"),"Bundle payload incorrect");
  Reset();WebGameAnalytics.Throw=true;Require(ThemeCatalog.Acquire(4,ThemeCatalog.Part.Background,"coins") && ThemeCatalog.IsUnlocked(4,ThemeCatalog.Part.Background),"Analytics error interrupted ownership");WebGameAnalytics.Throw=false;
  Reset();Require(!NatureBackgroundBundle.ShouldOffer(49,false) && NatureBackgroundBundle.ShouldOffer(50,false) && NatureBackgroundBundle.ShouldOffer(70,false) && NatureBackgroundBundle.ShouldOffer(90,false),"Offer cadence changed");
  Require(!NatureBackgroundBundle.ShouldOffer(69,false) && !NatureBackgroundBundle.ShouldOffer(50,true),"Off-cadence/seasonal offer allowed");
  NatureBackgroundBundle.MarkShown(50);Require(!NatureBackgroundBundle.ShouldOffer(50,false) && NatureBackgroundBundle.ShouldOffer(70,false),"Offer replay protection lost");
  CosmeticBundleCatalog.Nature.Grant();Require(NatureBackgroundBundle.ShouldOffer(70,false),"Owning nature hid remaining bundles");
  for(int i=0;i<100;i++)Require(CosmeticBundleCatalog.PickUnowned()!=CosmeticBundleCatalog.Nature,"Owned nature bundle selected");
  foreach(var remaining in CosmeticBundleCatalog.All){
   Reset();foreach(var bundle in CosmeticBundleCatalog.All)if(bundle!=remaining)bundle.Grant();
   for(int i=0;i<20;i++)Require(CosmeticBundleCatalog.PickUnowned()==(remaining.IsOwned?null:remaining),"Did not respect shared ownership when selecting sole unowned bundle");
   remaining.Grant();Require(CosmeticBundleCatalog.PickUnowned()==null && !NatureBackgroundBundle.ShouldOffer(50,false),"All-owned state still offered bundle");
  }
  foreach(var style in CosmeticBundleCatalog.All.Where(bundle=>bundle.IsStyleBundle)) {
   Reset();style.GrantPurchased();style.GrantPurchased();
   Require(Count("cosmetic_acquired")==3 && Count("cosmetic_selected")==0,"Style set must track exactly its three newly acquired parts");
   Require(WebGameAnalytics.Events.All(item=>(string)item.Value["product_id"]==style.Tag),"Style purchase used wrong product tag");
   Require(WebGameAnalytics.Events.Select(item=>(string)item.Value["cosmetic_category"]).Distinct().Count()==3,"Style acquisition categories missing");
   Reset();ThemeCatalog.Unlock(style.Backgrounds[0],ThemeCatalog.Part.Background);Require(!style.IsOwned,"Background alone owns a complete style set");
   style.GrantPurchased();Require(style.IsOwned && Count("cosmetic_acquired")==2,"Style upgrade did not grant/track only missing material parts");
   Reset();style.Grant();Require(WebGameAnalytics.Events.Count==0 && style.IsOwned,"Style restoration emitted acquisition");
  }
  foreach(var part in new[]{ThemeCatalog.Part.Conveyor,ThemeCatalog.Part.Casings}) {
   var prices=Enumerable.Range(0,ThemeCatalog.CountFor(part)).Where(id=>ThemeCatalog.AccessFor(id,part)==ThemeCatalog.Access.Coins).Select(id=>ThemeCatalog.CoinPrice(id,part)).ToArray();
   bool casings=part==ThemeCatalog.Part.Casings;
   Require(prices.Min()==(casings?100:400) && prices.Max()==(casings?600:1000) && prices.SequenceEqual(prices.OrderBy(price=>price)),"Material prices outside their category range or unsorted");
   int[] expected=casings?new[]{100,200,300,400,500,600}:new[]{400,500,600,700,800,900,1000};
   Require(prices.Take(expected.Length).SequenceEqual(expected),"Material entry price steps changed");
  }
  Require(Enumerable.Range(4,4).Select(id=>ThemeCatalog.CoinPrice(id,ThemeCatalog.Part.Background)).SequenceEqual(new[]{100,200,300,400}),"Basic background prices changed");
  Require(Enumerable.Range(31,7).All(id=>ThemeCatalog.CoinPrice(id,ThemeCatalog.Part.Background)==1000),"Nature gold background prices changed");
  var picture=new LevelData {gridWidth=3,gridHeight=2,cells=new byte[]{1,1,2,0,0,0},palette=new Color32[3]};
  var grid=new SandGrid(picture,1);var swatches=new PaletteAvailabilityVerification(grid,3);swatches.RefreshSwatchAvailability();
  Require(grid.CountRemainingForColor(1)==2 && grid.CountRemainingForColor(2)==1,"Initial per-color counts wrong");
  Require(swatches.swatchVisibility[0].alpha==1 && swatches.swatchVisibility[1].alpha==1 && swatches.swatchVisibility[2].alpha==1 && swatches.swatchVisibility[3].alpha==.25f,"Initial palette opacity wrong");
  grid.SetCell(0,0,0);swatches.RefreshSwatchAvailability();Require(swatches.swatchVisibility[1].alpha==1,"Color faded before its last grain");
  grid.MoveGrain(1,0,0,1);Require(grid.CountRemainingForColor(1)==1,"Movement changed color count");
  grid.SetCell(0,1,0);swatches.RefreshSwatchAvailability();Require(swatches.swatchVisibility[1].alpha==.25f && swatches.swatchVisibility[2].alpha==1,"Exhausted color did not fade independently");
  grid.SetCell(0,0,1);swatches.RefreshSwatchAvailability();Require(swatches.swatchVisibility[1].alpha==1,"Reappearing color remained faded");
  grid.SetCell(2,0,1);Require(grid.CountRemainingForColor(1)==2 && grid.CountRemainingForColor(2)==0,"Color replacement counts wrong");
  var fineGrid=new SandGrid(picture,2);Require(fineGrid.CountRemainingForColor(1)==8 && fineGrid.CountRemainingForColor(2)==4,"Sub-grain counts wrong");
  for(int i=0;i<1000;i++) {
   int x=UnityEngine.Random.Range(0,fineGrid.Width),y=UnityEngine.Random.Range(0,fineGrid.Height);
   if(i%2==0)fineGrid.SetCell(x,y,(byte)UnityEngine.Random.Range(0,4));else fineGrid.MoveGrain(x,y,UnityEngine.Random.Range(0,fineGrid.Width),UnityEngine.Random.Range(0,fineGrid.Height));
   var cells=fineGrid.GetCellsCopy();Require(fineGrid.CountRemaining()==cells.Count(color=>color!=0),"Total sand count drifted");
   foreach(byte color in new byte[]{0,1,2,3})Require(fineGrid.CountRemainingForColor(color)==(color==0?0:cells.Count(value=>value==color)),"Per-color count drifted");
  }
  Require(TutorialRedVerification.FindRedColor(new[]{new Color32(0,0,255,255),new Color32(240,20,10,255),new Color32(0,255,0,255)})==2,"Tutorial assumed first swatch");
  Require(TutorialRedVerification.FindRedColor(new[]{new Color32(255,0,0,0),new Color32(240,30,20,255)})==2,"Transparent red selected");
  Require(TutorialRedVerification.FindRedColor(null)==0,"Missing palette failed");
  var tutorial=new TutorialFilterVerification();tutorial.HandleFilterSelected(1);Require(tutorial.FilterTutorialStep==1,"Wrong color advanced tutorial");tutorial.HandleFilterSelected(7);Require(tutorial.FilterTutorialStep==2,"Red color did not advance tutorial");tutorial.HandleFilterSelected(5);Require(tutorial.FilterTutorialStep==2,"Other color ended tutorial");tutorial.HandleFilterSelected(0);Require(tutorial.FilterTutorialStep==0,"All colors did not finish tutorial");
 }
}
