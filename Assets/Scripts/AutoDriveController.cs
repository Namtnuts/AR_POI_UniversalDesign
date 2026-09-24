using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class AutoDriveController : MonoBehaviour
{
    public static AutoDriveController Instance;

    [Header("Movement Settings")]
    public Transform targetToMove;
    public float speedMetersPerSec = 13.0f;
    public float totalDriveDistance = 300f;

    [Header("UI Canvases")]
    public GameObject startMenuCanvas;
    public GameObject driveEndCanvas;
    public TMP_Text statusText;

    private Vector3 startPosition;
    private bool isDriveActive = false;
    private bool isPaused = false;
    private float distanceTraveled = 0f;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (targetToMove == null)
        {
            if (Camera.main != null)
            {
                targetToMove = Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform;
            }
        }

        if (targetToMove != null)
        {
            startPosition = targetToMove.position;
        }

        ShowStartMenu();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.pKey.wasPressedThisFrame && isDriveActive)
        {
            TogglePause();
        }

        if (!isDriveActive || isPaused || targetToMove == null) return;

        float moveStep = speedMetersPerSec * Time.deltaTime;
        targetToMove.Translate(Vector3.forward * moveStep, Space.World);

        distanceTraveled = Vector3.Distance(startPosition, targetToMove.position);

        if (statusText != null)
        {
            statusText.text = $"Dystans: {distanceTraveled:F0}m / {totalDriveDistance:F0}m\n" +
                              $"{(isPaused ? "<color=red>[PAUZA - naciśnij P]</color>" : "<color=green>[JAZDA - naciśnij P aby zatrzymać]</color>")}";
        }

        if (distanceTraveled >= totalDriveDistance)
        {
            FinishAutoDrive();
        }
    }

    public void StartFreeMode()
    {
        isDriveActive = false;
        isPaused = false;

        if (startMenuCanvas != null) startMenuCanvas.SetActive(false);
        if (driveEndCanvas != null) driveEndCanvas.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);

        if (POIDisplayManager.Instance != null)
        {
            POIDisplayManager.Instance.SetDisplayMode(POIDisplayManager.Instance.currentMode);
        }

        Debug.Log("<color=cyan>[AutoDrive]:</color> Aktywowano Tryb Wolny.");
    }

    public void StartAutoDriveMode()
    {
        ResetToStartPoint();

        distanceTraveled = 0f;
        isDriveActive = true;
        isPaused = false;

        if (startMenuCanvas != null) startMenuCanvas.SetActive(false);
        if (driveEndCanvas != null) driveEndCanvas.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(true);

        if (POIDisplayManager.Instance != null)
        {
            POIDisplayManager.Instance.SetDisplayMode(POIDisplayManager.Instance.currentMode);
        }

        Debug.Log("<color=green>[AutoDrive]:</color> Uruchomiono Autonomiczny Przejazd (300m).");
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Debug.Log($"<color=yellow>[AutoDrive]:</color> Stan pauzy: {isPaused}");
    }

    private void FinishAutoDrive()
    {
        isDriveActive = false;

        if (driveEndCanvas != null) driveEndCanvas.SetActive(true);
        if (statusText != null) statusText.gameObject.SetActive(false);

        if (POIDisplayManager.Instance != null)
        {
            POIDisplayManager.Instance.HideAllResearchUI();
        }

        Debug.Log("<color=purple>[AutoDrive]:</color> Przejazd zakończony.");
    }

    // Nowa, czysta metoda powrotu do menu bez przeładowywania całej sceny
    public void ReturnToMenu()
    {
        ResetToStartPoint();
        ShowStartMenu();
    }

    private void ResetToStartPoint()
    {
        if (targetToMove != null)
        {
            targetToMove.position = startPosition;
        }
        distanceTraveled = 0f;
    }

    public void ShowStartMenu()
    {
        isDriveActive = false;
        isPaused = false;

        if (startMenuCanvas != null) startMenuCanvas.SetActive(true);
        if (driveEndCanvas != null) driveEndCanvas.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);

        if (POIDisplayManager.Instance != null)
        {
            POIDisplayManager.Instance.HideAllResearchUI();
        }
    }
}