using System.Collections;
using UnityEngine;

public class PromptFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.15f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(Fade(1f));
    }

    public void Hide()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(Fade(0f));
    }

    private IEnumerator Fade(float target)
    {
        float start = canvasGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;

            canvasGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);

            yield return null;
        }

        canvasGroup.alpha = target;

        if (target == 0f)
            gameObject.SetActive(false);
    }
}