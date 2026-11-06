using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One continuous story using the scene's original Player and bus.</summary>
public partial class StoryCutsceneDirector : MonoBehaviour
{
    [Serializable] public class Actor
    {
        [Tooltip("Display name only. The internal roles (Ethan, Lucas, Noah, Bus Driver) and story routing stay unchanged.")]
        public string name;
        public Color color = Color.white;
        [HideInInspector] public float visualScale = 1; // Legacy; boys now match the Player body.
        [Tooltip("A billboard sprite prefab or other visual. Its origin should be at the character's feet.")]
        public GameObject artPrefab;
        [Tooltip("Optional replacement sprite. Names can be changed without changing the internal role.")]
        public Sprite bodySprite;
        public Sprite portrait; // Kept for compatibility with existing scene serialization.
    }
    [Serializable] public class ActorPlacement
    {
        public string actorName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
    }
    [Serializable] public class Shot
    {
        public string title;
        public int chapter;
        public Transform stage;
        [Tooltip("Camera position relative to Stage; used while this scene is a cutscene.")]
        public Vector3 cameraOffset = new Vector3(-4, 3, 8);
        public Vector3 lookAtOffset = Vector3.up;
        [Tooltip("Optional positions for each character, relative to Stage.")]
        public ActorPlacement[] actorPositions;
        [TextArea(4, 18)] public string dialogue;
    }

