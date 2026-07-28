using UnityEngine;

public class PickupTrigger : MonoBehaviour
{
    private PickUp pickUp;

    private void Awake()
    {
        pickUp = GetComponentInParent<PickUp>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        pickUp.SetPlayerInRange(true, other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        pickUp.SetPlayerInRange(false, null);
    }
}