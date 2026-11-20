using UnityEngine;

/// <summary>Shows the crosshair only during playable first-person objectives.</summary>
public class PlayerCrosshair : MonoBehaviour
{
    [Header("Existing crosshair UI")]
    [Tooltip("Drag the Canvas that already contains your crosshair here. It is hidden during dialogue, cutscenes and moments without an objective.")]
    public Canvas crosshairCanvas;

    PlayerMovement movement;
    MouseLook mouseLook;
    StoryCutsceneDirector story;
    bool storyAllowsControl;

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
            SetVisible(false);
        }
    }

    void LateUpdate()
    {
        ResolveCanvas();
        if (crosshairCanvas == null) return;
        if (story == null) story = FindFirstObjectByType<StoryCutsceneDirector>();

        bool visible = storyAllowsControl && story != null &&
            story.HasActiveObjective && story.IsFirstPersonGameplayActive &&
            movement != null && movement.isActiveAndEnabled &&
            mouseLook != null && mouseLook.isActiveAndEnabled &&
            (DialogueManager.Instance == null || !DialogueManager.Instance.isActiveAndEnabled ||
             !DialogueManager.Instance.IsDialogueActive);
        SetVisible(visible);
    }

    public void SetStoryControl(bool allowed)
    {
        storyAllowsControl = allowed;
        if (!allowed) SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (crosshairCanvas == null) return;
        crosshairCanvas.enabled = visible;
        var crosshair = crosshairCanvas.gameObject;
        if (crosshair != gameObject && crosshair.activeSelf != visible)
            crosshair.SetActive(visible);
    }

    void OnDisable() => SetVisible(false);
}
