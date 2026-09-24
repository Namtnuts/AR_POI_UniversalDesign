using System;
using UnityEngine;
using TMPro;

public class AccessibilityColorManager : MonoBehaviour
{
    public static AccessibilityColorManager Instance { get; private set; }

    public enum VisionMode
    {
        Default,        // Pełna percepcja barw (Standard)
        Protanopia,     // Brak czerwieni
        Deuteranopia,   // Brak zieleni
        Tritanopia      // Brak błękitu
    }

    [Header("UI Dropdown Reference")]
    [Tooltip("Opcjonalnie: Przeciągnij tu swój TMP_Dropdown")]
    public TMP_Dropdown visionModeDropdown;

    [Header("Current Mode")]
    [SerializeField] private VisionMode currentVisionMode = VisionMode.Default;
    public VisionMode CurrentVisionMode => currentVisionMode;

    public static event Action<VisionMode> OnVisionModeChanged;

    [System.Serializable]
    public struct CategoryPalette
    {
        public Color fuelColor;
        public Color foodColor;
        public Color hotelColor;
        public Color cultureColor;
        public Color sportColor;
        public Color shopColor;
        public Color parkingColor;      // Nowa kategoria
        public Color defaultColor;
        public Color highlightGaze;
    }

    [Header("Certified Accessible Palettes")]
    // 1. Domyślna
    public CategoryPalette defaultPalette = new CategoryPalette
    {
        fuelColor = new Color(0.10f, 0.75f, 0.95f, 1f),      // Błękitny / Cyjan
        foodColor = new Color(1.00f, 0.55f, 0.05f, 1f),      // Pomarańczowy
        hotelColor = new Color(0.55f, 0.30f, 0.90f, 1f),     // Fioletowy
        cultureColor = new Color(0.95f, 0.20f, 0.45f, 1f),   // Róż / Karmazyn
        sportColor = new Color(0.20f, 0.85f, 0.35f, 1f),     // Żywa zieleń
        shopColor = new Color(1.00f, 0.80f, 0.10f, 1f),      // Żółty
        parkingColor = new Color(0.25f, 0.55f, 1.00f, 1f),   // Wyrazisty klasyczny błękit parkingowy [P]
        defaultColor = new Color(0.70f, 0.70f, 0.70f, 1f),
        highlightGaze = new Color(1.0f, 0.85f, 0.0f, 1f)
    };

    // 2. Protanopia (Brak czerwieni -> barwy zimne + żółcie)
    public CategoryPalette protanopiaPalette = new CategoryPalette
    {
        fuelColor = new Color(0.00f, 0.35f, 0.85f, 1f),      // Ciemny niebieski
        foodColor = new Color(0.95f, 0.70f, 0.10f, 1f),      // Jasny bursztyn
        hotelColor = new Color(0.40f, 0.75f, 0.95f, 1f),     // Jasny błękit
        cultureColor = new Color(0.75f, 0.45f, 0.10f, 1f),   // Brązowo-złoty
        sportColor = new Color(0.95f, 0.95f, 0.30f, 1f),     // Cytrynowy
        shopColor = new Color(0.65f, 0.65f, 0.65f, 1f),      // Jasnoszary
        parkingColor = new Color(0.15f, 0.50f, 0.95f, 1f),   // Czysty błękit nieba
        defaultColor = new Color(0.40f, 0.40f, 0.40f, 1f),
        highlightGaze = new Color(1.0f, 1.0f, 1.0f, 1f)
    };

    // 3. Deuteranopia (Brak zieleni -> czyste kontrasty niebieski/pomarańcz)
    public CategoryPalette deuteranopiaPalette = new CategoryPalette
    {
        fuelColor = new Color(0.05f, 0.25f, 0.75f, 1f),      // Kobalt
        foodColor = new Color(0.95f, 0.50f, 0.00f, 1f),      // Pomarańcz
        hotelColor = new Color(0.30f, 0.70f, 0.90f, 1f),     // Błękit
        cultureColor = new Color(0.70f, 0.30f, 0.00f, 1f),   // Ciemna miedź
        sportColor = new Color(1.00f, 0.90f, 0.20f, 1f),     // Jasnożółty
        shopColor = new Color(0.60f, 0.60f, 0.60f, 1f),      // Platyna
        parkingColor = new Color(0.20f, 0.55f, 0.98f, 1f),   // Wyraźny błękit
        defaultColor = new Color(0.35f, 0.35f, 0.35f, 1f),
        highlightGaze = new Color(1.0f, 0.95f, 0.5f, 1f)
    };

