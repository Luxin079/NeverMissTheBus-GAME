using UnityEngine;

public class ItemSpin : MonoBehaviour
{
    [Header("Spin Settings")]
    [Tooltip("How fast the item rotates.")]
    public float spinSpeed = 50f;

    [Header("Bobbing Settings")]
    [Tooltip("How fast the item moves up and down.")]
    public float bobSpeed = 2f;
    [Tooltip("How high and low the item moves.")]
    public float bobHeight = 0.25f;

    [Header("Light Settings")]
    [Tooltip("Check this to make the light pulse. Uncheck for a static light.")]
    public bool enableLightPulse = true;
    [Tooltip("The minimum brightness of the light.")]
    public float minLightIntensity = 1f;
    [Tooltip("The maximum brightness of the light.")]
    public float maxLightIntensity = 3f;
    [Tooltip("How fast the light pulses between min and max intensity.")]
    public float lightPulseSpeed = 2f;

    private Vector3 startPos;
    private Light itemLight;

    void Start()
    {
        // Store the starting position so we know where to bounce around
        startPos = transform.position;

        // Try to find an existing Light component on the object or its children
        itemLight = GetComponentInChildren<Light>();

        // If there isn't one, create a point light for the effect automatically
        if (itemLight == null)
        {
            itemLight = gameObject.AddComponent<Light>();
            itemLight.type = LightType.Point;
            itemLight.range = 3f;
            itemLight.color = new Color(1f, 0.8f, 0.2f); // Default to a golden/yellow hue
        }
    }

    void Update()
    {
        // 1. Spinning Effect
        // Rotates the object around the global Y-axis
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // 2. Bobbing Effect
        // Uses a Sine wave to smoothly calculate the new Y position based on time
        float newY = startPos.y + (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // 3. Light Pulsing Effect
        if (enableLightPulse && itemLight != null)
        {
            // Mathf.PingPong calculates a steady back-and-forth value between 0 and 1
            float pulse = Mathf.PingPong(Time.time * lightPulseSpeed, 1f);

            // Lerp blends between our min and max intensity using that ping-pong value
            itemLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, pulse);
        }
    }
}