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

    [Header("Light Settings")]
    public bool enableLightPulse = true;
    public float minLightIntensity = 1f;
    public float maxLightIntensity = 3f;
    public float lightPulseSpeed = 2f;

    private Vector3 startPos;
    private Light itemLight;

    void Start()
    {
        startPos = transform.position;

        if (objectToSpin == null)
            objectToSpin = transform;

        itemLight = GetComponentInChildren<Light>();

        if (itemLight == null)
        {
            itemLight = gameObject.AddComponent<Light>();
            itemLight.type = LightType.Point;
            itemLight.range = 3f;
            itemLight.color = new Color(1f, 0.8f, 0.2f);
        }
    }

    void Update()
    {
        // Rotate only the assigned object
        objectToSpin.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Bob the entire pickup
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // Pulse the light
        if (enableLightPulse && itemLight != null)
        {
            float pulse = Mathf.PingPong(Time.time * lightPulseSpeed, 1f);
            itemLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, pulse);
        }
    }
}