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
    public float crouchHeight = 1.45f;
    public float standingHeight = 2.4f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    [Min(1f)] public float fallRecoveryDistance = 2.5f;

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
    private Vector3 lastSafePosition;
    private bool hasSafePosition;

    // Cutscene teleports must not carry a stored falling velocity into gameplay.
    public void ResetMotion()
    {
        velocity = Vector3.zero;
        inactiveTimer = 0f;
        lastSafePosition = transform.position;
        hasSafePosition = true;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private bool CheckGround()
    {
        if (controller.isGrounded) return true;
        float scale = Mathf.Abs(transform.lossyScale.y);
        Vector3 feet = transform.TransformPoint(controller.center) - Vector3.up * controller.height * scale * .5f;
        float radius = Mathf.Max(.05f, controller.radius * Mathf.Min(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * .8f);
        float reach = .12f + Mathf.Min(groundDistance, .22f);
        RaycastHit[] hits = Physics.SphereCastAll(feet + Vector3.up * .12f, radius, Vector3.down,
            reach, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
            if (hit.collider != null && !hit.transform.IsChildOf(transform) && hit.normal.y > .55f)
                return true;
        return false;
    }

    void Update()
    {
        wasGrounded = isGrounded;
        isGrounded = CheckGround();
        if (isGrounded)
        {
            lastSafePosition = transform.position;
            hasSafePosition = true;
        }
        else if (hasSafePosition && transform.position.y < lastSafePosition.y - fallRecoveryDistance)
        {
            controller.enabled = false;
            transform.position = lastSafePosition + Vector3.up * .08f;
            controller.enabled = true;
            ResetMotion();
            isGrounded = CheckGround();
        }

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

        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyUp(KeyCode.C))
        {
            controller.height = Input.GetKey(KeyCode.C) ? crouchHeight : standingHeight;
            // The capsule shrinks from the head, keeping its feet on the floor.
            controller.center = new Vector3(controller.center.x, (controller.height - standingHeight) * .5f, controller.center.z);
        }

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

        velocity.y = Mathf.Max(velocity.y + gravity * Time.deltaTime, -30f);
        controller.Move(velocity * Time.deltaTime);
    }
}
