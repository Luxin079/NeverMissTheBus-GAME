using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Runtime-created main menu for Never Miss The Bus.
/// Designed around the game's low-quality PS2 / old Eastern-European horror look.
/// Attach this to an empty GameObject in a menu scene.
/// </summary>
public class StartScreenUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Optional Background")]
    [SerializeField] private Sprite backgroundImage;

    [Header("Visuals")]
    [SerializeField] private Color darkColor = new Color(0.015f, 0.018f, 0.018f, 1f);
    [SerializeField] private Color panelColor = new Color(0.03f, 0.035f, 0.032f, 0.92f);
    [SerializeField] private Color textColor = new Color(0.78f, 0.78f, 0.70f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.90f, 0.68f, 1f);

    private Canvas canvas;
    private CanvasGroup fadeGroup;
    private TextMeshProUGUI title;
    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI[] buttons;
    private int selectedIndex;
    private float flickerTimer;

    private void Awake()
    {
        BuildMenu();
    }

    private void Update()
    {
        if (buttons == null || buttons.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            ChangeSelection(-1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            ChangeSelection(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            ActivateSelection();

        // Very subtle CRT/VHS-style title flicker.
        flickerTimer += Time.unscaledDeltaTime;
        if (flickerTimer > 0.08f)
        {
            flickerTimer = 0f;
            title.alpha = Random.Range(0.91f, 1.0f);
        }
    }

    private void BuildMenu()
    {
        GameObject canvasObject = new GameObject("StartScreen Canvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObject.AddComponent<GraphicRaycaster>();

        // Almost-black background.
        GameObject background = CreateUIObject("Background", canvas.transform);
        Image backgroundImageComponent = background.AddComponent<Image>();
        backgroundImageComponent.color = darkColor;
        Stretch(background.transform);

        if (backgroundImage != null)
        {
            backgroundImageComponent.sprite = backgroundImage;
            backgroundImageComponent.color = Color.white;
            backgroundImageComponent.type = Image.Type.Simple;
            backgroundImageComponent.preserveAspect = false;
        }

        // Dark overlay keeps the menu readable and gives the scene a horror-film look.
        GameObject overlay = CreateUIObject("Dark Overlay", canvas.transform);
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, backgroundImage != null ? 0.52f : 1f);
        Stretch(overlay.transform);

        // Slight vignette-like central panel.
        GameObject panel = CreateUIObject("Menu Panel", canvas.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.07f, 0.12f);
        panelRect.anchorMax = new Vector2(0.43f, 0.88f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        title = CreateText("NEVER MISS\nTHE BUS", panel.transform, 74, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = textColor;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.05f, 0.65f);
        titleRect.anchorMax = new Vector2(0.95f, 0.92f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        subtitle = CreateText("THE LAST BUS IS WAITING.", panel.transform, 19, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = new Color(textColor.r, textColor.g, textColor.b, 0.65f);
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.08f, 0.57f);
        subtitleRect.anchorMax = new Vector2(0.92f, 0.64f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;

        string[] labels = { "START GAME", "CONTROLS", "QUIT" };
        buttons = new TextMeshProUGUI[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = CreateText(labels[i], panel.transform, 28, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform r = buttons[i].rectTransform;
            float top = 0.48f - i * 0.10f;
            r.anchorMin = new Vector2(0.17f, top - 0.055f);
            r.anchorMax = new Vector2(0.83f, top + 0.055f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            buttons[i].raycastTarget = false;
        }

        TextMeshProUGUI hint = CreateText("W / S    SELECT       ENTER    CONFIRM", canvas.transform, 16, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.42f);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0.07f, 0.055f);
        hintRect.anchorMax = new Vector2(0.43f, 0.095f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;

        TextMeshProUGUI version = CreateText("NMTB // 01", canvas.transform, 13, FontStyles.Normal, TextAlignmentOptions.Right);
        version.color = new Color(textColor.r, textColor.g, textColor.b, 0.28f);
        RectTransform versionRect = version.rectTransform;
        versionRect.anchorMin = new Vector2(0.88f, 0.025f);
        versionRect.anchorMax = new Vector2(0.975f, 0.055f);
        versionRect.offsetMin = Vector2.zero;
        versionRect.offsetMax = Vector2.zero;

        fadeGroup = canvasObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        StartCoroutine(FadeIn());
        UpdateSelection();
    }

    private IEnumerator FadeIn()
    {
        float time = 0f;
        while (time < 1f)
        {
            time += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.SmoothStep(0f, 1f, time);
            yield return null;
        }
        fadeGroup.alpha = 1f;
    }

    private void ChangeSelection(int direction)
    {
        selectedIndex += direction;
        if (selectedIndex < 0) selectedIndex = buttons.Length - 1;
        if (selectedIndex >= buttons.Length) selectedIndex = 0;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            bool selected = i == selectedIndex;
            buttons[i].color = selected ? selectedColor : textColor;
            buttons[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            buttons[i].text = selected ? ">  " + buttons[i].text.TrimStart('>', ' ') : buttons[i].text.TrimStart('>', ' ');
        }
    }

    private void ActivateSelection()
    {
        switch (selectedIndex)
        {
            case 0:
                StartCoroutine(StartGame());
                break;
            case 1:
                ShowControls();
                break;
            case 2:
                Application.Quit();
                break;
        }
    }

    private IEnumerator StartGame()
    {
        fadeGroup.interactable = false;
        float time = 0f;
        while (time < 0.7f)
        {
            time += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(1f, 0f, time / 0.7f);
            yield return null;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    private void ShowControls()
    {
        subtitle.text = "WASD  MOVE     SHIFT  RUN     C  CROUCH     F  FLASHLIGHT";
        subtitle.color = selectedColor;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static TextMeshProUGUI CreateText(string text, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUIObject(text, parent);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static void Stretch(Transform transform)
    {
        RectTransform rect = transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
