using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class POITrackContainer
{
    public int routeId;
    public string routeName;
    public double originLat;
    public double originLon;
    public List<WaypointData> waypoints;
    public List<POITrackItem> pois;
}

[Serializable]
public class POITrackItem
{
    public string placeId;
    public string placeName;
    public string category;
    public float rating;
    public double latitude;
    public double longitude;
    public float localX;
    public float localZ;
}

#if UNITY_EDITOR
public class POITrackBaker : EditorWindow
{
    public string apiKey = "";

    [MenuItem("AR Tools/POIs/Bake Real Field Routes (JSON)")]
    public static void ShowWindow()
    {
        GetWindow<POITrackBaker>("Bake Routes");
    }

    private void OnGUI()
    {
        GUILayout.Label("Generator Tras Terenowych (Google Places Pre-Caching)", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        apiKey = EditorGUILayout.TextField("Google API Key:", apiKey);
        EditorGUILayout.Space(10);

        if (GUILayout.Button("Bake Wszystkie 3 Trasy (1-Klik)", GUILayout.Height(40)))
        {
            BakeAllRoutesSequence();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Bake Trasa 1 (BP -> Pajda Kebab)", GUILayout.Height(32)))
        {
            List<Vector2> waypoints = new List<Vector2>()
            {
                new Vector2(49.859808f, 19.228806f),
                new Vector2(49.880657f, 19.222833f),
                new Vector2(49.883374f, 19.230680f)
            };
            StartBake(1, "Trasa 1: BP Czaniec -> Pajda Kebab", waypoints);
        }

        if (GUILayout.Button("Bake Trasa 2 (Pajda -> Rynek/Kęty -> Parking Hejnał)", GUILayout.Height(32)))
        {
            List<Vector2> waypoints = new List<Vector2>()
            {
                new Vector2(49.883374f, 19.230680f),
                new Vector2(49.883640f, 19.238200f),
                new Vector2(49.884727f, 19.225980f),
                new Vector2(49.881150f, 19.202956f)
            };
            StartBake(2, "Trasa 2: Pajda Kebab -> Parking Hejnal", waypoints);
        }

        if (GUILayout.Button("Bake Trasa 3 (Parking Hejnał -> Żwirki i Wigury -> BP)", GUILayout.Height(32)))
        {
            List<Vector2> waypoints = new List<Vector2>()
            {
                new Vector2(49.881150f, 19.202956f),
                new Vector2(49.882216f, 19.217543f),
                new Vector2(49.871329f, 19.217398f),
                new Vector2(49.859808f, 19.228806f)
            };
            StartBake(3, "Trasa 3: Parking Hejnal -> BP Czaniec", waypoints);
        }
    }

    private void BakeAllRoutesSequence()
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 10)
        {
            EditorUtility.DisplayDialog("Błąd", "Wprowadź prawidłowy klucz Google API Key!", "OK");
            return;
        }

        EditorCoroutineRunner.StartEditorCoroutine(BakeAllRoutine());
    }

    private IEnumerator BakeAllRoutine()
    {
        List<Vector2> w1 = new List<Vector2>()
        {
            new Vector2(49.859808f, 19.228806f),
            new Vector2(49.880657f, 19.222833f),
            new Vector2(49.883374f, 19.230680f)
        };
        yield return BakeRoutine(1, "Trasa 1: BP Czaniec -> Pajda Kebab", w1);

        List<Vector2> w2 = new List<Vector2>()
        {
            new Vector2(49.883374f, 19.230680f),
            new Vector2(49.883640f, 19.238200f),
            new Vector2(49.884727f, 19.225980f),
            new Vector2(49.881150f, 19.202956f)
        };
        yield return BakeRoutine(2, "Trasa 2: Pajda Kebab -> Parking Hejnal", w2);

        List<Vector2> w3 = new List<Vector2>()
        {
            new Vector2(49.881150f, 19.202956f),
            new Vector2(49.882216f, 19.217543f),
            new Vector2(49.871329f, 19.217398f),
            new Vector2(49.859808f, 19.228806f)
        };
        yield return BakeRoutine(3, "Trasa 3: Parking Hejnal -> BP Czaniec", w3);

        EditorUtility.DisplayDialog("Sukces", "Wszystkie 3 trasy zostały pomyślnie wygenerowane i zapisane do JSON!", "Świetnie");
    }

