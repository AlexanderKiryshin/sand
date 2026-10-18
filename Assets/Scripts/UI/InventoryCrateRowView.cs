using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Crate panels alternate between odd and even columns across every board cell.</summary>
public sealed class InventoryCrateRowView : MonoBehaviour
{
    private BucketInventory inventory;
    private readonly Dictionary<int, BucketCoverGraphic> covers = new Dictionary<int, BucketCoverGraphic>();
    private float amount, from, to, elapsed = BucketMechanicVisual.CoverDuration;
    public float CoverAmount => amount;
    public bool IsCoverAnimating => elapsed < BucketMechanicVisual.CoverDuration;

    public void Bind(BucketInventory stock, Vector2 cellSize, Vector2 spacing)
    {
        if (inventory != stock)
        {
            inventory = stock;
            amount = from = to = stock.CratesOpen ? 0 : 1;
            elapsed = BucketMechanicVisual.CoverDuration;
        }
        foreach (int index in stock.CrateLayoutIndices)
        {
            if (!covers.TryGetValue(index, out var cover))
            {
                var obj = new GameObject("Crate_" + index, typeof(RectTransform), typeof(BucketCoverGraphic));
                obj.transform.SetParent(transform, false);
                cover = obj.GetComponent<BucketCoverGraphic>(); cover.raycastTarget = false;
                covers.Add(index, cover);
            }
            var rect = cover.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = cellSize;
            rect.anchoredPosition = new Vector2(index % LevelData.BucketLayoutColumns * (cellSize.x + spacing.x),
                -index / LevelData.BucketLayoutColumns * (cellSize.y + spacing.y));
            cover.Show(true, index % LevelData.BucketLayoutColumns % 2 == 0 ? amount : 1 - amount);
        }
        transform.SetAsLastSibling();
    }
    private void LateUpdate() => AdvanceCover(Time.unscaledDeltaTime);
    public void AdvanceCover(float deltaTime)
    {
        if (inventory == null) return;
        float target = inventory.CratesOpen ? 0 : 1;
        if (to != target) { from = amount; to = target; elapsed = 0; }
        elapsed = Mathf.Min(BucketMechanicVisual.CoverDuration, elapsed + Mathf.Max(0, deltaTime));
        amount = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / BucketMechanicVisual.CoverDuration));
        foreach (var pair in covers)
            pair.Value.Show(true, pair.Key % LevelData.BucketLayoutColumns % 2 == 0 ? amount : 1 - amount);
    }
}
