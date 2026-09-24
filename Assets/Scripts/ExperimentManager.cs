using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[System.Serializable]
public class WaypointData
{
    public float lat;
    public float lon;
}

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance { get; private set; }

    public enum ExperimentStage
    {
        InitialSetup,
        SelectMode,
        Driving,
        RouteFinished
    }

    [Header("Experiment State")]
    public ExperimentStage currentStage = ExperimentStage.InitialSetup;
    public int currentRouteIndex = 1;
    public string participantId = "P_01";

    [Header("XR Controller Visuals")]
    [Tooltip("Obiekt promienia kontrolera (np. Ray Interactor / Line Visual) do wygaszania podczas jazdy")]
    public GameObject controllerRayObject;

    [Header("Player / World Alignment")]
    [Tooltip("Obiekt XR Origin / Camera Offset reprezentujący pojazd")]
    public Transform playerRigTransform;
    [Tooltip("Statyczny kontener z punktami POI")]
    public Transform worldPoiContainer;

    [Header("Live Motion Settings")]
    public bool enableLiveGPSMotion = true;
    public float motionSmoothing = 4.5f;
    public float rotationSmoothing = 6.0f;

    [Header("UI Canvases - Standard Flow")]
    public GameObject startSetupCanvas;
    public GameObject modeSelectCanvas;
    public GameObject endRouteCanvas;
    [Tooltip("Canvas z 1 przyciskiem pokazywany po etapie 1 i 2")]
    public GameObject surveyPromptCanvas;
    public GameObject filterCanvas;
    public TextMeshProUGUI stageInfoText;

    [Header("UI Canvases - History Canvases")]
    [Tooltip("Canvas historii dla trybu Screen-Locked")]
    public GameObject screenlockedHistoryCanvas;
    [Tooltip("Canvas historii dla trybu Gaze-Activated")]
    public GameObject gazeActivatedHistoryCanvas;

    [Header("UI Canvases - Final Screen (Po 3. etapie)")]
    [Tooltip("Dedykowany nowy canvas końca całego badania z 2 przyciskami")]
    public GameObject finalEndCanvas;
    public Button finalResetButton;
    public Button finalQuitButton;

    [Header("Route Finish Settings")]
    public float arrivalThresholdMeters = 35f;

    private POITrackContainer activeRouteData;
    private Vector2 targetDestinationMeters;
    private Vector2 checkpointMeters;
    private bool isCheckpointPassed = false;
    private bool isArrivalPromptShown = false;
    private CanvasGroup endRouteCanvasGroup;
    private Coroutine endRouteAnimationCoroutine;

    private Vector3 targetRigPosition = Vector3.zero;
    private Quaternion targetRigRotation = Quaternion.identity;
    private bool isCalibrated = false;

    // Cache punktów trasy w metrach
    private List<Vector2> routeWaypointsMeters = new List<Vector2>();

    private readonly Vector2[] routeDestinationsGeo = new Vector2[]
    {
        new Vector2(49.883399f, 19.230737f), // Meta Trasa 1
        new Vector2(49.881367f, 19.202881f), // Meta Trasa 2
        new Vector2(49.859609f, 19.229037f)  // Meta Trasa 3
    };

    private readonly Vector2[] routeCheckpointsGeo = new Vector2[]
    {
        new Vector2(49.883640f, 19.231500f),
        new Vector2(49.881250f, 19.203300f),
        new Vector2(49.860100f, 19.228900f)
    };

    void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        arrivalThresholdMeters = 35f;

        if (filterCanvas != null) filterCanvas.SetActive(false);
        if (finalEndCanvas != null) finalEndCanvas.SetActive(false);
        EnsureEndCanvasGroup();

        if (playerRigTransform == null && Camera.main != null)
        {
            playerRigTransform = Camera.main.transform.root;
        }

        if (worldPoiContainer == null)
        {
            GameObject container = GameObject.Find("World_POI_Container");
            if (container != null) worldPoiContainer = container.transform;
        }
    }

    void Start()
    {
        ShowInitialSetup();
    }

    void Update()
    {
        if (currentStage == ExperimentStage.Driving)
        {
            if (enableLiveGPSMotion)
            {
                UpdateLiveVehicleTracking();
            }

            if (!isArrivalPromptShown)
            {
                CheckDestinationProximity();
            }
        }
    }

    private void SetControllerRayVisible(bool visible)
    {
        if (controllerRayObject != null)
        {
            controllerRayObject.SetActive(visible);
        }
    }

    private void EnsureEndCanvasGroup()
    {
        if (endRouteCanvas != null)
        {
            endRouteCanvasGroup = endRouteCanvas.GetComponent<CanvasGroup>();
            if (endRouteCanvasGroup == null)
            {
                endRouteCanvasGroup = endRouteCanvas.AddComponent<CanvasGroup>();
            }
        }
    }

    public void ShowInitialSetup()
    {
        currentStage = ExperimentStage.InitialSetup;

        SetControllerRayVisible(true);

        if (startSetupCanvas != null) startSetupCanvas.SetActive(true);
        if (modeSelectCanvas != null) modeSelectCanvas.SetActive(false);
        if (endRouteCanvas != null) endRouteCanvas.SetActive(false);
        if (surveyPromptCanvas != null) surveyPromptCanvas.SetActive(false);
        if (finalEndCanvas != null) finalEndCanvas.SetActive(false);
        if (filterCanvas != null) filterCanvas.SetActive(false);
        if (screenlockedHistoryCanvas != null) screenlockedHistoryCanvas.SetActive(false);
        if (gazeActivatedHistoryCanvas != null) gazeActivatedHistoryCanvas.SetActive(false);
    }

    public void ConfirmInitialSetup()
    {
        currentStage = ExperimentStage.SelectMode;
        if (startSetupCanvas != null) startSetupCanvas.SetActive(false);
        ShowModeSelection();
    }

    public void ShowModeSelection()
    {
        currentStage = ExperimentStage.SelectMode;

        SetControllerRayVisible(true);

        if (modeSelectCanvas != null) modeSelectCanvas.SetActive(true);
        if (endRouteCanvas != null) endRouteCanvas.SetActive(false);
        if (surveyPromptCanvas != null) surveyPromptCanvas.SetActive(false);
        if (finalEndCanvas != null) finalEndCanvas.SetActive(false);
        if (filterCanvas != null) filterCanvas.SetActive(false);
        if (screenlockedHistoryCanvas != null) screenlockedHistoryCanvas.SetActive(false);
        if (gazeActivatedHistoryCanvas != null) gazeActivatedHistoryCanvas.SetActive(false);

        if (stageInfoText != null)
        {
            string routeDesc = currentRouteIndex switch
            {
                1 => "ETAP 1 Z 3: WYBÓR WARIANTU INTERFEJSU (BP -> Pajda)",
                2 => "ETAP 2 Z 3: WYBÓR WARIANTU INTERFEJSU (Pajda -> Hejnał)",
                3 => "ETAP 3 Z 3: WYBÓR WARIANTU INTERFEJSU (Hejnał -> BP)",
                _ => "BADANIE UKOŃCZONE"
            };
            stageInfoText.text = routeDesc;
        }
    }

    public void StartSelectedRoute(int modeInt)
    {
        POIDisplayManager.DisplayMode selectedMode = (POIDisplayManager.DisplayMode)modeInt;

        if (POIDisplayManager.Instance != null)
        {
            POIDisplayManager.Instance.currentMode = selectedMode;
        }

        currentStage = ExperimentStage.Driving;
        isArrivalPromptShown = false;
        isCheckpointPassed = false;

        SetControllerRayVisible(false);

        LoadRouteJSON(currentRouteIndex);
        InitialCalibrate();
        UpdateResearchCanvases(selectedMode);

        if (filterCanvas != null)
        {
            filterCanvas.SetActive(selectedMode == POIDisplayManager.DisplayMode.WorldLocked);
        }

        POILabel[] allLabels = FindObjectsByType<POILabel>(FindObjectsInactive.Include);
        foreach (var lbl in allLabels)
        {
            if (lbl != null)
            {
                lbl.gameObject.SetActive(true);
                lbl.CheckVisibilityMode();
            }
        }

        if (POIFilterManager.Instance != null)
        {
            POIFilterManager.Instance.ApplyFilters();
        }

        if (modeSelectCanvas != null) modeSelectCanvas.SetActive(false);
    }

    private void InitialCalibrate()
    {
        if (worldPoiContainer != null)
        {
            worldPoiContainer.position = Vector3.zero;
            worldPoiContainer.rotation = Quaternion.identity;
        }

        if (playerRigTransform == null && Camera.main != null)
        {
            playerRigTransform = Camera.main.transform.root;
        }

        float initialRoadHeading = 0f;
        if (routeWaypointsMeters.Count >= 2)
        {
            Vector2 dir = routeWaypointsMeters[1] - routeWaypointsMeters[0];
            initialRoadHeading = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            if (initialRoadHeading < 0) initialRoadHeading += 360f;
        }

        targetRigRotation = Quaternion.Euler(0f, initialRoadHeading, 0f);

        if (GPSTelemetryReceiver.Instance != null && GPSTelemetryReceiver.Instance.hasValidFix && activeRouteData != null)
        {
            double phoneLat = GPSTelemetryReceiver.Instance.currentLat;
            double phoneLon = GPSTelemetryReceiver.Instance.currentLon;

            double latDiff = phoneLat - activeRouteData.originLat;
            double lonDiff = phoneLon - activeRouteData.originLon;

            float startX = (float)(lonDiff * 111320.0 * Math.Cos(activeRouteData.originLat * Math.PI / 180.0));
            float startZ = (float)(latDiff * 110540.0);

            targetRigPosition = new Vector3(startX, 0f, startZ);
        }
        else
        {
            targetRigPosition = Vector3.zero;
        }

        if (playerRigTransform != null)
        {
            playerRigTransform.position = targetRigPosition;
            playerRigTransform.rotation = targetRigRotation;
        }

        isCalibrated = true;
    }

    private void UpdateLiveVehicleTracking()
    {
        if (!isCalibrated || playerRigTransform == null || activeRouteData == null) return;
        if (GPSTelemetryReceiver.Instance == null || !GPSTelemetryReceiver.Instance.hasValidFix) return;

        double phoneLat = GPSTelemetryReceiver.Instance.currentLat;
        double phoneLon = GPSTelemetryReceiver.Instance.currentLon;

        double latDiff = phoneLat - activeRouteData.originLat;
        double lonDiff = phoneLon - activeRouteData.originLon;

        float currentCarMetersX = (float)(lonDiff * 111320.0 * Math.Cos(activeRouteData.originLat * Math.PI / 180.0));
        float currentCarMetersZ = (float)(latDiff * 110540.0);
        Vector2 carPos2D = new Vector2(currentCarMetersX, currentCarMetersZ);

        if (routeWaypointsMeters.Count >= 2)
        {
            float targetRoadHeading = GetRoadHeadingAtPosition(carPos2D);
            targetRigRotation = Quaternion.Euler(0f, targetRoadHeading, 0f);

            float angleDifference = Quaternion.Angle(playerRigTransform.rotation, targetRigRotation);
            float adaptiveTurnSpeed = rotationSmoothing * (1.0f + Mathf.Clamp01(angleDifference / 45.0f) * 2.5f);

            playerRigTransform.rotation = Quaternion.Slerp(playerRigTransform.rotation, targetRigRotation, Time.deltaTime * adaptiveTurnSpeed);
        }

        float speedMetersPerSec = GPSTelemetryReceiver.Instance.currentSpeedKmh / 3.6f;
        if (speedMetersPerSec > 0.5f)
        {
            Vector3 forwardStep = playerRigTransform.forward * (speedMetersPerSec * Time.deltaTime);
            targetRigPosition += forwardStep;
        }

        Vector3 rawGpsTarget = new Vector3(currentCarMetersX, 0f, currentCarMetersZ);
        targetRigPosition = Vector3.Lerp(targetRigPosition, rawGpsTarget, Time.deltaTime * 3.0f);
        playerRigTransform.position = Vector3.Lerp(playerRigTransform.position, targetRigPosition, Time.deltaTime * motionSmoothing);
    }

    private float GetRoadHeadingAtPosition(Vector2 carPos)
    {
        float minDistance = float.MaxValue;
        int bestSegmentIndex = 0;

        for (int i = 0; i < routeWaypointsMeters.Count - 1; i++)
        {
            Vector2 p1 = routeWaypointsMeters[i];
            Vector2 p2 = routeWaypointsMeters[i + 1];

            float dist = DistancePointToSegment(carPos, p1, p2);
            if (dist < minDistance)
            {
                minDistance = dist;
                bestSegmentIndex = i;
            }
        }

        Vector2 segDir = routeWaypointsMeters[bestSegmentIndex + 1] - routeWaypointsMeters[bestSegmentIndex];
        if (segDir.sqrMagnitude < 0.0001f) return 0f;

        float heading = Mathf.Atan2(segDir.x, segDir.y) * Mathf.Rad2Deg;
        if (heading < 0) heading += 360f;
        return heading;
    }

    private float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen < 0.0001f) return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection);
    }

    private void UpdateResearchCanvases(POIDisplayManager.DisplayMode mode)
    {
        if (POIDisplayManager.Instance != null)
        {
            if (POIDisplayManager.Instance.screenLockedCanvas != null)
                POIDisplayManager.Instance.screenLockedCanvas.SetActive(mode == POIDisplayManager.DisplayMode.ScreenLocked);

            if (POIDisplayManager.Instance.gazeActivatedCanvas != null)
                POIDisplayManager.Instance.gazeActivatedCanvas.SetActive(mode == POIDisplayManager.DisplayMode.GazeActivated);

            if (POIDisplayManager.Instance.worldSpaceArrow != null)
                POIDisplayManager.Instance.worldSpaceArrow.SetActive(mode == POIDisplayManager.DisplayMode.WorldLocked);
        }

        bool isSL = (mode == POIDisplayManager.DisplayMode.ScreenLocked);
        if (screenlockedHistoryCanvas != null)
        {
            screenlockedHistoryCanvas.SetActive(isSL);
        }

        bool isGA = (mode == POIDisplayManager.DisplayMode.GazeActivated);
        if (gazeActivatedHistoryCanvas != null)
        {
            gazeActivatedHistoryCanvas.SetActive(isGA);
        }

        POIHistoryPanel[] allHistoryPanels = FindObjectsByType<POIHistoryPanel>(FindObjectsInactive.Include);
        foreach (var hp in allHistoryPanels)
        {
            if (hp == null) continue;

            bool belongsToSL = screenlockedHistoryCanvas != null && hp.transform.IsChildOf(screenlockedHistoryCanvas.transform);
            bool belongsToGA = gazeActivatedHistoryCanvas != null && hp.transform.IsChildOf(gazeActivatedHistoryCanvas.transform);

            if ((isSL && belongsToSL) || (isGA && belongsToGA))
            {
                hp.gameObject.SetActive(true);
                if (hp.historyPanel != null) hp.historyPanel.SetActive(true);
            }
        }
    }

    private void CheckDestinationProximity()
    {
        if (playerRigTransform == null) return;

        Vector2 currentPos2D = new Vector2(playerRigTransform.position.x, playerRigTransform.position.z);

        if (!isCheckpointPassed)
        {
            float distToCheckpoint = Vector2.Distance(currentPos2D, checkpointMeters);
            if (distToCheckpoint <= 60f)
            {
                isCheckpointPassed = true;
            }
        }

        float distToFinish = Vector2.Distance(currentPos2D, targetDestinationMeters);
        if (distToFinish <= arrivalThresholdMeters && isCheckpointPassed)
        {
            TriggerArrival();
        }
    }

    private void TriggerArrival()
    {
        isArrivalPromptShown = true;
        currentStage = ExperimentStage.RouteFinished;

        // 1. Natychmiastowe przywrócenie promienia kontrolera, by umożliwić kliknięcie modalu
        SetControllerRayVisible(true);

        // 2. Fizyczne wyczyszczenie punktów ze świata
        ClearCurrentPOIs();

        // 3. Wyczyszczenie i ukrycie historii
        POIHistoryPanel[] allHistoryPanels = FindObjectsByType<POIHistoryPanel>(FindObjectsInactive.Include);
        foreach (var hp in allHistoryPanels)
        {
            if (hp != null)
            {
                hp.ClearView();
                if (hp.historyPanel != null) hp.historyPanel.SetActive(false);
                hp.gameObject.SetActive(false);
            }
        }

        if (screenlockedHistoryCanvas != null) screenlockedHistoryCanvas.SetActive(false);
        if (gazeActivatedHistoryCanvas != null) gazeActivatedHistoryCanvas.SetActive(false);

        // 4. Wygaszenie HUD-ów badawczych i reset celownika
        if (filterCanvas != null) filterCanvas.SetActive(false);
        if (POIDisplayManager.Instance != null)
        {
            POIDisplayManager.Instance.HideAllResearchUI();
        }

        GazePOIPointer gazePointer = FindAnyObjectByType<GazePOIPointer>();
        if (gazePointer != null)
        {
            gazePointer.ResetAllStates();
        }

        // 5. Zrzut bufora CSV
        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.FlushBuffer();
        }

        // 6. Animacja modalu mety
        if (endRouteCanvas != null)
        {
            if (endRouteAnimationCoroutine != null) StopCoroutine(endRouteAnimationCoroutine);
            endRouteAnimationCoroutine = StartCoroutine(AnimateEndRouteArrival());
        }
    }

    private IEnumerator AnimateEndRouteArrival()
    {
        EnsureEndCanvasGroup();
        endRouteCanvas.SetActive(true);

        RectTransform rt = endRouteCanvas.GetComponent<RectTransform>();
        float duration = 0.5f;
        float elapsed = 0f;

        if (endRouteCanvasGroup != null) endRouteCanvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (endRouteCanvasGroup != null)
                endRouteCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);

            if (rt != null)
            {
                float scale = Mathf.Lerp(0.85f, 1.0f, Mathf.Sin(t * Mathf.PI * 0.5f));
                rt.localScale = new Vector3(scale, scale, 1f);
            }

            yield return null;
        }

        if (endRouteCanvasGroup != null) endRouteCanvasGroup.alpha = 1f;
        if (rt != null) rt.localScale = Vector3.one;
        endRouteAnimationCoroutine = null;
    }

    public void OnFinishRouteButtonClicked()
    {
        if (endRouteCanvas != null) endRouteCanvas.SetActive(false);

        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.LogEvent($"ROUTE_COMPLETED;Route_{currentRouteIndex}");
            UXResearchLogger.Instance.LogEvent($"SURVEY_START;Route_{currentRouteIndex}");
            UXResearchLogger.Instance.FlushBuffer();
        }

        SetControllerRayVisible(true);

        if (currentRouteIndex < 3)
        {
            if (surveyPromptCanvas != null) surveyPromptCanvas.SetActive(true);
        }
        else
        {
            ShowFinalEndScreen();
        }
    }

    private void ShowFinalEndScreen()
    {
        if (finalEndCanvas == null) return;

        SetControllerRayVisible(true);
        finalEndCanvas.SetActive(true);

        if (finalResetButton != null)
        {
            finalResetButton.onClick.RemoveAllListeners();
            finalResetButton.onClick.AddListener(OnFinalResetClicked);
        }

        if (finalQuitButton != null)
        {
            finalQuitButton.onClick.RemoveAllListeners();
            finalQuitButton.onClick.AddListener(OnFinalQuitClicked);
        }
    }

    private void OnFinalResetClicked()
    {
        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.LogEvent($"EXPERIMENT_FINALIZED;Participant_{participantId}");
            UXResearchLogger.Instance.FlushBuffer();
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnFinalQuitClicked()
    {
        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.LogEvent($"APPLICATION_QUIT;Participant_{participantId}");
            UXResearchLogger.Instance.FlushBuffer();
        }
        Debug.Log("[ExperimentManager] Zamykanie aplikacji po zatwierdzeniu zapisu logów.");
        Application.Quit();
    }

    public void OnSurveyCompletedButtonClicked()
    {
        if (surveyPromptCanvas != null) surveyPromptCanvas.SetActive(false);

        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.LogEvent($"SURVEY_END;Route_{currentRouteIndex}");
            UXResearchLogger.Instance.FlushBuffer();
        }

        currentRouteIndex++;
        ShowModeSelection();
    }

    public void CompleteCurrentRoute()
    {
        OnFinishRouteButtonClicked();
    }

    private void LoadRouteJSON(int routeNum)
    {
        ClearCurrentPOIs();

        TextAsset jsonAsset = Resources.Load<TextAsset>($"Routes/Route_{routeNum}");
        if (jsonAsset == null)
        {
            Debug.LogError($"[ExperimentManager] Nie znaleziono pliku Resources/Routes/Route_{routeNum}.json!");
            return;
        }

        activeRouteData = JsonUtility.FromJson<POITrackContainer>(jsonAsset.text);
        if (activeRouteData == null) return;

        routeWaypointsMeters.Clear();
        if (activeRouteData.waypoints != null)
        {
            double cosLat = Math.Cos(activeRouteData.originLat * Math.PI / 180.0);
            foreach (var wp in activeRouteData.waypoints)
            {
                float x = (float)((wp.lon - activeRouteData.originLon) * 111320.0 * cosLat);
                float z = (float)((wp.lat - activeRouteData.originLat) * 110540.0);
                routeWaypointsMeters.Add(new Vector2(x, z));
            }
        }

        int idx = Mathf.Clamp(routeNum - 1, 0, routeDestinationsGeo.Length - 1);

        Vector2 finishGeo = routeDestinationsGeo[idx];
        float destX = (float)((finishGeo.y - activeRouteData.originLon) * 111320.0 * Math.Cos(activeRouteData.originLat * Math.PI / 180.0));
        float destZ = (float)((finishGeo.x - activeRouteData.originLat) * 110540.0);
        targetDestinationMeters = new Vector2(destX, destZ);

        Vector2 checkGeo = routeCheckpointsGeo[idx];
        float checkX = (float)((checkGeo.y - activeRouteData.originLon) * 111320.0 * Math.Cos(activeRouteData.originLat * Math.PI / 180.0));
        float checkZ = (float)((checkGeo.x - activeRouteData.originLat) * 110540.0);
        checkpointMeters = new Vector2(checkX, checkZ);

        if (POIRouteLoader.Instance != null)
        {
            POIRouteLoader.Instance.SpawnRoutePOIs(activeRouteData);
        }
    }

    private void ClearCurrentPOIs()
    {
        if (POIRouteLoader.Instance != null)
        {
            POIRouteLoader.Instance.ClearAllPOIs();
        }
    }

