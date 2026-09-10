using UnityEngine;
using UnityEngine.UI;

/// <summary>Finishes the existing bucket visual after its last grain has landed.</summary>
public class BucketCompletionAnimation : MonoBehaviour
{
    public const float Duration = .55f;
    private RectTransform rect;
    private CanvasGroup visibility;
    private Vector2 origin;
    private float elapsed;

    private void Awake()
    {
        rect = (RectTransform)transform;
        origin = rect.anchoredPosition;
        visibility = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts = false;
        visibility.interactable = false;
        Button button = GetComponent<Button>();
        if (button != null) button.enabled = false;
        BucketMechanicVisual mechanics = GetComponent<BucketMechanicVisual>();
        if (mechanics != null) mechanics.enabled = false;
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
    }

    private void Update() => Advance(Time.deltaTime);

    public void Advance(float deltaTime)
    {
        elapsed += Mathf.Max(0f, deltaTime);
        float t = Mathf.Clamp01(elapsed / Duration);
        float pop = Mathf.Sin(Mathf.Clamp01(t / .32f) * Mathf.PI);
        float exit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, 1f, t));
        float scale = Mathf.Lerp(1f, .08f, exit);
        rect.localScale = new Vector3(scale * (1f + pop * .13f), scale * (1f - pop * .08f), 1f);
        rect.anchoredPosition = origin + Vector2.up * (pop * 6f + exit * 66f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -9f * exit);
        visibility.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1f, t));
        if (t >= 1f) Destroy(gameObject);
    }
}
