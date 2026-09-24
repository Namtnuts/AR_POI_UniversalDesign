using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;

public class POILabel : MonoBehaviour
{
    public string placeName;
    public float rating;
    public string category = "Inne";

    [Header("UI Containers")]
    public Transform labelRootTransform;
    public GameObject fullBadgePanel;
    public GameObject lodDotObject;

    [Header("Full Badge Elements")]
    public Image categoryIconImage;
    public TMP_Text fullBadgeText;

    [Header("LOD Elements")]
    public TMP_Text lodDotText;

    [Header("Category Sprites")]
    public Sprite hotelSprite;
    public Sprite foodSprite;
    public Sprite museumSprite;
    public Sprite shopSprite;
    public Sprite parkingSprite;
    public Sprite defaultSprite;

    [Header("Vertical Elevation Settings")]
    public float baseHeightOffset = 4.8f;
    public float heightOffsetPerMeter = 0.035f;

    private Transform mainCameraTransform;
    private int lastDisplayedDistance = -1;
    private bool isLODActive = false;
    private CanvasGroup labelCanvasGroup;
    private Coroutine revealCoroutine;
    private float currentAngularScale = 0.0035f;

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        AccessibilityColorManager.OnVisionModeChanged += HandleVisionModeChanged;
    }

    void OnDisable()
    {
        AccessibilityColorManager.OnVisionModeChanged -= HandleVisionModeChanged;
    }

    void Start()
    {
        if (Camera.main != null) mainCameraTransform = Camera.main.transform;
        CheckVisibilityMode();
        ApplyAccessibilityColors();
    }

    private void HandleVisionModeChanged(AccessibilityColorManager.VisionMode mode)
    {
        ApplyAccessibilityColors();
        UpdateText(lastDisplayedDistance, isLODActive);
    }

    public void ApplyAccessibilityColors()
    {
        if (AccessibilityColorManager.Instance == null) return;

        Color themeColor = AccessibilityColorManager.Instance.GetColorForCategory(category);

        if (categoryIconImage != null)
        {
            categoryIconImage.color = themeColor;
        }

        string hexColor = "#" + ColorUtility.ToHtmlStringRGB(themeColor);

        if (lodDotText != null && isLODActive)
        {
            lodDotText.text = $"<color={hexColor}><size=230%>●</size></color>";
        }
        else if (fullBadgeText != null && !isLODActive)
        {
            string cleanName = CleanText(placeName);
            string displayName = string.IsNullOrEmpty(cleanName) ? "Punkt POI" : (cleanName.Length > 26 ? cleanName.Substring(0, 23) + ".." : cleanName);

            string shortCat = NormalizeCategoryDisplay(category);

            fullBadgeText.text = $"<b>{displayName}</b>\n<color={hexColor}>[{shortCat}]</color> <color=#FFFFFF>{rating:F1}*</color> <color=#AAAAAA>• {lastDisplayedDistance}m</color>";
        }
    }

    private string NormalizeCategoryDisplay(string cat)
    {
        string c = (cat ?? "").ToLower();
        if (c.Contains("park")) return "Parking";
        if (c.Contains("sklep") || c.Contains("market")) return "Sklep";
        if (c.Contains("gastro") || c.Contains("restauran")) return "Gastro";
        if (c.Contains("nocleg") || c.Contains("hotel")) return "Nocleg";
        if (c.Contains("zabyt") || c.Contains("museum")) return "Zabytek";
        if (c.Contains("stacja") || c.Contains("paliw")) return "Stacja";
        if (c.Contains("sport")) return "Sport";
        if (c.Contains("aptek")) return "Apteka";
        return "Inne";
    }

    private void EnsureReferences()
    {
        if (labelRootTransform == null)
        {
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null) labelRootTransform = canvas.transform;
            else labelRootTransform = transform;
        }

        if (labelCanvasGroup == null && labelRootTransform != null)
        {
            labelCanvasGroup = labelRootTransform.GetComponent<CanvasGroup>();
            if (labelCanvasGroup == null) labelCanvasGroup = labelRootTransform.gameObject.AddComponent<CanvasGroup>();
        }

        if (fullBadgePanel == null && labelRootTransform != null)
        {
            Transform panelT = labelRootTransform.Find("BadgePanel") ?? labelRootTransform.Find("FullBadgePanel");
            if (panelT != null) fullBadgePanel = panelT.gameObject;
        }

        if (lodDotObject == null && labelRootTransform != null)
        {
            Transform dotT = labelRootTransform.Find("LODDotText");
            if (dotT != null)
            {
                lodDotObject = dotT.gameObject;
                lodDotText = dotT.GetComponent<TMP_Text>();
            }
        }

        if (fullBadgeText == null && fullBadgePanel != null)
        {
            fullBadgeText = fullBadgePanel.GetComponentInChildren<TMP_Text>(true);
        }

        if (categoryIconImage == null && fullBadgePanel != null)
        {
            categoryIconImage = fullBadgePanel.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(img => img.gameObject.name.ToLower().Contains("icon"));
        }
    }

    void Update()
    {
        if (ExperimentManager.Instance != null && ExperimentManager.Instance.currentStage != ExperimentManager.ExperimentStage.Driving)
        {
            SetLabelVisible(false);
            return;
        }

        if (AutoDriveController.Instance != null &&
            AutoDriveController.Instance.startMenuCanvas != null &&
            AutoDriveController.Instance.startMenuCanvas.activeInHierarchy)
        {
            SetLabelVisible(false);
            return;
        }

        if (mainCameraTransform == null)
        {
            if (Camera.main != null) mainCameraTransform = Camera.main.transform;
            else return;
        }

        if (labelRootTransform == null) return;

        bool isGazeMode = POIDisplayManager.Instance != null &&
                          POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.GazeActivated;

        bool isWLMode = POIDisplayManager.Instance == null ||
                        POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.WorldLocked;

        if (isWLMode)
        {
            UpdateRankAndVisibility();
        }

        if (!labelRootTransform.gameObject.activeSelf) return;

        float realDistance = Vector3.Distance(mainCameraTransform.position, transform.position);

        float fixedY = isGazeMode ? 3.0f : 1.8f;
        labelRootTransform.localPosition = new Vector3(0f, fixedY, 0f);

        float angularMultiplier = isGazeMode ? 0.00020f : 0.00014f;
        float minScale = isGazeMode ? 0.0035f : 0.0025f;
        float maxScale = isGazeMode ? 0.048f : 0.035f;

        currentAngularScale = Mathf.Clamp(realDistance * angularMultiplier, minScale, maxScale);

        if (revealCoroutine == null)
        {
            labelRootTransform.localScale = Vector3.one * currentAngularScale;
        }

        Vector3 lookDir = labelRootTransform.position - mainCameraTransform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            labelRootTransform.rotation = Quaternion.LookRotation(lookDir);
        }

        int currentDistanceInt = Mathf.RoundToInt(realDistance / 10f) * 10;
        if (currentDistanceInt != lastDisplayedDistance)
        {
            lastDisplayedDistance = currentDistanceInt;
            UpdateText(lastDisplayedDistance, isLODActive);
        }
    }

    private void UpdateRankAndVisibility()
    {
        if (mainCameraTransform == null) return;

        float baseMaxDist = (POIFilterManager.Instance != null && POIFilterManager.Instance.gameObject.activeInHierarchy)
            ? POIFilterManager.Instance.maxDistance
            : 350f;

        POILabel[] candidates = FindObjectsByType<POILabel>(FindObjectsInactive.Exclude)
            .Where(p => {
                if (p == null || !p.gameObject.activeInHierarchy) return false;
                Vector3 localPos = mainCameraTransform.InverseTransformPoint(p.transform.position);
                float dist = Vector3.Distance(mainCameraTransform.position, p.transform.position);
                return localPos.z > 0.5f && dist <= baseMaxDist;
            })
            .OrderBy(p => Vector3.Distance(mainCameraTransform.position, p.transform.position))
            .ToArray();

        int myIndex = System.Array.IndexOf(candidates, this);

        if (myIndex < 0)
        {
            SetLabelVisible(false);
            return;
        }

        float myRealDist = Vector3.Distance(mainCameraTransform.position, transform.position);
        Camera mainCam = Camera.main;
        bool hasScreenCollision = false;

        if (mainCam != null)
        {
            Vector3 myScreenPos = mainCam.WorldToScreenPoint(transform.position);
            for (int i = 0; i < myIndex; i++)
            {
                if (candidates[i] == null) continue;
                Vector3 otherScreenPos = mainCam.WorldToScreenPoint(candidates[i].transform.position);

                if (Vector2.Distance(new Vector2(myScreenPos.x, myScreenPos.y), new Vector2(otherScreenPos.x, otherScreenPos.y)) < 160f)
                {
                    hasScreenCollision = true;
                    break;
                }
            }
        }

        if (hasScreenCollision)
        {
            SetLabelVisible(false);
            return;
        }

        if (myIndex < 6 && myRealDist <= baseMaxDist)
        {
            SetLabelVisible(true);
            if (isLODActive)
            {
                isLODActive = false;
                UpdateText(lastDisplayedDistance, false);
            }
        }
        else if (myIndex < 18)
        {
            SetLabelVisible(true);
            if (!isLODActive)
            {
                isLODActive = true;
                UpdateText(lastDisplayedDistance, true);
            }
        }
        else
        {
            SetLabelVisible(false);
        }
    }

    private void UpdateText(int distMeters, bool useLOD)
    {
        EnsureReferences();

        bool isWLMode = POIDisplayManager.Instance == null ||
                        POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.WorldLocked;

        string hexColor = "#FFCC00";
        if (AccessibilityColorManager.Instance != null)
        {
            Color catColor = AccessibilityColorManager.Instance.GetColorForCategory(category);
            hexColor = "#" + ColorUtility.ToHtmlStringRGB(catColor);
        }

        if (useLOD && isWLMode)
        {
            if (fullBadgePanel != null) fullBadgePanel.SetActive(false);
            if (lodDotObject != null)
            {
                lodDotObject.SetActive(true);
                lodDotObject.transform.localScale = Vector3.one * 2.2f;
            }

            if (lodDotText != null)
            {
                lodDotText.text = $"<color={hexColor}><size=230%>●</size></color>";
                lodDotText.alignment = TextAlignmentOptions.Center;
            }
        }
        else
        {
            if (lodDotObject != null) lodDotObject.SetActive(false);
            if (fullBadgePanel != null) fullBadgePanel.SetActive(true);

            if (categoryIconImage != null)
            {
                Sprite catSprite = GetCategorySprite(category);
                categoryIconImage.sprite = catSprite;
                categoryIconImage.enabled = (catSprite != null);
                ApplyAccessibilityColors();
            }

            if (fullBadgeText != null)
            {
                string cleanName = CleanText(placeName);
                string displayName = string.IsNullOrEmpty(cleanName) ? "Punkt POI" : cleanName;

                if (displayName.Length > 26)
                {
                    displayName = displayName.Substring(0, 23) + "..";
                }

                string shortCat = NormalizeCategoryDisplay(category);
                fullBadgeText.text = $"<b>{displayName}</b>\n<color={hexColor}>[{shortCat}]</color> <color=#FFFFFF>{rating:F1}*</color> <color=#AAAAAA>• {distMeters}m</color>";
            }
        }
    }

    private string CleanText(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return Regex.Replace(input, @"[^\u0000-\u007F\u0100-\u017F\u00A0-\u00FF]", "").Trim();
    }

    private Sprite GetCategorySprite(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return defaultSprite != null ? defaultSprite : hotelSprite;
        string normalized = cat.ToLower().Trim();

        if (normalized.Contains("parking") || normalized.Contains("postój") || normalized.Contains("parkuj"))
            return parkingSprite != null ? parkingSprite : defaultSprite;

        if (normalized.Contains("sklep") || normalized.Contains("supermarket") || normalized.Contains("store") || normalized.Contains("market") || normalized.Contains("zakup") || normalized.Contains("shop") || normalized.Contains("mall"))
            return shopSprite != null ? shopSprite : defaultSprite;

        if (normalized.Contains("nocleg") || normalized.Contains("hotel") || normalized.Contains("lodging") || normalized.Contains("hostel") || normalized.Contains("apart"))
            return hotelSprite != null ? hotelSprite : defaultSprite;

        if (normalized.Contains("gastro") || normalized.Contains("restauran") || normalized.Contains("jedzen") || normalized.Contains("food") || normalized.Contains("kawiarn") || normalized.Contains("cafe"))
            return foodSprite != null ? foodSprite : defaultSprite;

        if (normalized.Contains("zabytek") || normalized.Contains("zabytk") || normalized.Contains("museum") || normalized.Contains("muzeum") || normalized.Contains("kultur"))
            return museumSprite != null ? museumSprite : defaultSprite;

        return defaultSprite != null ? defaultSprite : hotelSprite;
    }

    public void CheckVisibilityMode()
    {
        if (ExperimentManager.Instance != null && ExperimentManager.Instance.currentStage != ExperimentManager.ExperimentStage.Driving)
        {
            SetLabelVisible(false);
            return;
        }

        if (AutoDriveController.Instance != null &&
            AutoDriveController.Instance.startMenuCanvas != null &&
            AutoDriveController.Instance.startMenuCanvas.activeInHierarchy)
        {
            SetLabelVisible(false);
            return;
        }

        if (POIDisplayManager.Instance == null) return;

        switch (POIDisplayManager.Instance.currentMode)
        {
            case POIDisplayManager.DisplayMode.WorldLocked:
                SetLabelVisible(true);
                break;
            case POIDisplayManager.DisplayMode.ScreenLocked:
                SetLabelVisible(false);
                break;
            case POIDisplayManager.DisplayMode.GazeActivated:
                isLODActive = false;
                if (lodDotObject != null) lodDotObject.SetActive(false);
                SetLabelVisible(false);
                break;
        }
    }

    public void Setup(string name, double poiRating, float distance = 0f, string poiCategory = "Inne")
    {
        EnsureReferences();
        placeName = name;
        rating = (float)poiRating;
        category = poiCategory;

        if (Camera.main != null) mainCameraTransform = Camera.main.transform;

        float currentDist = (mainCameraTransform != null)
            ? Vector3.Distance(mainCameraTransform.position, transform.position)
            : distance;

        lastDisplayedDistance = Mathf.RoundToInt(currentDist);
        ApplyAccessibilityColors();
        UpdateText(lastDisplayedDistance, false);
        CheckVisibilityMode();
    }

    public void SetLabelVisible(bool isVisible)
    {
        EnsureReferences();
        if (labelRootTransform != null) labelRootTransform.gameObject.SetActive(isVisible);
    }

    public void SetGazeHighlight(bool isHighlighted)
    {
        EnsureReferences();

        POICircleMarker myCircle = GetComponent<POICircleMarker>();

        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (isHighlighted)
        {
            SetLabelVisible(true);
            isLODActive = false;
            if (lodDotObject != null) lodDotObject.SetActive(false);
            ApplyAccessibilityColors();
            UpdateText(lastDisplayedDistance, false);

            if (myCircle != null) myCircle.SetSuppressed(true);
            SuppressNearbyRings(transform.position, 30f, true);

            revealCoroutine = StartCoroutine(AnimateGazeRevealRoutine(true));
        }
        else
        {
            if (myCircle != null) myCircle.SetSuppressed(false);
            SuppressNearbyRings(transform.position, 30f, false);

            revealCoroutine = StartCoroutine(AnimateGazeRevealRoutine(false));
        }
    }

    private void SuppressNearbyRings(Vector3 center, float radius, bool suppress)
    {
        POICircleMarker[] allCircles = FindObjectsByType<POICircleMarker>(FindObjectsInactive.Exclude);
        foreach (var c in allCircles)
        {
            if (c == null || c.gameObject == gameObject) continue;
            if (Vector3.Distance(center, c.transform.position) <= radius)
            {
                c.SetSuppressed(suppress);
            }
        }
    }

    private IEnumerator AnimateGazeRevealRoutine(bool show)
    {
        float duration = 0.22f;
        float elapsed = 0f;

        float startAlpha = labelCanvasGroup != null ? labelCanvasGroup.alpha : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (show)
            {
                float scaleMultiplier = Mathf.Sin(smoothT * Mathf.PI * 0.5f) + Mathf.Sin(smoothT * Mathf.PI) * 0.12f;
                labelRootTransform.localScale = Vector3.one * (currentAngularScale * scaleMultiplier);
            }

            if (labelCanvasGroup != null)
            {
                labelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
            }

            yield return null;
        }

        if (labelCanvasGroup != null) labelCanvasGroup.alpha = targetAlpha;

        if (show)
        {
            labelRootTransform.localScale = Vector3.one * currentAngularScale;
        }
        else
        {
            SetLabelVisible(false);
        }

        revealCoroutine = null;
    }

    public void SetPOIActiveByFilter(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}