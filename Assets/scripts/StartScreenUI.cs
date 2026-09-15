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
    private TextMeshProUGUI[] buttons;
    private TextMeshProUGUI sensitivityValue;
    private GameObject settingsPanel;
    private Slider sensitivitySlider;
    private int selectedIndex;
    private bool settingsOpen;

    private const float MinSensitivity = 0.1f;
    private const float MaxSensitivity = 5f;
    private const float DefaultSensitivity = 2f;

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureEventSystem();
        BuildMenu();
    }

    private void Update()
    {
        if (settingsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
            return;
        }

        if (buttons == null) return;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) ChangeSelection(-1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) ChangeSelection(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) ActivateSelection();
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

        Texture2D art = Resources.Load<Texture2D>("MainMenuBackground");
        GameObject background = CreateUIObject("Background Art", canvas.transform);
        RawImage raw = background.AddComponent<RawImage>();
        raw.texture = art;
        raw.color = Color.white;
        raw.raycastTarget = false;
        AspectRatioFitter aspect = background.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = 1.5f;
        Stretch(background.transform);

        // Only covers the baked-in menu labels from the artwork. The title and scene remain untouched.
        GameObject menuBackplate = CreateUIObject("Interactive Menu Backplate", canvas.transform);
        Image backplateImage = menuBackplate.AddComponent<Image>();
        backplateImage.color = new Color(0f, 0f, 0f, 0.72f);
        SetAnchors(menuBackplate.GetComponent<RectTransform>(), new Vector2(0.065f, 0.27f), new Vector2(0.43f, 0.55f));

        GameObject menu = CreateUIObject("Interactive Menu", canvas.transform);
        SetAnchors(menu.GetComponent<RectTransform>(), new Vector2(0.075f, 0.25f), new Vector2(0.43f, 0.56f));

        string[] labels = { "START GAME", "SETTINGS", "QUIT" };
        buttons = new TextMeshProUGUI[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            GameObject buttonObject = CreateUIObject(labels[i], menu.transform);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            float y = 0.78f - i * 0.31f;
            SetAnchors(rect, new Vector2(0.10f, y - 0.13f), new Vector2(0.95f, y + 0.13f));

            Button button = buttonObject.AddComponent<Button>();
            Image hitImage = buttonObject.AddComponent<Image>();
            hitImage.color = new Color(0f, 0f, 0f, 0f);
            hitImage.raycastTarget = true;
            button.targetGraphic = hitImage;
            button.onClick.AddListener(() => { selectedIndex = index; ActivateSelection(); });

            TextMeshProUGUI text = CreateText(labels[i], buttonObject.transform, 28, FontStyles.Normal, TextAlignmentOptions.Left);
            text.raycastTarget = false;
            Stretch(text.transform);
            text.rectTransform.offsetMin = new Vector2(35f, 0f);
            text.rectTransform.offsetMax = Vector2.zero;
            buttons[i] = text;
        }

        TextMeshProUGUI hint = CreateText("W / S  SELECT     ENTER  CONFIRM", canvas.transform, 15, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.48f);
        SetAnchors(hint.rectTransform, new Vector2(0.065f, 0.045f), new Vector2(0.43f, 0.085f));

        TextMeshProUGUI version = CreateText("NMTB // 01", canvas.transform, 13, FontStyles.Normal, TextAlignmentOptions.Right);
        version.color = new Color(textColor.r, textColor.g, textColor.b, 0.3f);
        SetAnchors(version.rectTransform, new Vector2(0.88f, 0.025f), new Vector2(0.975f, 0.055f));

        BuildSettings(canvas.transform);

        fadeGroup = canvasObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        StartCoroutine(FadeIn());
        UpdateSelection();
    }

    private void BuildSettings(Transform canvas)
    {
        settingsPanel = CreateUIObject("Settings Panel", canvas);
        Image panelImage = settingsPanel.AddComponent<Image>();
        panelImage.color = new Color(0.015f, 0.018f, 0.017f, 0.96f);
        SetAnchors(settingsPanel.GetComponent<RectTransform>(), new Vector2(0.27f, 0.20f), new Vector2(0.73f, 0.80f));

        TextMeshProUGUI title = CreateText("SETTINGS", settingsPanel.transform, 42, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = textColor;
        SetAnchors(title.rectTransform, new Vector2(0.10f, 0.72f), new Vector2(0.90f, 0.90f));

        TextMeshProUGUI label = CreateText("MOUSE SENSITIVITY", settingsPanel.transform, 22, FontStyles.Normal, TextAlignmentOptions.Left);
        label.color = textColor;
        SetAnchors(label.rectTransform, new Vector2(0.12f, 0.53f), new Vector2(0.55f, 0.64f));

        sensitivityValue = CreateText("2.0", settingsPanel.transform, 22, FontStyles.Bold, TextAlignmentOptions.Right);
        sensitivityValue.color = selectedColor;
        SetAnchors(sensitivityValue.rectTransform, new Vector2(0.65f, 0.53f), new Vector2(0.88f, 0.64f));

        GameObject sliderObject = CreateUIObject("Sensitivity Slider", settingsPanel.transform);
        SetAnchors(sliderObject.GetComponent<RectTransform>(), new Vector2(0.12f, 0.39f), new Vector2(0.88f, 0.47f));
        sensitivitySlider = sliderObject.AddComponent<Slider>();
        sensitivitySlider.minValue = MinSensitivity;
        sensitivitySlider.maxValue = MaxSensitivity;
        sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", DefaultSensitivity);
        sensitivitySlider.wholeNumbers = false;
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);

        GameObject track = CreateUIObject("Track", sliderObject.transform);
        Image trackImage = track.AddComponent<Image>();
        trackImage.color = new Color(0.35f, 0.35f, 0.32f, 1f);
        Stretch(track.transform);

        GameObject fill = CreateUIObject("Fill", sliderObject.transform);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = selectedColor;
        SetAnchors(fill.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f));
        sensitivitySlider.fillRect = fill.GetComponent<RectTransform>();

        GameObject handle = CreateUIObject("Handle", sliderObject.transform);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = textColor;
        SetAnchors(handle.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f));
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 0f);
        sensitivitySlider.handleRect = handle.GetComponent<RectTransform>();
        sensitivitySlider.direction = Slider.Direction.LeftToRight;

        Button backButton = CreateButton("BACK", settingsPanel.transform, new Vector2(0.35f, 0.10f), new Vector2(0.65f, 0.22f), 24);
        backButton.onClick.AddListener(CloseSettings);
        settingsPanel.SetActive(false);
    }

    private Button CreateButton(string text, Transform parent, Vector2 min, Vector2 max, float size)
    {
        GameObject obj = CreateUIObject(text, parent);
        SetAnchors(obj.GetComponent<RectTransform>(), min, max);
        Button button = obj.AddComponent<Button>();
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        button.targetGraphic = image;
        TextMeshProUGUI label = CreateText(text, obj.transform, size, FontStyles.Normal, TextAlignmentOptions.Center);
        label.color = textColor;
        Stretch(label.transform);
        return button;
    }

    private void SetSensitivity(float value)
    {
        value = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();
        if (sensitivityValue != null) sensitivityValue.text = value.ToString("0.0");
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
        else if (selectedIndex == 1) OpenSettings();
        else Application.Quit();
    }

    private void OpenSettings()
    {
        settingsOpen = true;
        settingsPanel.SetActive(true);
        sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", DefaultSensitivity);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void CloseSettings()
    {
        settingsOpen = false;
        settingsPanel.SetActive(false);
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

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
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