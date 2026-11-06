using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Components")]
    public Light spotlight;
    public KeyCode toggleKey = KeyCode.F;
    public float beamRange = 22f;
    public float beamIntensity = 12f;
    private bool lightWasOn;
    public void ResetUnequipped()
    {
        lightWasOn = false;
        if (spotlight != null) spotlight.enabled = false;
    }

    private void ConfigureBeam()
    {
        if (spotlight == null) spotlight = GetComponentInChildren<Light>(true);
        if (spotlight == null) return;
        spotlight.type = LightType.Spot;
        spotlight.range = beamRange;
        spotlight.intensity = beamIntensity;
        spotlight.spotAngle = 52;
        spotlight.innerSpotAngle = 28;
        spotlight.color = new Color(1f,.92f,.78f);
        spotlight.shadows = LightShadows.Soft;
        spotlight.shadowBias = .035f;
        spotlight.shadowNormalBias = .12f;
        spotlight.cullingMask = ~0;
    }

    [Header("Audio Sounds")]
    [Tooltip("Geluid wanneer je de zaklamp pakt (equipt).")]
    public AudioClip equipSound;
    [Tooltip("Geluid wanneer je de zaklamp wegstopt (unequipt).")]
    public AudioClip unequipSound;
    [Tooltip("Geluid wanneer je de zaklamp aanzet.")]
    public AudioClip turnOnSound;
    [Tooltip("Geluid wanneer je de zaklamp uitzet.")]
    public AudioClip turnOffSound;

    private void Awake()
    {
        ConfigureBeam();
        if (spotlight != null) spotlight.enabled = false;
    }

    private void OnEnable()
    {
        ConfigureBeam();
        if (spotlight != null) spotlight.enabled = lightWasOn;
    }

    private void OnDisable()
    {
        if (spotlight != null) { lightWasOn = spotlight.enabled; spotlight.enabled = false; }
    }

    // Called by the inventory only for a deliberate slot change, not scene startup.
    public void PlayEquipSound() => SoundManager.Instance?.PlayOneShot(equipSound);
    public void PlayUnequipSound() => SoundManager.Instance?.PlayOneShot(unequipSound);

    private void Update()
    {
        var movement = GetComponentInParent<PlayerMovement>();
        if (Time.timeScale <= 0 || (movement != null && !movement.isActiveAndEnabled)) return;
        if (Input.GetKeyDown(toggleKey))
        {
            if (spotlight != null)
            {
                spotlight.enabled = !spotlight.enabled;

                if (spotlight.enabled)
                {
                    SoundManager.Instance?.PlayOneShot(turnOnSound);
                }
                else
                {
                    SoundManager.Instance?.PlayOneShot(turnOffSound);
                }
            }
        }
    }

    private void LateUpdate()
    {
        Camera view = Camera.main;
        if (spotlight == null || view == null || !spotlight.enabled) return;
        spotlight.transform.SetPositionAndRotation(view.transform.position + view.transform.forward*.15f, view.transform.rotation);
    }
}
