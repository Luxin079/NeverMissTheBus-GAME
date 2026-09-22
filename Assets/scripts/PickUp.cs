using System;
using TMPro;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Item")]
    public string itemTag;
    public bool questItem;
    [Header("Prompt child of this pickup prefab")]
    [Tooltip("Drag the Press E child object from THIS SAME prefab here. No second prefab needed.")]
    public GameObject promptObject;
    public Vector3 promptOffset = new Vector3(0, 1.1f, 0);
    public Vector3 promptScale = Vector3.one;
    public string promptText = "Press E to pick up";
    [Header("Audio")]
    public AudioClip pickupSound;
    [Range(0, 1)] public float pickupVolume = .8f;
    [Header("Pickup settings")]
    [Range(.5f, 10)] public float pickupRange = 3;

    public event Action<PickUp> Collected;
    GameObject player, prompt;
    InventorySystem inventory;
    PlayerAnimations animations;
    bool playerInRange;

    void Awake()
    {
        if (promptObject != null)
        {
            prompt = promptObject;
            prompt.transform.localPosition = promptOffset;
            prompt.transform.localScale = promptScale;
        }
        else
        {
            prompt = new GameObject("Pickup prompt");
            prompt.transform.SetParent(transform, false);
            prompt.transform.localPosition = promptOffset;
            var text = prompt.AddComponent<TextMeshPro>();
            text.text = promptText; text.fontSize = 2.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(5, 1);
            prompt.AddComponent<YBillboard>();
        }
        prompt.SetActive(false);
    }

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
        if (prompt != null)
        {
            prompt.SetActive(inRange);
            if (inRange && Camera.main != null) prompt.transform.LookAt(Camera.main.transform);
        }
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
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position + promptOffset, .15f);
    }
}
