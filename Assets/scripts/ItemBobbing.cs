using UnityEngine;

public class ItemBobbing : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Sleep hier de CharacterController van de speler naartoe om te checken of je springt of valt.")]
    public CharacterController playerController;

    [Header("Bobbing Amplitudes (In PIXELS voor UI!)")]
    public float horizontalAmount = 25f;
    public float verticalAmount = 20f;

    [Header("Bobbing Speeds (Hoe snel wiegt het?)")]
    public float walkBobSpeed = 12f;
    public float sprintBobSpeed = 16f;

    [Header("Mid-Air Settings (Springen en Vallen)")]
    [Tooltip("Hoeveel pixels zakt de zaklamp naar BENEDEN als je valt/springt?")]
    public float midAirDroopAmount = 15f;
    [Tooltip("Hoeveel wiegt de zaklamp heen en weer in de lucht door de wind?")]
    public float midAirHorizontalSway = 5f;
    [Tooltip("Hoe snel wiegt hij in de lucht?")]
    public float midAirBobSpeed = 4f;

    [Header("Smoothing")]
    [Tooltip("Hoe vloeiend het item teruggaat naar zijn startpositie als je stopt.")]
    public float smoothReturnSpeed = 6f;

    private float timer = 0f;
    private Vector3 startLocalPosition;

    void Start()
    {
        // Sla de oorspronkelijke lokale positie op
        startLocalPosition = transform.localPosition;
    }

    void Update()
    {
        // Vraag direct aan Unity of de speler de WASD / Pijltjestoetsen indrukt
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        bool isMoving = (horizontalInput != 0f || verticalInput != 0f);

        // Check of we een playerController hebben en of die in de lucht hangt
        bool isMidAir = (playerController != null && !playerController.isGrounded);

        if (isMidAir)
        {
            // --- MID-AIR EFFECT ---
            // De timer loopt langzamer voor een rustige zweef/wind-beweging
            timer += midAirBobSpeed * Time.deltaTime;

            // Bereken een lichte zweving + trek de Y positie naar beneden (droop) wegens zwaartekracht
            float offsetX = Mathf.Cos(timer) * midAirHorizontalSway;
            float offsetY = (Mathf.Sin(timer) * 5f) - midAirDroopAmount;

            Vector3 targetPosition = startLocalPosition + new Vector3(offsetX, offsetY, 0f);

            // Pas vloeiend toe
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * smoothReturnSpeed);
        }
        else if (isMoving)
        {
            // --- NORMAAL LOPEN/SPRINTEN (Op de grond) ---
            float currentBobSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintBobSpeed : walkBobSpeed;
            timer += currentBobSpeed * Time.deltaTime;

            float offsetX = Mathf.Cos(timer) * horizontalAmount;
            float offsetY = Mathf.Sin(timer * 2f) * verticalAmount;

            Vector3 targetPosition = startLocalPosition + new Vector3(offsetX, offsetY, 0f);

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * currentBobSpeed);
        }
        else
        {
            // --- STILSTAAN ---
            timer = 0f;
            transform.localPosition = Vector3.Lerp(transform.localPosition, startLocalPosition, Time.deltaTime * smoothReturnSpeed);
        }
    }
}