using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class POICircleMarker : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color baseRingColor = new Color(0.05f, 0.65f, 0.92f, 0.35f);
    public Color progressFillColor = new Color(1.0f, 0.8f, 0.0f, 0.9f);
    public float radius = 2.5f;

    private GameObject ringContainer;
    private RectTransform ringContainerRT;
    private CanvasGroup ringCanvasGroup;
    private Image baseRingImage;
    private Image progressRingImage;
    private Coroutine flashCoroutine;
    private Coroutine fadeCoroutine;
    private bool isSuppressed = false;

    void Awake()
    {
        CreateWorldCanvasRing();
    }

    void Start()
    {
        UpdateColorsFromAccessibility();
    }

    void OnEnable()
    {
        AccessibilityColorManager.OnVisionModeChanged += HandleVisionModeChanged;
        UpdateColorsFromAccessibility();
    }

    void OnDisable()
    {
        AccessibilityColorManager.OnVisionModeChanged -= HandleVisionModeChanged;
    }

    private void HandleVisionModeChanged(AccessibilityColorManager.VisionMode mode)
    {
        UpdateColorsFromAccessibility();
    }

    public void UpdateColorsFromAccessibility()
    {
        if (AccessibilityColorManager.Instance == null) return;

        var activePalette = AccessibilityColorManager.Instance.GetActivePalette();

        // 1. Kolor paska postępu (Highlight Gaze dedykowany dla danego trybu wzroku)
        progressFillColor = activePalette.highlightGaze;
        if (progressRingImage != null)
        {
            progressRingImage.color = progressFillColor;
        }

        // 2. Kolor pierścienia bazowego dostosowany do trybu (35% alpha)
        Color dynamicBase = activePalette.fuelColor;
        dynamicBase.a = 0.35f;
        baseRingColor = dynamicBase;

        if (baseRingImage != null)
        {
            baseRingImage.color = baseRingColor;
        }
    }

    void Update()
    {
        // Pierścień żyje WYŁĄCZNIE wtedy, gdy ExperimentManager jest w stanie jazdy (Driving)
        bool isDriving = ExperimentManager.Instance != null &&
                         ExperimentManager.Instance.currentStage == ExperimentManager.ExperimentStage.Driving;

        bool isGazeMode = isDriving &&
                          POIDisplayManager.Instance != null &&
                          POIDisplayManager.Instance.currentMode == POIDisplayManager.DisplayMode.GazeActivated;

        if (ringContainer != null)
        {
            bool shouldBeActive = isGazeMode && !isSuppressed;

            if (ringContainer.activeSelf != shouldBeActive)
            {
                ringContainer.SetActive(shouldBeActive);
            }

            if (shouldBeActive && Camera.main != null)
            {
                ringContainer.transform.LookAt(Camera.main.transform);
            }
        }
    }

    private void CreateWorldCanvasRing()
    {
        ringContainer = new GameObject("GazeRing_Canvas");
        ringContainer.transform.SetParent(transform, false);
        ringContainer.transform.localPosition = Vector3.zero;

        Canvas canvas = ringContainer.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        ringCanvasGroup = ringContainer.AddComponent<CanvasGroup>();
        ringCanvasGroup.alpha = 1f;

        ringContainerRT = ringContainer.GetComponent<RectTransform>();
        ringContainerRT.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        ringContainerRT.localScale = Vector3.one * 0.01f;

        Sprite ringSprite = CreateRingSprite(128);

        // 1. Warstwa bazowa
        GameObject baseObj = new GameObject("BaseRing");
        baseObj.transform.SetParent(ringContainer.transform, false);
        RectTransform baseRT = baseObj.AddComponent<RectTransform>();
        baseRT.sizeDelta = new Vector2(radius * 200f, radius * 200f);

        baseRingImage = baseObj.AddComponent<Image>();
        baseRingImage.sprite = ringSprite;
        baseRingImage.color = baseRingColor;

        // 2. Warstwa ładowania (wypełnienie radialne)
        GameObject progObj = new GameObject("ProgressRing");
        progObj.transform.SetParent(ringContainer.transform, false);
        RectTransform progRT = progObj.AddComponent<RectTransform>();
        progRT.sizeDelta = new Vector2(radius * 200f, radius * 200f);

        progressRingImage = progObj.AddComponent<Image>();
        progressRingImage.sprite = ringSprite;
        progressRingImage.color = progressFillColor;
        progressRingImage.type = Image.Type.Filled;
        progressRingImage.fillMethod = Image.FillMethod.Radial360;
        progressRingImage.fillOrigin = (int)Image.Origin360.Top;
        progressRingImage.fillClockwise = true;
        progressRingImage.fillAmount = 0f;

        UpdateColorsFromAccessibility();
        ringContainer.SetActive(false);
    }

    private Sprite CreateRingSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f;
        float innerRadius = outerRadius * 0.8f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= outerRadius && dist >= innerRadius)
                    texture.SetPixel(x, y, Color.white);
                else
                    texture.SetPixel(x, y, Color.clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public void SetProgress(float progress)
    {
        if (progressRingImage != null)
        {
            progressRingImage.fillAmount = Mathf.Clamp01(progress);
        }
    }

    public void TriggerConfirmationPulse()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(PulseFlashRoutine());
    }

    private IEnumerator PulseFlashRoutine()
    {
        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 baseScale = Vector3.one * 0.01f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scaleMultiplier = Mathf.Lerp(1.0f, 1.25f, t);
            if (ringContainerRT != null) ringContainerRT.localScale = baseScale * scaleMultiplier;

            if (progressRingImage != null)
            {
                Color flashColor = Color.Lerp(Color.white, progressFillColor, t);
                flashColor.a = Mathf.Lerp(1f, 0f, t);
                progressRingImage.color = flashColor;
            }

            yield return null;
        }

        if (ringContainerRT != null) ringContainerRT.localScale = baseScale;
        flashCoroutine = null;
    }

    public void SetSuppressed(bool suppress)
    {
        isSuppressed = suppress;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(!suppress));
    }

    private IEnumerator FadeRoutine(bool targetVisible)
    {
        if (ringCanvasGroup == null) yield break;

        float startAlpha = ringCanvasGroup.alpha;
        float targetAlpha = targetVisible ? 1f : 0f;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ringCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        ringCanvasGroup.alpha = targetAlpha;
        if (!targetVisible && ringContainer != null)
        {
            ringContainer.SetActive(false);
        }
        else if (targetVisible)
        {
            ResetMarker();
        }
        fadeCoroutine = null;
    }

    public void ResetMarker()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        if (ringContainerRT != null) ringContainerRT.localScale = Vector3.one * 0.01f;
        if (progressRingImage != null)
        {
            progressRingImage.color = progressFillColor;
            progressRingImage.fillAmount = 0f;
        }
    }
}