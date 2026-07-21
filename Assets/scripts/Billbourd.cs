using UnityEngine;

public class YBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        Vector3 camPos = Camera.main.transform.position;
        camPos.y = transform.position.y;

        transform.LookAt(camPos);
    }
}