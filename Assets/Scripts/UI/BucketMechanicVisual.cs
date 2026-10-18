using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared mechanic cues for upright/closed inventory cups and conveyor cups.</summary>
public sealed class BucketMechanicVisual : MonoBehaviour
{
    private Bucket bucket;
    private Image body, section, badge, gateLock;
    private TextMeshProUGUI label, badgeText;
    private BucketCoverGraphic cover;
    private LevelData level;
    private BucketInventory inventory;
    private int lastFill = -1, lastColor = -1;
    private float coverAmount;
    private float coverFrom, coverTo, coverElapsed = .65f;
    public const float CoverDuration = .65f;
    public bool IsCoverAnimating => cover != null && coverElapsed < CoverDuration;
    private Color displayedColor;

    public void Initialize(Bucket value, Image image, TextMeshProUGUI count, LevelData data, BucketInventory stock = null)
    {
        bucket = value; body = image; label = count; level = data; inventory = stock;
        displayedColor = image.color;
        if (bucket.HasSecondSection)
        {
            section = MakeImage("SecondSection", body.transform, BucketArt.Open, Vector2.zero, Vector2.zero);
            section.rectTransform.anchorMin = Vector2.zero; section.rectTransform.anchorMax = Vector2.one;
            section.rectTransform.offsetMin = section.rectTransform.offsetMax = Vector2.zero;
            section.type = Image.Type.Filled; section.fillMethod = Image.FillMethod.Horizontal; section.fillOrigin = 1; section.fillAmount = .5f;
            section.enabled = !bucket.IsMystery;
            section.transform.SetAsFirstSibling();
        }
        if (bucket.KeyId != 0 || bucket.SlotCost > 1 || bucket.SwapPartner != null || bucket.HasSecondSection)
        {
            badge = MakeImage("MechanicBadge", transform, HintUiSprites.RoundedPanel, new Vector2(-28, 32), new Vector2(42, 32));
            badge.type = Image.Type.Sliced;
            badge.color = bucket.KeyId != 0 ? new Color(1, .75f, .12f) : bucket.SwapPartner != null ? new Color(.80f, .24f, .73f) : new Color(.13f, .57f, .68f);
            if (bucket.KeyId != 0 || bucket.SwapPartner != null)
            {
                var symbol = bucket.KeyId != 0 ? GameUiSymbol.Key : GameUiSymbol.Shuffle;
                MakeImage("Symbol", badge.transform, GameUiSymbols.Get(symbol), Vector2.zero, new Vector2(29, 29));
            }
            else
            {
                var text = new GameObject("Kind", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(badge.transform, false);
                badgeText = text.GetComponent<TextMeshProUGUI>(); badgeText.rectTransform.sizeDelta = new Vector2(42, 32);
                badgeText.fontSize = 23; badgeText.fontStyle = FontStyles.Bold; badgeText.alignment = TextAlignmentOptions.Center;
                badgeText.text = bucket.SlotCost > 1 ? "2" : "1|2"; badgeText.color = Color.white; badgeText.raycastTarget = false;
            }
        }
        // Crates belong to board cells and are rendered by InventoryCrateRowView.
        if (inventory != null && bucket.GateKeyId != 0)
        {
            var go = new GameObject("BucketCover", typeof(RectTransform), typeof(BucketCoverGraphic)); go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(100, 100);
            cover = go.GetComponent<BucketCoverGraphic>(); cover.raycastTarget = false;
            gateLock = MakeImage("GateLock", go.transform, GameUiSymbols.Get(GameUiSymbol.Lock), Vector2.zero, new Vector2(47, 47));
            coverAmount = inventory.IsGateClosed(bucket) ? 1 : 0;
            coverFrom = coverTo = coverAmount;
            coverElapsed = CoverDuration;
            cover.Show(false, coverAmount);
        }
        LateUpdate();
        var focus = gameObject.GetComponent<BucketColorFocus>() ?? gameObject.AddComponent<BucketColorFocus>();
        focus.Initialize(bucket, inventory, body);
    }

    private void LateUpdate()
    {
        if (bucket == null || body == null || level == null) return;
        if (section != null)
        {
            section.sprite = body.sprite;
            section.enabled = !bucket.IsMystery;
            body.color = bucket.IsMystery ? new Color(.3f, .36f, .48f) : SandDisplayColors.FromPalette(level.GetPaletteColor(bucket.FirstColor));
            section.color = SandDisplayColors.FromPalette(level.GetPaletteColor(bucket.SecondColor));
            if (!bucket.IsMystery && bucket.CurrentFill >= bucket.FirstSectionCapacity) body.color *= new Color(.55f, .55f, .55f, 1);
            label.text = bucket.IsMystery ? "?" : bucket.RemainingForColor(bucket.FirstColor) + "|" + bucket.RemainingForColor(bucket.SecondColor);
            label.fontSize = 21; label.color = Color.white;
        }
        else if (bucket.SwapPartner != null && !bucket.IsMystery)
        {
            Color target = SandDisplayColors.FromPalette(level.GetPaletteColor(bucket.ColorIndex));
            displayedColor = Color.Lerp(displayedColor, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
            body.color = displayedColor;
        }
        else if (bucket.State == BucketState.OnConveyor && (lastFill != bucket.CurrentFill || lastColor != bucket.ColorIndex))
            body.color = SandDisplayColors.FromPalette(level.GetPaletteColor(bucket.ColorIndex));
        if (bucket.IsMystery) { label.text = "?"; label.color = Color.white; }
        lastFill = bucket.CurrentFill; lastColor = bucket.ColorIndex;
        AdvanceCover(Time.unscaledDeltaTime);
    }

    public void AdvanceCover(float deltaTime)
    {
        if (cover != null && inventory != null)
        {
            bool closed = inventory.IsGateClosed(bucket);
            float target = closed ? 1 : 0;
            if (coverTo != target)
            {
                coverFrom = coverAmount; coverTo = target; coverElapsed = 0;
            }
            coverElapsed = Mathf.Min(CoverDuration, coverElapsed + Mathf.Max(0, deltaTime));
            float t = coverElapsed / CoverDuration;
            coverAmount = Mathf.Lerp(coverFrom, coverTo, t * t * (3 - 2 * t));
            cover.Show(false, coverAmount);
            gateLock.color = new Color(1, 1, 1, inventory.IsGateClosed(bucket) ? coverAmount : 0);
        }
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size; return image;
    }
}
