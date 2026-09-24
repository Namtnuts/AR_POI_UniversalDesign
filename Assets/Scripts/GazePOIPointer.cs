using UnityEngine;

public class GazePOIPointer : MonoBehaviour
{
    [Header("References")]
    public Transform userCamera;

    [Header("Adaptive Reticle Canvas Settings")]
    [Tooltip("Główny RectTransform Canvasu celownika (np. GazeActivatedCanvas podpięty pod Main Camera)")]
    public RectTransform reticleCanvasRect;
    [Tooltip("Komfortowy dystans celownika w dal, gdy patrzymy w wolną przestrzeń (w metrach)")]
    public float defaultReticleDistance = 15.0f;
    [Tooltip("Prędkość adaptacji głębi celownika")]
    public float reticleSmoothSpeed = 25.0f;
    [Tooltip("Mnożnik skali kątowej celownika (aby zachować stałą wielkość w oku)")]
    public float reticleBaseScaleFactor = 0.001f;

    [Header("Gaze & Snap Settings")]
    [Tooltip("Maksymalny zasięg detekcji wzroku (w metrach)")]
    public float maxGazeDistance = 120f;
    [Tooltip("Zwiększony promień stożka pod kątem wybojów w pojeździe")]
    public float sphereCastRadius = 4.5f;
    public float requiredDwellTime = 0.45f;
    [Tooltip("Bufor czasu tolerancji zgubienia wzroku na nierównościach drogi (w sekundach)")]
    public float gazeLostGracePeriod = 0.25f;

    [Header("Freeze Settings")]
    public float freezeDuration = 4.0f;

    [Header("Gaze Tether (Laser) Settings")]
    [Tooltip("Włącz/wyłącz wiązkę laserową (On/Off) do testów UX")]
    public bool enableLaserTether = false;
    public Color tetherStartColor = new Color(0.1f, 0.75f, 0.95f, 0.6f);
    public Color tetherEndColor = new Color(1.0f, 0.85f, 0.0f, 0.95f);
    [Tooltip("Grubość promienia")]
    public float tetherWidth = 0.06f;
    [Tooltip("Punkt startu wiązki względem kamery: (X: prawo/lewo, Y: góra/dół, Z: przód)")]
    public Vector3 tetherOriginOffset = new Vector3(0.25f, -0.2f, 0.5f);

    private LineRenderer tetherLine;
    private POILabel currentTargetPOI;
    private POICircleMarker currentCircle;
    private float currentDwellTimer = 0f;
    private float gazeLostGraceTimer = 0f;
    private bool isActivationCompleted = false;

    private POILabel frozenPOI;
    private float freezeTimer = 0f;
    private bool isFrozen = false;

    void Awake()
    {
        SetupLaserTether();
    }

