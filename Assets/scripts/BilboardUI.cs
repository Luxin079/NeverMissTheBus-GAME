using UnityEngine;

[DefaultExecutionOrder(1000)]
public class BillboardUI : MonoBehaviour
{
    public Transform player;

    void LateUpdate()
    {
        // Face the readable side of the world-space Canvas toward the viewer.
        Camera activeCamera = Camera.main;
        if (activeCamera == null || !activeCamera.isActiveAndEnabled)
            foreach (Camera candidate in Camera.allCameras)
                if (candidate.isActiveAndEnabled) { activeCamera = candidate; break; }
        Transform viewer = activeCamera != null && activeCamera.isActiveAndEnabled ? activeCamera.transform : player;
        if (viewer == null) return;
        // Canvas text is readable from local -Z, unlike a LookAt target (+Z).
        Vector3 direction = transform.position - viewer.position;

        // Alleen draaien op de Y-as zodat hij recht blijft
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
