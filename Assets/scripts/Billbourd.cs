using UnityEngine;

[DefaultExecutionOrder(900)]
public class YBillboard : MonoBehaviour
{
    [Tooltip("Enable only for sprites whose front faces local -Z. Existing scene sprites retain their original +Z facing.")]
    public bool frontFacesNegativeZ = false;
    void LateUpdate()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null || !activeCamera.isActiveAndEnabled)
            foreach (Camera candidate in Camera.allCameras)
                if (candidate.isActiveAndEnabled) { activeCamera = candidate; break; }
        if (activeCamera == null || !activeCamera.isActiveAndEnabled) return;
        Vector3 camPos = activeCamera.transform.position;
        camPos.y = transform.position.y;

        if ((camPos - transform.position).sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(
                frontFacesNegativeZ ? transform.position - camPos : camPos - transform.position,
                Vector3.up);
    }
}
