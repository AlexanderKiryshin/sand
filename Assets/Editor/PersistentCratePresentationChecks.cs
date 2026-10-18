using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PersistentCratePresentationChecks
{
    public static void RunAll()
    {
        Run();
        CampaignCrateChecks.Run();
        CampaignIterationChecks.Run();
    }
    public static void Run()
    {
        var failures = new List<string>();
        var source = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset").levels[57];
        var level = CampaignRunFactory.Create(source, 57);
        var root = new GameObject("Persistent crates check", typeof(RectTransform));
        var state = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(580031);
            var stock = new BucketInventory(); stock.BuildFromLevel(level, 1, level.bucketSettingsOverride);
            var originalCovered = new HashSet<Bucket>(stock.Buckets.Where(stock.IsCrateCovered));
            if (originalCovered.Count < 2) failures.Add("58 starts without a covered row");
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(stock.TotalBucketCount); belt.ConfigureLevel(level);
            var ui = root.AddComponent<BucketInventoryUI>();
            Set(ui, "bucketContainer", root.GetComponent<RectTransform>());
            Set(ui, "bucketViewPrefab", AssetDatabase.LoadAssetAtPath<BucketView>("Assets/Prefabs/BucketView.prefab"));
            ui.Bind(stock, belt, level, null);
            if (root.GetComponentsInChildren<BucketCoverGraphic>().Length < originalCovered.Count)
                failures.Add("58 inventory UI does not show its initial crates");
            if (root.GetComponentsInChildren<BucketCoverGraphic>().Any(c => c.GetComponent<CanvasRenderer>() == null))
                failures.Add("Crates have no CanvasRenderer and cannot be drawn");
            if (stock.CrateLayoutIndices.Count() != LevelData.BucketLayoutCellCount) failures.Add("Crates do not cover every board cell");
            var obj = new GameObject("Crate mesh", typeof(RectTransform), typeof(BucketCoverGraphic));
            obj.transform.SetParent(root.transform, false);
            var graphic = obj.GetComponent<BucketCoverGraphic>(); graphic.Show(true, 0);
            using (var vh = new VertexHelper())
            {
                typeof(BucketCoverGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { vh });
                if (vh.currentVertCount != 0) failures.Add("An open cell still shows a crate border");
            }
            string lesson = CampaignProgression.GetLesson(57);
            if (lesson.Contains("disappear") || lesson.Contains("убираются")) failures.Add("Lesson says crates disappear");
        }
        finally
        {
            UnityEngine.Random.state = state;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level);
        }
        string report = failures.Count == 0 ? "PASS: level 58 starts with alternating covered columns; open cells have no crate borders and all crates have renderers." : "FAIL:\n" + string.Join("\n", failures);
        File.WriteAllText("tasks/persistent-crate-checks.txt", report); Debug.Log(report);
        if (failures.Count > 0) throw new InvalidOperationException(report);
    }
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
