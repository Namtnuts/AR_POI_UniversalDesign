using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class POIFilterManager : MonoBehaviour
{
    public static POIFilterManager Instance;

    [Header("UI References")]
    public TMP_Dropdown categoryDropdown;
    public Slider distanceSlider;
    public TMP_Text distanceValueText;

    [Header("Settings")]
    public float maxDistance = 200f;
    public string selectedCategory = "Wszystkie";

    private Transform userCameraTransform;
    private float nextFilterUpdateTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (Camera.main != null)
            userCameraTransform = Camera.main.transform;

        if (distanceSlider != null)
        {
            distanceSlider.minValue = 50f;
            distanceSlider.maxValue = 400f;
            distanceSlider.value = maxDistance;
            distanceSlider.onValueChanged.AddListener(OnDistanceChanged);
        }

        if (categoryDropdown != null)
        {
            categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        }

        UpdateDistanceText(maxDistance);
    }

    void Update()
    {
        // Odświeżanie zasięgu 2 razy na sekundę, aby pojawiały się nowe punkty w miarę jazdy
        if (Time.time >= nextFilterUpdateTime)
        {
            nextFilterUpdateTime = Time.time + 0.5f;
            ApplyFilters();
        }
    }

    public void OnCategoryChanged(int index)
    {
        if (categoryDropdown == null) return;
        selectedCategory = categoryDropdown.options[index].text;
        ApplyFilters();
    }

    public void OnDistanceChanged(float value)
    {
        maxDistance = value;
        UpdateDistanceText(value);
        ApplyFilters();
    }

    private void UpdateDistanceText(float val)
    {
        if (distanceValueText != null)
            distanceValueText.text = $"Zasięg: {val:F0}m";
    }

    public void ApplyFilters()
    {
        if (userCameraTransform == null && Camera.main != null)
            userCameraTransform = Camera.main.transform;

        POILabel[] pois = FindObjectsByType<POILabel>(FindObjectsInactive.Include);

        foreach (var poi in pois)
        {
            if (poi == null) continue;

            // 1. Warunek kategorii
            bool passCategory = selectedCategory == "Wszystkie" ||
                                poi.category.Equals(selectedCategory, System.StringComparison.OrdinalIgnoreCase);

            // 2. Warunek odległości
            bool passDistance = true;
            if (userCameraTransform != null)
            {
                float dist = Vector3.Distance(userCameraTransform.position, poi.transform.position);
                passDistance = dist <= maxDistance;
            }

            // Włącz/wyłącz cały GameObject (razem z bryłą Mesh/Cube i etykietą)
            poi.SetPOIActiveByFilter(passCategory && passDistance);
        }
    }
}