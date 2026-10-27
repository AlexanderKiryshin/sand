using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class CampaignCrateChecks
{
 public static void Run()
 {
  var source=AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/Campaign100/Level_070.asset");
  var level=CampaignRunFactory.Create(source,69);
  var root=new GameObject("CrateColumnCheck",typeof(RectTransform));
  try {
   var stock=new BucketInventory();stock.BuildFromLevel(level,1,level.bucketSettingsOverride);
   var belt=root.AddComponent<ConveyorBelt>();belt.Configure(stock.TotalBucketCount);belt.ConfigureLevel(level);
   var row=root.AddComponent<InventoryCrateRowView>();row.Bind(stock,Vector2.one*96,Vector2.one*10);
   Require(stock.CrateLayoutIndices.Count()==LevelData.BucketLayoutCellCount,"Crates span the entire board");
   Require(Enumerable.Range(0,25).All(i=>stock.IsCrateCellClosed(i)==(i%5%2==0)),"Every row starts with columns 1, 3 and 5 closed");
   Require(ClosedColumns(stock)==3,"Initially columns 1, 3 and 5 must be closed");
   Require(stock.Buckets.Where(stock.IsCrateCovered).All(b=>b.LayoutIndex%5%2==0),"Closed columns cover every bucket row");
   var first=stock.Buckets.First(b=>stock.IsBucketAccessible(b,level));
   Require(first.LayoutIndex%5%2==1,"Initial moves come from columns 2 and 4");
   Require(stock.TryTakeBucket(first,belt,level),"First move succeeds");
   Require(ClosedColumns(stock)==2,"Next tick closes columns 2 and 4");
   Require(Enumerable.Range(0,25).All(i=>stock.IsCrateCellClosed(i)==(i%5%2==1)),"Every row switches to columns 2 and 4");
   row.AdvanceCover(.2f);Require(row.IsCoverAnimating&&row.CoverAmount>0&&row.CoverAmount<1,"Both groups animate together");
   row.AdvanceCover(.5f);Require(row.CoverAmount==0,"First animation reaches its new phase");
   bool phase=stock.CratesOpen;Require(!stock.TryTakeBucket(first,belt,level)&&stock.CratesOpen==phase,"Rejected click preserves phase");
   belt.ResetConveyor();
   var second=stock.Buckets.First(b=>stock.IsBucketAccessible(b,level));
   Require(second.LayoutIndex%5%2==0,"Next move comes from columns 1, 3 or 5");
   Require(stock.TryTakeBucket(second,belt,level)&&ClosedColumns(stock)==3,"Third tick closes three columns again");
   row.AdvanceCover(.7f);Require(row.CoverAmount==1,"Second animation completes");
   Require(root.GetComponentsInChildren<BucketCoverGraphic>().All(g=>g.canvasRenderer!=null),"Every crate has a renderer");
   System.IO.File.WriteAllText("tasks/crate-validation.md","PASS: full columns alternate 1/3/5 and 2/4; accessibility, rejected clicks and both animation phases match.\n");
  } finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride);UnityEngine.Object.DestroyImmediate(level);}
 }
 private static int ClosedColumns(BucketInventory stock)=>Enumerable.Range(0,5).Count(stock.IsCrateCellClosed);
 private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
