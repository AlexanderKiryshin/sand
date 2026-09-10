using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Observes real transitions in the disposable Play Mode profile; never mutates buckets.</summary>
public static class BucketPresentationObservations
{
    private static readonly Dictionary<Bucket, float> Last = new Dictionary<Bucket, float>();
    private static bool flip, entering, hidden, exiting, wrapped, centered, shared, visibleSpawn;

    public static void Reset()
    {
        Last.Clear(); flip = entering = hidden = exiting = wrapped = centered = shared = visibleSpawn = false;
    }

    public static void Observe(Action<string> capture)
    {
        if (!flip)
        {
            var turning = UnityEngine.Object.FindObjectsOfType<BucketView>().FirstOrDefault(v => v.IsFlipping && v.FlipProgress > .27f && v.FlipProgress < .46f);
            if (turning != null)
            {
                capture("09-bucket-flip-mid"); flip = true;
                Debug.Log("BUCKET_FLIP_MID_PASS: real access transition, progress=" + turning.FlipProgress);
            }
        }
        var belt = UnityEngine.Object.FindObjectOfType<ConveyorBelt>();
        var view = UnityEngine.Object.FindObjectOfType<ConveyorBeltView>();
        var deck = UnityEngine.Object.FindObjectOfType<ConveyorSurfaceGraphic>();
        if (belt == null || view == null || deck == null) return;
        float half = ((RectTransform)view.transform).rect.width * .5f;
        foreach (var entry in belt.Entries)
        {
            float x = entry.PositionX;
            if (!Last.ContainsKey(entry.Bucket))
            {
                if (Mathf.Abs(x) <= ConveyorBelt.VisibleSpawnLimit + 12 && !visibleSpawn)
                { capture("17-visible-initial-spawn"); visibleSpawn = true; }
                Debug.Log($"VISIBLE_SPAWN_PASS: x={x:F1}; exposed limit={ConveyorBelt.VisibleSpawnLimit:F1}; free={belt.FreeSlots}/{belt.Capacity}; ready={belt.AvailableSpawnSlots}");
            }
            foreach (var other in belt.Entries)
                if (other != entry && Mathf.Abs(x - other.PositionX) < ConveyorBelt.BucketSpacing * (entry.Bucket.SlotCost + other.Bucket.SlotCost) * .5f - .2f)
                    throw new InvalidOperationException("Conveyor buckets overlapped their available positions.");
            if (!entering && Mathf.Abs(x + half - 26) < 10) { capture("10-hood-enter"); entering = true; }
            if (!hidden && x < -half - 34) { capture("11-hood-hidden"); hidden = true; }
            if (Last.TryGetValue(entry.Bucket, out float before) && x - before > ConveyorBelt.BucketSpacing)
            {
                if (before + ConveyorBelt.BucketWidth * .5f > -half + 26 || x - ConveyorBelt.BucketWidth * .5f < half - 26)
                    throw new InvalidOperationException("Bucket transferred before reaching an opaque hood.");
                if (!wrapped) Debug.Log($"HOOD_TRANSFER_PASS: previous x={before:F1}, next x={x:F1}, half deck={half:F1}");
                wrapped = true;
            }
            if (wrapped && !exiting && Mathf.Abs(x - half + 26) < 10) { capture("12-hood-exit"); exiting = true; }
            Last[entry.Bucket] = x;
        }
        if (!centered && belt.Entries.Count > 0)
        {
            var bodies = view.GetComponentsInChildren<Image>().Where(i => i.name == "BeltBucket").ToArray();
            foreach (var body in bodies)
            {
                RectTransform rect = body.rectTransform;
                Vector3 bottom = rect.TransformPoint(new Vector3(0, BucketArt.Footprint * rect.rect.height, 0));
                float actual = deck.rectTransform.InverseTransformPoint(bottom).y;
                if (Mathf.Abs(actual - deck.DeckPoint(.5f, .5f).y) > .1f) return; // Wait for the first layout/render update.
                if (body.sprite != BucketArt.Open) throw new InvalidOperationException("Conveyor did not use shared bucket artwork.");
            }
            if (bodies.Length > 0)
            {
                centered = true;
                shared = UnityEngine.Object.FindObjectsOfType<BucketView>().All(v => v.OpenBucketSprite == BucketArt.Open && v.LockedBucketSprite == BucketArt.Inverted);
                Debug.Log("BUCKET_PLACEMENT_PASS: painted base ellipse CENTER lies on exposed deck midline; shared artwork on belt and inventory. Validate perceived seating in actual frames.");
            }
        }
    }

    public static void AssertComplete()
    {
        if (!(flip && entering && hidden && exiting && wrapped && centered && shared && visibleSpawn))
            throw new InvalidOperationException($"Missing visual observations: flip={flip}, enter={entering}, hidden={hidden}, exit={exiting}, wrap={wrapped}, centered={centered}, shared={shared}");
        Debug.Log("BUCKET_PRESENTATION_PASS");
    }
}
