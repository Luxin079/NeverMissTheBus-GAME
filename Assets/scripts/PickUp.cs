using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Tooltip("The tag of this specific item (must match the tag in the InventorySystem mappings).")]
    public string itemTag;

    [Header("Audio")]
    [Tooltip("Sleep hier het geluidje in dat je direct wilt horen als je dit voorwerp oppakt.")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    private void OnTriggerEnter(Collider other)
    {
        // Check of het de speler is die het voorwerp aanraakt
        if (other.CompareTag("Player"))
        {
            InventorySystem inventory = other.GetComponent<InventorySystem>();

            if (inventory != null)
            {
                Debug.Log($"Item '{itemTag}' opgepakt! Geluid wordt nu direct afgespeeld.");

                // Speelt direct het geluid af op de 3D positie van dit voorwerp
                SoundManager.Instance?.PlayAtPoint(pickupSound, transform.position, pickupVolume);

                // Voeg toe aan inventory en verwijder uit de wereld
                inventory.AddItem(itemTag);
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("Player does not have an InventorySystem script attached!");
            }
        }
    }
}