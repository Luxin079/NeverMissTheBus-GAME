using UnityEngine;

public class ItemSpin : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The object that should spin (usually the 3D model).")]
    public Transform objectToSpin;

    [Header("Spin Settings")]
    public float spinSpeed = 50f;

    [Header("Bobbing Settings")]
    public float bobSpeed = 2f;
    public float bobHeight = 0.25f;

    [HideInInspector] public bool enableLightPulse = true; // Legacy prefab settings; pickups no longer generate lights.
    [HideInInspector] public float minLightIntensity = 1f;
    [HideInInspector] public float maxLightIntensity = 3f;
    [HideInInspector] public float lightPulseSpeed = 2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;

        if (objectToSpin == null)
            objectToSpin = transform;

    }

    void Update()
    {
        // Rotate only the assigned object
        objectToSpin.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Bob the entire pickup
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

    }
}
