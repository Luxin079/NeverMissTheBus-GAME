using UnityEngine;
using System.Collections;

public class PromptFade : MonoBehaviour
{
    [Header("Objects to Fade")]
    public GameObject spriteContainer;
    public GameObject textContainer;

    [Header("Fade Settings")]
    public float fadeSpeed = 5f;

    private SpriteRenderer[] sprites;
    private CanvasGroup textCanvasGroup;

    private Coroutine fadeRoutine;


    private void Awake()
    {
        // Vind alle squares
        if (spriteContainer != null)
            sprites = spriteContainer.GetComponentsInChildren<SpriteRenderer>(true);

        // Vind CanvasGroup van tekst
        if (textContainer != null)
            textCanvasGroup = textContainer.GetComponentInChildren<CanvasGroup>();

        SetAlpha(0);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        FadeTo(1);
    }


    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        FadeTo(0);
    }


    private void FadeTo(float target)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(Fade(target));
    }


    private IEnumerator Fade(float target)
    {
        float current = GetCurrentAlpha();

        while (!Mathf.Approximately(current, target))
        {
            current = Mathf.MoveTowards(
                current,
                target,
                fadeSpeed * Time.deltaTime
            );

            SetAlpha(current);

            yield return null;
        }
    }


    private void SetAlpha(float alpha)
    {
        // Squares
        if (sprites != null)
        {
            foreach (SpriteRenderer sprite in sprites)
            {
                Color color = sprite.color;
                color.a = alpha;
                sprite.color = color;
            }
        }

        // Tekst
        if (textCanvasGroup != null)
        {
            textCanvasGroup.alpha = alpha;
        }
    }


    private float GetCurrentAlpha()
    {
        if (textCanvasGroup != null)
            return textCanvasGroup.alpha;

        if (sprites != null && sprites.Length > 0)
            return sprites[0].color.a;

        return 0;
    }
}