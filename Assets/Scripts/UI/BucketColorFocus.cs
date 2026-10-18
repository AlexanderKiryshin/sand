using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class BucketColorFocus : MonoBehaviour
{
    private Bucket bucket;
    private BucketInventory inventory;
    private CanvasRenderer[] visuals;
    public float FilterAlpha => IsDimmed ? .28f : 1f;
    public bool IsDimmed { get; private set; }
    public void Initialize(Bucket value, BucketInventory stock, Image image = null)
    {
        bucket = value;
        inventory = stock;
        visuals = GetComponentsInChildren<CanvasRenderer>(true);
        Refresh();
    }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (bucket == null) return;
        byte selected = ColorFocusPanel.SelectedColor;
        bool hidden = bucket.IsMystery || inventory != null && inventory.IsGateClosed(bucket);
        bool matches = bucket.HasSecondSection
            ? bucket.FirstColor == selected && bucket.RemainingForColor(bucket.FirstColor) > 0
                || bucket.SecondColor == selected && bucket.RemainingForColor(bucket.SecondColor) > 0
            : bucket.ColorIndex == selected;
        IsDimmed = selected != 0 && !hidden && !matches;
        if (visuals != null) foreach (var visual in visuals) if (visual != null) visual.SetAlpha(FilterAlpha);
    }
    private void OnDisable() { if (visuals != null) foreach (var visual in visuals) if (visual != null) visual.SetAlpha(1); }
}
