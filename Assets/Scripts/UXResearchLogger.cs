using System.IO;
using System.Text;
using UnityEngine;

public class UXResearchLogger : MonoBehaviour
{
    public static UXResearchLogger Instance;

    [Header("Participant Settings")]
    [Tooltip("Identyfikator badanego (np. P01, P02)")]
    public string participantID = "P01";

    [Header("File Settings")]
    public string fileName = "UX_Research_Log.csv";

    private string filePath;
    private StringBuilder logBuffer = new StringBuilder();
    private float modeStartTime;
    private string currentMode = "WorldLocked";
    private bool isLogging = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(filePath))
        {
            // Nagłówek pliku CSV z uwzględnieniem ID badanego
            logBuffer.AppendLine("Timestamp;Participant_ID;Event_Type;Mode;Duration_Seconds");
            FlushBuffer();
        }

        StartLogging();
        Debug.Log($"<color=cyan>[UX Logger]:</color> Plik logów zapisze się w: {filePath}");
    }

    public void StartLogging()
    {
        isLogging = true;
        modeStartTime = Time.time;
        LogEvent($"MODE_START;{currentMode}");
    }

    public void OnModeChanged(string newModeName)
    {
        if (isLogging)
        {
            LogEvent($"MODE_END;{currentMode}");
        }

        currentMode = newModeName;
        modeStartTime = Time.time;
        LogEvent($"MODE_START;{currentMode}");
    }

    public void LogEvent(string eventDescription)
    {
        if (!isLogging) return;

        float duration = Time.time - modeStartTime;
        string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logLine = $"{timeStamp};{participantID};{eventDescription};{currentMode};{duration:F2}";

        logBuffer.AppendLine(logLine);
        FlushBuffer();
        Debug.Log($"<color=yellow>[UX LOG]:</color> {logLine}");
    }

    public void FlushBuffer()
    {
        try
        {
            if (logBuffer.Length > 0)
            {
                File.AppendAllText(filePath, logBuffer.ToString(), Encoding.UTF8);
                logBuffer.Clear();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UX Logger Error]: Błąd zapisu do CSV: {e.Message}");
        }
    }

    public void LogGazeActivation(string poiName, float dwellTime)
    {
        LogEvent($"GAZE_SELECTION;{poiName};{dwellTime}");
    }

    void OnApplicationQuit()
    {
        if (isLogging)
        {
            LogEvent($"MODE_END;{currentMode}");
            FlushBuffer();
        }
    }
}