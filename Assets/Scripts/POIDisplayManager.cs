using UnityEngine;
using UnityEngine.InputSystem;

public class POIDisplayManager : MonoBehaviour
{
    public enum DisplayMode
    {
        WorldLocked,
        ScreenLocked,
        GazeActivated
    }

    public static POIDisplayManager Instance;

    [Header("Current Research Condition")]
    public DisplayMode currentMode = DisplayMode.WorldLocked;

    [Header("UI Containers for Research Variants")]
    [Tooltip("Zaznacz, jeśli chcesz wyświetlać niebieską strzałkę 3D w trybie World-Locked")]
    public bool showWorldSpaceArrow = false; // DOMYŚLNIE WYŁĄCZONA
    public GameObject worldSpaceArrow;
    public GameObject screenLockedCanvas;
    public GameObject gazeActivatedCanvas;
    public GameObject filterCanvas; // Dodany panel filtrów

    [Header("References for Recenter")]
    public Transform cameraTransform;
    public Transform xrOriginTransform;

    private UXResearchLogger logger;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        logger = GetComponent<UXResearchLogger>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Ukrywamy elementy na czas menu startowego
        HideAllResearchUI();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            SetDisplayMode(DisplayMode.WorldLocked);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            SetDisplayMode(DisplayMode.ScreenLocked);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            SetDisplayMode(DisplayMode.GazeActivated);
        }

        if (keyboard.rKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            RecenterView();
        }
    }

    public void HideAllResearchUI()
    {
        if (worldSpaceArrow != null) worldSpaceArrow.SetActive(false);
        if (screenLockedCanvas != null) screenLockedCanvas.SetActive(false);
        if (gazeActivatedCanvas != null) gazeActivatedCanvas.SetActive(false);
        if (filterCanvas != null) filterCanvas.SetActive(false);
    }

    public void SetDisplayMode(DisplayMode mode)
    {
        currentMode = mode;

        // Strzałka włącza się TYLKO w trybie WorldLocked ORAZ gdy ptaszek showWorldSpaceArrow jest zaznaczony
        if (worldSpaceArrow != null)
        {
            worldSpaceArrow.SetActive(mode == DisplayMode.WorldLocked && showWorldSpaceArrow);
        }

        if (screenLockedCanvas != null) screenLockedCanvas.SetActive(mode == DisplayMode.ScreenLocked);
        if (gazeActivatedCanvas != null) gazeActivatedCanvas.SetActive(mode == DisplayMode.GazeActivated);
        if (filterCanvas != null) filterCanvas.SetActive(true);

        POILabel[] allPOIs = FindObjectsByType<POILabel>(FindObjectsInactive.Include);
        foreach (var poi in allPOIs)
        {
            if (poi != null) poi.CheckVisibilityMode();
        }

        if (logger != null)
        {
            logger.OnModeChanged(mode.ToString());
        }

        Debug.Log($"<color=purple>[Research Mode]:</color> Zmieniono tryb wyświetlania na: <b>{mode}</b>");
    }

    public void RecenterView()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform == null) return;

        float currentYaw = cameraTransform.eulerAngles.y;

        if (xrOriginTransform != null)
        {
            xrOriginTransform.Rotate(0f, -currentYaw, 0f, Space.World);
            Debug.Log("<color=yellow>[Recenter]:</color> Zresetowano orientację widoku użytkownika.");
        }
        else
        {
            Debug.LogWarning("[Recenter]: Brak przypisanego XR Origin Transform w Inspectorze.");
        }
    }
}