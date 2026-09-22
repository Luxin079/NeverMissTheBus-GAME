using UnityEngine;

/// <summary>Shows an existing crosshair Canvas only while this Player is controllable.</summary>
public class PlayerCrosshair : MonoBehaviour
{
    [Header("Existing crosshair UI")]
    [Tooltip("Drag the Canvas that already contains your crosshair here. This script does not create UI.")]
    public Canvas crosshairCanvas;

    PlayerMovement movement;
    MouseLook mouseLook;
    StoryCutsceneDirector story;
    bool storyAllowsControl = true;

    public Canvas CrosshairCanvas => crosshairCanvas;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        mouseLook = GetComponentInChildren<MouseLook>(true);
        story = FindFirstObjectByType<StoryCutsceneDirector>();
        if (crosshairCanvas != null) crosshairCanvas.enabled = false;
    }

    void LateUpdate()
    {
        if (crosshairCanvas == null) return;
        if (story == null) story = FindFirstObjectByType<StoryCutsceneDirector>();

        bool visible = storyAllowsControl && Time.timeScale > 0 &&
            movement != null && movement.isActiveAndEnabled &&
            mouseLook != null && mouseLook.isActiveAndEnabled &&
            (DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueActive) &&
            (story == null || !story.IsPlaying ||
             (!story.IsDialogueLinePlaying && !story.IsScreenFaded));
        if (crosshairCanvas.enabled != visible) crosshairCanvas.enabled = visible;
    }

    public void SetStoryControl(bool allowed)
    {
        storyAllowsControl = allowed;
        if (!allowed && crosshairCanvas != null) crosshairCanvas.enabled = false;
    }

    void OnDisable()
    {
        if (crosshairCanvas != null) crosshairCanvas.enabled = false;
    }
}
