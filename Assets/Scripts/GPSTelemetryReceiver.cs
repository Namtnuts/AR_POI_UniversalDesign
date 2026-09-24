using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

public class GPSTelemetryReceiver : MonoBehaviour
{
    public static GPSTelemetryReceiver Instance { get; private set; }

    [Header("Server Settings")]
    public int listenPort = 8080;

    [Header("Live Telemetry Data")]
    public double currentLat = 0.0;
    public double currentLon = 0.0;
    public float currentHeading = 0f;
    public float currentSpeedKmh = 0f;
    public bool hasValidFix = false;
    public int receivedPacketsCount = 0;

    private HttpListener httpListener;
    private Thread listenerThread;
    private bool isRunning = true;
    private readonly object dataLock = new object();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        StartHttpServer();
    }

    private void StartHttpServer()
    {
        try
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://*:{listenPort}/data/");
            httpListener.Prefixes.Add($"http://*:{listenPort}/");
            httpListener.Start();

            listenerThread = new Thread(ListenLoop) { IsBackground = true };
            listenerThread.Start();
            Debug.Log($"<color=cyan>[Telemetry Receiver]:</color> Serwer HTTP nasluchuje na porcie {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry Server Error]: {e.Message}");
        }
    }

    private void ListenLoop()
    {
        while (isRunning && httpListener != null && httpListener.IsListening)
        {
            try
            {
                var context = httpListener.GetContext();
                using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    string json = reader.ReadToEnd();
                    ParsePayload(json);
                }

                byte[] responseBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry Loop Warning]: {ex.Message}");
            }
        }
    }

    private void ParsePayload(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        lock (dataLock)
        {
            try
            {
                if (json.Contains("\"latitude\"") || json.Contains("\"location\""))
                {
                    ExtractDouble(json, "\"latitude\":", ref currentLat);
                    ExtractDouble(json, "\"longitude\":", ref currentLon);
                    ExtractFloat(json, "\"bearing\":", ref currentHeading);
                    ExtractFloat(json, "\"speed\":", ref currentSpeedKmh);
                    hasValidFix = true;
                    receivedPacketsCount++;
                }

                if (json.Contains("\"magneticHeading\"") || json.Contains("\"compass\""))
                {
                    ExtractFloat(json, "\"magneticHeading\":", ref currentHeading);
                    ExtractFloat(json, "\"heading\":", ref currentHeading);
                }
            }
            catch { }
        }
    }

    private void ExtractDouble(string src, string key, ref double target)
    {
        int idx = src.IndexOf(key);
        if (idx == -1) return;
        idx += key.Length;
        int end = src.IndexOfAny(new char[] { ',', '}', ']' }, idx);
        if (end != -1 && double.TryParse(src.Substring(idx, end - idx).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
            target = val;
    }

    private void ExtractFloat(string src, string key, ref float target)
    {
        int idx = src.IndexOf(key);
        if (idx == -1) return;
        idx += key.Length;
        int end = src.IndexOfAny(new char[] { ',', '}', ']' }, idx);
        if (end != -1 && float.TryParse(src.Substring(idx, end - idx).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
            target = val;
    }

    void OnDestroy()
    {
        isRunning = false;
        if (httpListener != null) { httpListener.Stop(); httpListener.Close(); }
        if (listenerThread != null && listenerThread.IsAlive) listenerThread.Abort();
    }
}