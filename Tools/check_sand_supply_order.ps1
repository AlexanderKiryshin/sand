$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'check_bucket_column_access.ps1')
$method=Read-Method 'Assets/Scripts/Conveyor/BucketInventory.cs' 'private static List<ColorQuantizer.BucketQueueItem> OrderBucketsBySandSupply('
$harness=@'
using System;
using System.Linq;
using System.Collections.Generic;
namespace SandSupplyOrderRegression {
 public class LevelData {public int gridWidth,gridHeight;public byte[] cells;}
 public static class ColorQuantizer {public struct BucketQueueItem {public byte colorIndex;public int capacity;}}
 public static class Checks {
  METHOD
  static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
  public static void Run(){
   var level=new LevelData{gridWidth=2,gridHeight=4,cells=new byte[]{3,3,2,2,1,2,1,1}};
   foreach(int scale in new[]{1,4}){
    var queue=new List<ColorQuantizer.BucketQueueItem>{
     new ColorQuantizer.BucketQueueItem{colorIndex=3,capacity=2*scale},
     new ColorQuantizer.BucketQueueItem{colorIndex=2,capacity=2*scale},
     new ColorQuantizer.BucketQueueItem{colorIndex=1,capacity=2*scale},
     new ColorQuantizer.BucketQueueItem{colorIndex=2,capacity=scale},
     new ColorQuantizer.BucketQueueItem{colorIndex=1,capacity=scale}};
    var result=OrderBucketsBySandSupply(level,queue,scale);
    Require(result.Select(b=>b.colorIndex).SequenceEqual(new byte[]{1,1,2,2,3}),"Colors unavailable near the bottom reached the queue too early at grain scale "+scale);
    Require(result.Count==queue.Count && result.Sum(b=>b.capacity)==queue.Sum(b=>b.capacity),"Ordering changed bucket count or sand budget");
    foreach(byte color in new byte[]{1,2,3})
     Require(result.Where(b=>b.colorIndex==color).Select(b=>b.capacity).SequenceEqual(queue.Where(b=>b.colorIndex==color).Select(b=>b.capacity)),"Per-color capacities changed");
    var empty=new LevelData{gridWidth=2,gridHeight=4,cells=new byte[8]};
    var fallback=OrderBucketsBySandSupply(empty,queue,scale);
    Require(fallback.Count==queue.Count&&fallback.Sum(b=>b.capacity)==queue.Sum(b=>b.capacity),"Empty padding lost externally authored budgets");
   }
   Console.WriteLine("PASS: bottom-first supply ordering, rare top colors delayed, exact counts/capacities, grain scales 1 and 4, empty padding fallback");
  }
 }
}
'@
Add-Type ($harness.Replace('METHOD',$method)) -IgnoreWarnings -WarningAction SilentlyContinue
[SandSupplyOrderRegression.Checks]::Run()
