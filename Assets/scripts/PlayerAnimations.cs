using System.Collections;
using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    [Header("Sprite Renderer")]
    [SerializeField] private SpriteRenderer animationSpriteRenderer; // Gewijzigd van Image

    [Header("Pickup Animation")]
    [SerializeField] private Sprite[] pickupFrames;
    [SerializeField] private float pickupFrameTime = 0.06f;

    private Coroutine currentAnimation;
    private Material foregroundMaterial;
    private Material originalMaterial;

    private void Awake()
    {
        if (animationSpriteRenderer != null)
        {
            originalMaterial = animationSpriteRenderer.sharedMaterial;
            var shader = Resources.Load<Shader>("PickupForeground");
            if (shader != null)
            {
                foregroundMaterial = new Material(shader);
                animationSpriteRenderer.sharedMaterial = foregroundMaterial;
                animationSpriteRenderer.sortingOrder = 32767;
            }
            animationSpriteRenderer.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = null;
        if (animationSpriteRenderer != null) animationSpriteRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (animationSpriteRenderer != null && foregroundMaterial != null)
            animationSpriteRenderer.sharedMaterial = originalMaterial;
        if (foregroundMaterial != null) Destroy(foregroundMaterial);
    }

    public void PlayPickupAnimation()
    {
        if (animationSpriteRenderer == null || pickupFrames == null || pickupFrames.Length == 0)
        {
            Debug.LogWarning("Assign a SpriteRenderer and pickup frames to PlayerAnimations.", this);
            return;
        }
        if (!gameObject.activeInHierarchy) return;
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(PlayAnimation(pickupFrames, pickupFrameTime));
    }

    private IEnumerator PlayAnimation(Sprite[] frames, float frameTime)
    {
        if (frames == null || frames.Length == 0)
            yield break;

        animationSpriteRenderer.enabled = true;

        foreach (Sprite frame in frames)
        {
            animationSpriteRenderer.sprite = frame;
            yield return new WaitForSecondsRealtime(Mathf.Max(.01f, frameTime));
        }

        animationSpriteRenderer.enabled = false;
        currentAnimation = null;
    }
}
