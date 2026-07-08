using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Components")]
    public Light spotlight;
    public KeyCode toggleKey = KeyCode.F;

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
        if (spotlight != null) spotlight.enabled = false;
    }

    private void OnEnable()
    {
        // Speelt af wanneer de zaklamp actief wordt
        SoundManager.Instance?.PlayOneShot(equipSound);
    }

    private void OnDisable()
    {
        // Speelt af wanneer de zaklamp wordt weggestopt
        SoundManager.Instance?.PlayOneShot(unequipSound);
    }

    private void Update()
    {
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
}