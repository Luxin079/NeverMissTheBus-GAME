using UnityEngine;

public class BusSeat : MonoBehaviour
{
    [Tooltip("Where the Player object is placed when sitting. If empty, this GameObject is used.")]
    public Transform sitPoint;
    public float interactDistance = 1.5f;

    public bool IsOccupied { get; private set; }
    public Transform SitPoint => sitPoint != null ? sitPoint : transform;

    private void Reset()
    {
        sitPoint = transform;
    }

    public bool CanUse(Transform player)
    {
        return !IsOccupied && player != null &&
               Vector3.Distance(player.position, SitPoint.position) <= interactDistance;
    }

    public void Occupy()
    {
        IsOccupied = true;
    }
}
