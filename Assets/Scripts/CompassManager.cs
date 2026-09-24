using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CompassManager : MonoBehaviour
{
    [Header("References")]
    public Camera userCamera;
    public RawImage compassRawImage;

    [Header("Debug Controls")]
    public float manualRotationSpeed = 45f;

    private float manualAngleOffset = 0f;

    void Update()
    {
        if (userCamera == null || compassRawImage == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Emulacja klawiszami J / L w nowym Input System
            if (keyboard.jKey.isPressed)
            {
                manualAngleOffset -= manualRotationSpeed * Time.deltaTime;
            }
            if (keyboard.lKey.isPressed)
            {
                manualAngleOffset += manualRotationSpeed * Time.deltaTime;
            }
        }

        // Pobieramy kąt obrotu kamery w osi Y
        float cameraYaw = userCamera.transform.eulerAngles.y + manualAngleOffset;

        // Mapowanie kąta (0-360) na współrzędne UV paska kompasu (0-1)
        float uvX = (cameraYaw / 360f) % 1f;
        if (uvX < 0) uvX += 1f;

        compassRawImage.uvRect = new Rect(uvX, 0f, 1f, 1f);
    }
}