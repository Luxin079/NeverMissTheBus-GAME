using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Simple in-game pause UI that also works during a story cutscene.</summary>
public class GamePauseMenu : MonoBehaviour
{
    [SerializeField] string mainMenuScene = "MainMenu";
    [SerializeField] string gameScene = "SampleScene";
    StoryCutsceneDirector story;
    Canvas canvas;
    GameObject mainPanel, settingsPanel;
    TMP_Text sensitivityValue;
    Slider sensitivitySlider;
    bool paused, inSettings;
    float previousTimeScale;
    CursorLockMode previousLock;
    bool previousCursor;
    bool previousAudioPause;
    int resetCount;
    float lastResetPress;

    void Awake()
    {
        story = GetComponent<StoryCutsceneDirector>();
        EnsureEventSystem();
        BuildUI();
        canvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            resetCount = Time.unscaledTime - lastResetPress <= 2 ? resetCount + 1 : 1;
            lastResetPress = Time.unscaledTime;
            if (resetCount >= 3) { ResetGame(); return; }
        }
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (inSettings) ShowMain();
        else if (paused) Resume();
        else Pause();
    }

    void Pause()
    {
        paused = true;
        previousTimeScale = Time.timeScale;
        previousLock = Cursor.lockState; previousCursor = Cursor.visible;
        previousAudioPause = AudioListener.pause;
        Time.timeScale = 0;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        canvas.gameObject.SetActive(true);
        ShowMain();
    }

    void Resume()
    {
        paused = inSettings = false;
        canvas.gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousLock; Cursor.visible = previousCursor;
    }

    void ShowMain()
    {
        inSettings = false;
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    void ShowSettings()
    {
        inSettings = true;
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        sensitivitySlider.SetValueWithoutNotify(GameSaveStore.LoadSensitivity(PlayerPrefs.GetFloat("MouseSensitivity", 2)));
        sensitivityValue.text = sensitivitySlider.value.ToString("0.0");
    }

    void OnSensitivityChanged(float value)
    {
        sensitivityValue.text = value.ToString("0.0");
        GameSaveStore.SaveSensitivity(value);
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();
        var look = FindFirstObjectByType<MouseLook>(FindObjectsInactive.Include);
        if (look != null) look.mouseSensitivity = value;
    }

    void SaveAndExit()
    {
        var state = story != null ? story.CaptureSaveState() : new GameSaveState();
        if (!GameSaveStore.SaveGame(state)) return;
        Time.timeScale = 1; AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    void ResetGame()
    {
        resetCount = 0;
        GameSaveStore.ResetProgress();
        Time.timeScale = 1; AudioListener.pause = false;
        SceneManager.LoadScene(gameScene);
    }

    void OnDestroy()
    {
        if (paused) { Time.timeScale = 1; AudioListener.pause = false; }
    }

    void BuildUI()
    {
        var root = new GameObject("Pause Menu", typeof(RectTransform)); root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root.AddComponent<GraphicRaycaster>();
        var shade = Rect("Dark screen", root.transform, Vector2.zero, Vector2.one);
        shade.gameObject.AddComponent<Image>().color = new Color(.01f, .012f, .014f, .86f);
        mainPanel = Rect("Pause options", root.transform, new Vector2(.32f, .19f), new Vector2(.68f, .81f)).gameObject;
        Text("PAUSED", mainPanel.transform, new Vector2(0, .74f), new Vector2(1, .94f), 46);
        Button("RESUME", mainPanel.transform, new Vector2(.15f, .52f), new Vector2(.85f, .66f), Resume);
        Button("SETTINGS", mainPanel.transform, new Vector2(.15f, .34f), new Vector2(.85f, .48f), ShowSettings);
        Button("SAVE & EXIT", mainPanel.transform, new Vector2(.15f, .16f), new Vector2(.85f, .30f), SaveAndExit);
        Text("0  0  0  /  RESTART", mainPanel.transform, new Vector2(0, .02f), new Vector2(1, .10f), 15);

        settingsPanel = Rect("Pause settings", root.transform, new Vector2(.29f, .18f), new Vector2(.71f, .82f)).gameObject;
        Text("SETTINGS", settingsPanel.transform, new Vector2(0, .73f), new Vector2(1, .94f), 42);
        Text("MOUSE SENSITIVITY", settingsPanel.transform, new Vector2(.08f, .54f), new Vector2(.70f, .64f), 22);
        sensitivityValue = Text("2.0", settingsPanel.transform, new Vector2(.73f, .54f), new Vector2(.92f, .64f), 22);
        var sliderRoot = Rect("Sensitivity slider", settingsPanel.transform, new Vector2(.12f, .39f), new Vector2(.88f, .46f));
        var track = Rect("Track", sliderRoot, Vector2.zero, Vector2.one);
        track.gameObject.AddComponent<Image>().color = new Color(.25f, .27f, .26f);
        var fill = Rect("Fill", sliderRoot, Vector2.zero, Vector2.one);
        fill.gameObject.AddComponent<Image>().color = new Color(.78f, .77f, .68f);
        var handle = Rect("Handle", sliderRoot, Vector2.zero, Vector2.one);
        handle.gameObject.AddComponent<Image>().color = Color.white;
        handle.anchorMin = new Vector2(0, 0);
        handle.anchorMax = new Vector2(0, 1);
        handle.sizeDelta = new Vector2(18, 0);
        sensitivitySlider = sliderRoot.gameObject.AddComponent<Slider>();
        sensitivitySlider.minValue = .1f; sensitivitySlider.maxValue = 5;
        sensitivitySlider.fillRect = fill; sensitivitySlider.handleRect = handle;
        sensitivitySlider.SetValueWithoutNotify(GameSaveStore.LoadSensitivity(PlayerPrefs.GetFloat("MouseSensitivity", 2)));
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        Button("BACK", settingsPanel.transform, new Vector2(.29f, .12f), new Vector2(.71f, .26f), ShowMain);
        settingsPanel.SetActive(false);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    public TMP_FontAsset menuFont;
    TMP_Text Text(string value, Transform parent, Vector2 min, Vector2 max, float size)
    {
        var text = Rect(value, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = menuFont != null ? menuFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        text.text = value; text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(.92f, .93f, .91f); text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = size;
        return text;
    }
    void Button(string value, Transform parent, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(value, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.04f, .045f, .04f, .9f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var label = Text(value, rect, Vector2.zero, Vector2.one, 29);
        button.targetGraphic = label;
        var colors = button.colors;
        colors.normalColor = new Color(.48f, .49f, .48f);
        colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.7f, .7f, .7f);
        button.colors = colors;
    }
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>(); go.AddComponent<StandaloneInputModule>();
    }
}
