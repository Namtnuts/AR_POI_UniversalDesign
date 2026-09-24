using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorldLockedHistoryUI : MonoBehaviour, IPOIHistoryView
{
    [Header("UI Containers")]
    public GameObject historyPanel;
    public Transform itemsContainer;
    public GameObject historyRowPrefab;
    public TMP_Text emptyStateText;

    [Header("Display Settings")]
    public int maxDisplayItems = 4; // LIMIT DLA DOLNEGO PANELU (WL)

    [Header("Category Sprites")]
    public Sprite hotelSprite;
    public Sprite foodSprite;
    public Sprite museumSprite;
    public Sprite shopSprite;
    public Sprite defaultSprite;

    [Header("Animation Durations")]
    public float appearDuration = 0.7f;
    public float disappearDuration = 0.5f;

    private Transform userCamera;
    private List<HistoryRowUI> activeRows = new List<HistoryRowUI>();

    private void Awake()
    {
        if (Camera.main != null) userCamera = Camera.main.transform;
        ClearView();
    }

    private void Start()
    {
        TryRegister();
    }

    private void OnEnable()
    {
        TryRegister();
    }

    private void OnDisable()
    {
        if (POIHistoryManager.Instance != null)
        {
            POIHistoryManager.Instance.UnregisterView(this);
        }
    }

    private void TryRegister()
    {
        if (POIHistoryManager.Instance != null)
        {
            POIHistoryManager.Instance.RegisterView(this);
        }
    }

    public void RefreshHistory(List<HistoryEntry> entries, float maxDistanceLimit)
    {
        if (historyPanel == null) return;

        bool isWorldLocked = POIDisplayManager.Instance != null &&
                             POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.WorldLocked;

        bool isMenuOpen = AutoDriveController.Instance != null &&
                          AutoDriveController.Instance.startMenuCanvas != null &&
                          AutoDriveController.Instance.startMenuCanvas.activeSelf;

        bool shouldShowPanel = isWorldLocked && !isMenuOpen;
        historyPanel.SetActive(shouldShowPanel);

        if (!shouldShowPanel) return;

        if (userCamera == null && Camera.main != null) userCamera = Camera.main.transform;
        Vector3 camPos = userCamera != null ? userCamera.position : Vector3.zero;

        if (entries == null || entries.Count == 0)
        {
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(true);
            ClearSpawnedRows();
            return;
        }

        if (emptyStateText != null) emptyStateText.gameObject.SetActive(false);

        if (activeRows.Count == 0 || activeRows[0].BoundEntry != entries[0])
        {
            AddNewRowAtTop(entries[0], camPos, true);
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

        // --- ZAMIAST "4" UŻYWAMY LIMITU Z INSPEKTORA ---
        if (activeRows.Count >= maxDisplayItems)
        {
            HistoryRowUI lastRow = activeRows[activeRows.Count - 1];
            activeRows.RemoveAt(activeRows.Count - 1);
            if (lastRow != null)
            {
                lastRow.DisappearAndDestroy(disappearDuration);
            }
        }

        GameObject newRowObj = Instantiate(historyRowPrefab, itemsContainer);
        newRowObj.transform.SetAsFirstSibling();

        HistoryRowUI rowUI = newRowObj.GetComponent<HistoryRowUI>();
        if (rowUI != null)
        {
            float dist = Vector3.Distance(camPos, entry.passWorldPosition);
            Sprite catSprite = GetCategorySprite(entry.category);
            rowUI.Appear(entry, dist, animate ? appearDuration : 0.05f, catSprite);
            activeRows.Insert(0, rowUI);
        }
    }

    private Sprite GetCategorySprite(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return defaultSprite;
        string normalized = cat.ToLower().Trim();

        if (normalized.Contains("nocleg") || normalized.Contains("hotel") || normalized.Contains("lodging"))
            return hotelSprite != null ? hotelSprite : defaultSprite;
        if (normalized.Contains("gastro") || normalized.Contains("restauran") || normalized.Contains("jedzen") || normalized.Contains("food"))
            return foodSprite != null ? foodSprite : defaultSprite;
        if (normalized.Contains("zabytek") || normalized.Contains("zabytk") || normalized.Contains("museum") || normalized.Contains("kultur"))
            return museumSprite != null ? museumSprite : defaultSprite;
        if (normalized.Contains("sklep") || normalized.Contains("store") || normalized.Contains("zakup") || normalized.Contains("shop"))
            return shopSprite != null ? shopSprite : defaultSprite;

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