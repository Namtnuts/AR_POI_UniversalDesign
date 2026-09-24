using UnityEngine;

public class ARDirectionArrow : MonoBehaviour
{
    public Transform userCamera;
    public Transform targetPOI;

    [Header("Positioning Settings")]
    public float distanceFromCamera = 1.5f;
    public float heightOffset = -0.5f;

    [Header("Targeting Mode")]
    public bool trackClosestPOI = true;
    [Tooltip("Inny obiekt musi być o tyle metrów bliżej, aby strzałka zmieniła cel.")]
    public float switchThresholdMeters = 3.0f;

    void LateUpdate()
    {
        if (userCamera == null)
        {
            if (Camera.main != null) userCamera = Camera.main.transform;
            else return;
        }

        // Natychmiast porzucamy cel, jeśli został wyłączony przez filtr
        if (targetPOI != null && !targetPOI.gameObject.activeInHierarchy)
        {
            targetPOI = null;
        }

        // 1. Szukanie najbliższego AKTYWNEGO punktu POI z progiem stabilności
        if (trackClosestPOI)
        {
            FindClosestPOI();
        }

        // 2. Pozycja strzałki przed użytkownikiem
        Vector3 desiredPosition = userCamera.position + (userCamera.forward * distanceFromCamera);
        desiredPosition.y = userCamera.position.y + heightOffset;
        transform.position = desiredPosition;

        // 3. Celowanie w wybrany POI
        if (targetPOI != null && targetPOI.gameObject.activeInHierarchy)
        {
            Vector3 direction = targetPOI.position - transform.position;
            direction.y = 0; // Obrót tylko w poziomie

            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private void FindClosestPOI()
    {
        // Zastąpienie przestarzałego ciągu FindObjectsSortMode
        POILabel[] pois = FindObjectsByType<POILabel>(FindObjectsInactive.Exclude);
        if (pois.Length == 0)
        {
            targetPOI = null;
            return;
        }

        // Walidacja obecnego celu
        bool hasValidTarget = targetPOI != null && targetPOI.gameObject.activeInHierarchy;
        float currentTargetDist = hasValidTarget
            ? Vector3.Distance(userCamera.position, targetPOI.position)
            : Mathf.Infinity;

        Transform potentialNewTarget = hasValidTarget ? targetPOI : null;
        float shortestDistance = currentTargetDist;

        foreach (var poi in pois)
        {
            // Ignorujemy obiekty ukryte/odfiltrowane
            if (poi == null || !poi.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(userCamera.position, poi.transform.position);

            // Szukamy nowego celu, gdy nie mamy obecnego LUB gdy inny aktywny POI jest bliżej o switchThresholdMeters
            if (!hasValidTarget || dist < (currentTargetDist - switchThresholdMeters))
            {
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    potentialNewTarget = poi.transform;
                }
            }
        }

        targetPOI = potentialNewTarget;
    }
}