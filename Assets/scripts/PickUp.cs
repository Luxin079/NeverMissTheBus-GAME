using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Item")]
    public string itemTag;

    [Header("Audio")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    [Header("Pickup Settings")]
    [Tooltip("Maximum distance (meters) from which the player can pick up this item.")]
    [Range(0.5f, 10f)]
    public float pickupRange = 3f;

    private bool playerInRange;
    private GameObject player;
    private float sqrPickupRange;

    private InventorySystem inventory;
    private PlayerAnimations animationController;

    private void Awake()
    {
        sqrPickupRange = pickupRange * pickupRange;

        // Try to find player by tag once at startup. If not found we'll try again in Update.
        player = GameObject.FindWithTag("Player");
        if (player == null)
            Debug.LogWarning($"{name}: No GameObject with tag 'Player' found at Awake. The pickup range will be checked each frame until a player is found.");
    }

    /// <summary>
    /// Backwards-compatible method if other scripts call this.
    /// </summary>
    public void SetPlayerInRange(bool value, GameObject playerObj)
    {
        playerInRange = value;

        if (value)
        {
            player = playerObj;
            inventory = player.GetComponent<InventorySystem>();
            animationController = player.GetComponentInChildren<PlayerAnimations>();
        }
        else
        {
            inventory = null;
            animationController = null;
        }
    }

    private void Update()
    {
        // If we don't have a player reference yet, try to find it.
        if (player == null)
            player = GameObject.FindWithTag("Player");

        // If still no player, nothing to do.
        if (player == null)
            return;

        // Update squared range in case user changes pickupRange in inspector at runtime.
        sqrPickupRange = pickupRange * pickupRange;

        // Distance check (squared for a small perf win)
        float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
        bool nowInRange = sqrDistance <= sqrPickupRange;

        if (nowInRange && !playerInRange)
        {
            // Enter range
            playerInRange = true;
            inventory = player.GetComponent<InventorySystem>();
            animationController = player.GetComponentInChildren<PlayerAnimations>();
        }
        else if (!nowInRange && playerInRange)
        {
            // Exit range
            playerInRange = false;
            inventory = null;
            animationController = null;
        }

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

    // Draw pickup range in the Scene view for easier tuning
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}