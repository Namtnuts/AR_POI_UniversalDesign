using UnityEngine;

public class SLHistoryRowUI : HistoryRowUI
{
    protected override string FormatText(string originalTitle, string shortCategory, int distance, float rating)
    {
        int maxChars = 19;
        string displayTitle = originalTitle;
        if (!string.IsNullOrEmpty(displayTitle) && displayTitle.Length > maxChars)
        {
            displayTitle = displayTitle.Substring(0, maxChars) + "..";
        }

        string hexColor = "#FFCC00";
        if (AccessibilityColorManager.Instance != null && BoundEntry != null)
        {
            hexColor = AccessibilityColorManager.Instance.GetColorHexForCategory(BoundEntry.category);
        }
        else if (AccessibilityColorManager.Instance != null)
        {
            hexColor = AccessibilityColorManager.Instance.GetColorHexForCategory(shortCategory);
        }

        return $"<b>{displayTitle}</b>\n<size=85%><color={hexColor}>[{shortCategory}]</color> <color=#FFFFFF>{rating:F1}*</color> <color=#AAAAAA>• {distance}m temu</color></size>";
    }

    protected override float GetSlideInStartX()
    {
        return -150f; // Wchodzi z lewej strony
    }

    protected override float GetSlideOutEndX()
    {
        return 150f; // Wychodzi w prawą stronę (poza ekran)
    }
}