#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

public class POICategoryFixer : EditorWindow
{
    [MenuItem("AR Tools/POIs/Napraw Kategorie w Istniejących JSON (Bezpieczne)")]
    public static void FixCategoriesInPlace()
    {
        string dirPath = Path.Combine(Application.dataPath, "Resources/Routes");
        if (!Directory.Exists(dirPath))
        {
            EditorUtility.DisplayDialog("Błąd", "Nie znaleziono folderu Resources/Routes!", "OK");
            return;
        }

        int fixedCount = 0;

        for (int routeId = 1; routeId <= 3; routeId++)
        {
            string filePath = Path.Combine(dirPath, $"Route_{routeId}.json");
            if (!File.Exists(filePath)) continue;

            string json = File.ReadAllText(filePath);
            POITrackContainer container = JsonUtility.FromJson<POITrackContainer>(json);

            if (container == null || container.pois == null) continue;

            foreach (var poi in container.pois)
            {
                string oldCat = poi.category;
                string newCat = CorrectCategory(poi.placeName, oldCat);

                if (oldCat != newCat)
                {
                    poi.category = newCat;
                    fixedCount++;
                }
            }

            string updatedJson = JsonUtility.ToJson(container, true);
            File.WriteAllText(filePath, updatedJson);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Sukces", $"Kategorie zostały zaktualizowane!\nPoprawiono {fixedCount} obiektów.\nWaypointy pozostały nienaruszone.", "Super");
    }

    private static string CorrectCategory(string placeName, string currentCategory)
    {
        string n = (placeName ?? "").ToLower().Trim();

        // 1. PARKING
        if (n.Contains("parking") || n.Contains("postój") || n.Contains("park & ride") || n.Contains("parkuj"))
        {
            return "Parking";
        }

        // 2. STACJA PALIW
        if (n.Contains("stacja paliw") || n.Contains("orlen") || n.Contains("bp") || n.Contains("shell") || n.Contains("moya") || n.Contains("circle k") || n.Contains("circle-k"))
        {
            return "Stacja";
        }

        // 3. SKLEP (sprawdzane PRZED gastronomią)
        if (n.Contains("żabka") || n.Contains("zabka") || n.Contains("biedronka") || n.Contains("lidl") ||
            n.Contains("aldi") || n.Contains("dino") || n.Contains("kaufland") || n.Contains("delikatesy") ||
            n.Contains("market") || n.Contains("sklep") || n.Contains("rossmann") || n.Contains("pepco") ||
            n.Contains("stokrotka") || n.Contains("carrefour") || n.Contains("netto") || n.Contains("lewiatan") ||
            n.Contains("spożywczy") || n.Contains("odido") || n.Contains("gama"))
        {
            return "Sklep";
        }

        // 4. GASTRO
        if (n.Contains("kebab") || n.Contains("pajda") || n.Contains("pizzeria") || n.Contains("pizza") ||
            n.Contains("bistro") || n.Contains("burger") || n.Contains("restauracja") || n.Contains("lodziarnia") ||
            n.Contains("kawiarnia") || n.Contains("cafe") || n.Contains("bar ") || n.EndsWith("bar") || n.Contains("cukiernia") || n.Contains("piekarnia"))
        {
            return "Gastro";
        }

        // 5. APTEKA
        if (n.Contains("apteka") || n.Contains("farmacja") || n.Contains("cefarm") || n.Contains("remedium"))
        {
            return "Apteka";
        }

        // 6. ZABYTEK / KULTURA
        if (n.Contains("kościół") || n.Contains("parafia") || n.Contains("kaplica") || n.Contains("zamek") ||
            n.Contains("muzeum") || n.Contains("pomnik") || n.Contains("dwór") || n.Contains("pałac"))
        {
            return "Zabytek";
        }

        // 7. NOCLEG
        if (n.Contains("hotel") || n.Contains("hostel") || n.Contains("nocleg") || n.Contains("apartament") || n.Contains("pokoje"))
        {
            return "Nocleg";
        }

        if (currentCategory == "Sklep" || currentCategory == "Zabytek" || currentCategory == "Nocleg" || currentCategory == "Sport" || currentCategory == "Stacja")
        {
            return currentCategory;
        }

        return "Inne";
    }
}
#endif