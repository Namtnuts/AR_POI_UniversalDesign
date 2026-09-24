using UnityEngine;
using UnityEngine.InputSystem;

public class EditorCameraMovementHelper : MonoBehaviour
{
    [Header("Settings")]
    public Transform xrOriginTransform;
    public Camera mainCamera;

    [Tooltip("Prędkość obrotu głową w stopniach na sekundę")]
    public float fineTurnSpeed = 100.0f; // Zwiększona prędkość dla dynamiczniejszego obrotu

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (xrOriginTransform == null && mainCamera != null)
        {
            xrOriginTransform = mainCamera.transform.root;
        }
    }

    void Update()
    {
        if (xrOriginTransform == null || mainCamera == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Klawisz U - obrót w LEWO (dodatni zwrot)
        if (keyboard.uKey.isPressed)
        {
            xrOriginTransform.RotateAround(
                mainCamera.transform.position,
                Vector3.up,
                fineTurnSpeed * Time.deltaTime
            );
        }

        // Klawisz O - obrót w PRAWO (ujemny zwrot)
        if (keyboard.oKey.isPressed)
        {
            xrOriginTransform.RotateAround(
                mainCamera.transform.position,
                Vector3.up,
                -fineTurnSpeed * Time.deltaTime
            );
        }
    }
}