using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class CampaignRequestedRulesChecks
{
 public static void Run()
 {
  var db=AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
  int previousCrates=-10; var loops=new HashSet<string>(); int loopCount=0,compactCount=0;var sizes=new HashSet<int>();
  for(int index=0;index<db.LevelCount;index++)
  {
   var level=CampaignRunFactory.Create(db.levels[index],index);
   try {
    Require(level.beltCapacity==ConveyorBelt.MaxOnBelt,"Slot limit at level "+(index+1));
    if(level.alternatingCrates){Require(index+1-previousCrates>=5,"Crates too frequent");previousCrates=index+1;}
    if(level.loopInventory)
    {
     loopCount++; InventoryLoopRoute.Bind(level);var route=InventoryLoopRoute.Cells;
     Require(!level.bucketLayout.Any(LevelData.IsBucketLayoutWall),"Unused loop cells are filled with walls");
     var stock=new BucketInventory();stock.BuildFromLevel(level,1,level.bucketSettingsOverride);
     Require(stock.Buckets.Any(b=>Array.IndexOf(route,b.LayoutIndex)>=0),"No buckets on the loop");
     Require(stock.Buckets.Any(b=>Array.IndexOf(route,b.LayoutIndex)<0),"No buckets outside the loop");
     Require(stock.Buckets.Count==level.bucketLayout.Count(c=>!LevelData.IsBucketLayoutWall(c)&&!LevelData.IsBucketLayoutConveyorSlot(c)),"Ordinary board cells are not filled with buckets at level "+(index+1));
     int rows=route.Select(c=>c/5).Distinct().Count(),columns=route.Select(c=>c%5).Distinct().Count();
     if(rows<5||columns<5)compactCount++;sizes.Add(route.Length);
     Require(route.Length>=8&&route.Any(c=>c/5==0),"Loop too small or disconnected from front");
     Require(route.Distinct().Count()==route.Length,"Loop repeats a cell");
     for(int i=0;i<route.Length;i++){int a=route[i],b=route[(i+1)%route.Length];Require(Math.Abs(a%5-b%5)+Math.Abs(a/5-b/5)==1,"Disconnected turn");}
     loops.Add(string.Join(",",route));
    }
   }finally{UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride);UnityEngine.Object.DestroyImmediate(level);}
  }
  Require(!CampaignProgression.IsLessonStart(149)&&CampaignProgression.GetLessonId(CampaignProgression.MysteryLevel - 1)=="lesson_mystery","Wrong lesson introduction");
  Require(compactCount>0&&compactCount<loopCount&&sizes.Count>=3,"Loop sizes do not vary");
  Require(!db.levels[CampaignProgression.SwapLevel - 1].alternatingCrates,"Swap lesson still has crates");
  File.WriteAllText("tasks/requested-rules-checks.txt","PASS: five belt slots on every level, crate intervals and swap lesson; "+loopCount+" unique loops, including "+compactCount+" compact loops; cell counts: "+string.Join(",",sizes.OrderBy(n=>n)));
  PersistentCratePresentationChecks.RunAll();
 }
 public static void SaveRules()
 {
  var db=AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
  for(int i=0;i<db.LevelCount;i++)
  {
   var level=db.levels[i]; CampaignProgression.ApplyPersistentRules(level,i);
   var rules=level.bucketSettingsOverride;
   rules.layoutTemplate=(byte[])level.bucketLayout.Clone();rules.conveyorDirectionTemplate=(byte[])level.conveyorDirections.Clone();rules.conveyorBucketCountTemplate=(byte[])level.conveyorBucketCounts.Clone();
   EditorUtility.SetDirty(level);EditorUtility.SetDirty(rules);
  }
  AssetDatabase.SaveAssets();Run();
 }
 private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
