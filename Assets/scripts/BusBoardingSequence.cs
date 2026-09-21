using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class BusBoardingSequence : MonoBehaviour
{
    [Header("Existing objects")]
    [Tooltip("Drag your existing Player object here.")]
    public GameObject player;
    [Tooltip("Drag School_Bus_damage here.")]
    public Transform bus;
    [Tooltip("Drag the bus-driver SpriteRenderer shown in your screenshot here.")]
    public SpriteRenderer busDriver;

    [Header("Sequence points")]
    [Tooltip("Empty object outside the bus door. The intro walks the player here.")]
    public Transform outsideDoorPoint;
    [Tooltip("Empty object just inside the bus.")]
    public Transform insideDoorPoint;
    [Tooltip("Where the bus driver sprite should be positioned.")]
    public Transform driverPoint;

    [Header("Player control")]
    public PlayerMovement playerMovement;
    public MouseLook mouseLook;
    public CharacterController characterController;
    [Tooltip("If enabled, the player automatically walks to the bus when Play starts.")]
    public bool playIntroOnStart = true;
    public float cutsceneWalkSpeed = 2.2f;
    public float turnSpeed = 7f;

    [Header("Boarding")]
    public float doorInteractDistance = 2.3f;
    public KeyCode interactKey = KeyCode.E;
    public TMP_Text interactionText;

    [Header("Dialogue")]
    public DialogueManager dialogueManager;
    [TextArea(2, 5)] public string driverLine1 = "Tickets, please. Get on and find a seat.";
    [TextArea(2, 5)] public string driverLine2 = "We are leaving soon.";

    [Header("Seat")]
    [Tooltip("Optional. Assign one seat point. If empty, any BusSeat component in range can be used.")]
    public BusSeat defaultSeat;

    private bool introFinished;
    private bool boarded;
    private bool dialogueStarted;
    private bool dialogueFinished;
    private bool seated;
    private bool sequenceBusy;

    private void Reset()
    {
        playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null) player = playerMovement.gameObject;
        mouseLook = FindFirstObjectByType<MouseLook>();
        dialogueManager = FindFirstObjectByType<DialogueManager>();
    }

    private void Awake()
    {
        if (player != null)
        {
            if (playerMovement == null) playerMovement = player.GetComponent<PlayerMovement>();
            if (characterController == null) characterController = player.GetComponent<CharacterController>();
            if (mouseLook == null) mouseLook = player.GetComponentInChildren<MouseLook>();
        }

        if (dialogueManager == null) dialogueManager = DialogueManager.Instance ?? FindFirstObjectByType<DialogueManager>();

        if (busDriver != null && driverPoint != null)
        {
            busDriver.transform.SetPositionAndRotation(driverPoint.position, driverPoint.rotation);
        }

        SetPrompt("");
    }

    private IEnumerator Start()
    {
        yield return null;

        if (playIntroOnStart && player != null && outsideDoorPoint != null)
        {
            sequenceBusy = true;
            SetPlayerControl(false);
            yield return WalkPlayerTo(outsideDoorPoint.position);
            FaceDirection(outsideDoorPoint.forward);
            SetPlayerControl(true);
            sequenceBusy = false;
        }

        introFinished = true;
    }

    private void Update()
    {
        if (!introFinished || sequenceBusy || player == null) return;

        if (!boarded)
        {
            float distance = outsideDoorPoint != null
                ? Vector3.Distance(player.transform.position, outsideDoorPoint.position)
                : Vector3.Distance(player.transform.position, transform.position);

            if (distance <= doorInteractDistance)
            {
                SetPrompt("Press E to enter the bus");
                if (Input.GetKeyDown(interactKey)) StartCoroutine(BoardBus());
            }
            else SetPrompt("");
            return;
        }

        if (!dialogueFinished || seated)
        {
            if (seated) SetPrompt("");
            return;
        }

        BusSeat nearestSeat = FindNearestSeat();
        if (nearestSeat != null && nearestSeat.CanUse(player.transform))
        {
            SetPrompt("Press E to sit down");
            if (Input.GetKeyDown(interactKey)) StartCoroutine(SitDown(nearestSeat));
        }
        else SetPrompt("");
    }

    private IEnumerator BoardBus()
    {
        if (boarded || sequenceBusy || insideDoorPoint == null) yield break;

        sequenceBusy = true;
        SetPrompt("");
        SetPlayerControl(false);

        // Keep the SAME player object. We move it through the door instead of swapping players.
        yield return WalkPlayerTo(insideDoorPoint.position);
        boarded = true;

        StartDriverDialogue();
        yield return new WaitUntil(() => dialogueManager == null || !dialogueManager.IsDialogueActive);

        dialogueFinished = true;
        SetPlayerControl(true);
        sequenceBusy = false;
    }

    private void StartDriverDialogue()
    {
        if (dialogueStarted || dialogueManager == null) return;
        dialogueStarted = true;

        Dialogue dialogue = new Dialogue
        {
            lines = new[]
            {
                new DialogueLine { characterName = "Bus Driver", text = driverLine1 },
                new DialogueLine { characterName = "Bus Driver", text = driverLine2 }
            }
        };

        dialogueManager.StartDialogue(dialogue);
    }

    private BusSeat FindNearestSeat()
    {
        BusSeat[] seats = FindObjectsByType<BusSeat>(FindObjectsSortMode.None);
        BusSeat best = null;
        float bestDistance = float.MaxValue;

        foreach (BusSeat seat in seats)
        {
            if (seat == null || seat.IsOccupied) continue;
            float d = Vector3.Distance(player.transform.position, seat.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = seat;
            }
        }

        if (defaultSeat != null && !defaultSeat.IsOccupied)
        {
            float d = Vector3.Distance(player.transform.position, defaultSeat.transform.position);
            if (d <= bestDistance) best = defaultSeat;
        }

        return best;
    }

    private IEnumerator SitDown(BusSeat seat)
    {
        if (seat == null || sequenceBusy) yield break;
        sequenceBusy = true;
        SetPrompt("");
        SetPlayerControl(false);

        yield return WalkPlayerTo(seat.SitPoint.position);
        seat.Occupy();

        if (characterController != null) characterController.enabled = false;
        player.transform.SetPositionAndRotation(seat.SitPoint.position, seat.SitPoint.rotation);
        if (characterController != null) characterController.enabled = true;

        seated = true;
        sequenceBusy = false;
        // Movement stays disabled while seated. Mouse look is enabled so the player can look around.
        if (mouseLook != null) mouseLook.enabled = true;
    }

    private IEnumerator WalkPlayerTo(Vector3 target)
    {
        if (player == null) yield break;

        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;

        while (Vector3.Distance(Flat(player.transform.position), Flat(target)) > 0.08f)
        {
            Vector3 direction = target - player.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

                Vector3 movement = direction.normalized * cutsceneWalkSpeed * Time.deltaTime;
                if (movement.magnitude > direction.magnitude) movement = direction;

                if (characterController != null && characterController.enabled)
                    characterController.Move(movement);
                else
                    player.transform.position += movement;
            }

            yield return null;
        }
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            player.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void SetPlayerControl(bool enabled)
    {
        if (playerMovement != null) playerMovement.enabled = enabled;
        if (mouseLook != null) mouseLook.enabled = enabled;
    }

    private void SetPrompt(string message)
    {
        if (interactionText == null) return;
        interactionText.text = message;
        interactionText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
