using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Complete runtime-built main menu for Never Miss The Bus.
/// The background is generated at low resolution to keep the PS2/VHS look
/// without requiring an extra binary art asset in the project.
/// </summary>
public class StartScreenUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Visuals")]
    [SerializeField] private Color panelColor = new Color(0.015f, 0.018f, 0.017f, 0.82f);
    [SerializeField] private Color textColor = new Color(0.78f, 0.78f, 0.70f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.78f, 0.42f, 1f);

    private CanvasGroup fadeGroup;
    private TextMeshProUGUI title;
    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI controlsText;
    private TextMeshProUGUI[] buttons;
    private int selectedIndex;
    private float flickerTimer;
    private bool controlsOpen;

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        BuildMenu();
    }

    private void Update()
    {
        if (buttons == null || buttons.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) ChangeSelection(-1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) ChangeSelection(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) ActivateSelection();
        if (Input.GetKeyDown(KeyCode.Escape) && controlsOpen) CloseControls();

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
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject background = CreateUIObject("Background Art", canvas.transform);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.sprite = CreateBackgroundSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        Stretch(background.transform);

        GameObject overlay = CreateUIObject("Dark Overlay", canvas.transform);
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.34f);
        Stretch(overlay.transform);

        GameObject scanlines = CreateUIObject("VHS Scanlines", canvas.transform);
        Image scanlineImage = scanlines.AddComponent<Image>();
        scanlineImage.sprite = CreateScanlineSprite();
        scanlineImage.color = new Color(1f, 1f, 1f, 0.08f);
        scanlineImage.type = Image.Type.Tiled;
        Stretch(scanlines.transform);

        GameObject panel = CreateUIObject("Menu Panel", canvas.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.055f, 0.105f);
        panelRect.anchorMax = new Vector2(0.445f, 0.895f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        title = CreateText("NEVER MISS\nTHE BUS", panel.transform, 72, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = textColor;
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.94f));

        subtitle = CreateText("THE LAST BUS IS WAITING.", panel.transform, 18, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = new Color(textColor.r, textColor.g, textColor.b, 0.65f);
        SetAnchors(subtitle.rectTransform, new Vector2(0.08f, 0.60f), new Vector2(0.92f, 0.66f));

        string[] labels = { "START GAME", "CONTROLS", "QUIT" };
        buttons = new TextMeshProUGUI[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = CreateText(labels[i], panel.transform, 28, FontStyles.Normal, TextAlignmentOptions.Left);
            float top = 0.48f - i * 0.105f;
            SetAnchors(buttons[i].rectTransform, new Vector2(0.17f, top - 0.055f), new Vector2(0.83f, top + 0.055f));
            buttons[i].raycastTarget = false;
        }

        controlsText = CreateText("CONTROLS\n\nW A S D     MOVE\nSHIFT       RUN\nC           CROUCH\nSPACE       JUMP\nF           FLASHLIGHT\nMOUSE       LOOK\n\nESC         BACK", canvas.transform, 22, FontStyles.Normal, TextAlignmentOptions.Left);
        controlsText.color = textColor;
        SetAnchors(controlsText.rectTransform, new Vector2(0.58f, 0.25f), new Vector2(0.88f, 0.78f));
        controlsText.gameObject.SetActive(false);

        TextMeshProUGUI hint = CreateText("W / S  SELECT     ENTER  CONFIRM", canvas.transform, 15, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.48f);
        SetAnchors(hint.rectTransform, new Vector2(0.055f, 0.045f), new Vector2(0.445f, 0.085f));

        TextMeshProUGUI version = CreateText("NMTB // 01", canvas.transform, 13, FontStyles.Normal, TextAlignmentOptions.Right);
        version.color = new Color(textColor.r, textColor.g, textColor.b, 0.32f);
        SetAnchors(version.rectTransform, new Vector2(0.88f, 0.025f), new Vector2(0.975f, 0.055f));

        fadeGroup = canvasObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        StartCoroutine(FadeIn());
        UpdateSelection();
    }

    private Sprite CreateBackgroundSprite()
    {
        const int width = 320;
        const int height = 180;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color sky = new Color(0.025f, 0.034f, 0.035f);
        Color ground = new Color(0.045f, 0.055f, 0.047f);
        for (int y = 0; y < height; y++)
        {
            Color row = Color.Lerp(ground, sky, y / (float)height);
            for (int x = 0; x < width; x++) texture.SetPixel(x, y, row);
        }

        // Wet road.
        for (int y = 0; y < 72; y++)
        {
            int left = 125 - y / 4;
            int right = 250 + y / 3;
            FillRect(texture, left, y, right - left, 1, new Color(0.055f, 0.058f, 0.052f));
        }

        // Forest silhouettes.
        for (int i = 0; i < 18; i++)
            DrawTree(texture, 8 + i * 17 + (i % 3) * 4, 52 + (i % 4) * 7, 35 + (i % 5) * 9);

        // Bus stop sign.
        FillRect(texture, 178, 54, 4, 62, new Color(0.08f, 0.08f, 0.07f));
        FillRect(texture, 171, 107, 18, 23, new Color(0.07f, 0.075f, 0.07f));
        FillRect(texture, 174, 110, 12, 17, new Color(0.13f, 0.13f, 0.11f));
        FillRect(texture, 177, 115, 6, 5, new Color(0.25f, 0.24f, 0.18f));

        // Street lamp.
        FillRect(texture, 207, 76, 3, 70, new Color(0.08f, 0.08f, 0.07f));
        FillRect(texture, 207, 144, 21, 3, new Color(0.08f, 0.08f, 0.07f));
        FillRect(texture, 223, 140, 7, 4, new Color(0.72f, 0.64f, 0.43f));

        // Old yellow bus.
        FillRect(texture, 228, 35, 70, 76, new Color(0.28f, 0.25f, 0.12f));
        FillRect(texture, 232, 42, 62, 52, new Color(0.16f, 0.16f, 0.13f));
        FillRect(texture, 228, 35, 70, 8, new Color(0.33f, 0.29f, 0.13f));
        FillRect(texture, 231, 94, 64, 15, new Color(0.36f, 0.31f, 0.12f));
        FillRect(texture, 237, 50, 12, 28, new Color(0.07f, 0.08f, 0.075f));
        FillRect(texture, 252, 50, 12, 28, new Color(0.07f, 0.08f, 0.075f));
        FillRect(texture, 267, 50, 18, 28, new Color(0.07f, 0.08f, 0.075f));
        FillRect(texture, 236, 96, 10, 8, new Color(0.78f, 0.67f, 0.39f));
        FillRect(texture, 280, 96, 10, 8, new Color(0.78f, 0.67f, 0.39f));
        FillRect(texture, 252, 39, 26, 5, new Color(0.65f, 0.44f, 0.16f));

        // Low-resolution VHS noise.
        Random.InitState(1337);
        for (int i = 0; i < 2600; i++)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            Color old = texture.GetPixel(x, y);
            float n = Random.Range(-0.035f, 0.035f);
            texture.SetPixel(x, y, new Color(Mathf.Clamp01(old.r + n), Mathf.Clamp01(old.g + n), Mathf.Clamp01(old.b + n), 1f));
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateScanlineSprite()
    {
        Texture2D texture = new Texture2D(2, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < 4; y++)
        {
            Color c = y == 0 ? new Color(0, 0, 0, 0.45f) : new Color(0, 0, 0, 0f);
            texture.SetPixel(0, y, c);
            texture.SetPixel(1, y, c);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 2, 4), new Vector2(0.5f, 0.5f), 1f);
    }

    private static void DrawTree(Texture2D texture, int x, int baseY, int height)
    {
        Color trunk = new Color(0.035f, 0.04f, 0.034f);
        Color leaves = new Color(0.018f, 0.028f, 0.026f);
        int trunkW = Mathf.Max(2, height / 12);
        FillRect(texture, x - trunkW / 2, baseY, trunkW, height / 2, trunk);
        for (int y = baseY + height / 3; y < baseY + height; y += 6)
        {
            int half = Mathf.RoundToInt((y - baseY) * 0.22f);
            FillRect(texture, x - half, y, half * 2 + 1, 8, leaves);
        }
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        int maxX = texture.width - 1;
        int maxY = texture.height - 1;
        for (int px = Mathf.Max(0, x); px <= Mathf.Min(maxX, x + width - 1); px++)
            for (int py = Mathf.Max(0, y); py <= Mathf.Min(maxY, y + height - 1); py++)
                texture.SetPixel(px, py, color);
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
        if (controlsOpen) return;
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
        if (controlsOpen) return;
        switch (selectedIndex)
        {
            case 0: StartCoroutine(StartGame()); break;
            case 1: ShowControls(); break;
            case 2: Application.Quit(); break;
        }
    }

    private void ShowControls()
    {
        controlsOpen = true;
        controlsText.gameObject.SetActive(true);
        foreach (TextMeshProUGUI button in buttons) button.gameObject.SetActive(false);
        subtitle.text = "HOW TO SURVIVE THE NIGHT";
    }

    private void CloseControls()
    {
        controlsOpen = false;
        controlsText.gameObject.SetActive(false);
        foreach (TextMeshProUGUI button in buttons) button.gameObject.SetActive(true);
        subtitle.text = "THE LAST BUS IS WAITING.";
        UpdateSelection();
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
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
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
