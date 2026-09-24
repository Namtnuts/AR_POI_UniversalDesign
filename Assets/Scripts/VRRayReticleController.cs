using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRRayReticleController : MonoBehaviour
{
    [Header("Przypisania")]
    public XRRayInteractor rayInteractor;
    public GameObject reticleVisual; // Obiekt kropki w scenie

    private void Awake()
    {
        if (rayInteractor == null)
            rayInteractor = GetComponent<XRRayInteractor>();

        if (reticleVisual != null)
            reticleVisual.SetActive(false);
    }

    private void Update()
    {
        if (rayInteractor == null || reticleVisual == null) return;

        // Sprawdzamy czy promień w coś trafia (zarówno 3D jak i Canvas UI)
        bool hasHit = rayInteractor.TryGetCurrentUIRaycastResult(out var uiHit) ||
                      rayInteractor.TryGetCurrent3DRaycastHit(out var hit3D);

        if (hasHit)
        {
            reticleVisual.SetActive(true);

            Vector3 hitPoint = uiHit.isValid ? uiHit.worldPosition : rayInteractor.TryGetCurrent3DRaycastHit(out var h) ? h.point : transform.position;
            Vector3 hitNormal = uiHit.isValid ? uiHit.worldNormal : rayInteractor.TryGetCurrent3DRaycastHit(out var n) ? n.normal : Vector3.up;

            reticleVisual.transform.position = hitPoint + (hitNormal * 0.002f); // Lekki offset, by nie przenikała przez Canvas
            reticleVisual.transform.rotation = Quaternion.LookRotation(-hitNormal);
        }
        else
        {
            reticleVisual.SetActive(false);
        }
    }
}