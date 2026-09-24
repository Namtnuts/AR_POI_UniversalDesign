using UnityEngine;
using System.Collections.Generic;

public class ScreenLockedHistoryUI : MonoBehaviour, IPOIHistoryView
{
    [Header("UI Containers")]
    public GameObject historyPanel;
    public Transform itemsContainer;
    public GameObject historyRowPrefab;

    [Header("Display Settings")]
    public int maxDisplayItems = 7; // LIMIT DLA PANELU BOCZNEGO (SL)

    [Header("Category Sprites")]
    public Sprite hotelSprite;
    public Sprite foodSprite;
    public Sprite museumSprite;
    public Sprite shopSprite;
    public Sprite defaultSprite;

    private Transform userCamera;
    private List<HistoryRowUI> activeRows = new List<HistoryRowUI>();
    private List<HistoryEntry> lastEntriesCache = new List<HistoryEntry>();

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

        bool isScreenLocked = POIDisplayManager.Instance != null &&
                             POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.ScreenLocked;

        bool isMenuOpen = AutoDriveController.Instance != null &&
                          AutoDriveController.Instance.startMenuCanvas != null &&
                          AutoDriveController.Instance.startMenuCanvas.activeSelf;

        bool shouldShowPanel = isScreenLocked && !isMenuOpen;
        historyPanel.SetActive(shouldShowPanel);

        if (!shouldShowPanel) return;

        if (userCamera == null && Camera.main != null) userCamera = Camera.main.transform;
        Vector3 camPos = userCamera != null ? userCamera.position : Vector3.zero;

        if (entries == null || entries.Count == 0)
        {
            ClearSpawnedRows();
            lastEntriesCache.Clear();
            return;
        }

        // --- IZOLACJA WIDOKU: Ucinamy listę do limitu zdefiniowanego dla tego ekranu ---
        int limit = Mathf.Min(entries.Count, maxDisplayItems);
        List<HistoryEntry> viewEntries = entries.GetRange(0, limit);

        bool listsAreSame = (lastEntriesCache.Count == viewEntries.Count);
        if (listsAreSame)
        {
            for (int i = 0; i < viewEntries.Count; i++)
            {
                if (lastEntriesCache[i] != viewEntries[i])
                {
                    listsAreSame = false;
                    break;
                }
            }
        }

        if (!listsAreSame)
        {
            lastEntriesCache = new List<HistoryEntry>(viewEntries);
            RebuildRows(viewEntries, camPos); // Używamy uciętej listy
        }
        else
        {
            for (int i = 0; i < activeRows.Count; i++)
            {
                if (activeRows[i] != null && activeRows[i].BoundEntry != null)
                {
                    float dist = Vector3.Distance(camPos, activeRows[i].BoundEntry.passWorldPosition);
                    activeRows[i].UpdateDistance(dist);
                }
            }
        }
    }

    private void RebuildRows(List<HistoryEntry> entries, Vector3 camPos)
    {
        ClearSpawnedRows();

        if (historyRowPrefab == null || itemsContainer == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            GameObject newRowObj = Instantiate(historyRowPrefab, itemsContainer);
            HistoryRowUI rowUI = newRowObj.GetComponent<HistoryRowUI>();

            if (rowUI != null)
            {
                float dist = Vector3.Distance(camPos, entries[i].passWorldPosition);
                Sprite catSprite = GetCategorySprite(entries[i].category);
                rowUI.Appear(entries[i], dist, 0.05f, catSprite);
                activeRows.Add(rowUI);
            }
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
        lastEntriesCache.Clear();
    }
}