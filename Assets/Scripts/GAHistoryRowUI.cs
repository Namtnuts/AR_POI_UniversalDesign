using UnityEngine;

public class GAHistoryRowUI : HistoryRowUI
{
    protected override string FormatText(string originalTitle, string shortCategory, int distance, float rating)
    {
        int maxChars = 22;
        string displayTitle = originalTitle;
        if (!string.IsNullOrEmpty(displayTitle) && displayTitle.Length > maxChars)
        {
            displayTitle = displayTitle.Substring(0, maxChars) + "..";
        }

        string hexColor = "#FFFFFF";

        if (AccessibilityColorManager.Instance != null)
        {
            // 1. Priorytet: pełna nazwa kategorii z BoundEntry
            if (BoundEntry != null && !string.IsNullOrEmpty(BoundEntry.category))
            {
                hexColor = AccessibilityColorManager.Instance.GetColorHexForCategory(BoundEntry.category);
            }
            // 2. Rezerwa: skrócona nazwa kategorii
            else if (!string.IsNullOrEmpty(shortCategory))
            {
                hexColor = AccessibilityColorManager.Instance.GetColorHexForCategory(shortCategory);
            }
        }

        return $"<b>{displayTitle}</b> <color={hexColor}>[{shortCategory}]</color> <color=#FFFFFF>{rating:F1}*</color> <color=#AAAAAA>• {distance}m temu</color>";
    }

    protected override float GetSlideInStartX()
    {
        return 0f;
    }

    protected override float GetSlideOutEndX()
    {
        return 0f;
    }
}