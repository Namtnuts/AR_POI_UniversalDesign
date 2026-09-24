using UnityEngine;
using System.Collections.Generic;

public class POIHistoryManager : MonoBehaviour
{
    public static POIHistoryManager Instance;

    [Header("Detection Settings")]
    public float maxDetectionDistance = 50.0f;
    public int maxHistoryItems = 4;

    private Transform userCamera;
    private List<HistoryEntry> historyEntries = new List<HistoryEntry>();
    private HashSet<POILabel> passedPOIs = new HashSet<POILabel>();

    private List<IPOIHistoryView> registeredViews = new List<IPOIHistoryView>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        FindUserCamera();
    }

    void Update()
    {
        if (userCamera == null)
        {
            FindUserCamera();
            if (userCamera == null) return;
        }

        DetectPassedPOIs();

        for (int i = 0; i < registeredViews.Count; i++)
        {
            registeredViews[i]?.RefreshHistory(historyEntries, 0f);
        }
    }

    private void FindUserCamera()
    {
        if (Camera.main != null)
        {
            userCamera = Camera.main.transform;
        }
        else
        {
            var cam = FindAnyObjectByType<Camera>();
            if (cam != null) userCamera = cam.transform;
        }
    }

    public void RegisterView(IPOIHistoryView view)
    {
        if (view != null && !registeredViews.Contains(view))
        {
            registeredViews.Add(view);
            view.RefreshHistory(historyEntries, 0f);
        }
    }

    public void UnregisterView(IPOIHistoryView view)
    {
        if (registeredViews.Contains(view))
        {
            view.ClearView();
            registeredViews.Remove(view);
        }
    }

    private void DetectPassedPOIs()
    {
        // W trybie Gaze rejestrujemy TYLKO po spojrzeniu (GazePOIPointer)
        if (POIDisplayManager.Instance != null &&
            POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.GazeActivated)
        {
            return;
        }

        POILabel[] allPOIs = FindObjectsByType<POILabel>(FindObjectsInactive.Exclude);

        foreach (var poi in allPOIs)
        {
            if (poi == null || passedPOIs.Contains(poi)) continue;

            Vector3 localPos = userCamera.InverseTransformPoint(poi.transform.position);
            float distance = Vector3.Distance(userCamera.position, poi.transform.position);

            if (distance <= maxDetectionDistance && localPos.z < -0.2f)
            {
                RegisterPassedPOI(poi);
            }
        }
    }

    public void RegisterInspectedPOI(POILabel poi)
    {
        if (poi == null) return;

        // Blokada duplikatu, jeśli ten obiekt jest już na samej górze
        if (historyEntries.Count > 0 && historyEntries[0].title.StartsWith(poi.placeName.Length > 20 ? poi.placeName.Substring(0, 15) : poi.placeName))
        {
            return;
        }

        RegisterPassedPOI(poi);
    }

    private void RegisterPassedPOI(POILabel poi)
    {
        passedPOIs.Add(poi);

        string formattedTitle = poi.placeName;
        if (!string.IsNullOrEmpty(formattedTitle) && formattedTitle.Length > 34)
        {
            formattedTitle = formattedTitle.Substring(0, 32) + "..";
        }

        HistoryEntry newEntry = new HistoryEntry
        {
            title = formattedTitle,
            category = poi.category,
            rating = poi.rating,
            passWorldPosition = poi.transform.position
        };

        historyEntries.Insert(0, newEntry);

        if (historyEntries.Count > maxHistoryItems)
        {
            historyEntries.RemoveAt(historyEntries.Count - 1);
        }
    }

    public void ResetHistory()
    {
        historyEntries.Clear();
        passedPOIs.Clear();
        for (int i = 0; i < registeredViews.Count; i++)
        {
            registeredViews[i]?.ClearView();
        }
    }
}