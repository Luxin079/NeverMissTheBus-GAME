using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Item")]
    public string itemTag;

    [Header("Audio")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    private bool playerInRange;

    private InventorySystem inventory;
    private PlayerAnimations animationController;

    public void SetPlayerInRange(bool value, GameObject player)
    {
        playerInRange = value;

        if (value)
        {
            inventory = player.GetComponent<InventorySystem>();
            animationController = player.GetComponentInChildren<PlayerAnimations>();
        }
    }

    private void Update()
    {
        if (!playerInRange)
            return;

        if (Input.GetKeyDown(KeyCode.E))
            PickUpItem();
    }

    private void PickUpItem()
    {
        if (inventory == null)
            return;

        SoundManager.Instance?.PlayAtPoint(pickupSound, transform.position, pickupVolume);

        inventory.AddItem(itemTag);

        if (animationController != null)
            animationController.PlayPickupAnimation();

        Destroy(gameObject);
    }
}