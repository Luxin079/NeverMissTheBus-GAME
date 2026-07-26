using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Item")]
    [Tooltip("The tag of this specific item (must match the tag in the InventorySystem mappings).")]
    public string itemTag;

    [Header("UI")]
    [Tooltip("Sleep hier het volledige Prompt GameObject in (met Canvas, Text, etc.).")]
    public PromptFade prompt;

    [Header("Audio")]
    [Tooltip("Sleep hier het geluidje in dat je direct wilt horen als je dit voorwerp oppakt.")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    private bool playerInRange = false;

    private InventorySystem inventory;
    private PlayerAnimations animationController;

    private void Update()
    {
        if (!playerInRange)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            PickUpItem();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;

        inventory = other.GetComponent<InventorySystem>();
        animationController = other.GetComponentInChildren<PlayerAnimations>();

        if (prompt != null)
            prompt.Show();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;

        if (prompt != null)
            prompt.Hide();
    }

    private void PickUpItem()
    {
        if (inventory == null)
        {
            Debug.LogWarning("Player does not have an InventorySystem attached!");
            return;
        }

        Debug.Log($"Picked up '{itemTag}'");

        // Speel pickup geluid af
        SoundManager.Instance?.PlayAtPoint(pickupSound, transform.position, pickupVolume);

        // Voeg toe aan inventory
        inventory.AddItem(itemTag);

        // Speel pickup animatie af
        if (animationController != null)
        {
            animationController.PlayPickupAnimation();
        }

        // Verberg prompt
        if (prompt != null)
            prompt.Hide();

        // Verwijder item uit de wereld
        Destroy(gameObject);
    }
}