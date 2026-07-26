using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    public Transform player;

    void LateUpdate()
    {
        if (player == null) return;

        // Richt naar speler
        Vector3 direction = transform.position - player.position;

        // Alleen draaien op de Y-as zodat hij recht blijft
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}