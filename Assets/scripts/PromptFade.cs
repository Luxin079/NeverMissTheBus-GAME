using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PromptFade : MonoBehaviour
{
    [Header("UI Container (drag the parent of your pickup UI here)")]
    [Tooltip("Drag the parent GameObject of your pickup UI. The parent and all children (world-space Canvas, UI Graphics, SpriteRenderers, Meshes) will be hidden until the player enters the trigger.")]
    public GameObject uiContainer;

    [Header("Runtime")]
    [Tooltip("Tag used to identify the player")]
    public string playerTag = "Player";

    [Tooltip("How fast the UI fades in/out")]
    public float fadeSpeed = 5f;

    private CanvasGroup canvasGroup;
    private Graphic[] uiGraphics;
    private SpriteRenderer[] spriteRenderers;
    private Renderer[] otherRenderers;

    // detection fallback for CharacterController (no Rigidbody)
    private bool useOverlapFallback;
    private Collider myCollider;
    private GameObject playerInstance;
    private bool playerInside;

    private Coroutine fadeRoutine;
    private PickUp owner;

    public bool IsReadable(Camera view)
    {
        if (!isActiveAndEnabled || uiContainer == null || !uiContainer.activeInHierarchy ||
            view == null || GetCurrentAlpha() < .8f) return false;
        foreach (var text in uiContainer.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            if (!text.isActiveAndEnabled || text.color.a < .8f) continue;
            Vector3 point = text.rectTransform.TransformPoint(text.rectTransform.rect.center);
            Vector3 screen = view.WorldToViewportPoint(point);
            if (screen.z <= 0 || screen.x < .03f || screen.x > .97f || screen.y < .03f || screen.y > .97f) continue;
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(view.transform.position, point-view.transform.position,
                Vector3.Distance(point,view.transform.position), ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
                if (view.transform.IsChildOf(hit.transform) || hit.transform.GetComponentInParent<PlayerMovement>() != null) continue;
                blocked = true; break;
            }
            if (!blocked) return true;
        }
        return false;
    }

    private void Awake()
    {
        owner = GetComponentInParent<PickUp>();
        if (uiContainer == null)
        {
            Debug.LogWarning($"{name}: uiContainer not assigned — using this GameObject as container.");
            uiContainer = gameObject;
        }

        // collect components from the container and all children (include inactive)
        uiGraphics = uiContainer.GetComponentsInChildren<Graphic>(true);
        spriteRenderers = uiContainer.GetComponentsInChildren<SpriteRenderer>(true);
        otherRenderers = uiContainer.GetComponentsInChildren<Renderer>(true);

        // prefer an existing CanvasGroup on the container; add if missing so we can fade whole world-space Canvas / UI group
        canvasGroup = uiContainer.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = uiContainer.AddComponent<CanvasGroup>();

        // start hidden
        SetAlphaImmediate(0f);

        myCollider = GetComponent<Collider>();
        if (myCollider == null)
            Debug.LogWarning($"{name}: No Collider found on the PromptFade object. Add a Collider and set 'Is Trigger'.");

        // detect player and whether it has a Rigidbody
        playerInstance = GameObject.FindWithTag(playerTag);
        if (playerInstance != null)
        {
            useOverlapFallback = playerInstance.GetComponent<Rigidbody>() == null;
        }
        else
        {
            useOverlapFallback = true;
        }
    }

    private void Update()
    {
        if (owner != null)
        {
            bool eligible = owner.isActiveAndEnabled && owner.CanOfferPickup;
            if (eligible != playerInside) { playerInside = eligible; FadeTo(eligible ? 1 : 0); }
            return;
        }
        if (!useOverlapFallback) return;
        if (myCollider == null) return;

        // Overlap fallback using the trigger collider's bounds (AABB).
        Bounds b = myCollider.bounds;
        Collider[] hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity);

        bool found = false;
        foreach (var h in hits)
        {
            if (h != null && h.CompareTag(playerTag))
            {
                found = true;
                break;
            }
        }

        if (found && !playerInside)
        {
            playerInside = true;
            FadeTo(1f);
        }
        else if (!found && playerInside)
        {
            playerInside = false;
            FadeTo(0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) return;
        // will work when a Rigidbody is present (recommended but not required with overlap fallback)
        if (!other.CompareTag(playerTag)) return;
        FadeTo(1f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner != null) return;
        if (!other.CompareTag(playerTag)) return;
        FadeTo(0f);
    }

    private void FadeTo(float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeCoroutine(target));
    }

    private IEnumerator FadeCoroutine(float target)
    {
        float current = GetCurrentAlpha();

        while (!Mathf.Approximately(current, target))
        {
            current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);
            SetAlpha(current);
            yield return null;
        }

        FinalizeVisibility(target);
    }

    private void SetAlpha(float alpha)
    {
        // CanvasGroup controls whole group including world-space Canvas
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = alpha > 0.001f;
            canvasGroup.interactable = alpha > 0.001f;
        }

        // UI Graphics (Image, Text, TMP etc.)
        if (uiGraphics != null)
        {
            foreach (var g in uiGraphics)
            {
                if (g == null) continue;
                Color c = g.color;
                c.a = alpha;
                g.color = c;
            }
        }

        // SpriteRenderers (e.g. textbox sprite children)
        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s == null) continue;
                Color c = s.color;
                c.a = alpha;
                s.color = c;
            }
        }

        // Other renderers (meshes). Toggle enabled when effectively invisible/visible.
        if (otherRenderers != null)
        {
            bool shouldEnable = alpha > 0.001f;
            foreach (var r in otherRenderers)
            {
                if (r == null) continue;
                r.enabled = shouldEnable;
            }
        }
    }

    private void SetAlphaImmediate(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (uiGraphics != null)
        {
            foreach (var g in uiGraphics) if (g != null) { Color c = g.color; c.a = alpha; g.color = c; }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers) if (s != null) { Color c = s.color; c.a = alpha; s.color = c; }
        }

        if (otherRenderers != null)
        {
            bool shouldEnable = alpha > 0.001f;
            foreach (var r in otherRenderers) if (r != null) r.enabled = shouldEnable;
        }
    }

    private void FinalizeVisibility(float alpha)
    {
        bool visible = alpha > 0.001f;
        if (otherRenderers != null)
        {
            foreach (var r in otherRenderers) if (r != null) r.enabled = visible;
        }
    }

    private float GetCurrentAlpha()
    {
        if (canvasGroup != null) return canvasGroup.alpha;
        if (uiGraphics != null && uiGraphics.Length > 0 && uiGraphics[0] != null) return uiGraphics[0].color.a;
        if (spriteRenderers != null && spriteRenderers.Length > 0 && spriteRenderers[0] != null) return spriteRenderers[0].color.a;
        return 0f;
    }

    // Visual helper in Scene view
    private void OnDrawGizmosSelected()
    {
        if (myCollider == null) myCollider = GetComponent<Collider>();
        if (myCollider == null) return;
        Gizmos.color = Color.cyan;
        Bounds b = myCollider.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
