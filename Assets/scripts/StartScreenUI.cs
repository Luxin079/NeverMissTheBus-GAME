using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class StartScreenUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private Color textColor = new Color(0.78f, 0.78f, 0.70f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.78f, 0.42f, 1f);
    [Header("Menu transitions")]
    [SerializeField, Min(0.01f)] private float menuFadeInSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float playFadeOutSeconds = 1.2f;

    private CanvasGroup fadeGroup;
    private CanvasGroup blackCover;
    private Coroutine introFade;
    private bool startingGame;
    [Header("Original menu soundtrack")]
    public AudioClip menuMusic;
    [Range(0, 1)] public float menuMusicVolume = .22f;
    private AudioSource menuMusicSource;
    private AudioClip generatedMenuMusic;
    private readonly List<Texture2D> menuGlyphTextures = new List<Texture2D>();
    private RawImage[] buttons;
    private RectTransform highlightRect;
    private RectTransform arrowRect;
    private TextMeshProUGUI sensitivityValue;
    private GameObject settingsPanel;
    private GameObject controlsPanel;
    private Slider sensitivitySlider;
    private int selectedIndex;
    private bool settingsOpen;
    private bool controlsOpen;

    private const float MinSensitivity = 0.1f;
    private const float MaxSensitivity = 5f;
    private const float DefaultSensitivity = 2f;

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        // ScreenSpaceOverlay alone does not count as a rendering camera in the Game view.
        Camera menuCamera = new GameObject("Main Menu Camera").AddComponent<Camera>();
        menuCamera.clearFlags = CameraClearFlags.SolidColor;
        menuCamera.backgroundColor = Color.black;
        menuCamera.cullingMask = 0;
        if (FindFirstObjectByType<AudioListener>() == null)
            menuCamera.gameObject.AddComponent<AudioListener>();
        EnsureEventSystem();
        BuildMenu();
        menuMusicSource = gameObject.AddComponent<AudioSource>();
        menuMusicSource.playOnAwake = false; menuMusicSource.loop = true;
        menuMusicSource.spatialBlend = 0; menuMusicSource.volume = menuMusicVolume;
        if (menuMusic == null) generatedMenuMusic = OriginalAmbientScore.Create(true);
        menuMusicSource.clip = menuMusic != null ? menuMusic : generatedMenuMusic;
        menuMusicSource.Play();
    }

    private void OnDestroy()
    {
        if (generatedMenuMusic != null) Destroy(generatedMenuMusic);
        foreach (Texture2D texture in menuGlyphTextures)
            if (texture != null) Destroy(texture);
    }

    private void Update()
    {
        if (menuMusicSource != null) menuMusicSource.volume = Mathf.MoveTowards(menuMusicSource.volume,
            startingGame ? 0 : menuMusicVolume, Time.unscaledDeltaTime * .2f);
        if (startingGame) return;
        if (settingsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
            return;
        }
        if (controlsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseControls();
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

        Texture2D art = Resources.Load<Texture2D>("MainMenuReferenceClean");
        Texture2D letters = Resources.Load<Texture2D>("MainMenuReferenceTypography");
        GameObject frame = CreateUIObject("Reference 16:9 layout", canvas.transform);
        Stretch(frame.transform);
        AspectRatioFitter frameAspect = frame.AddComponent<AspectRatioFitter>();
        frameAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        frameAspect.aspectRatio = 1672f / 941f;
        Transform layout = frame.transform;

        GameObject background = CreateUIObject("Background Art", layout);
        RawImage raw = background.AddComponent<RawImage>();
        raw.texture = art;
        raw.color = Color.white;
        raw.raycastTarget = false;
        Stretch(background.transform);

        GameObject highlight = CreateUIObject("Highlighted menu row", layout);
        highlightRect = highlight.GetComponent<RectTransform>();
        highlight.AddComponent<Image>().color = new Color(0f, 0f, 0f, .43f);

        string[] labels = { "START GAME", "CONTROLS", "QUIT" };
        buttons = new RawImage[labels.Length];
        int[] left = { 203, 203, 203 };
        int[] top = { 512, 584, 657 };
        int[] width = { 250, 220, 125 };
        int[] height = { 58, 58, 58 };
        float[] threshold = { 72f, 31f, 29f };
        float[] solid = { 150f, 55f, 55f };

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            GameObject buttonObject = CreateUIObject(labels[i] + " hit area", layout);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            float rowBottom = 1f - (top[i] + height[i] + 9f) / 941f;
            float rowTop = 1f - (top[i] - 8f) / 941f;
            SetAnchors(rect, new Vector2(.085f, rowBottom), new Vector2(.36f, rowTop));

            Button button = buttonObject.AddComponent<Button>();
            Image hitImage = buttonObject.AddComponent<Image>();
            hitImage.color = new Color(0f, 0f, 0f, 0f);
            hitImage.raycastTarget = true;
            button.targetGraphic = hitImage;
            button.onClick.AddListener(() => { selectedIndex = index; ActivateSelection(); });
            EventTrigger hover = buttonObject.AddComponent<EventTrigger>();
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { selectedIndex = index; UpdateSelection(); });
            hover.triggers.Add(enter);

            GameObject glyphObject = CreateUIObject(labels[i] + " original lettering", layout);
            RawImage glyph = glyphObject.AddComponent<RawImage>();
            Texture2D mask = ExtractMenuLettering(letters, left[i], top[i], width[i], height[i], threshold[i], solid[i]);
            menuGlyphTextures.Add(mask);
            glyph.texture = mask;
            glyph.raycastTarget = false;
            SetImagePixels(glyph.rectTransform, left[i], top[i], width[i], height[i]);
            buttons[i] = glyph;
        }

        GameObject arrowObject = CreateUIObject("Selection arrow", layout);
        RawImage arrow = arrowObject.AddComponent<RawImage>();
        Texture2D arrowMask = ExtractMenuLettering(letters, 149, 515, 37, 53, 72f, 150f);
        menuGlyphTextures.Add(arrowMask);
        arrow.texture = arrowMask;
        arrow.color = new Color(.91f, .92f, .9f);
        arrow.raycastTarget = false;
        arrowRect = arrow.rectTransform;

        BuildSettings(layout);
        BuildControls(layout);

        GameObject cover = CreateUIObject("Black Transition", canvas.transform);
        Stretch(cover.transform);
        cover.AddComponent<Image>().color = Color.black;
        blackCover = cover.AddComponent<CanvasGroup>();
        blackCover.alpha = 1f;
        blackCover.blocksRaycasts = false;

        fadeGroup = canvasObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
        introFade = StartCoroutine(FadeIn());
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
        sensitivitySlider.value = GameSaveStore.LoadSensitivity(PlayerPrefs.GetFloat("MouseSensitivity", DefaultSensitivity));
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
        GameSaveStore.SaveSensitivity(value);
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
            buttons[i].color = i == selectedIndex
                ? new Color(.92f, .93f, .91f, 1f)
                : new Color(.36f, .37f, .38f, 1f);
        }
        int rowTop = 504 + selectedIndex * 73;
        SetImagePixels(highlightRect, 190, rowTop, 325, 70);
        SetImagePixels(arrowRect, 149, 515 + selectedIndex * 73, 37, 53);
    }

    private void ActivateSelection()
    {
        if (selectedIndex == 0) StartCoroutine(StartGame());
        else if (selectedIndex == 1) OpenControls();
        else Application.Quit();
    }

    private void OpenSettings()
    {
        settingsOpen = true;
        settingsPanel.SetActive(true);
        sensitivitySlider.value = GameSaveStore.LoadSensitivity(PlayerPrefs.GetFloat("MouseSensitivity", DefaultSensitivity));
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void CloseSettings()
    {
        settingsOpen = false;
        settingsPanel.SetActive(false);
        if (controlsOpen) controlsPanel.SetActive(true);
    }

    private void OpenControls()
    {
        controlsOpen = true;
        controlsPanel.SetActive(true);
    }

    private void CloseControls()
    {
        controlsOpen = false;
        controlsPanel.SetActive(false);
    }

    private void BuildControls(Transform canvas)
    {
        controlsPanel = CreateUIObject("Controls Panel", canvas);
        controlsPanel.AddComponent<Image>().color = new Color(.015f, .018f, .017f, .96f);
        SetAnchors(controlsPanel.GetComponent<RectTransform>(), new Vector2(.27f, .17f), new Vector2(.73f, .83f));
        var title = CreateText("CONTROLS", controlsPanel.transform, 42, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchors(title.rectTransform, new Vector2(.08f, .75f), new Vector2(.92f, .91f));
        var details = CreateText("WASD  MOVE     MOUSE  LOOK\nE  INTERACT / CONTINUE\nF  FLASHLIGHT     ESC  PAUSE", controlsPanel.transform,
            23, FontStyles.Normal, TextAlignmentOptions.Center);
        details.color = textColor;
        SetAnchors(details.rectTransform, new Vector2(.07f, .34f), new Vector2(.93f, .69f));
        CreateButton("SETTINGS", controlsPanel.transform, new Vector2(.12f, .12f), new Vector2(.47f, .25f), 23)
            .onClick.AddListener(() => { controlsPanel.SetActive(false); OpenSettings(); });
        CreateButton("BACK", controlsPanel.transform, new Vector2(.53f, .12f), new Vector2(.88f, .25f), 23)
            .onClick.AddListener(CloseControls);
        controlsPanel.SetActive(false);
    }

    private IEnumerator FadeIn()
    {
        float time = 0f;
        while (time < menuFadeInSeconds)
        {
            time += Time.unscaledDeltaTime;
            blackCover.alpha = 1f - Mathf.SmoothStep(0f, 1f, time / menuFadeInSeconds);
            yield return null;
        }
        blackCover.alpha = 0f;
    }

    private IEnumerator StartGame()
    {
        if (startingGame) yield break;
        startingGame = true;
        if (introFade != null) StopCoroutine(introFade);
        fadeGroup.interactable = false;
        float time = 0f;
        float fromAlpha = blackCover.alpha;
        while (time < playFadeOutSeconds)
        {
            time += Time.unscaledDeltaTime;
            blackCover.alpha = Mathf.Lerp(fromAlpha, 1f, Mathf.SmoothStep(0f, 1f, time / playFadeOutSeconds));
            yield return null;
        }
        blackCover.alpha = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private static void SetImagePixels(RectTransform rect, int left, int top, int width, int height)
    {
        SetAnchors(rect,
            new Vector2(left / 1672f, 1f - (top + height) / 941f),
            new Vector2((left + width) / 1672f, 1f - top / 941f));
    }

    private static Texture2D ExtractMenuLettering(Texture2D source, int left, int top,
        int width, int height, float threshold, float solid)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[width * height];
        if (source != null && source.isReadable)
        {
            Color32[] original = source.GetPixels32();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Source coordinates are measured in the original reference image.
                    // Scale them if a platform imports the texture at a different size.
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt((left + x + .5f) * source.width / 1672f), 0, source.width - 1);
                    int sourceY = source.height - 1 - Mathf.Clamp(
                        Mathf.RoundToInt((top + y + .5f) * source.height / 941f), 0, source.height - 1);
                    Color32 sample = original[sourceY * source.width + sourceX];
                    float brightness = (sample.r + sample.g + sample.b) / 3f;
                    byte alpha = (byte)(255f * Mathf.Clamp01((brightness - threshold) / (solid - threshold)));
                    pixels[(height - 1 - y) * width + x] = new Color32(255, 255, 255, alpha);
                }
        }
        else Debug.LogError("MainMenuReferenceTypography must be a readable texture in Resources.");
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    public TMP_FontAsset menuFont;
    private TextMeshProUGUI CreateText(string text, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUIObject(text, parent);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = menuFont != null ? menuFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
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
