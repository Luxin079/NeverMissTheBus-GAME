using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class StartScreenUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private Color textColor = new Color(0.78f, 0.78f, 0.70f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.78f, 0.42f, 1f);

    private CanvasGroup fadeGroup;
    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI controls;
    private TextMeshProUGUI[] buttons;
    private int selectedIndex;
    private bool controlsOpen;
    private float flickerTimer;

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        BuildMenu();
    }

    private void Update()
    {
        if (buttons == null) return;

        if (Input.GetKeyDown(KeyCode.Escape) && controlsOpen)
        {
            CloseControls();
            return;
        }

        if (controlsOpen) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) ChangeSelection(-1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) ChangeSelection(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) ActivateSelection();

        flickerTimer += Time.unscaledDeltaTime;
        if (flickerTimer > 0.08f)
        {
            flickerTimer = 0f;
            // Small title flicker only. The actual background is the supplied image asset.
            GameObject titleObject = GameObject.Find("Menu Title");
            if (titleObject != null)
                titleObject.GetComponent<TextMeshProUGUI>().alpha = Random.Range(0.91f, 1f);
        }
    }

    private void BuildMenu()
    {
        GameObject canvasObject = new GameObject("StartScreen Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObject.AddComponent<GraphicRaycaster>();

        // THE USER-SUPPLIED ART. No procedural background is generated.
        Texture2D backgroundTexture = Resources.Load<Texture2D>("MainMenuBackground");
        GameObject background = CreateUIObject("Background Art", canvas.transform);
        RawImage backgroundImage = background.AddComponent<RawImage>();
        backgroundImage.texture = backgroundTexture;
        backgroundImage.color = Color.white;
        Stretch(background.transform);

        // Covers the baked-in title/menu on the left so our real interactive UI is used instead.
        GameObject leftPanel = CreateUIObject("Menu Backplate", canvas.transform);
        Image leftImage = leftPanel.AddComponent<Image>();
        leftImage.color = new Color(0f, 0f, 0f, 0.78f);
        RectTransform leftRect = leftPanel.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0.47f, 1f);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = Vector2.zero;

        GameObject vignette = CreateUIObject("Dark Overlay", canvas.transform);
        Image vignetteImage = vignette.AddComponent<Image>();
        vignetteImage.color = new Color(0f, 0f, 0f, 0.18f);
        Stretch(vignette.transform);

        GameObject panel = CreateUIObject("Menu UI", canvas.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.055f, 0.12f);
        panelRect.anchorMax = new Vector2(0.44f, 0.88f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI title = CreateText("NEVER MISS\nTHE BUS", panel.transform, 70, FontStyles.Bold, TextAlignmentOptions.Center);
        title.gameObject.name = "Menu Title";
        title.color = textColor;
        SetAnchors(title.rectTransform, new Vector2(0.02f, 0.68f), new Vector2(0.98f, 0.96f));

        subtitle = CreateText("THE LAST BUS IS WAITING.", panel.transform, 17, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = new Color(textColor.r, textColor.g, textColor.b, 0.7f);
        SetAnchors(subtitle.rectTransform, new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.66f));

        string[] labels = { "START GAME", "CONTROLS", "QUIT" };
        buttons = new TextMeshProUGUI[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = CreateText(labels[i], panel.transform, 28, FontStyles.Normal, TextAlignmentOptions.Left);
            float y = 0.47f - i * 0.11f;
            SetAnchors(buttons[i].rectTransform, new Vector2(0.18f, y - 0.05f), new Vector2(0.84f, y + 0.05f));
            buttons[i].raycastTarget = false;
        }

        controls = CreateText(
            "CONTROLS\n\nW A S D     MOVE\nSHIFT       RUN\nC           CROUCH\nSPACE       JUMP\nF           FLASHLIGHT\nMOUSE       LOOK\n\nESC         BACK",
            panel.transform, 21, FontStyles.Normal, TextAlignmentOptions.Left);
        controls.color = textColor;
        SetAnchors(controls.rectTransform, new Vector2(0.16f, 0.18f), new Vector2(0.88f, 0.58f));
        controls.gameObject.SetActive(false);

        TextMeshProUGUI hint = CreateText("W / S  SELECT     ENTER  CONFIRM", canvas.transform, 15, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.5f);
        SetAnchors(hint.rectTransform, new Vector2(0.055f, 0.045f), new Vector2(0.44f, 0.085f));

        TextMeshProUGUI version = CreateText("NMTB // 01", canvas.transform, 13, FontStyles.Normal, TextAlignmentOptions.Right);
        version.color = new Color(textColor.r, textColor.g, textColor.b, 0.3f);
        SetAnchors(version.rectTransform, new Vector2(0.88f, 0.025f), new Vector2(0.975f, 0.055f));

        fadeGroup = canvasObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        StartCoroutine(FadeIn());
        UpdateSelection();
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
            buttons[i].text = (selected ? ">  " : "") + buttons[i].text.TrimStart('>', ' ');
        }
    }

    private void ActivateSelection()
    {
        if (selectedIndex == 0) StartCoroutine(StartGame());
        else if (selectedIndex == 1) ShowControls();
        else Application.Quit();
    }

    private void ShowControls()
    {
        controlsOpen = true;
        controls.gameObject.SetActive(true);
        foreach (TextMeshProUGUI button in buttons) button.gameObject.SetActive(false);
        subtitle.text = "HOW TO SURVIVE THE NIGHT";
    }

    private void CloseControls()
    {
        controlsOpen = false;
        controls.gameObject.SetActive(false);
        foreach (TextMeshProUGUI button in buttons) button.gameObject.SetActive(true);
        subtitle.text = "THE LAST BUS IS WAITING.";
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
        return tmp;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
