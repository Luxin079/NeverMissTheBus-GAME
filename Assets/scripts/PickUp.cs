using System;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Item")]
    public string itemTag;
    public bool questItem;
    [HideInInspector] public GameObject promptObject; // Legacy field; the item's own UI controls itself.
    [HideInInspector] public Vector3 promptOffset = new Vector3(0, 1.1f, 0);
    [HideInInspector] public Vector3 promptScale = Vector3.one;
    [HideInInspector] public string promptText = "Press E to pick up";
    [Header("Audio")]
    public AudioClip pickupSound;
    [Range(0, 1)] public float pickupVolume = .8f;
    [Header("Pickup settings")]
    [Range(.5f, 10)] public float pickupRange = 3;

    public event Action<PickUp> Collected;
    GameObject player;
    InventorySystem inventory;
    PlayerAnimations animations;
    bool playerInRange;

    public void SetPlayerInRange(bool value, GameObject playerObj)
    {
        player = playerObj; playerInRange = value;
        inventory = value && player != null ? player.GetComponent<InventorySystem>() : null;
        animations = value && player != null ? player.GetComponentInChildren<PlayerAnimations>() : null;
    }

    void Update()
    {
        if (player == null) player = GameObject.FindWithTag("Player");
        bool inRange = player != null && (player.transform.position - transform.position).sqrMagnitude <= pickupRange * pickupRange;
        if (inRange != playerInRange) SetPlayerInRange(inRange, player);
        if (inRange && Input.GetKeyDown(KeyCode.E)) PickUpItem();
    }

    void PickUpItem()
    {
        if (inventory == null) return;
        SoundManager.Instance?.PlayAtPoint(pickupSound, transform.position, pickupVolume);
        if (questItem) inventory.AddQuestItem(itemTag);
        else inventory.AddItem(itemTag);
        if (animations != null) animations.PlayPickupAnimation();
        Collected?.Invoke(this);
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