    private void StartBake(int routeId, string routeName, List<Vector2> waypoints)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 10)
        {
            EditorUtility.DisplayDialog("Błąd", "Wprowadź prawidłowy klucz Google API Key!", "OK");
            return;
        }

        EditorCoroutineRunner.StartEditorCoroutine(BakeRoutine(routeId, routeName, waypoints));
    }

    private IEnumerator BakeRoutine(int routeId, string routeName, List<Vector2> waypoints)
    {
        Debug.Log($"[TrackBaker] Rozpoczynam pobieranie danych dla: {routeName}...");

        Vector2 origin = waypoints[0];
        List<Vector2> samplePoints = new List<Vector2>();

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Vector2 segStart = waypoints[i];
            Vector2 segEnd = waypoints[i + 1];

            float distKm = CalculateDistanceKm(segStart.x, segStart.y, segEnd.x, segEnd.y);
            int steps = Mathf.Max(2, Mathf.CeilToInt((distKm * 1000f) / 110f));

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                samplePoints.Add(Vector2.Lerp(segStart, segEnd, t));
            }
        }

        Dictionary<string, POITrackItem> collectedPois = new Dictionary<string, POITrackItem>();

        string[] searchTypes = new string[]
        {
            "restaurant", "fast_food", "meal_takeaway", "cafe",
            "lodging", "tourist_attraction", "supermarket", "store",
            "gas_station", "pharmacy", "sports_complex", "stadium"
        };

        int totalQueries = samplePoints.Count * searchTypes.Length;
        int currentQuery = 0;

        foreach (var sample in samplePoints)
        {
            foreach (var type in searchTypes)
            {
                currentQuery++;
                float progress = (float)currentQuery / totalQueries;
                EditorUtility.DisplayProgressBar("Pobieranie POI z Google Maps API", $"{routeName}\nPrzetwarzanie: {currentQuery}/{totalQueries}...", progress);

                string url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={sample.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{sample.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radius=160&type={type}&key={apiKey}&language=pl";

                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.SendWebRequest();
                    while (!req.isDone)
                    {
                        yield return null;
                    }

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        ParseAndFilterPlaces(req.downloadHandler.text, origin, collectedPois);
                    }
                    else
                    {
                        Debug.LogWarning($"[TrackBaker] Błąd zapytania Google API: {req.error}");
                    }
                }
            }
        }

        EditorUtility.ClearProgressBar();

        POITrackContainer container = new POITrackContainer
        {
            routeId = routeId,
            routeName = routeName,
            originLat = origin.x,
            originLon = origin.y,
            pois = collectedPois.Values.OrderBy(p => p.localZ).ToList()
        };

        string dirPath = Path.Combine(Application.dataPath, "Resources/Routes");
        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

        string filePath = Path.Combine(dirPath, $"Route_{routeId}.json");
        string json = JsonUtility.ToJson(container, true);
        File.WriteAllText(filePath, json);
        AssetDatabase.Refresh();

        Debug.Log($"[TrackBaker] SUKCES! Zapisano {filePath} z łączną liczbą {container.pois.Count} wartościowych punktów POI!");
        EditorUtility.DisplayDialog("Sukces", $"Trasa {routeId} została wygenerowana!\nLiczba punktów POI: {container.pois.Count}", "Super");
    }

    private void ParseAndFilterPlaces(string jsonStr, Vector2 origin, Dictionary<string, POITrackItem> poiDict)
    {
        GooglePlacesResponse response = JsonUtility.FromJson<GooglePlacesResponse>(jsonStr);
        if (response == null || response.results == null) return;

        foreach (var p in response.results)
        {
            if (string.IsNullOrEmpty(p.place_id) || poiDict.ContainsKey(p.place_id)) continue;
            if (p.geometry == null || p.geometry.location == null) continue;

            string cat = CategorizePlace(p.types, p.name);
            if (cat == "IGNORUJ") continue;

            float xMeters = (float)((p.geometry.location.lng - origin.y) * 111320.0 * Math.Cos(origin.x * Math.PI / 180.0));
            float zMeters = (float)((p.geometry.location.lat - origin.x) * 110540.0);

            POITrackItem entry = new POITrackItem
            {
                placeId = p.place_id,
                placeName = p.name,
                category = cat,
                rating = (float)(p.rating > 0 ? p.rating : 4.2),
                latitude = p.geometry.location.lat,
                longitude = p.geometry.location.lng,
                localX = xMeters,
                localZ = zMeters
            };

            poiDict[p.place_id] = entry;
        }
    }

    private string CategorizePlace(IEnumerable<string> types, string name)
    {
        string n = name.ToLower();
        string allTypes = types != null ? string.Join(" ", types).ToLower() : "";

        if (allTypes.Contains("accounting") || allTypes.Contains("lawyer") || allTypes.Contains("insurance_agency") ||
            allTypes.Contains("general_contractor") || allTypes.Contains("real_estate_agency") ||
            n.Contains("biuro") || n.Contains("sp. z o.o.") || n.Contains("usługi") || n.Contains("doradztwo") || n.Contains("kancelaria"))
        {
            return "IGNORUJ";
        }

        if (allTypes.Contains("stadium") || allTypes.Contains("gym") || allTypes.Contains("sports_complex") ||
            n.Contains("orlik") || n.Contains("kort") || n.Contains("tenis") || n.Contains("fitness") || n.Contains("boisko") || n.Contains("hala sportowa"))
        {
            return "Sport";
        }

        if (allTypes.Contains("pharmacy") || allTypes.Contains("drugstore") || n.Contains("apteka") || n.Contains("farmacja"))
        {
            return "Apteka";
        }

        if (allTypes.Contains("gas_station") || n.Contains("stacja paliw") || n.Contains("orlen") || n.Contains("bp") || n.Contains("shell") || n.Contains("moya") || n.Contains("circle k"))
        {
            return "Stacja";
        }

        if (allTypes.Contains("restaurant") || allTypes.Contains("food") || allTypes.Contains("cafe") || allTypes.Contains("meal_takeaway") || allTypes.Contains("fast_food") ||
            n.Contains("kebab") || n.Contains("pajda") || n.Contains("pizzeria") || n.Contains("bistro") || n.Contains("burger") || n.Contains("restauracja") || n.Contains("lodziarnia"))
        {
            return "Gastro";
        }

        if (allTypes.Contains("lodging") || n.Contains("hotel") || n.Contains("hostel") || n.Contains("nocleg") || n.Contains("apartament") || n.Contains("pokoj"))
        {
            return "Nocleg";
        }

        if (allTypes.Contains("tourist_attraction") || allTypes.Contains("museum") || n.Contains("kościół") || n.Contains("zamek") || n.Contains("muzeum") || n.Contains("pomnik"))
        {
            return "Zabytek";
        }

        if (allTypes.Contains("supermarket") || allTypes.Contains("grocery_or_supermarket") ||
            n.Contains("biedronka") || n.Contains("dino") || n.Contains("lidl") || n.Contains("delikatesy") || n.Contains("market"))
        {
            return "Sklep";
        }

        return "Inne";
    }

    private float CalculateDistanceKm(float lat1, float lon1, float lat2, float lon2)
    {
        float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return 6371f * 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
    }
}

public static class EditorCoroutineRunner
{
    public static void StartEditorCoroutine(IEnumerator routine)
    {
        EditorApplication.CallbackFunction updateAction = null;
        updateAction = () =>
        {
            if (!routine.MoveNext())
            {
                EditorApplication.update -= updateAction;
            }
        };
        EditorApplication.update += updateAction;
    }
}
#endif