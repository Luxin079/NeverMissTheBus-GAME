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

    public Canvas CrosshairCanvas { get { ResolveCanvas(); return crosshairCanvas; } }

    void ResolveCanvas()
    {
        if (crosshairCanvas != null) return;
        foreach (var candidate in GetComponentsInChildren<Canvas>(true))
            if (candidate.name.IndexOf("crosshair", System.StringComparison.OrdinalIgnoreCase) >= 0)
            { crosshairCanvas = candidate; break; }
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        mouseLook = GetComponentInChildren<MouseLook>(true);
        story = FindFirstObjectByType<StoryCutsceneDirector>();
        ResolveCanvas();
        if (crosshairCanvas != null)
        {
            crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            crosshairCanvas.overrideSorting = true;
            crosshairCanvas.sortingOrder = 200;
            crosshairCanvas.enabled = false;
        }
    }

    void LateUpdate()
    {
        ResolveCanvas();
        if (crosshairCanvas == null) return;
        if (story == null) story = FindFirstObjectByType<StoryCutsceneDirector>();

        bool visible = storyAllowsControl && Time.timeScale > 0 &&
            movement != null && movement.isActiveAndEnabled &&
            mouseLook != null && mouseLook.isActiveAndEnabled &&
            (DialogueManager.Instance == null || !DialogueManager.Instance.isActiveAndEnabled ||
             !DialogueManager.Instance.IsDialogueActive) &&
            (story == null || !story.IsPlaying ||
             (!story.IsDialogueLinePlaying && !story.IsScreenFaded));
        if (visible && !crosshairCanvas.gameObject.activeSelf) crosshairCanvas.gameObject.SetActive(true);
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