#if UNITY_EDITOR
    // --- ETAP 1 (SL: Screen-Locked) ---
    [ContextMenu("TEST: 01. Start ETAP 1 (SL - ScreenLocked)")]
    public void DebugTest01_StartSL()
    {
        ConfirmInitialSetup();
        StartSelectedRoute((int)POIDisplayManager.DisplayMode.ScreenLocked);
    }

    [ContextMenu("TEST: 02. Meta ETAPU 1 (Dojechanie)")]
    public void DebugTest02_MetaEtap1()
    {
        TriggerArrival();
    }

    [ContextMenu("TEST: 03. Kliknij Zakoncz Odcinek 1")]
    public void DebugTest03_FinishOdcinek1()
    {
        OnFinishRouteButtonClicked();
    }

    [ContextMenu("TEST: 04. Ankieta 1 Zrobiona -> Przejdz Dalej")]
    public void DebugTest04_NextOdcinek2()
    {
        OnSurveyCompletedButtonClicked();
    }

    // --- ETAP 2 (WL: World-Locked) ---
    [ContextMenu("TEST: 05. Start ETAP 2 (WL - WorldLocked)")]
    public void DebugTest05_StartWL()
    {
        StartSelectedRoute((int)POIDisplayManager.DisplayMode.WorldLocked);
    }

    [ContextMenu("TEST: 06. Meta ETAPU 2 (Dojechanie)")]
    public void DebugTest06_MetaEtap2()
    {
        TriggerArrival();
    }

    [ContextMenu("TEST: 07. Kliknij Zakoncz Odcinek 2")]
    public void DebugTest07_FinishOdcinek2()
    {
        OnFinishRouteButtonClicked();
    }

    [ContextMenu("TEST: 08. Ankieta 2 Zrobiona -> Przejdz Dalej")]
    public void DebugTest08_NextOdcinek3()
    {
        OnSurveyCompletedButtonClicked();
    }

    // --- ETAP 3 (GA: Gaze-Activated - FINAŁ) ---
    [ContextMenu("TEST: 09. Start ETAP 3 (GA - GazeActivated)")]
    public void DebugTest09_StartGA()
    {
        StartSelectedRoute((int)POIDisplayManager.DisplayMode.GazeActivated);
    }

    [ContextMenu("TEST: 10. Meta ETAPU 3 (Finał - Dojechanie)")]
    public void DebugTest10_MetaEtap3()
    {
        TriggerArrival();
    }

    [ContextMenu("TEST: 11. Kliknij Zakoncz Odcinek 3 (Pokaz Final)")]
    public void DebugTest11_FinishOdcinek3()
    {
        OnFinishRouteButtonClicked();
    }

    [ContextMenu("TEST: 12. Kliknij Wyjscie z Aplikacji (FinalQuit)")]
    public void DebugTest12_FinalQuit()
    {
        finalQuitButton.onClick.Invoke();
    }
#endif
}