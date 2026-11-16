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
    [Range(5, 60)] public float pickupViewAngle = 35;

    public event Action<PickUp> Collected;
    GameObject player;
    InventorySystem inventory;
    PlayerAnimations animations;
    bool playerInRange;
    bool collected;

    public void SetPlayerInRange(bool value, GameObject playerObj)
    {
        // Triggers may report a child collider rather than the Player root.
        inventory = value && playerObj != null ? playerObj.GetComponentInParent<InventorySystem>() : null;
        if (value && inventory == null)
        {
            var taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null) inventory = taggedPlayer.GetComponent<InventorySystem>();
        }
        player = inventory != null ? inventory.gameObject : playerObj;
        playerInRange = value;
        animations = value && player != null ? player.GetComponentInChildren<PlayerAnimations>(true) : null;
    }

    void Update()
    {
        if (collected) return;
        if (player == null) player = GameObject.FindWithTag("Player");
        if (inventory == null && player != null) inventory = player.GetComponentInParent<InventorySystem>();
        // Dialogue/cutscenes disable movement: E must not also collect an item.
        var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (Time.timeScale <= 0 || (movement != null && !movement.isActiveAndEnabled)) return;
        // Imported pickup roots can be far from the visible glasses. Measure
        // from the player's camera to the item artwork, not the prefab pivot/UI.
        var view = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        Vector3 origin = view != null ? view.transform.position : player != null ? player.transform.position : Vector3.zero;
        Renderer artwork = GetComponentInChildren<SpriteRenderer>();
        if (artwork == null) artwork = GetComponentInChildren<MeshRenderer>();
        Vector3 target = artwork != null ? artwork.bounds.ClosestPoint(origin) : transform.position;
        float reach = itemTag == "BrokenGlasses" ? Mathf.Min(pickupRange, 2f) : pickupRange;
        bool inRange = player != null && (origin - target).sqrMagnitude <= reach * reach;
        playerInRange = inRange;
        bool visible = view != null && view.isActiveAndEnabled && artwork != null && artwork.enabled;
        Vector3 aim = artwork != null ? artwork.bounds.center : target;
        if (visible)
        {
            Vector3 viewport = view.WorldToViewportPoint(aim);
            visible = viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1
                && Vector3.Angle(view.transform.forward, aim - origin) <= pickupViewAngle;
            foreach (var hit in Physics.RaycastAll(origin, (aim - origin).normalized,
                Vector3.Distance(origin, aim), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform)) continue;
                visible = false;
                break;
            }
        }
        if (inRange && visible && Input.GetKeyDown(KeyCode.E)) PickUpItem();
    }

    void PickUpItem()
    {
        if (inventory == null) return;
        collected = true;
        if (animations == null) animations = inventory.GetComponentInChildren<PlayerAnimations>(true);
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
