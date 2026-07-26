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
        // Check of de speler het voorwerp aanraakt
        if (!other.CompareTag("Player"))
            return;

        InventorySystem inventory = other.GetComponent<InventorySystem>();

        if (inventory == null)
        {
            Debug.LogWarning("Player does not have an InventorySystem script attached!");
            return;
        }

        Debug.Log($"Item '{itemTag}' opgepakt!");

        // Speel pickup geluid af
        SoundManager.Instance?.PlayAtPoint(pickupSound, transform.position, pickupVolume);

        // Voeg item toe aan inventory
        inventory.AddItem(itemTag);

        // Speel pickup animatie af
        PlayerAnimations animationController =
            other.GetComponentInChildren<PlayerAnimations>();

        if (animationController != null)
        {
            animationController.PlayPickupAnimation();
        }
        else
        {
            Debug.LogWarning("No PlayerAnimations found on the player.");
        }

        // Verwijder het item uit de wereld
        Destroy(gameObject);
    }
}