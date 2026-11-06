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

    [Header("Crouch")]
    [Tooltip("Fraction of the scene-authored CharacterController height while crouching. Standing dimensions and camera position come directly from the scene.")]
    [Range(.4f, .9f)] public float crouchFraction = .65f;

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
    private Transform viewTransform;
    private float standingHeight, standingStepOffset, crouchCameraOffset;
    private Vector3 standingCenter, standingCameraLocal;
    private Vector3 velocity;
    private bool isGrounded;
    private bool wasGrounded;
    private float inactiveTimer = 0f;
    private Vector3 lastSafePosition;
    private bool hasSafePosition;
    public bool IsCrouching => isActiveAndEnabled && Input.GetKey(KeyCode.C);
    public bool IsSprinting => isActiveAndEnabled && !IsCrouching && Input.GetKey(KeyCode.LeftShift);
    public float CurrentMoveSpeed => IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;

    // Cutscene teleports must not carry a stored falling velocity into gameplay.
    public void ResetMotion()
    {
        velocity = Vector3.zero;
        inactiveTimer = 0f;
        SoundManager.Instance?.StopLoopInstant();
        lastSafePosition = transform.position;
        hasSafePosition = true;
    }

    void OnDisable()
    {
        inactiveTimer = 0f;
        SoundManager.Instance?.StopLoopInstant();
        if (controller != null)
        {
            controller.height = standingHeight;
            controller.center = standingCenter;
            controller.stepOffset = standingStepOffset;
        }
        if (viewTransform != null && crouchCameraOffset > 0)
        {
            Vector3 local = viewTransform.localPosition;
            local.y = standingCameraLocal.y;
            viewTransform.localPosition = local;
            crouchCameraOffset = 0;
        }
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        standingCenter = controller.center;
        standingStepOffset = controller.stepOffset;
        var look = GetComponentInChildren<MouseLook>(true);
        viewTransform = look != null ? look.transform : null;
        if (viewTransform != null) standingCameraLocal = viewTransform.localPosition;
    }

    void ApplyBodyDimensions(bool crouched)
    {
        if (controller == null) return;
        controller.height = crouched ? Mathf.Max(standingHeight * crouchFraction, controller.radius * 2) : standingHeight;
        // Lower the top while crouching without moving the feet through the floor.
        controller.center = standingCenter + Vector3.up * ((controller.height - standingHeight) * .5f);
        controller.stepOffset = Mathf.Min(standingStepOffset, controller.height * .5f);
    }

    void SetViewHeight(bool crouched, bool instant)
    {
        if (viewTransform == null) return;
        float targetOffset = crouched ? standingHeight - controller.height : 0;
        if (targetOffset == 0 && crouchCameraOffset == 0) return;
        crouchCameraOffset = instant ? targetOffset : Mathf.MoveTowards(crouchCameraOffset, targetOffset, 3f * Time.deltaTime);
        Vector3 local = viewTransform.localPosition;
        local.y = standingCameraLocal.y - crouchCameraOffset;
        viewTransform.localPosition = local;
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

        ApplyBodyDimensions(IsCrouching);

        SetViewHeight(Input.GetKey(KeyCode.C), false);

        if (Input.GetKey(KeyCode.C)) currentSpeed = crouchSpeed;

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
        if (isGrounded && move.sqrMagnitude > .01f && horizontalVelocity.sqrMagnitude > .01f)
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
