using HUDIndicator;
using System.Collections.Generic;
using UnityEngine;

public class POISpawner : MonoBehaviour
{
    [Header("Origin Location (Srodek ukladu)")]
    public double originLatitude = 50.06143;
    public double originLongitude = 19.93658;

    [Header("Prefab")]
    public GameObject poiPrefab;

    private List<GameObject> spawnedPOIs = new List<GameObject>();

    public void SpawnPOIs(List<PlaceItem> places)
    {
        ClearPOIs();

        if (places == null) return;

        foreach (var place in places)
        {
            if (place.geometry == null || place.geometry.location == null) continue;

            // Pobranie i przetłumaczenie kategorii z listy typów
            string category = MapGoogleTypeToPolishCategory(place.types);

            // Przeliczanie GPS -> Unity 3D
            Vector3 localPos = GPSToLocalWorld(place.geometry.location.lat, place.geometry.location.lng);

            // Instancjonowanie prefaba
            GameObject poiObj = Instantiate(poiPrefab, localPos, Quaternion.identity, transform);
            spawnedPOIs.Add(poiObj);

            // Obliczenie dystansu od punktu (0,0,0) / pozycji startowej
            float distance = Vector3.Distance(Vector3.zero, localPos);

            // Wypełnianie etykiety informacjami (nazwa, ocena, dystans, kategoria)
            POILabel label = poiObj.GetComponent<POILabel>();
            if (label != null)
            {
                label.Setup(place.name, place.rating, distance, category);
            }
        }

        Debug.Log($"<color=green>[POISpawner]:</color> Wygenerowano {spawnedPOIs.Count} punktów POI ze wskaźnikami HUD.");
    }

    public void ClearPOIs()
    {
        foreach (var poi in spawnedPOIs)
        {
            if (poi != null) Destroy(poi);
        }
        spawnedPOIs.Clear();
    }

    private Vector3 GPSToLocalWorld(double lat, double lon)
    {
        double earthRadius = 6371000.0; // Promień Ziemi w metrach

        double dLat = (lat - originLatitude) * Mathf.Deg2Rad;
        double dLon = (lon - originLongitude) * Mathf.Deg2Rad;

        double x = dLon * Mathf.Cos((float)(originLatitude * Mathf.Deg2Rad)) * earthRadius;
        double z = dLat * earthRadius;

        return new Vector3((float)x, 0f, (float)z);
    }

    public string MapGoogleTypeToPolishCategory(List<string> googleTypes)
    {
        if (googleTypes == null || googleTypes.Count == 0) return "Inne";

        foreach (string type in googleTypes)
        {
            string t = type.ToLower();

            // 1. GASTRONOMIA
            if (t == "restaurant" || t == "cafe" || t == "bakery" || t == "bar" ||
                t == "food" || t == "meal_takeaway" || t == "meal_delivery" || t == "gastronomia")
            {
                return "Gastronomia";
            }

            // 2. ZABYTKI I KULTURA
            if (t == "tourist_attraction" || t == "museum" || t == "church" ||
                t == "place_of_worship" || t == "city_hall" || t == "art_gallery" ||
                t == "historical_landmark" || t == "library" || t == "zabytki")
            {
                return "Zabytki i Kultura";
            }

            // 3. ZAKUPY
            if (t == "store" || t == "shopping_mall" || t == "supermarket" ||
                t == "grocery_or_supermarket" || t == "convenience_store" ||
                t == "clothing_store" || t == "shoe_store" || t == "electronics_store" || t == "zakupy")
            {
                return "Zakupy";
            }

            // 4. USŁUGI I TRANSPORT
            if (t == "gas_station" || t == "parking" || t == "bank" || t == "atm" ||
                t == "pharmacy" || t == "hospital" || t == "doctor" ||
                t == "car_repair" || t == "car_wash" || t == "post_office" ||
                t == "bus_station" || t == "transit_station" || t == "usługi i transport")
            {
                return "Usługi i Transport";
            }

            // 5. NOCLEGI
            if (t == "lodging" || t == "hotel" || t == "hostel" || t == "campground" || t == "noclegi")
            {
                return "Noclegi";
            }

            // 6. ROZRYWKA I SPORT
            if (t == "park" || t == "night_club" || t == "amusement_park" ||
                t == "movie_theater" || t == "bowling_alley" || t == "gym" || t == "stadium" || t == "rozrywka")
            {
                return "Rozrywka i Sport";
            }
        }

        return "Inne";
    }
}