    // 4. Tritanopia (Brak błękitu -> czerwień / morski turkus)
    public CategoryPalette tritanopiaPalette = new CategoryPalette
    {
        fuelColor = new Color(0.00f, 0.70f, 0.60f, 1f),      // Wyrazisty Teal / Turkus
        foodColor = new Color(0.95f, 0.15f, 0.30f, 1f),     // Karmazynowa czerwień
        hotelColor = new Color(0.20f, 0.85f, 0.75f, 1f),     // Jasny turkus
        cultureColor = new Color(0.90f, 0.40f, 0.50f, 1f),   // Róż koralowy
        sportColor = new Color(0.65f, 0.05f, 0.15f, 1f),     // Ciemny szkarłat
        shopColor = new Color(0.85f, 0.85f, 0.85f, 1f),      // Jasny neutralny
        parkingColor = new Color(0.10f, 0.50f, 0.55f, 1f),   // Ciemny turkus / morski
        defaultColor = new Color(0.40f, 0.40f, 0.40f, 1f),
        highlightGaze = new Color(1.0f, 0.30f, 0.40f, 1f)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (visionModeDropdown == null)
        {
            visionModeDropdown = FindAnyObjectByType<TMP_Dropdown>();
        }

        if (visionModeDropdown != null)
        {
            visionModeDropdown.onValueChanged.RemoveListener(SetVisionMode);
            visionModeDropdown.onValueChanged.AddListener(SetVisionMode);
        }
    }

    public void SetVisionMode(int modeIndex)
    {
        SetVisionMode((VisionMode)modeIndex);
    }

    public void SetVisionMode(VisionMode mode)
    {
        currentVisionMode = mode;
        Debug.Log($"<color=cyan>[Accessibility]</color> Aktywowano paletę: <b>{mode}</b>");

        OnVisionModeChanged?.Invoke(currentVisionMode);

        POILabel[] allLabels = FindObjectsByType<POILabel>(FindObjectsInactive.Include);
        foreach (var label in allLabels)
        {
            if (label != null)
            {
                label.ApplyAccessibilityColors();
            }
        }
    }

    public CategoryPalette GetActivePalette()
    {
        return currentVisionMode switch
        {
            VisionMode.Protanopia => protanopiaPalette,
            VisionMode.Deuteranopia => deuteranopiaPalette,
            VisionMode.Tritanopia => tritanopiaPalette,
            _ => defaultPalette
        };
    }

    public Color GetColorForCategory(string category)
    {
        CategoryPalette p = GetActivePalette();
        if (string.IsNullOrEmpty(category)) return p.defaultColor;

        string c = category.ToLower().Trim();

        // 1. Parking
        if (c.Contains("parking") || c.Contains("postój") || c.Contains("parkuj"))
            return p.parkingColor;

        // 2. Stacja
        if (c.Contains("paliw") || c.Contains("stacja") || c.Contains("fuel") || c.Contains("ładowan") || c.Contains("bp") || c.Contains("orlen"))
            return p.fuelColor;

        // 3. Sklepy i supermarkety
        if (c.Contains("sklep") || c.Contains("market") || c.Contains("zakup") || c.Contains("shop") || c.Contains("mall") || c.Contains("galeria"))
            return p.shopColor;

        // 4. Gastro
        if (c.Contains("gastro") || c.Contains("jedzen") || c.Contains("restaurac") || c.Contains("food") || c.Contains("kawiarn") || c.Contains("cafe") || c.Contains("bar"))
            return p.foodColor;

        // 5. Nocleg
        if (c.Contains("nocleg") || c.Contains("hotel") || c.Contains("hostel") || c.Contains("apartament") || c.Contains("lodging"))
            return p.hotelColor;

        // 6. Zabytek
        if (c.Contains("zabytek") || c.Contains("kultur") || c.Contains("muzeum") || c.Contains("turyst") || c.Contains("castle") || c.Contains("kościół"))
            return p.cultureColor;

        // 7. Sport
        if (c.Contains("sport") || c.Contains("siłown") || c.Contains("basen") || c.Contains("fitness") || c.Contains("stadion") || c.Contains("rekreac"))
            return p.sportColor;

        return p.defaultColor;
    }

    public string GetColorHexForCategory(string category)
    {
        Color col = GetColorForCategory(category);
        return "#" + ColorUtility.ToHtmlStringRGB(col);
    }
}