using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class LinkedBucketPresentationChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    public static void RunAll()
    {
        Run();
        CheckHiddenPresentation();
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var report = new List<string> { "Hidden two-section buckets start at 203; hidden linked pairs start at 208.", "Level | Hidden sections | Hidden pairs" };
        var state = UnityEngine.Random.state;
        try
        {
            for (int index = 199; index < db.LevelCount; index++)
            for (int run = 0; run < 3; run++)
            {
                var level = CampaignRunFactory.Create(db.levels[index], index);
                try
                {
                    UnityEngine.Random.InitState(7319 + index * 104729 + run);
                    var stock = new BucketInventory(); stock.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    var all = stock.Buckets.Concat(stock.ReservedBuckets).Concat(stock.ConveyorSlots.SelectMany(s => s.ReservedBuckets)).ToArray();
                    int sections = all.Count(b => b.HasSecondSection && b.IsMystery);
                    int pairs = all.Count(b => b.LinkedPartner != null && b.IsMystery && b.LinkedPartner.IsMystery) / 2;
                    Require((sections > 0) == CampaignProgression.HasHiddenSections(level, index + 1), "Hidden section schedule at " + (index + 1));
                    Require((pairs > 0) == CampaignProgression.HasHiddenLinked(level, index + 1), "Hidden linked schedule at " + (index + 1));
                    if (index + 1 == 203) Require(stock.Buckets.Any(b => b.HasSecondSection && b.IsMystery && !stock.IsBucketAccessible(b, level)), "203 has a visible blocked hidden section");
                    if (index + 1 == 208) Require(stock.Buckets.Any(b => b.LinkedPartner != null && b.IsMystery && !stock.IsBucketAccessible(b, level)), "208 has a visible blocked hidden pair");
                    if (run == 0) report.Add((index + 1) + " | " + sections + " | " + pairs);
                }
                finally { UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        finally { UnityEngine.Random.state = state; }
        File.WriteAllLines("tasks/hidden-combinations-checks.txt", report);
        CampaignIterationChecks.Run();
        Debug.Log("LINKED_AND_HIDDEN_PRESENTATION_PASS: geometry, hidden/revealed visuals, advanced schedule and campaign dispatch routes.");
    }

    private static void CheckHiddenPresentation()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.palette = new[] { new Color32(255, 90, 30, 255), new Color32(40, 130, 240, 255) };
        var root = new GameObject("Hidden visual check", typeof(RectTransform), typeof(BucketView));
        ((RectTransform)root.transform).sizeDelta = new Vector2(96, 96);
        try
        {
            var cup = new Bucket(1, 10); cup.AddSection(2, 10); cup.SetModifiers(0, true);
            var view = root.GetComponent<BucketView>();
            view.Initialize(cup, level.palette[0], null);
            view.ConfigureInteraction(false, false, false);
            var body = (Image)Get(view, "bucketImage");
            var visual = root.GetComponent<BucketMechanicVisual>();
            Set(visual, "level", level); Call(visual, "LateUpdate");
            Require(body.rectTransform.localScale == Vector3.one && body.rectTransform.rect.size == new Vector2(96, 96), "Hidden body matches ordinary size");
            Set(view, "moveFrom", Vector2.zero); Set(view, "layoutAnchoredPosition", new Vector2(0, -112));
            Set(view, "moveTime", .5f); Call(view, "ApplyTutorialOffset");
            Vector2 movingPosition = ((RectTransform)root.transform).anchoredPosition;
            view.ConfigureInteraction(false, false, false);
            Require(((RectTransform)root.transform).anchoredPosition == movingPosition, "Interaction refresh preserves animated position");
            Require(!root.transform.Find("BucketBody/SecondSection").GetComponent<Image>().enabled, "Hidden second color does not leak");
            Require(((TMPro.TextMeshProUGUI)Get(view, "capacityLabel")).text == "?", "Hidden capacity remains concealed");
            view.ConfigureInteraction(true, true, false); Call(visual, "LateUpdate");
            Require(!cup.IsMystery && root.transform.Find("BucketBody/SecondSection").GetComponent<Image>().enabled, "Opening reveals both sections");
            var large = new Bucket(1, 10); large.MakeLarge(10);
            Set(view, "bucket", large); Set(view, "flipping", false); Call(view, "ApplyVisual");
            Require(body.sprite == BucketArt.LargeOpen && body.rectTransform.rect.width == body.rectTransform.rect.height, "Large bucket uses square artwork without stretching");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var failures = new List<string>();
        var root = new GameObject("Linked presentation check", typeof(RectTransform));
        var level = ScriptableObject.CreateInstance<LevelData>();
        try
        {
            var stock = new BucketInventory();
            var a = new Bucket(1, 10, 0);
            var b = new Bucket(1, 10, 1);
            a.LinkedPartner = b; b.LinkedPartner = a;
            ((List<Bucket>)stock.Buckets).AddRange(new[] { a, b });
            level.palette = new[] { new Color32(255, 120, 50, 255) };
            var ui = root.AddComponent<BucketInventoryUI>();
            Set(ui, "bucketContainer", root.GetComponent<RectTransform>());
            Set(ui, "inventory", stock); Set(ui, "levelData", level);
            var background = new GameObject("Wall", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var views = (Dictionary<int, BucketView>)Get(ui, "activeViews");
            foreach (var bucket in new[] { a, b, new Bucket(1, 10, 6) })
            {
                var go = new GameObject("Cup", typeof(RectTransform), typeof(BucketView));
                go.transform.SetParent(root.transform, false);
                var view = go.GetComponent<BucketView>(); Set(view, "bucket", bucket);
                Call(view, "EnsureBodyVisual");
                views[bucket.LayoutIndex] = view;
                Call(ui, "PositionBucketView", view, bucket.LayoutIndex);
            }
            Call(ui, "RefreshLinkedPairLines");
            var spawner = new GameObject("ConveyorSlot", typeof(RectTransform), typeof(ConveyorSlotView));
            spawner.transform.SetParent(root.transform, false);
            // Drive the real layout animation at deterministic progress values.
            foreach (float t in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                foreach (var view in views.Values)
                {
                    Set(view, "layoutAnchoredPosition", new Vector2(view.Bucket.LayoutIndex * 112, view.Bucket == b ? -448 : -112));
                    Set(view, "moveFrom", new Vector2(view.Bucket.LayoutIndex * 112, 0));
                    Set(view, "moveTime", t); Call(view, "ApplyTutorialOffset");
                }
                // Spawners move to the first sibling when refreshed.
                spawner.transform.SetAsFirstSibling();
                CallIfPresent(ui, "Update"); CallIfPresent(ui, "LateUpdate");
                var line = root.GetComponentInChildren<LinkedBucketLinkGraphic>();
                int layer = line.transform.parent.GetSiblingIndex();
                Require(layer == 0 && background.transform.GetSiblingIndex() > layer && spawner.transform.GetSiblingIndex() > layer && views.Values.All(view => view.transform.GetSiblingIndex() > layer),
                    "Joining bars render behind walls, rebuilt spawners and all cups");
                using (var vh = new VertexHelper())
                {
                    Call(line, "OnPopulateMesh", vh);
                    var v = new UIVertex(); vh.PopulateUIVertex(ref v, 0);
                    Vector2 center = line.rectTransform.InverseTransformPoint(views[0].transform.TransformPoint(((RectTransform)views[0].transform).rect.center));
                    if (Mathf.Abs(v.position.y - center.y) > 8)
                        failures.Add("Moving link leaves cup at progress " + t + ": link y=" + v.position.y + ", cup y=" + center.y);
                }
            }
            foreach (bool upright in new[] { false, true })
            {
                var obj = new GameObject("Mesh check", typeof(RectTransform), typeof(LinkedBucketLinkGraphic));
                obj.transform.SetParent(root.transform, false);
                var line = obj.GetComponent<LinkedBucketLinkGraphic>();
                line.SetLink(Vector2.zero, new Vector2(112, 0), 96, Color.white, Color.white, upright);
                using (var vh = new VertexHelper())
                {
                    Call(line, "OnPopulateMesh", vh);
                    var v = new UIVertex(); vh.PopulateUIVertex(ref v, 0);
                    if (Mathf.Abs(v.position.x) > .01f) failures.Add("Link must attach at cup center behind its body: x=" + v.position.x);
                    if (Mathf.Abs(v.position.y) > 8) failures.Add("Link not at body center for " + (upright ? "open" : "closed") + " cup: y=" + v.position.y);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
        string report = failures.Count == 0 ? "PASS: linked mesh follows real layout animation at five phases; open/closed links attach at cup centers behind cup bodies, walls and rebuilt spawners." : "FAIL:\n" + string.Join("\n", failures);
        Directory.CreateDirectory("tasks"); File.WriteAllText("tasks/linked-presentation-checks.txt", report);
        Debug.Log(report);
        if (failures.Count > 0) throw new InvalidOperationException(report);
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, Hidden).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
    private static void CallIfPresent(object target, string method) => target.GetType().GetMethod(method, Hidden)?.Invoke(target, null);
}
