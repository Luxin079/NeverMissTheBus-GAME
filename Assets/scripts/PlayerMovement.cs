using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.0f;
    public float sprintSpeed = 6.0f;
    public float crouchSpeed = 1.5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Crouch Settings")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Audio Settings")]
    public AudioClip movementSound;
    public AudioClip jumpSound;
    public AudioClip landSound;

    [Tooltip("Wait this many seconds after stopping before fading the audio.")]
    public float soundCutoffDelay = 0.1f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool wasGrounded;
    private float inactiveTimer = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && !wasGrounded && velocity.y < 0)
        {
            SoundManager.Instance?.PlayOneShot(landSound);
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        float currentSpeed = walkSpeed;

        if (Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.C))
        {
            currentSpeed = sprintSpeed;
        }

        if (Input.GetKeyDown(KeyCode.C)) controller.height = crouchHeight;
        else if (Input.GetKeyUp(KeyCode.C)) controller.height = standingHeight;

        if (Input.GetKey(KeyCode.C)) currentSpeed = crouchSpeed;

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (isGrounded && controller.velocity.magnitude > 0.1f)
        {
            inactiveTimer = 0f;
            float pitch = 1.0f;
            if (currentSpeed == sprintSpeed) pitch = 1.3f;
            else if (currentSpeed == crouchSpeed) pitch = 0.7f;

            SoundManager.Instance?.PlayLoop(movementSound, pitch);
        }
        else
        {
            if (isGrounded && inactiveTimer < soundCutoffDelay)
            {
                inactiveTimer += Time.deltaTime;
                if (inactiveTimer >= soundCutoffDelay)
                {
                    SoundManager.Instance?.StopLoopFade();
                }
            }
            else if (!isGrounded)
            {
                SoundManager.Instance?.StopLoopInstant();
            }
        }

        if (Input.GetButtonDown("Jump") && isGrounded && !Input.GetKey(KeyCode.C))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            SoundManager.Instance?.PlayOneShot(jumpSound);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}