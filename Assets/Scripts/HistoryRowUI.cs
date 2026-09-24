using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public abstract class HistoryRowUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text rowText;
    public RectTransform slideContainer; // NOWE: Kontener wysuwający się na boki

    protected LayoutElement layoutElement;
    protected CanvasGroup canvasGroup;

    public HistoryEntry BoundEntry { get; private set; }

    [Header("Row Settings")]
    public float targetHeight = 28f;

    protected virtual void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = gameObject.AddComponent<LayoutElement>();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (rowText == null) rowText = GetComponentInChildren<TMP_Text>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>();
    }

    public void Appear(HistoryEntry entry, float distance, float duration, Sprite categorySprite = null)
    {
        EnsureComponents();
        BoundEntry = entry;

        transform.localScale = Vector3.one;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        layoutElement.minHeight = 0f;

        if (iconImage != null)
        {
            iconImage.sprite = categorySprite;
            iconImage.enabled = (categorySprite != null);
        }

        UpdateDistance(distance);

        if (rowText != null) rowText.alpha = 0f;
        if (iconImage != null) SetIconAlpha(0f);
        canvasGroup.alpha = 0f;
        layoutElement.preferredHeight = 0f;

        StopAllCoroutines();
        StartCoroutine(AppearRoutine(duration));
    }

    public void UpdateDistance(float rawDist)
    {
        if (rowText == null || BoundEntry == null) return;

        int roundedDist = Mathf.RoundToInt(rawDist / 10f) * 10;
        if (roundedDist < 10) roundedDist = 10;

        string shortCat = BoundEntry.category;
        if (shortCat.ToLower().Contains("gastro")) shortCat = "Gastro";
        else if (shortCat.ToLower().Contains("nocleg")) shortCat = "Nocleg";
        else if (shortCat.ToLower().Contains("zabyt")) shortCat = "Zabytek";

        rowText.text = FormatText(BoundEntry.title, shortCat, roundedDist, BoundEntry.rating);
    }

    // --- METODY ABSTRAKCYJNE ---
    protected abstract string FormatText(string originalTitle, string shortCategory, int distance, float rating);
    protected abstract float GetSlideInStartX();
    protected abstract float GetSlideOutEndX(); // NOWE: Punkt wyjazdu
    // -----------------------------------------------------------------------------------

    public void DisappearAndDestroy(float duration)
    {
        EnsureComponents();
        StopAllCoroutines();
        StartCoroutine(DisappearRoutine(duration));
    }

    private IEnumerator AppearRoutine(float duration)
    {
        float elapsed = 0f;
        float startX = GetSlideInStartX();

        RectTransform rect = slideContainer != null ? slideContainer : GetComponent<RectTransform>();

        // Modyfikujemy X tylko wtedy, gdy faktycznie animujemy przesunięcie (startX != 0)
        if (rect != null && Mathf.Abs(startX) > 0.001f)
        {
            Vector2 initialPos = rect.anchoredPosition;
            initialPos.x = startX;
            rect.anchoredPosition = initialPos;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = 1f - Mathf.Pow(1f - rawT, 3f);

            // Pionowe rozsuwanie
            layoutElement.preferredHeight = Mathf.Lerp(0f, targetHeight, t);

            // Bezpieczne przesuwanie w poziomie tylko dla animacji bocznej
            if (rect != null && Mathf.Abs(startX) > 0.001f)
            {
                Vector2 pos = rect.anchoredPosition;
                pos.x = Mathf.Lerp(startX, 0f, t);
                rect.anchoredPosition = pos;
            }

            // Fade In
            float alphaVal = Mathf.SmoothStep(0f, 1f, rawT);
            if (rowText != null) rowText.alpha = alphaVal;
            if (iconImage != null) SetIconAlpha(alphaVal);
            canvasGroup.alpha = alphaVal;

            yield return null;
        }

        layoutElement.preferredHeight = targetHeight;

        if (rect != null && Mathf.Abs(startX) > 0.001f)
        {
            Vector2 finalPos = rect.anchoredPosition;
            finalPos.x = 0f;
            rect.anchoredPosition = finalPos;
        }

        if (rowText != null) rowText.alpha = 1f;
        if (iconImage != null) SetIconAlpha(1f);
        canvasGroup.alpha = 1f;
    }

    private IEnumerator DisappearRoutine(float duration)
    {
        float elapsed = 0f;
        float startH = layoutElement.preferredHeight > 0f ? layoutElement.preferredHeight : targetHeight;
        float endX = GetSlideOutEndX();
        RectTransform rect = slideContainer != null ? slideContainer : GetComponent<RectTransform>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);

            float alphaVal = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, rawT));
            if (rowText != null) rowText.alpha = alphaVal;
            if (iconImage != null) SetIconAlpha(alphaVal);
            if (canvasGroup != null) canvasGroup.alpha = alphaVal;

            // Pionowe zwijanie
            layoutElement.preferredHeight = Mathf.Lerp(startH, 0f, rawT * rawT);

            // Wylot na bok
            if (rect != null)
            {
                Vector2 pos = rect.anchoredPosition;
                pos.x = Mathf.Lerp(0f, endX, rawT * rawT);
                rect.anchoredPosition = pos;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetIconAlpha(float alpha)
    {
        if (iconImage == null) return;
        Color c = iconImage.color;
        c.a = alpha;
        iconImage.color = c;
    }
}