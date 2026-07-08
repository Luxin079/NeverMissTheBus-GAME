using UnityEngine;

public class ItemBobbing : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Sleep hier de CharacterController van de speler naartoe om de loopsnelheid te meten.")]
    public CharacterController playerController;

    [Header("Bobbing Amplitudes (Hoe ver beweegt het?)")]
    public float horizontalAmount = 0.03f;
    public float verticalAmount = 0.03f;

    [Header("Bobbing Speeds (Hoe snel wiegt het?)")]
    public float walkBobSpeed = 12f;
    public float sprintBobSpeed = 16f;
    public float crouchBobSpeed = 7f;

    [Header("Smoothing")]
    [Tooltip("Hoe vloeiend het item teruggaat naar zijn startpositie als je stopt.")]
    public float smoothReturnSpeed = 6f;

    private float timer = 0f;
    private Vector3 startLocalPosition;

    void Start()
    {
        // Sla de oorspronkelijke lokale positie van het item/canvas op
        startLocalPosition = transform.localPosition;
    }

    void Update()
    {
        if (playerController == null) return;

        // Bereken de horizontale snelheid van de speler (vallen/springen telt niet mee)
        Vector3 horizontalVelocity = new Vector3(playerController.velocity.x, 0, playerController.velocity.z);
        float speed = horizontalVelocity.magnitude;

        // Alleen bobben als de speler op de grond staat en echt loopt
        if (playerController.isGrounded && speed > 0.1f)
        {
            // Bepaal de snelheid van het wiegen op basis van de loopsnelheid van de speler
            float currentBobSpeed = walkBobSpeed;

            if (speed > 4.5f) // Sprinten
            {
                currentBobSpeed = sprintBobSpeed;
            }
            else if (speed < 2.0f) // Kruipen
            {
                currentBobSpeed = crouchBobSpeed;
            }

            // De timer loopt door op basis van de huidige snelheid
            timer += currentBobSpeed * Time.deltaTime;

            // Bereken de nieuwe X en Y posities met Sinus en Cosinus voor een mooie '8-vormige' wieg
            float newX = startLocalPosition.x + Mathf.Cos(timer) * horizontalAmount;
            float newY = startLocalPosition.y + Mathf.Sin(timer * 2f) * verticalAmount;

            // Pas de positie vloeiend toe
            transform.localPosition = new Vector3(newX, newY, transform.localPosition.z);
        }
        else
        {
            // Als de speler stilstaat of springt, keert het item rustig terug naar de startpositie
            timer = 0f;
            transform.localPosition = Vector3.Lerp(transform.localPosition, startLocalPosition, Time.deltaTime * smoothReturnSpeed);
        }
    }
}