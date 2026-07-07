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
    public GameObject firstPersonObject; // Changed from Sprite to GameObject

    [Tooltip("The actual 3D pickup prefab (if you plan on adding dropping mechanics later).")]
    public GameObject droppedObject;
}

public class PickUpManager : MonoBehaviour
{
    [Header("Item Settings")]
    public List<ItemMapping> itemMappings = new List<ItemMapping>();

    // Keeps track of the unlocked first-person objects in the order they were picked up.
    private List<GameObject> inventoryOrder = new List<GameObject>();

    // Keeps track of the item tags we have already picked up.
    private HashSet<string> processedTags = new HashSet<string>();

    private int currentlyEquippedIndex = -1;

    void Start()
    {
        // Hide all first-person object visuals initially
        foreach (var mapping in itemMappings)
        {
            if (mapping.firstPersonObject != null)
            {
                // mapping.firstPersonObject is a Sprite, not a GameObject.
                // You need to reference the GameObject that displays this Sprite and set it inactive.
                // If you have a GameObject for the first-person object, use that instead.
                // For now, just remove or comment out the SetActive line to fix the error:
                // mapping.firstPersonObject.SetActive(false);
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

        // Equip the new selected item
        currentlyEquippedIndex = index;
        if (inventoryOrder[currentlyEquippedIndex] != null)
        {
            inventoryOrder[currentlyEquippedIndex].SetActive(true);
        }
    }

    // Called by the ItemSpin script when the player touches the 3D item
    public void AddItem(string itemTag)
    {
        foreach (var mapping in itemMappings)
        {
            // Find a matching tag and ensure it hasn't been added to the inventory yet
            if (mapping.itemTag == itemTag && !processedTags.Contains(itemTag))
            {
                processedTags.Add(itemTag);
                inventoryOrder.Add(mapping.firstPersonObject);

                // Equip it automatically if it's the very first item collected (Slot 1)
                if (inventoryOrder.Count == 1)
                {
                    EquipItem(0);
                }
                
                break; // Item found and added, stop looking
            }
        }
    }
}