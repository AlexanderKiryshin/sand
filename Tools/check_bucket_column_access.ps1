$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Read-Method([string]$path,[string]$signature) {
 $source=[IO.File]::ReadAllText((Join-Path $root $path))
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing method $signature"}
 $brace=$source.IndexOf('{',$start);$arrow=$source.IndexOf('=>',$start)
 if($arrow -ge 0 -and ($brace -lt 0 -or $arrow -lt $brace)){return $source.Substring($start,$source.IndexOf(';',$arrow)-$start+1)}
 $position=$brace;$depth=1
 while($depth -gt 0){$position++;if($source[$position] -eq '{'){$depth++};if($source[$position] -eq '}'){$depth--}}
 return $source.Substring($start,$position-$start+1)
}
$methods=@('public bool IsBucketAccessible(','private bool IsSingleBucketAccessible(','public Bucket GetBucketAtLayoutIndex(')|ForEach-Object{Read-Method 'Assets/Scripts/Conveyor/BucketInventory.cs' $_}
$open=Read-Method 'Assets/Scripts/UI/BucketInventoryUI.cs' 'private bool IsCupOpenVisual('
$flip=Read-Method 'Assets/Scripts/UI/BucketView.cs' 'public void ConfigureInteraction('
$routes=@('BucketGridAccess','BucketRouteAccess')|ForEach-Object{[regex]::Replace([IO.File]::ReadAllText((Join-Path $root "Assets/Scripts/Conveyor/$_.cs")),'(?m)^using [^\r\n]+;\r?\n','')}
$harness=@'
using System;
using System.Collections.Generic;
public class LevelData {
 public const int BucketLayoutColumns=5,BucketLayoutRows=5,BucketLayoutCellCount=25;
 public byte[] cells=new byte[25];
 public byte GetBucketLayoutCell(int x,int y)=>cells[y*5+x];
 public static bool IsBucketLayoutWall(byte c)=>c==255;
 public static bool IsBucketLayoutConveyorSlot(byte c)=>c==254;
}
public class Bucket {
 public int LayoutIndex;public bool IsFrozen,GateClosed,CrateCovered,Revealed;public Bucket LinkedPartner;
 public Bucket(int index){LayoutIndex=index;}public void Reveal(){Revealed=true;}
}
public class BucketInventory {
 public readonly List<Bucket> buckets=new List<Bucket>();
 public bool IsGateClosed(Bucket b)=>b.GateClosed;public bool IsCrateCovered(Bucket b)=>b.CrateCovered;
 METHODS
}
public class BucketInventoryUI {
 private BucketInventory inventory;private LevelData levelData;
 public BucketInventoryUI(BucketInventory s,LevelData l){inventory=s;levelData=l;}
 public bool ShouldOpen(Bucket b)=>IsCupOpenVisual(b);
 OPEN
}
public class Button{public bool interactable;}
public class BucketView {
 private bool looksOpen=true,interactionInitialized,flipping;private float flipElapsed;
 private Bucket bucket;private Button button=new Button();
 public bool LooksOpen=>looksOpen;public bool IsFlipping=>flipping;
 public BucketView(Bucket b){bucket=b;}
 private void ResetBodyPose(){}private void SetMagnetTapHint(bool hint){}
 private void ApplyVisual(){}private void UpdateRemainingCapacity(){}
 FLIP
}
ROUTES
public static class ColumnAccessChecks {
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  int count=0;
  for(int column=0;column<5;column++)for(int row=1;row<5;row++){
   var level=new LevelData();var stock=new BucketInventory();
   var front=new Bucket(column);var rear=new Bucket(row*5+column);
   stock.buckets.AddRange(new[]{front,rear});var ui=new BucketInventoryUI(stock,level);var view=new BucketView(rear);
   view.ConfigureInteraction(false,false,false);
   view.ConfigureInteraction(ui.ShouldOpen(rear),stock.IsBucketAccessible(rear,level),false);
   Require(!view.LooksOpen&&!view.IsFlipping&&!rear.Revealed,
    "FAIL: bucket at "+rear.LayoutIndex+" opened/flipped through a free neighboring column while bucket "+front.LayoutIndex+" still blocks its own column");
   stock.buckets.Remove(front);view.ConfigureInteraction(ui.ShouldOpen(rear),stock.IsBucketAccessible(rear,level),false);
   Require(view.LooksOpen&&view.IsFlipping&&rear.Revealed,"Clearing its own column must open and flip the bucket");count++;
  }
  Console.WriteLine("PASS: "+count+" own-column blocking and real visual flip transitions");
  var board=new LevelData();var inventory=new BucketInventory();var a=new Bucket(5);var b=new Bucket(6);
  a.LinkedPartner=b;b.LinkedPartner=a;var first=new Bucket(0);var second=new Bucket(1);
  inventory.buckets.AddRange(new[]{a,b,first,second});var pairUI=new BucketInventoryUI(inventory,board);
  Require(!pairUI.ShouldOpen(a)&&!pairUI.ShouldOpen(b),"Blocked pair opens");
  inventory.buckets.Remove(first);Require(!pairUI.ShouldOpen(a)&&!pairUI.ShouldOpen(b),"Pair must wait for both columns");
  inventory.buckets.Remove(second);Require(pairUI.ShouldOpen(a)&&pairUI.ShouldOpen(b),"Clear pair must open together");
  a.LinkedPartner=b.LinkedPartner=null;a.IsFrozen=true;Require(!pairUI.ShouldOpen(a),"Ice bypass");
  a.IsFrozen=false;a.GateClosed=true;Require(!pairUI.ShouldOpen(a),"Gate bypass");
  a.GateClosed=false;a.CrateCovered=true;Require(!pairUI.ShouldOpen(a),"Crate bypass");a.CrateCovered=false;
  inventory.buckets.Remove(b);board.cells[0]=255;
  Require(pairUI.ShouldOpen(a),"Wall detour should remain available when no cup blocks the column");
  for(int i=1;i<5;i++)board.cells[i]=255;
  Require(!pairUI.ShouldOpen(a),"Sealed wall must block access");
  Console.WriteLine("PASS: linked pairs, ice, gates, crates, wall detours and sealed routes");
 }
}
'@
$harness=$harness.Replace('METHODS',($methods -join "`n")).Replace('OPEN',$open).Replace('FLIP',$flip).Replace('ROUTES',($routes -join "`n"))
Add-Type $harness -IgnoreWarnings -WarningAction SilentlyContinue
[ColumnAccessChecks]::Run()
