using System.Collections.Generic;
using UnityEngine;

public class POIRouteLoader : MonoBehaviour
{
    public static POIRouteLoader Instance { get; private set; }

    [Header("Prefab POI")]
    [Tooltip("Przeciągnij tutaj prefab POIMarker")]
    public GameObject poiPrefab;

    [Header("Spawn Container")]
    public Transform poiContainer;

    [Header("Wysokość etykiet nad ziemią")]
    public float defaultHeightOffset = 3.5f;

    private List<GameObject> spawnedPOIs = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Jeśli nie przypisano kontenera, stwórz czysty obiekt w korzeniu świata (Root)
        if (poiContainer == null)
        {
            GameObject containerObj = new GameObject("World_POI_Container");
            containerObj.transform.position = Vector3.zero;
            containerObj.transform.rotation = Quaternion.identity;
            containerObj.transform.localScale = Vector3.one;
            poiContainer = containerObj.transform;
        }
    }

    public void SpawnRoutePOIs(POITrackContainer route)
    {
        ClearAllPOIs();

        if (route == null || route.pois == null || poiPrefab == null)
        {
            Debug.LogWarning("[POIRouteLoader] Brak danych trasy lub nieprzypisany POI Prefab!");
            return;
        }

        foreach (var poiData in route.pois)
        {
            // Pozycja lokalna w metrach (X = lewo/prawo od trasy, Y = wysokość, Z = przód)
            Vector3 spawnPos = new Vector3(poiData.localX, defaultHeightOffset, poiData.localZ);

            // Bezpieczne instancjonowanie: ustawienie pozycji bezpośrednio w przestrzeni świata kontenera
            GameObject obj = Instantiate(poiPrefab, poiContainer);
            obj.transform.localPosition = spawnPos;
            obj.transform.localRotation = Quaternion.identity;

            // Inicjalizacja etykiety POILabel
            POILabel label = obj.GetComponent<POILabel>();
            if (label != null)
            {
                label.Setup(poiData.placeName, poiData.rating, poiData.localZ, poiData.category);
            }

            obj.SetActive(true);
            spawnedPOIs.Add(obj);
        }

        Debug.Log($"[POIRouteLoader] Pomyślnie zespawnowano {spawnedPOIs.Count} punktów dla etapu: {route.routeName}");

        // Natychmiastowe zastosowanie aktualnych filtrów (kategoria, zasięg)
        if (POIFilterManager.Instance != null)
        {
            POIFilterManager.Instance.ApplyFilters();
        }
    }

    public void ClearAllPOIs()
    {
        foreach (var obj in spawnedPOIs)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedPOIs.Clear();
    }
}