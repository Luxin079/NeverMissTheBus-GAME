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

    private void Awake()
    {
        if (animationSpriteRenderer != null)
        {
            animationSpriteRenderer.enabled = false;
        }
    }

    public void PlayPickupAnimation()
    {
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
            yield return new WaitForSeconds(frameTime);
        }

        animationSpriteRenderer.enabled = false;
        currentAnimation = null;
    }
}