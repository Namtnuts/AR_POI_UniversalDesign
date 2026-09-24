using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GooglePlacesManager : MonoBehaviour
{
    [Header("Mode Config")]
    [Tooltip("Zaznacz, aby generować punkty bez płatności w Google Cloud")]
    [SerializeField] private bool useMockData = true;

    [Header("API Config")]
    [SerializeField] private string apiKey = "API_KEY";
    [SerializeField] private int searchRadiusMeters = 1000;

    [Header("Mock Coordinates (Kraków Rynek)")]
    [SerializeField] private double testLatitude = 50.0614300;
    [SerializeField] private double testLongitude = 19.9365800;

    void Start()
    {
        FetchNearbyPOI(testLatitude, testLongitude);
    }

    public void FetchNearbyPOI(double lat, double lng)
    {
        if (useMockData)
        {
            GenerateMockPOI(lat, lng);
        }
        else
        {
            StartCoroutine(GetPlacesRoutine(lat, lng));
        }
    }

    private void GenerateMockPOI(double centerLat, double centerLng)
    {
        Debug.Log("<color=cyan>[Mock System]:</color> Generuję 20 punktów POI wzdłuż trasy 300 metrów...");

        // Przeliczniki: 1m lat ≈ 0.000009, 1m lng ≈ 0.000014 (dla 50°N)
        double latM = 0.000009;
        double lngM = 0.000014;

        GooglePlacesResponse response = new GooglePlacesResponse();
        response.results = new List<PlaceItem>
        {
            // Trasa 0m - 100m
            CreateMockItem("Kawiarnia Sukiennice", "Gastronomia", 4.8, centerLat + (15 * latM), centerLng + (18 * lngM)),   // Prawo 15m
            CreateMockItem("Muzeum Narodowe", "Zabytki", 4.6, centerLat + (30 * latM), centerLng - (16 * lngM)),      // Lewo 30m
            CreateMockItem("Restauracja Pod Aniołami", "Gastronomia", 4.9, centerLat + (45 * latM), centerLng + (20 * lngM)),  // Prawo 45m
            CreateMockItem("Pizzeria Verona", "Gastronomia", 4.3, centerLat + (60 * latM), centerLng - (15 * lngM)),       // Lewo 60m
            CreateMockItem("Wieża Ratuszowa", "Zabytki", 4.7, centerLat + (75 * latM), centerLng + (17 * lngM)),       // Prawo 75m
            CreateMockItem("Bar Mleczny Pod Temidą", "Gastronomia", 4.2, centerLat + (90 * latM), centerLng - (19 * lngM)),   // Lewo 90m

            // Trasa 100m - 200m
            CreateMockItem("Klub Pod Jaszczurami", "Rozrywka", 4.5, centerLat + (105 * latM), centerLng + (15 * lngM)),  // Prawo 105m
            CreateMockItem("Kino Pod Baranami", "Rozrywka", 4.6, centerLat + (120 * latM), centerLng - (18 * lngM)),     // Lewo 120m
            CreateMockItem("Teatr im. Słowackiego", "Zabytki", 4.8, centerLat + (135 * latM), centerLng + (22 * lngM)),   // Prawo 135m
            CreateMockItem("Brama Floriańska", "Zabytki", 4.9, centerLat + (150 * latM), centerLng - (16 * lngM)),       // Lewo 150m
            CreateMockItem("Lody Rzemieślnicze", "Gastronomia", 4.7, centerLat + (165 * latM), centerLng + (14 * lngM)),  // Prawo 165m
            CreateMockItem("Galeria Sztuki Dawnej", "Zabytki", 4.4, centerLat + (180 * latM), centerLng - (20 * lngM)),   // Lewo 180m
            CreateMockItem("Pub Pod Ziemią", "Rozrywka", 4.1, centerLat + (195 * latM), centerLng + (18 * lngM)),         // Prawo 195m

            // Trasa 200m - 300m
            CreateMockItem("Kawiarnia Literacka", "Gastronomia", 4.8, centerLat + (210 * latM), centerLng - (15 * lngM)),  // Lewo 210m
            CreateMockItem("Kościół św. Wojciecha", "Zabytki", 4.9, centerLat + (225 * latM), centerLng + (19 * lngM)),    // Prawo 225m
            CreateMockItem("Escape Room Enigma", "Rozrywka", 4.6, centerLat + (240 * latM), centerLng - (17 * lngM)),      // Lewo 240m
            CreateMockItem("Bistro Parkowe", "Gastronomia", 4.3, centerLat + (255 * latM), centerLng + (16 * lngM)),      // Prawo 255m
            CreateMockItem("Muzeum Sztuki Nowoczesnej", "Zabytki", 4.7, centerLat + (270 * latM), centerLng - (21 * lngM)),// Lewo 270m
            CreateMockItem("Piekarnia Cynamon", "Gastronomia", 4.5, centerLat + (285 * latM), centerLng + (15 * lngM)),    // Prawo 285m
            CreateMockItem("Punkt Widokowy Bastion", "Zabytki", 4.9, centerLat + (295 * latM), centerLng - (18 * lngM))    // Lewo 295m
        };

        ProcessResponse(response);
    }

    private PlaceItem CreateMockItem(string name, string category, double rating, double lat, double lng)
    {
        return new PlaceItem
        {
            name = name,
            rating = rating,
            types = new List<string> { category },
            geometry = new GeometryData { location = new LocationData { lat = lat, lng = lng } }
        };
    }

    private IEnumerator GetPlacesRoutine(double lat, double lng)
    {
        string url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radius={searchRadiusMeters}&key={apiKey}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Google API Error]: {request.error}");
            }
            else
            {
                string jsonResult = request.downloadHandler.text;
                GooglePlacesResponse response = JsonUtility.FromJson<GooglePlacesResponse>(jsonResult);
                ProcessResponse(response);
            }
        }
    }

    private void ProcessResponse(GooglePlacesResponse response)
    {
        POISpawner spawner = GetComponent<POISpawner>();

        if (response != null && response.results != null && response.results.Count > 0)
        {
            Debug.Log($"<color=green>[Success]</color> Otrzymano {response.results.Count} punktów POI!");
            foreach (var place in response.results)
            {
                string cat = "Inne";
                if (spawner != null)
                {
                    cat = spawner.MapGoogleTypeToPolishCategory(place.types);
                }
                Debug.Log($"📍 **{place.name}** [{cat}] | Ocena: {place.rating}★ | Lat: {place.geometry.location.lat}, Lng: {place.geometry.location.lng}");
            }
        }
        else
        {
            Debug.LogWarning("[Google API]: Brak wyników.");
        }

        if (spawner != null)
        {
            spawner.SpawnPOIs(response.results);
        }
    }
}