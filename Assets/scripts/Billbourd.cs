using UnityEngine;

public class YBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return;
        Vector3 camPos = activeCamera.transform.position;
        camPos.y = transform.position.y;

        if ((camPos - transform.position).sqrMagnitude > 0.0001f)
            transform.LookAt(camPos);
    }
}
