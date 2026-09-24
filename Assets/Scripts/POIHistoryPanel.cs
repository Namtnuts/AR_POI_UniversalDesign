using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class POIHistoryPanel : MonoBehaviour, IPOIHistoryView
{
    public enum PanelType { ScreenLocked, WorldLocked, GazeActivated }

    [Header("Panel Settings")]
    public PanelType panelType;
    public int maxDisplayItems = 6;

    [Header("UI Containers")]
    public GameObject historyPanel;
    public TMP_Text headerText;
    public Transform itemsContainer;
    public GameObject historyRowPrefab;
    public TMP_Text emptyStateText;

    [Header("Category Sprites")]
    public Sprite hotelSprite;
    public Sprite foodSprite;
    public Sprite museumSprite;
    public Sprite shopSprite;
    public Sprite parkingSprite;
    public Sprite defaultSprite;

    [Header("Animation Durations")]
    public float appearDuration = 0.5f;
    public float disappearDuration = 0.35f;

    private Transform userCamera;
    private List<HistoryRowUI> activeRows = new List<HistoryRowUI>();
    private List<HistoryEntry> lastEntries = new List<HistoryEntry>();
    private float lastMaxDist = 0f;

    private void Awake()
    {
        if (Camera.main != null) userCamera = Camera.main.transform;
        EnsureHeaderReference();
    }

    private void Start()
    {
        TryRegister();
        UpdateHeaderColor();
    }

    private void OnEnable()
    {
        TryRegister();
        UpdateHeaderColor();
        AccessibilityColorManager.OnVisionModeChanged += HandleVisionModeChanged;
    }

    private void OnDisable()
    {
        AccessibilityColorManager.OnVisionModeChanged -= HandleVisionModeChanged;
        if (POIHistoryManager.Instance != null)
            POIHistoryManager.Instance.UnregisterView(this);
    }

    private void EnsureHeaderReference()
    {
        if (headerText == null && historyPanel != null)
        {
            Transform h = historyPanel.transform.Find("HeaderText") ?? historyPanel.transform.Find("TitleText");
            if (h != null) headerText = h.GetComponent<TMP_Text>();
            if (headerText == null) headerText = historyPanel.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void UpdateHeaderColor()
    {
        if (headerText != null && AccessibilityColorManager.Instance != null)
        {
            headerText.color = AccessibilityColorManager.Instance.GetActivePalette().highlightGaze;
        }
    }

    private void HandleVisionModeChanged(AccessibilityColorManager.VisionMode mode)
    {
        UpdateHeaderColor();

        if (lastEntries != null && lastEntries.Count > 0)
        {
            ClearSpawnedRows();
            if (userCamera == null && Camera.main != null) userCamera = Camera.main.transform;
            Vector3 camPos = userCamera != null ? userCamera.position : Vector3.zero;

            int count = Mathf.Min(lastEntries.Count, maxDisplayItems);
            for (int i = count - 1; i >= 0; i--)
            {
                AddNewRowAtTop(lastEntries[i], camPos, false);
            }
        }
    }

    private void TryRegister()
    {
        if (POIHistoryManager.Instance != null)
            POIHistoryManager.Instance.RegisterView(this);
    }

    public void RefreshHistory(List<HistoryEntry> entries, float maxDistanceLimit)
    {
        if (historyPanel == null) return;

        lastEntries = entries;
        lastMaxDist = maxDistanceLimit;

        bool isCorrectMode = false;
        if (POIDisplayManager.Instance != null)
        {
            if (panelType == PanelType.ScreenLocked && POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.ScreenLocked)
                isCorrectMode = true;
            else if (panelType == PanelType.WorldLocked && POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.WorldLocked)
                isCorrectMode = true;
            else if (panelType == PanelType.GazeActivated && POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.GazeActivated)
                isCorrectMode = true;
        }
        else
        {
            isCorrectMode = true;
        }

        bool isVisible = isCorrectMode;
        if (ExperimentManager.Instance != null)
        {
            isVisible = isCorrectMode && (ExperimentManager.Instance.currentStage == ExperimentManager.ExperimentStage.Driving);
        }

        if (historyPanel.activeSelf != isVisible)
        {
            historyPanel.SetActive(isVisible);
        }

        if (!isVisible) return;

        UpdateHeaderColor();

        if (userCamera == null && Camera.main != null) userCamera = Camera.main.transform;
        Vector3 camPos = userCamera != null ? userCamera.position : Vector3.zero;

        if (entries == null || entries.Count == 0)
        {
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(true);
            ClearSpawnedRows();
            return;
        }

        if (emptyStateText != null) emptyStateText.gameObject.SetActive(false);

        int newItemsCount = 0;
        for (int i = 0; i < entries.Count && i < maxDisplayItems; i++)
        {
            bool alreadyInList = false;
            for (int r = 0; r < activeRows.Count; r++)
            {
                if (activeRows[r] != null && activeRows[r].BoundEntry == entries[i])
                {
                    alreadyInList = true;
                    break;
                }
            }

            if (!alreadyInList)
            {
                newItemsCount++;
            }
            else
            {
                break;
            }
        }

        for (int i = newItemsCount - 1; i >= 0; i--)
        {
            AddNewRowAtTop(entries[i], camPos, true);
        }

        for (int i = 0; i < activeRows.Count; i++)
        {
            if (activeRows[i] != null && activeRows[i].BoundEntry != null)
            {
                float dist = Vector3.Distance(camPos, activeRows[i].BoundEntry.passWorldPosition);
                activeRows[i].UpdateDistance(dist);
            }
        }
    }

    private void AddNewRowAtTop(HistoryEntry entry, Vector3 camPos, bool animate)
    {
        if (historyRowPrefab == null || itemsContainer == null) return;

        if (activeRows.Count >= maxDisplayItems)
        {
            HistoryRowUI lastRow = activeRows[activeRows.Count - 1];
            activeRows.RemoveAt(activeRows.Count - 1);
            if (lastRow != null) lastRow.DisappearAndDestroy(disappearDuration);
        }

        GameObject newRowObj = Instantiate(historyRowPrefab, itemsContainer);
        newRowObj.transform.SetAsFirstSibling();

        HistoryRowUI rowUI = newRowObj.GetComponent<HistoryRowUI>();
        if (rowUI != null)
        {
            float dist = Vector3.Distance(camPos, entry.passWorldPosition);
            Sprite catSprite = GetCategorySprite(entry.category);
            rowUI.Appear(entry, dist, animate ? appearDuration : 0.05f, catSprite);

            if (AccessibilityColorManager.Instance != null)
            {
                Color themeCol = AccessibilityColorManager.Instance.GetColorForCategory(entry.category);
                Image rowIcon = rowUI.GetComponentInChildren<Image>();
                if (rowIcon != null && rowIcon.gameObject != rowUI.gameObject)
                {
                    rowIcon.color = themeCol;
                }
            }

            activeRows.Insert(0, rowUI);
        }
    }

    private Sprite GetCategorySprite(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return defaultSprite;
        string normalized = cat.ToLower().Trim();

        if (normalized.Contains("parking") || normalized.Contains("postój") || normalized.Contains("parkuj"))
            return parkingSprite != null ? parkingSprite : defaultSprite;

        if (normalized.Contains("sklep") || normalized.Contains("supermarket") || normalized.Contains("store") || normalized.Contains("market") || normalized.Contains("zakup") || normalized.Contains("shop") || normalized.Contains("mall"))
            return shopSprite != null ? shopSprite : defaultSprite;

        if (normalized.Contains("nocleg") || normalized.Contains("hotel") || normalized.Contains("lodging") || normalized.Contains("hostel"))
            return hotelSprite != null ? hotelSprite : defaultSprite;

        if (normalized.Contains("gastro") || normalized.Contains("restauran") || normalized.Contains("jedzen") || normalized.Contains("food") || normalized.Contains("kebab") || normalized.Contains("cafe"))
            return foodSprite != null ? foodSprite : defaultSprite;

        if (normalized.Contains("zabytek") || normalized.Contains("zabytk") || normalized.Contains("museum") || normalized.Contains("muzeum") || normalized.Contains("kultur"))
            return museumSprite != null ? museumSprite : defaultSprite;

        return defaultSprite;
    }

    private void ClearSpawnedRows()
    {
        foreach (var r in activeRows)
        {
            if (r != null) Destroy(r.gameObject);
        }
        activeRows.Clear();
    }

    public void ClearView()
    {
        if (historyPanel != null) historyPanel.SetActive(false);
        ClearSpawnedRows();
    }
}