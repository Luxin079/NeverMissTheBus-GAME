using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemMapping
{
    [Tooltip("The name of the item for your own organization.")]
    public string itemName;

    [Tooltip("The tag of the 3D world object the player touches (e.g., 'Flashlight').")]
    public string itemTag;

    [Tooltip("The first-person view object (2D or 3D) linked to the camera.")]
    public GameObject firstPersonObject;

    [Tooltip("The actual 3D pickup prefab (if you plan on adding dropping mechanics later).")]
    public GameObject droppedObject;
}

public class InventorySystem : MonoBehaviour
{
    [Header("Item Settings")]
    public List<ItemMapping> itemMappings = new List<ItemMapping>();

    // Keeps track of the unlocked first-person objects in the order they were picked up.
    private List<GameObject> inventoryOrder = new List<GameObject>();

    // Keeps track of the item tags we have already picked up.
    private HashSet<string> processedTags = new HashSet<string>();

    private int currentlyEquippedIndex = -1;
    private readonly HashSet<string> questItems = new HashSet<string>();
    public void AddQuestItem(string id) { questItems.Add(id); }
    public bool HasQuestItem(string id) { return questItems.Contains(id); }

    void Start()
    {
        // Hide all first-person object visuals initially
        foreach (var mapping in itemMappings)
        {
            if (mapping.firstPersonObject != null)
            {
                mapping.firstPersonObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        // Cycle through keyboard numbers 1 through 9 (KeyCode.Alpha1 to Alpha9)
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                // Check if the player has collected an item for the number key pressed
                if (i < inventoryOrder.Count)
                {
                    EquipItem(i);
                }
                else
                {
                    // If the slot hasn't been unlocked yet, equip "nothing" (-1)
                    EquipItem(-1);
                }
            }
        }
    }

    private void EquipItem(int index)
    {
        if (currentlyEquippedIndex == index) return;

        // Unequip currently equipped item
        if (currentlyEquippedIndex >= 0 && currentlyEquippedIndex < inventoryOrder.Count)
        {
            if (inventoryOrder[currentlyEquippedIndex] != null)
            {
                inventoryOrder[currentlyEquippedIndex].SetActive(false);
            }
        }

        // Set the new index
        currentlyEquippedIndex = index;

        // Equip the new selected item, or leave hands empty if we passed -1
        if (currentlyEquippedIndex >= 0 && currentlyEquippedIndex < inventoryOrder.Count)
        {
            Debug.Log($"Equipping slot {index + 1}");
            if (inventoryOrder[currentlyEquippedIndex] != null)
            {
                inventoryOrder[currentlyEquippedIndex].SetActive(true);
            }
        }
        else
        {
            Debug.Log("Equipped empty hands.");
        }
    }

    // Called by the PickUp script when the player touches the 3D item
    public void AddItem(string itemTag)
    {
        bool itemFound = false;

        foreach (var mapping in itemMappings)
        {
            if (mapping.itemTag == itemTag && !processedTags.Contains(itemTag))
            {
                processedTags.Add(itemTag);
                inventoryOrder.Add(mapping.firstPersonObject);
                itemFound = true;

                Debug.Log($"SUCCESS: Picked up {mapping.itemName}. It is in inventory slot {inventoryOrder.Count}.");
                break; 
            }
        }

        if (!itemFound)
        {
            Debug.LogError($"FAILED: Could not find item mapping for tag '{itemTag}'");
        }
    }
}