    void Update()
    {
        if (ExperimentManager.Instance != null &&
            ExperimentManager.Instance.currentStage != ExperimentManager.ExperimentStage.Driving)
        {
            ResetAllStates();
            if (reticleCanvasRect != null) reticleCanvasRect.gameObject.SetActive(false);
            return;
        }

        if (POIDisplayManager.Instance == null ||
            POIDisplayManager.Instance.currentMode != POIDisplayManager.DisplayMode.GazeActivated)
        {
            ResetAllStates();
            if (reticleCanvasRect != null) reticleCanvasRect.gameObject.SetActive(false);
            return;
        }

        if (userCamera == null)
        {
            if (Camera.main != null) userCamera = Camera.main.transform;
            else return;
        }

        if (reticleCanvasRect != null && !reticleCanvasRect.gameObject.activeSelf)
        {
            reticleCanvasRect.gameObject.SetActive(true);
        }

        if (isFrozen)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0f)
            {
                UnfreezeLabel();
            }
        }

        float effectiveMaxDistance = maxGazeDistance;
        if (POIFilterManager.Instance != null && POIFilterManager.Instance.maxDistance > 0f)
        {
            effectiveMaxDistance = Mathf.Min(maxGazeDistance, POIFilterManager.Instance.maxDistance);
        }

        Ray ray = new Ray(userCamera.position, userCamera.forward);
        RaycastHit hit;

        if (Physics.SphereCast(ray, sphereCastRadius, out hit, effectiveMaxDistance))
        {
            POILabel poi = hit.collider.GetComponentInParent<POILabel>();

            if (poi != null && poi.gameObject.activeInHierarchy)
            {
                float distanceToPoi = Vector3.Distance(userCamera.position, poi.transform.position);
                if (distanceToPoi <= effectiveMaxDistance)
                {
                    gazeLostGraceTimer = 0f;

                    Vector3 hitPos = (hit.point != Vector3.zero) ? hit.point : poi.transform.position;
                    UpdateAdaptiveReticle(true, hitPos);

                    if (frozenPOI == poi)
                    {
                        freezeTimer = freezeDuration;
                        HideTether();
                        return;
                    }

                    if (currentTargetPOI != poi)
                    {
                        ResetTargetHighlight();
                        currentTargetPOI = poi;
                        currentCircle = poi.GetComponent<POICircleMarker>();
                        currentDwellTimer = 0f;
                        isActivationCompleted = false;
                    }

                    if (isActivationCompleted)
                    {
                        if (currentCircle != null) currentCircle.SetProgress(1f);
                        HideTether();
                        return;
                    }

                    currentDwellTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(currentDwellTimer / requiredDwellTime);

                    if (currentCircle != null)
                    {
                        currentCircle.SetProgress(progress);
                    }

                    if (enableLaserTether)
                    {
                        Vector3 originWorldPos = userCamera.TransformPoint(tetherOriginOffset);
                        UpdateTetherPosition(originWorldPos, poi.transform.position, progress);
                    }
                    else
                    {
                        HideTether();
                    }

                    if (currentDwellTimer >= requiredDwellTime)
                    {
                        isActivationCompleted = true;
                        TriggerSnapAndFreeze(currentTargetPOI);
                    }
                    return;
                }
            }
        }

        // Bufor tolerancji wybojów drogi
        if (currentTargetPOI != null && !isActivationCompleted)
        {
            gazeLostGraceTimer += Time.deltaTime;
            if (gazeLostGraceTimer < gazeLostGracePeriod)
            {
                return;
            }
        }

        UpdateAdaptiveReticle(false, Vector3.zero);
        ResetGazeTracking();
    }

    private void UpdateAdaptiveReticle(bool hasTarget, Vector3 targetHitPoint)
    {
        if (reticleCanvasRect == null || userCamera == null) return;

        float targetDistance = defaultReticleDistance;

        if (hasTarget)
        {
            float hitDist = Vector3.Distance(userCamera.position, targetHitPoint);
            targetDistance = Mathf.Max(0.5f, hitDist - 0.1f);
        }

        Vector3 currentLocalPos = reticleCanvasRect.localPosition;
        float smoothZ = Mathf.Lerp(currentLocalPos.z, targetDistance, Time.deltaTime * reticleSmoothSpeed);

        reticleCanvasRect.localPosition = new Vector3(0f, 0f, smoothZ);
        reticleCanvasRect.localRotation = Quaternion.identity;

        float scaleFactor = smoothZ * reticleBaseScaleFactor;
        reticleCanvasRect.localScale = Vector3.one * scaleFactor;
    }

    private void SetupLaserTether()
    {
        tetherLine = GetComponent<LineRenderer>();
        if (tetherLine == null) tetherLine = gameObject.AddComponent<LineRenderer>();

        tetherLine.positionCount = 2;
        tetherLine.startWidth = tetherWidth;
        tetherLine.endWidth = tetherWidth * 1.6f;
        tetherLine.useWorldSpace = true;
        tetherLine.numCapVertices = 4;
        tetherLine.numCornerVertices = 4;

        Shader laserShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (laserShader == null) laserShader = Shader.Find("Sprites/Default");
        if (laserShader == null) laserShader = Shader.Find("Unlit/Color");

        Material lineMat = new Material(laserShader);
        tetherLine.material = lineMat;
        tetherLine.startColor = tetherStartColor;
        tetherLine.endColor = tetherEndColor;
        tetherLine.enabled = false;
    }

    private void UpdateTetherPosition(Vector3 startPos, Vector3 targetPos, float progress)
    {
        if (tetherLine == null) return;

        tetherLine.enabled = true;
        tetherLine.SetPosition(0, startPos);
        tetherLine.SetPosition(1, targetPos);

        float currentW = Mathf.Lerp(tetherWidth * 0.7f, tetherWidth * 1.5f, progress);
        tetherLine.startWidth = currentW * 0.5f;
        tetherLine.endWidth = currentW;

        Color cEnd = Color.Lerp(tetherStartColor, tetherEndColor, progress);
        tetherLine.startColor = tetherStartColor;
        tetherLine.endColor = cEnd;
    }

    private void HideTether()
    {
        if (tetherLine != null && tetherLine.enabled)
        {
            tetherLine.enabled = false;
        }
    }

    private void TriggerSnapAndFreeze(POILabel poi)
    {
        if (poi == null) return;

        HideTether();

        if (frozenPOI != null && frozenPOI != poi)
        {
            frozenPOI.SetGazeHighlight(false);
        }

        frozenPOI = poi;
        frozenPOI.SetGazeHighlight(true);
        isFrozen = true;
        freezeTimer = freezeDuration;

        if (currentCircle != null)
        {
            currentCircle.TriggerConfirmationPulse();
        }

        if (POIHistoryManager.Instance != null)
        {
            POIHistoryManager.Instance.RegisterInspectedPOI(poi);
        }

        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.LogGazeActivation(poi.placeName, requiredDwellTime);
        }
    }

    private void UnfreezeLabel()
    {
        isFrozen = false;
        if (frozenPOI != null)
        {
            frozenPOI.SetGazeHighlight(false);
            frozenPOI = null;
        }
    }

    private void ResetTargetHighlight()
    {
        if (currentCircle != null)
        {
            currentCircle.ResetMarker();
            currentCircle = null;
        }
    }

    private void ResetGazeTracking()
    {
        ResetTargetHighlight();
        HideTether();
        currentTargetPOI = null;
        currentDwellTimer = 0f;
        gazeLostGraceTimer = 0f;
        isActivationCompleted = false;
    }

    public void ResetAllStates()
    {
        ResetGazeTracking();
        UnfreezeLabel();
    }
}