    [Header("Story")]
    public bool playOnStart = true;
    public bool firstPersonStory = true;
    [Serializable] public class ChapterIntro
    {
        public int chapter = 1;
        public string heading = "CHAPTER 1";
        public string subtitle = "THE LAST BUS";
        [Tooltip("Optional full chapter artwork, laid out like the Chapter 1 reference. Without artwork, editable TMP text is used.")]
        public Texture2D artwork;
    }
    [Header("Chapter intro")]
    public bool showOpeningChapter = true;
    public ChapterIntro[] chapterIntros = {
        new ChapterIntro(),
        new ChapterIntro { chapter = 2, heading = "CHAPTER 2", subtitle = "" },
        new ChapterIntro { chapter = 3, heading = "CHAPTER 3", subtitle = "" }
    };
    public TMP_FontAsset chapterFont;
    [Min(.02f)] public float chapterSecondsPerCharacter = .18f;
    [Min(0)] public float chapterLinePause = .5f;
    [Min(0)] public float chapterHoldSeconds = 1.8f;
    [Min(.1f)] public float chapterFadeSeconds = 1.2f;
    public AudioClip chapterTypingSound;
    [Range(0, 1)] public float chapterTypingVolume = .4f;
    GameObject chapterOverlay;
    AudioClip generatedChapterTick;
    [Range(.005f, .1f)] public float secondsPerCharacter = .025f;
    [Header("Fade durations (seconds)")]
    [Min(.01f)] public float openingFadeInSeconds = 2.8f;
    [Min(0)] public float openingSchoolHoldSeconds = 1.8f;
    [Min(.01f)] public float defaultFadeOutSeconds = .45f;
    [Min(.01f)] public float defaultFadeInSeconds = .45f;
    [Min(.01f)] public float busStopFadeOutSeconds = .65f;
    [Min(.01f)] public float busStopFadeInSeconds = .65f;
    [Min(.01f)] public float boardingFadeOutSeconds = .45f;
    [Min(.01f)] public float boardingFadeInSeconds = .45f;
    [Min(.01f)] public float seatingFadeOutSeconds = .7f;
    [Min(.01f)] public float seatingFadeInSeconds = .7f;
    [Min(.01f)] public float crashFadeOutSeconds = 1.15f;
    [Min(.01f)] public float crashFadeInSeconds = 1.15f;
    [Tooltip("Seconds to turn the Player's view toward the front of the bus before closing their eyes.")]
    [Min(.1f)] public float sleepLookForwardSeconds = 1f;
    [Min(0)] public float sleepBeforeFadeSeconds = 2.5f;
    [Min(0)] public float sleepToCrashBlackHoldSeconds = 2.2f;
    public Actor[] actors = {
        new Actor { name="Mass", color=new Color(.25f,.65f,.9f) },
        new Actor { name="Dimash", color=new Color(.95f,.65f,.2f) },
        new Actor { name="Aremi", color=new Color(.35f,.85f,.55f) },
        new Actor { name="Bus Driver", color=new Color(.65f,.25f,.25f) },
        new Actor { name="Dog", color=new Color(.65f,.45f,.25f) }
    };
    static readonly string[] BoyRoles = { "Ethan", "Lucas", "Noah" };
    string DisplayName(string role)
    {
        for (int i = 0; actors != null && i < actors.Length && i < 4; i++)
            if (role == (i < BoyRoles.Length ? BoyRoles[i] : "Bus Driver") &&
                actors[i] != null && !string.IsNullOrWhiteSpace(actors[i].name))
                return actors[i].name.Trim();
        return role;
    }
    string ActorRole(string name)
    {
        if (name == "Ethan" || name == "Lucas" || name == "Noah" || name == "Bus Driver") return name;
        for (int i = 0; actors != null && i < actors.Length && i < 4; i++)
            if (actors[i] != null && string.Equals(actors[i].name?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i < BoyRoles.Length ? BoyRoles[i] : "Bus Driver";
        return name;
    }
    public Shot[] shots;
    public TextAsset story;
    public Transform[] stages;
    [Header("School entrance")]
    public Transform schoolExit;
    [Min(1)] public float schoolWalkDistance = 5;
    [Min(1)] public float schoolWalkDuration = 4;
    [Tooltip("Direction out of the selected school doorway, in its local coordinates.")]
    public Vector3 schoolWalkLocalDirection = Vector3.forward;
    [Min(0)] public float actorGroundClearance = .03f;
    public bool IsPlaying { get; private set; }
    public bool IsDialogueLinePlaying { get; private set; }
    public bool IsScreenFaded => fade != null && fade.alpha > .02f;
    public int CurrentScene { get; private set; }
    public string OpeningCheckpoint { get; private set; } = "school";
    GameSaveState resumeState;

    readonly List<Behaviour> suspended = new List<Behaviour>();
    readonly List<GameObject> hidden = new List<GameObject>();
    readonly List<Material> materials = new List<Material>();
    Camera gameCamera, storyCamera;
    Canvas canvas;
    CanvasGroup fade;
    TMP_Text subtitle, hint;
    Coroutine playback;
    CursorLockMode oldLock;
    bool oldCursor, advance;

    void Awake()
    {
        walkBobProperties = new MaterialPropertyBlock();
        HideSceneGlasses();
        if (GetComponent<GamePauseMenu>() == null) gameObject.AddComponent<GamePauseMenu>();
        // Cover the first rendered frame; Start waits one frame for scene references.
        BuildUI();
        fade.alpha = 1;
        canvas.gameObject.SetActive(true);
    }

    IEnumerator Start()
    {
        yield return null;
        if (story == null) story = Resources.Load<TextAsset>("NeverMissTheBusStory");
        resumeState = GameSaveStore.LoadGame();
        if (resumeState != null && resumeState.sceneName != gameObject.scene.name) resumeState = null;
        BuildPlaceholderStory();
        PrepareTerrainSurface();
        ApplyAtmosphere(false);
        BuildCityOutskirts();
        if (playOnStart) Play();
        else canvas.gameObject.SetActive(false);
    }
    void Update()
    {
        if (IsPlaying && Time.timeScale > 0)
            advance |= Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }
    public void Play() => PlayFrom(0);
    public void PlayFrom(int scene)
    {
        if (IsPlaying || canvas == null) return;
        gameCamera=Camera.main;
        if (gameCamera == null) { Debug.LogError("Story requires the Player Main Camera.",this); return; }
        HideSceneGlasses();
        PrepareBusSequence();
        if (!BusReady || schoolExit == null) { Debug.LogError("Assign the school entrance, Player, bus and driver in the Inspector.",this); return; }
        AttachSceneGlassesToBus();
        playerCrosshair?.SetStoryControl(false);
        oldLock=Cursor.lockState; oldCursor=Cursor.visible;
        foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (b.enabled && (b is PlayerMovement || b is MouseLook || b is InventorySystem || b is PickUp || b is Flashlight || b is DialogueManager))
            { suspended.Add(b); b.enabled=false; }
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != canvas && c != playerCrosshair?.CrosshairCanvas && c.renderMode != RenderMode.WorldSpace &&
                c.GetComponentInParent<GamePauseMenu>() == null && c.gameObject.activeSelf)
            { hidden.Add(c.gameObject); c.gameObject.SetActive(false); }
        var cameraObject=new GameObject("Story Camera"); cameraObject.transform.SetParent(transform);
        cameraObject.tag="MainCamera";
        storyCamera=cameraObject.AddComponent<Camera>(); storyCamera.CopyFrom(gameCamera);
        storyCamera.fieldOfView=55; storyCamera.enabled=true; gameCamera.enabled=false;
        fade.alpha = scene == 0 && (resumeState == null || resumeState.checkpoint == "school") ? 1 : 0;
        canvas.gameObject.SetActive(true); IsPlaying=true;
        playback=StartCoroutine(RunFirstPerson(Mathf.Clamp(scene,0,17)));
    }
    public void Stop()
    {
        if (playback!=null) StopCoroutine(playback);
        playback=null; Restore();
    }
    public GameSaveState CaptureSaveState()
    {
        return new GameSaveState {
            sceneName = gameObject.scene.name,
            checkpoint = OpeningCheckpoint,
            hasPlayerPosition = existingPlayer != null,
            playerPosition = existingPlayer != null ? existingPlayer.position : Vector3.zero,
            playerRotation = existingPlayer != null ? existingPlayer.rotation : Quaternion.identity,
            storyScene = CurrentScene,
            glassesCollected = glassesCollected,
            driverSeatFound = driverSeatFound
        };
    }
    void Restore()
    {
        if (!IsPlaying) return;
        IsPlaying=false;
        IsDialogueLinePlaying=false;
        if (chapterOverlay != null) Destroy(chapterOverlay);
        CleanupFirstPerson();
        RestoreBusSequence();
        foreach (var b in suspended) if (b!=null) b.enabled=true;
        playerCrosshair?.SetStoryControl(true);
        suspended.Clear();
        foreach (var g in hidden) if(g!=null) g.SetActive(true);
        hidden.Clear();
        if (storyCamera!=null) { storyCamera.enabled=false; Destroy(storyCamera.gameObject); }
        if (gameCamera!=null) gameCamera.enabled=true;
        canvas.gameObject.SetActive(false);
        Cursor.lockState=oldLock; Cursor.visible=oldCursor;
    }
    void OnDisable() => Stop();
    void OnDestroy()
    {
        if (generatedChapterTick != null) Destroy(generatedChapterTick);
        RestoreTerrainSurface();
        foreach(var material in materials) if(material!=null && Application.isPlaying) Destroy(material);
    }

    public void BuildPlaceholderStory()
    {
        if (story == null) story=Resources.Load<TextAsset>("NeverMissTheBusStory");
        if (story == null) { Debug.LogError("Missing NeverMissTheBusStory text asset.",this); return; }
        var sections=story.text.Replace("\r", "").Split(new[]{"#SCENE "},StringSplitOptions.RemoveEmptyEntries);
        var previous=shots;
        shots=new Shot[sections.Length];
        for(int i=0;i<sections.Length;i++)
        {
            int newline=sections[i].IndexOf('\n'); if(newline<0)continue;
            var header=sections[i].Substring(0,newline).Split('|');
            Shot old=previous!=null && i<previous.Length?previous[i]:null;
            shots[i]=new Shot {
                title=header[0], chapter=int.Parse(header[1]),
                stage=old!=null?old.stage:stages!=null && int.Parse(header[2])<stages.Length?stages[int.Parse(header[2])]:null,
                cameraOffset=old!=null?old.cameraOffset:new Vector3(-4,3,8),
                lookAtOffset=old!=null?old.lookAtOffset:Vector3.up,
                actorPositions=old!=null?old.actorPositions:null,
                dialogue=sections[i].Substring(newline+1).Trim()
            };
        }
    }
    public void EnsureActorPlacements()
    {
        if (shots == null || shots.Length == 0) BuildPlaceholderStory();
        string[] names = { "Ethan", "Lucas", "Noah" };
        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i] == null || (shots[i].actorPositions != null && shots[i].actorPositions.Length > 0)) continue;
            shots[i].actorPositions = new ActorPlacement[names.Length];
            Transform anchor = i == 0 && schoolExit != null ? schoolExit : shots[i].stage;
            Vector3 worldCenter = i == 0 && schoolExit != null
                ? schoolExit.position
                : anchor != null ? anchor.position : Vector3.zero;
            for (int n = 0; n < names.Length; n++)
            {
                Vector3 world = worldCenter + Vector3.right * (n - 1) * 1.3f;
                shots[i].actorPositions[n] = new ActorPlacement {
                    actorName = names[n],
                    localPosition = anchor != null ? anchor.InverseTransformPoint(world) : world
                };
            }
        }
    }
    IEnumerator FadeTo(float alpha, float seconds = -1)
    {
        if (seconds <= 0) seconds = alpha > fade.alpha ? defaultFadeOutSeconds : defaultFadeInSeconds;
        while(!Mathf.Approximately(fade.alpha,alpha))
        {
            if (Time.timeScale > 0)
                fade.alpha=Mathf.MoveTowards(fade.alpha,alpha,Time.unscaledDeltaTime/Mathf.Max(.01f,seconds));
            yield return null;
        }
    }
    IEnumerator Say(string who,string words,bool focusSpeaker=true)
    {
        bool restoreMovement = playerMovement != null && playerMovement.enabled;
        if (restoreMovement) playerMovement.enabled = false;
        IsDialogueLinePlaying=true;
        subtitle.transform.parent.gameObject.SetActive(true);
        subtitle.text=DisplayName(ActorRole(who))+": "+words; subtitle.maxVisibleCharacters=0; subtitle.ForceMeshUpdate(); hint.text="";
        if (focusSpeaker && !FocusInvestigationLine(words)) FocusSpeaker(who);
        int length=subtitle.textInfo.characterCount;
        for(float shown=0;shown<length;shown+=Time.timeScale>0?Time.unscaledDeltaTime/Mathf.Max(.005f,secondsPerCharacter):0)
        { subtitle.maxVisibleCharacters=(int)shown; advance=false; yield return null; }
        subtitle.maxVisibleCharacters=int.MaxValue; advance=false; hint.text="▼";
        yield return null;
        while(!advance) yield return null;
        advance=false; hint.text="";
        if(dialogueAudio!=null && advanceBlip!=null) dialogueAudio.PlayOneShot(advanceBlip,.25f);
        IsDialogueLinePlaying=false;
        if (restoreMovement && playerMovement != null) playerMovement.enabled = true;
    }
    IEnumerator SpeakScene(int index)
    {
        CurrentScene=index;
        if (index>=shots.Length || shots[index]==null)yield break;
        foreach(string raw in shots[index].dialogue.Split('\n'))
        {
            int separator=raw.IndexOf('|'); if(separator<0)continue;
            string who=raw.Substring(0,separator).Trim();
            if(who=="ACTION")
            {
                if(index>=5 && index<=7) yield return ForestStoryAction(index,raw.Substring(separator+1).Trim());
                continue;
            }
            if(who=="SFX")continue;
            yield return Say(who,raw.Substring(separator+1).Trim());
        }
    }
    void SetObjective(string text)
    {
        subtitle.transform.parent.gameObject.SetActive(text.Length>0);
        subtitle.text=text; subtitle.maxVisibleCharacters=int.MaxValue; hint.text="";
    }
    void BuildUI()
    {
        canvas=new GameObject("Story subtitles",typeof(RectTransform)).AddComponent<Canvas>(); canvas.transform.SetParent(transform);
        canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=300;
        var scaler=canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        var panel=UIRect("Dialogue background",canvas.transform,new Vector2(.08f,.035f),new Vector2(.92f,.20f));
        var background=panel.gameObject.AddComponent<Image>(); background.color=new Color(.015f,.02f,.03f,.91f);
        subtitle=UIRect("Name and dialogue",panel,new Vector2(.03f,.15f),new Vector2(.96f,.93f)).gameObject.AddComponent<TextMeshProUGUI>();
        subtitle.fontSize=31; subtitle.enableAutoSizing=true; subtitle.fontSizeMin=20; subtitle.fontSizeMax=31;
        subtitle.color=new Color(.95f,.94f,.87f); subtitle.raycastTarget=false;
        hint=UIRect("Continue",panel,new Vector2(.88f,.02f),new Vector2(.98f,.22f)).gameObject.AddComponent<TextMeshProUGUI>();
        hint.fontSize=20; hint.alignment=TextAlignmentOptions.Right; hint.raycastTarget=false;
        panel.gameObject.SetActive(false);
        var black=UIRect("Black fade",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>(); black.color=Color.black;
        fade=black.gameObject.AddComponent<CanvasGroup>(); fade.alpha=0; fade.blocksRaycasts=false;
        canvas.gameObject.SetActive(false);
    }
    RectTransform UIRect(string title,Transform parent,Vector2 min,Vector2 max)
    {
        var go=new GameObject(title,typeof(RectTransform)); var rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    void Shape(Transform parent,string title,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
    {
        var go=GameObject.CreatePrimitive(type);go.name=title;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
        var material=new Material(shader);material.color=color;materials.Add(material);go.GetComponent<Renderer>().sharedMaterial=material;
    }
    void Label(Transform parent,string value,Vector3 position)
    {
        var go=new GameObject(value);go.transform.SetParent(parent,false);go.transform.position=position;
        var label=go.AddComponent<TextMeshPro>();label.text=value;label.fontSize=2.5f;label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(12,1);label.color=Color.white;
    }

    // Yield this from a story transition before revealing a later chapter.
    public IEnumerator ShowChapterIntro(int chapter)
    {
        ChapterIntro data = Array.Find(chapterIntros ?? Array.Empty<ChapterIntro>(), x => x != null && x.chapter == chapter);
        if (data == null) yield break;
        chapterOverlay = new GameObject("Chapter intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        chapterOverlay.transform.SetParent(transform, false);
        var overlay = chapterOverlay.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 350;
        var scaler = chapterOverlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        UIRect("Black background", overlay.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>().color = Color.black;
        var content = UIRect("Chapter lettering", overlay.transform, Vector2.zero, Vector2.one);
        var opacity = content.gameObject.AddComponent<CanvasGroup>();
        Texture2D art = data.artwork;
        if (art == null && chapter == 1 && data.heading == "CHAPTER 1" && data.subtitle == "THE LAST BUS")
            art = Resources.Load<Texture2D>("Chapter1Title");
        var title = ChapterLine(content, "Heading", data.heading, art, new Rect(.20f, .46f, .60f, .30f),
            new Vector2(.20f, .46f), new Vector2(.80f, .76f), 140);
        var subtitleLine = ChapterLine(content, "Subtitle", data.subtitle, art, new Rect(.25f, .25f, .50f, .20f),
            new Vector2(.25f, .25f), new Vector2(.75f, .45f), 95);
        if (chapterTypingSound == null && generatedChapterTick == null)
        {
            const int rate = 22050;
            var samples = new float[660];
            var noise = new System.Random(41);
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = Mathf.Exp(-8f * i / samples.Length);
                samples[i] = ((float)noise.NextDouble() * 2 - 1) * envelope * .55f;
            }
            generatedChapterTick = AudioClip.Create("Chapter typewriter tick", samples.Length, 1, rate, false);
            generatedChapterTick.SetData(samples, 0);
        }
        AudioClip tick = chapterTypingSound != null ? chapterTypingSound : generatedChapterTick;
        yield return TypeChapterLine(title, data.heading, tick);
        yield return WaitUnpaused(chapterLinePause);
        yield return TypeChapterLine(subtitleLine, data.subtitle, tick);
        yield return WaitUnpaused(chapterHoldSeconds);
        for (float elapsed = 0; elapsed < chapterFadeSeconds;)
        {
            if (Time.timeScale > 0) elapsed += Time.unscaledDeltaTime;
            opacity.alpha = 1 - Mathf.Clamp01(elapsed / Mathf.Max(.1f, chapterFadeSeconds));
            yield return null;
        }
        Destroy(chapterOverlay);
        chapterOverlay = null;
        advance = false;
    }

    RectTransform ChapterLine(RectTransform parent, string name, string text, Texture2D art,
        Rect uv, Vector2 min, Vector2 max, float size)
    {
        var line = UIRect(name, parent, min, max);
        if (art != null)
        {
            // A clipping panel reveals the supplied lettering without replacing its font.
            line.gameObject.AddComponent<RectMask2D>();
            var picture = UIRect("Artwork", line, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            picture.texture = art; picture.uvRect = uv; picture.raycastTarget = false;
            // Keep the artwork at its full width as the clipping panel grows.
            picture.rectTransform.anchorMax = new Vector2(0, 1);
            picture.rectTransform.pivot = new Vector2(0, .5f);
            Canvas.ForceUpdateCanvases();
            picture.rectTransform.sizeDelta = new Vector2(line.rect.width, 0);
        }
        else
        {
            var label = line.gameObject.AddComponent<TextMeshProUGUI>();
            if (chapterFont != null) label.font = chapterFont;
            label.text = text ?? ""; label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
            label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = size;
            label.maxVisibleCharacters = 0; label.raycastTarget = false;
        }
        line.gameObject.SetActive(false);
        return line;
    }

    IEnumerator TypeChapterLine(RectTransform line, string text, AudioClip tick)
    {
        text = text ?? "";
        Vector2 fullAnchor = line.anchorMax;
        var label = line.GetComponent<TMP_Text>();
        if (label == null) line.anchorMax = new Vector2(line.anchorMin.x, fullAnchor.y);
        line.gameObject.SetActive(true);
        for (int i = 0; i < text.Length; i++)
        {
            yield return WaitUnpaused(chapterSecondsPerCharacter);
            if (label != null) label.maxVisibleCharacters = i + 1;
            else line.anchorMax = new Vector2(Mathf.Lerp(line.anchorMin.x, fullAnchor.x, (i + 1f) / text.Length), fullAnchor.y);
            if (!char.IsWhiteSpace(text[i]) && tick != null && dialogueAudio != null)
                dialogueAudio.PlayOneShot(tick, chapterTypingVolume);
        }
    }
}
