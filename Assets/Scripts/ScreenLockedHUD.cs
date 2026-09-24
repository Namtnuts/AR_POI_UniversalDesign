using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ScreenLockedHUD : MonoBehaviour
{
    [Header("Primary Target (Active)")]
    public Camera userCamera;
    public RectTransform arrowRectTransform;
    public Image arrowImage;
    public GameObject poiCardRoot;
    public RectTransform primaryContentRect;
    public CanvasGroup primaryContentCanvasGroup;
    public Image poiIconImage;
    public TMP_Text poiInfoText;

    [Header("Secondary Target (Up Next Queue)")]
    public GameObject nextPoiCardRoot;
    public TMP_Text nextPoiHeaderLabel; // Etykieta nagłówka "Następny punkt:" / "W kolejce:"
    public RectTransform nextContentRect;
    public CanvasGroup nextContentCanvasGroup;
    public Image nextPoiIconImage;
    public TMP_Text nextPoiInfoText;

    [Header("Category Sprites")]
    public Sprite hotelSprite;
    public Sprite foodSprite;
    public Sprite museumSprite;
    public Sprite shopSprite;
    public Sprite parkingSprite;
    public Sprite defaultSprite;

    [Header("Settings")]
    public float arrowRotationSpeed = 8f;
    public float animDuration = 0.55f;
    public float nextOnlyAnimDuration = 0.35f;
    public float slideOffset = 35f;

    [Header("Anti-Jitter / Lock Settings")]
    public float minActiveDwellTime = 3.5f;

    private POILabel currentPoiLabel;
    private POILabel nextPoiLabel;

    private Vector2 primaryBasePos = Vector2.zero;
    private Vector2 nextBasePos = Vector2.zero;

    private Coroutine transitionCoroutine;
    private Coroutine nextOnlyCoroutine;
    private Quaternion targetArrowRotation = Quaternion.identity;

    private float activeDwellTimer = 0f;
    private float lastKnownPrimaryDist = float.MaxValue;
    private HashSet<POILabel> loggedPassedPOIs = new HashSet<POILabel>();

    private void Awake()
    {
        InitializeContentReferences();
    }

    private void OnEnable()
    {
        InitializeContentReferences();
        ResetPanels();
        activeDwellTimer = 0f;
        lastKnownPrimaryDist = float.MaxValue;
        loggedPassedPOIs.Clear();
        UpdateHeaderColor();
        AccessibilityColorManager.OnVisionModeChanged += HandleVisionModeChanged;
    }

    private void OnDisable()
    {
        AccessibilityColorManager.OnVisionModeChanged -= HandleVisionModeChanged;
    }

    private void HandleVisionModeChanged(AccessibilityColorManager.VisionMode mode)
    {
        UpdateHeaderColor();

        Transform playerT = GetActivePlayerTransform();
        if (currentPoiLabel != null && playerT != null)
        {
            float rawDist = Vector3.Distance(playerT.position, currentPoiLabel.transform.position);
            UpdatePrimaryCardContent(rawDist);
        }
        if (nextPoiLabel != null && playerT != null)
        {
            UpdateNextCardContent(playerT);
        }
    }

    private void UpdateHeaderColor()
    {
        if (nextPoiHeaderLabel != null && AccessibilityColorManager.Instance != null)
        {
            Color targetCol = AccessibilityColorManager.Instance.GetActivePalette().highlightGaze;
            string hexCol = "#" + ColorUtility.ToHtmlStringRGB(targetCol);

            // Wymuś biały kolor bazowy TMP, by nie modyfikował odcienia
            nextPoiHeaderLabel.color = Color.white;

            // Wytnij jakikolwiek stary tag koloru i nałóż dynamiczny kolor z palety
            string cleanText = System.Text.RegularExpressions.Regex.Replace(nextPoiHeaderLabel.text, @"<color=.*?>|</color>", "");
            nextPoiHeaderLabel.text = $"<color={hexCol}>{cleanText}</color>";
        }
    }

    private void InitializeContentReferences()
    {
        if (arrowImage == null && arrowRectTransform != null)
        {
            arrowImage = arrowRectTransform.GetComponent<Image>();
        }

        if (primaryContentRect == null && poiCardRoot != null)
        {
            Transform c = poiCardRoot.transform.Find("Content");
            if (c != null)
            {
                primaryContentRect = c.GetComponent<RectTransform>();
                primaryContentCanvasGroup = c.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
            }
        }
        else if (primaryContentRect != null && primaryContentCanvasGroup == null)
        {
            primaryContentCanvasGroup = primaryContentRect.GetComponent<CanvasGroup>() ?? primaryContentRect.gameObject.AddComponent<CanvasGroup>();
        }

        if (nextPoiCardRoot != null)
        {
            if (nextPoiHeaderLabel == null)
            {
                Transform headerT = nextPoiCardRoot.transform.Find("Header") ??
                                   nextPoiCardRoot.transform.Find("HeaderText") ??
                                   nextPoiCardRoot.transform.Find("TitleText");

                if (headerT != null)
                {
                    nextPoiHeaderLabel = headerT.GetComponent<TMP_Text>();
                }
            }

            if (nextContentRect == null)
            {
                Transform c = nextPoiCardRoot.transform.Find("Content");
                if (c != null)
                {
                    nextContentRect = c.GetComponent<RectTransform>();
                    nextContentCanvasGroup = c.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        if (nextContentRect != null && nextContentCanvasGroup == null)
        {
            nextContentCanvasGroup = nextContentRect.GetComponent<CanvasGroup>() ?? nextContentRect.gameObject.AddComponent<CanvasGroup>();
        }

        if (primaryContentRect != null) primaryBasePos = primaryContentRect.anchoredPosition;
        if (nextContentRect != null) nextBasePos = nextContentRect.anchoredPosition;

        UpdateHeaderColor();
    }

    private void ResetPanels()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        if (nextOnlyCoroutine != null)
        {
            StopCoroutine(nextOnlyCoroutine);
            nextOnlyCoroutine = null;
        }

        if (primaryContentCanvasGroup != null) primaryContentCanvasGroup.alpha = 1f;
        if (primaryContentRect != null) primaryContentRect.anchoredPosition = primaryBasePos;

        if (nextContentCanvasGroup != null) nextContentCanvasGroup.alpha = 1f;
        if (nextContentRect != null) nextContentRect.anchoredPosition = nextBasePos;
    }

    private Transform GetActivePlayerTransform()
    {
        if (userCamera != null && userCamera.gameObject.activeInHierarchy) return userCamera.transform;
        if (Camera.main != null) return Camera.main.transform;
        return transform;
    }

    void LateUpdate()
    {
        if (ExperimentManager.Instance != null && ExperimentManager.Instance.currentStage != ExperimentManager.ExperimentStage.Driving)
        {
            HideAllHUD();
            return;
        }

        Transform playerT = GetActivePlayerTransform();
        if (playerT == null) return;

        CheckSpatialPassageToHistory(playerT);
        UpdateCandidatePOIs(playerT);

        if (currentPoiLabel == null)
        {
            HideAllHUD();
            return;
        }

        if (poiCardRoot != null && !poiCardRoot.activeSelf) poiCardRoot.SetActive(true);
        if (arrowRectTransform != null && !arrowRectTransform.gameObject.activeSelf) arrowRectTransform.gameObject.SetActive(true);

        float rawDist = Vector3.Distance(playerT.position, currentPoiLabel.transform.position);
        UpdatePrimaryCardContent(rawDist);

        if (nextOnlyCoroutine == null && transitionCoroutine == null)
        {
            UpdateNextCardContent(playerT);
        }

        if (arrowRectTransform != null)
        {
            Vector3 localTargetPos = playerT.InverseTransformPoint(currentPoiLabel.transform.position);
            float angle = Mathf.Atan2(localTargetPos.x, localTargetPos.z) * Mathf.Rad2Deg;
            targetArrowRotation = Quaternion.Euler(0, 0, -angle);

            arrowRectTransform.localRotation = Quaternion.Slerp(
                arrowRectTransform.localRotation,
                targetArrowRotation,
                Time.deltaTime * arrowRotationSpeed
            );
        }
    }

    private void HideAllHUD()
    {
        if (poiCardRoot != null && poiCardRoot.activeSelf) poiCardRoot.SetActive(false);
        if (nextPoiCardRoot != null && nextPoiCardRoot.activeSelf) nextPoiCardRoot.SetActive(false);
        if (arrowRectTransform != null && arrowRectTransform.gameObject.activeSelf) arrowRectTransform.gameObject.SetActive(false);
    }

    private void CheckSpatialPassageToHistory(Transform playerT)
    {
        POILabel[] allLabels = FindObjectsByType<POILabel>(FindObjectsInactive.Exclude);
        Vector3 pPos = playerT.position;

        foreach (var poi in allLabels)
        {
            if (poi == null || !poi.gameObject.activeInHierarchy || loggedPassedPOIs.Contains(poi)) continue;

            Vector3 localPos = playerT.InverseTransformPoint(poi.transform.position);
            float dist = Vector3.Distance(pPos, poi.transform.position);

            if (localPos.z < -2f && dist <= 30f)
            {
                loggedPassedPOIs.Add(poi);
                if (POIHistoryManager.Instance != null)
                {
                    POIHistoryManager.Instance.RegisterInspectedPOI(poi);
                }
            }
        }
    }

    private void UpdateCandidatePOIs(Transform playerT)
    {
        if (transitionCoroutine != null) return;

        activeDwellTimer += Time.deltaTime;

        float activeMaxDistance = (POIFilterManager.Instance != null)
            ? POIFilterManager.Instance.maxDistance
            : 220f;

        Vector3 pPos = playerT.position;
        Vector3 pForward = playerT.forward;

        POILabel[] allLabels = FindObjectsByType<POILabel>(FindObjectsInactive.Exclude)
            .Where(p => p != null && p.gameObject.activeInHierarchy)
            .ToArray();

        if (allLabels.Length == 0)
        {
            currentPoiLabel = null;
            nextPoiLabel = null;
            return;
        }

        var scoredCandidates = allLabels
            .Select(p => {
                Vector3 toPoi = p.transform.position - pPos;
                float d = toPoi.magnitude;
                float dot = Vector3.Dot(pForward, toPoi.normalized);
                Vector3 localPos = playerT.InverseTransformPoint(p.transform.position);

                float forwardBonus = (dot > 0.2f && localPos.z > 0f) ? 0f : 120f;
                float ratingBonus = Mathf.Clamp(p.rating, 1f, 5f) * 4f;
                float finalScore = (d + forwardBonus) - ratingBonus;

                return new { Label = p, Distance = d, Score = finalScore, IsInFront = (localPos.z > -1f) };
            })
            .OrderBy(x => x.Score)
            .ToList();

        POILabel bestPrimary = scoredCandidates
            .Where(x => x.Distance <= activeMaxDistance && x.IsInFront)
            .Select(x => x.Label)
            .FirstOrDefault();

        POILabel bestNext = scoredCandidates
            .Where(x => x.Label != bestPrimary && x.IsInFront)
            .Select(x => x.Label)
            .FirstOrDefault();

        if (currentPoiLabel == null && bestPrimary != null)
        {
            currentPoiLabel = bestPrimary;
            nextPoiLabel = bestNext;
            activeDwellTimer = 0f;
            lastKnownPrimaryDist = Vector3.Distance(playerT.position, currentPoiLabel.transform.position);
            ResetPanels();
            return;
        }

        if (currentPoiLabel != null)
        {
            float curDist = Vector3.Distance(playerT.position, currentPoiLabel.transform.position);
            Vector3 localP = playerT.InverseTransformPoint(currentPoiLabel.transform.position);

            bool hasPassedCurrent = (localP.z < -2f) || (curDist > lastKnownPrimaryDist + 8f && curDist > 25f);
            bool isDwellExpired = (activeDwellTimer >= minActiveDwellTime);

            lastKnownPrimaryDist = curDist;

            if ((isDwellExpired || hasPassedCurrent) && bestPrimary != null && bestPrimary != currentPoiLabel)
            {
                activeDwellTimer = 0f;
                lastKnownPrimaryDist = Vector3.Distance(playerT.position, bestPrimary.transform.position);
                TriggerSwitchAnimation(bestPrimary, bestNext);
                return;
            }
        }

        if (bestNext != nextPoiLabel && nextOnlyCoroutine == null && transitionCoroutine == null)
        {
            TriggerNextOnlyAnimation(bestNext, playerT);
        }
    }

    private void TriggerSwitchAnimation(POILabel newPrimary, POILabel newNext)
    {
        if (gameObject.activeInHierarchy)
        {
            if (nextOnlyCoroutine != null)
            {
                StopCoroutine(nextOnlyCoroutine);
                nextOnlyCoroutine = null;
            }
            transitionCoroutine = StartCoroutine(SmoothContentTransitionRoutine(newPrimary, newNext));
        }
        else
        {
            currentPoiLabel = newPrimary;
            nextPoiLabel = newNext;
        }
    }

    private void TriggerNextOnlyAnimation(POILabel newNext, Transform playerT)
    {
        if (gameObject.activeInHierarchy && nextContentRect != null)
        {
            nextOnlyCoroutine = StartCoroutine(SmoothNextOnlyTransitionRoutine(newNext, playerT));
        }
        else
        {
            nextPoiLabel = newNext;
            UpdateNextCardContent(playerT);
        }
    }

    private IEnumerator SmoothContentTransitionRoutine(POILabel newPrimary, POILabel newNext)
    {
        float halfTime = animDuration * 0.5f;
        float elapsed = 0f;

        Vector2 pOut = primaryBasePos + new Vector2(slideOffset, 0);
        Vector2 nOut = nextBasePos + new Vector2(0, slideOffset);

        try
        {
            while (elapsed < halfTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfTime);
                float easeOut = Mathf.SmoothStep(0f, 1f, t);

                if (primaryContentCanvasGroup != null) primaryContentCanvasGroup.alpha = 1f - easeOut;
                if (primaryContentRect != null) primaryContentRect.anchoredPosition = Vector2.Lerp(primaryBasePos, pOut, easeOut);

                if (nextContentCanvasGroup != null) nextContentCanvasGroup.alpha = 1f - easeOut;
                if (nextContentRect != null) nextContentRect.anchoredPosition = Vector2.Lerp(nextBasePos, nOut, easeOut);

                yield return null;
            }

            currentPoiLabel = newPrimary;
            nextPoiLabel = newNext;

            Vector2 pIn = primaryBasePos - new Vector2(slideOffset, 0);
            Vector2 nIn = nextBasePos - new Vector2(slideOffset, 0);

            if (primaryContentRect != null) primaryContentRect.anchoredPosition = pIn;
            if (nextContentRect != null) nextContentRect.anchoredPosition = nIn;

            elapsed = 0f;
            while (elapsed < halfTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfTime);
                float easeIn = Mathf.SmoothStep(0f, 1f, t);

                if (primaryContentCanvasGroup != null) primaryContentCanvasGroup.alpha = easeIn;
                if (primaryContentRect != null) primaryContentRect.anchoredPosition = Vector2.Lerp(pIn, primaryBasePos, easeIn);

                if (nextContentCanvasGroup != null) nextContentCanvasGroup.alpha = easeIn;
                if (nextContentRect != null) nextContentRect.anchoredPosition = Vector2.Lerp(nIn, nextBasePos, easeIn);

                yield return null;
            }
        }
        finally
        {
            ResetPanels();
            transitionCoroutine = null;
        }
    }

    private IEnumerator SmoothNextOnlyTransitionRoutine(POILabel newNext, Transform playerT)
    {
        float halfTime = nextOnlyAnimDuration * 0.5f;
        float elapsed = 0f;

        Vector2 nOut = nextBasePos + new Vector2(0, slideOffset);

        try
        {
            while (elapsed < halfTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfTime);
                float easeOut = Mathf.SmoothStep(0f, 1f, t);

                if (nextContentCanvasGroup != null) nextContentCanvasGroup.alpha = 1f - easeOut;
                if (nextContentRect != null) nextContentRect.anchoredPosition = Vector2.Lerp(nextBasePos, nOut, easeOut);

                yield return null;
            }

            nextPoiLabel = newNext;
            UpdateNextCardContent(playerT);

            Vector2 nIn = nextBasePos - new Vector2(slideOffset, 0);
            if (nextContentRect != null) nextContentRect.anchoredPosition = nIn;

            elapsed = 0f;
            while (elapsed < halfTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfTime);
                float easeIn = Mathf.SmoothStep(0f, 1f, t);

                if (nextContentCanvasGroup != null) nextContentCanvasGroup.alpha = easeIn;
                if (nextContentRect != null) nextContentRect.anchoredPosition = Vector2.Lerp(nIn, nextBasePos, easeIn);

                yield return null;
            }
        }
        finally
        {
            if (nextContentCanvasGroup != null) nextContentCanvasGroup.alpha = 1f;
            if (nextContentRect != null) nextContentRect.anchoredPosition = nextBasePos;
            nextOnlyCoroutine = null;
        }
    }

    private void UpdatePrimaryCardContent(float rawDist)
    {
        if (poiInfoText == null || currentPoiLabel == null) return;

        string hexColor = "#FFCC00";
        Color catColor = Color.white;

        if (AccessibilityColorManager.Instance != null)
        {
            hexColor = AccessibilityColorManager.Instance.GetColorHexForCategory(currentPoiLabel.category);
            catColor = AccessibilityColorManager.Instance.GetColorForCategory(currentPoiLabel.category);
        }

        if (arrowImage != null)
        {
            arrowImage.color = catColor;
        }

        if (poiIconImage != null)
        {
            Sprite catSprite = GetCategorySprite(currentPoiLabel.category);
            poiIconImage.sprite = catSprite;
            poiIconImage.color = catColor;
            poiIconImage.enabled = (catSprite != null);
        }

        int roundedDist = Mathf.RoundToInt(rawDist / 10f) * 10;
        if (roundedDist < 10) roundedDist = 10;

        string shortCat = NormalizeCategory(currentPoiLabel.category);
        string nameStr = string.IsNullOrEmpty(currentPoiLabel.placeName) ? "POI" : currentPoiLabel.placeName;

        if (nameStr.Length > 28)
        {
            nameStr = nameStr.Substring(0, 26) + "..";
        }

        poiInfoText.text = $"<b>{nameStr}</b> <color={hexColor}>[{shortCat}]</color> <color=#FFFFFF>{currentPoiLabel.rating:F1}*</color> <color=#AAAAAA>• {roundedDist}m</color>";
    }

    private void UpdateNextCardContent(Transform playerT)
    {
        if (nextPoiCardRoot == null) return;

        if (nextPoiLabel == null)
        {
            if (nextPoiCardRoot.activeSelf) nextPoiCardRoot.SetActive(false);
            return;
        }

        if (!nextPoiCardRoot.activeSelf) nextPoiCardRoot.SetActive(true);

        UpdateHeaderColor();

        string hexColor = "#FFCC00";
        Color catColor = Color.white;

        if (AccessibilityColorManager.Instance != null)
        {
            hexColor = AccessibilityColorManager.Instance.GetColorHexForCategory(nextPoiLabel.category);
            catColor = AccessibilityColorManager.Instance.GetColorForCategory(nextPoiLabel.category);
        }

        if (nextPoiIconImage != null)
        {
            Sprite catSprite = GetCategorySprite(nextPoiLabel.category);
            nextPoiIconImage.sprite = catSprite;
            nextPoiIconImage.color = catColor;
            nextPoiIconImage.enabled = (catSprite != null);
        }

        if (nextPoiInfoText != null && playerT != null)
        {
            float nextDist = Vector3.Distance(playerT.position, nextPoiLabel.transform.position);
            int roundedDist = Mathf.RoundToInt(nextDist / 10f) * 10;
            if (roundedDist < 10) roundedDist = 10;

            string shortCat = NormalizeCategory(nextPoiLabel.category);
            string nameStr = string.IsNullOrEmpty(nextPoiLabel.placeName) ? "POI" : nextPoiLabel.placeName;

            if (nameStr.Length > 22)
            {
                nameStr = nameStr.Substring(0, 20) + "..";
            }

            nextPoiInfoText.text = $"<b>{nameStr}</b>\n<color={hexColor}>[{shortCat}]</color> <color=#FFFFFF>{nextPoiLabel.rating:F1}*</color> <color=#AAAAAA>• {roundedDist}m</color>";
        }
    }

    private string NormalizeCategory(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return "Inne";
        string shortCat = cat;
        if (shortCat.ToLower().Contains("park")) return "Parking";
        if (shortCat.ToLower().Contains("sklep") || shortCat.ToLower().Contains("market")) return "Sklep";
        if (shortCat.ToLower().Contains("gastro") || shortCat.ToLower().Contains("restauran")) return "Gastro";
        if (shortCat.ToLower().Contains("nocleg")) return "Nocleg";
        if (shortCat.ToLower().Contains("zabyt")) return "Zabytek";
        if (shortCat.ToLower().Contains("sport")) return "Sport";
        if (shortCat.ToLower().Contains("paliw") || shortCat.ToLower().Contains("stacja")) return "Stacja";
        if (shortCat.ToLower().Contains("aptek")) return "Apteka";
        return shortCat;
    }

    private Sprite GetCategorySprite(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return defaultSprite;

        string normalized = cat.ToLower().Trim();

        if (normalized.Contains("parking") || normalized.Contains("postój") || normalized.Contains("parkuj"))
            return parkingSprite != null ? parkingSprite : defaultSprite;

        if (normalized.Contains("sklep") || normalized.Contains("supermarket") || normalized.Contains("store") || normalized.Contains("market") || normalized.Contains("zakup") || normalized.Contains("shop") || normalized.Contains("mall"))
            return shopSprite != null ? shopSprite : defaultSprite;

        if (normalized.Contains("nocleg") || normalized.Contains("hotel") || normalized.Contains("lodging") || normalized.Contains("hostel") || normalized.Contains("apart"))
            return hotelSprite != null ? hotelSprite : defaultSprite;

        if (normalized.Contains("gastro") || normalized.Contains("restauran") || normalized.Contains("jedzen") || normalized.Contains("food") || normalized.Contains("cafe"))
            return foodSprite != null ? foodSprite : defaultSprite;

        if (normalized.Contains("zabytek") || normalized.Contains("zabytk") || normalized.Contains("museum") || normalized.Contains("muzeum") || normalized.Contains("kultur"))
            return museumSprite != null ? museumSprite : defaultSprite;

        return defaultSprite;
    }
}