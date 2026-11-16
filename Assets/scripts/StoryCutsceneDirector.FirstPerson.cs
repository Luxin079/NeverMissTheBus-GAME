using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class StoryCutsceneDirector
{
    [Header("Sequence and pickups")]
    [Tooltip("Sleep het complete brillen-GameObject uit de scene hierheen, inclusief PickUp en zijn eigen Press E-UI. Plaats het zelf op de chauffeursstoel.")]
    public GameObject sceneGlassesPickup;
    [HideInInspector] public Sprite brokenGlassesSprite; // Legacy serialized field.
    [HideInInspector] public GameObject glassesPickupPrefab; // Legacy serialized field; never instantiated.
    [HideInInspector] public GameObject pickupPromptTemplate; // Legacy serialized field; never cloned.
    public float busTravelDistance = 45;
    public float walkToBusSpeed = 3;
    public float runToBusSpeed = 5;
    [HideInInspector] public Vector3 lastStopOffset = new Vector3(0, 0, 360); // Legacy scene field; no longer used.
    public Transform townSource;
    public GameObject forestTreePrefab;
    [Header("Camera and actors")]
    public Vector3 schoolWideCameraOffset = new Vector3(-6, 5.5f, 12);
    [Tooltip("Static front-of-school shot shown through the opening fade.")]
    public Vector3 schoolEstablishingCameraOffset = new Vector3(-8, 6.5f, 17);
    public float schoolEstablishingLookHeight = 2.8f;
    [Min(0)] public float schoolExitStaggerSeconds = .8f;
    public Vector3 speakerCameraOffset = new Vector3(-2.5f, 2.2f, -4.5f);
    [Tooltip("Close speaker shot at school: sideways, height above face, distance in front.")]
    public Vector3 schoolSpeakerCloseOffset = new Vector3(-.6f, .35f, 2.4f);
    [Range(25, 65)] public float schoolSpeakerFieldOfView = 38f;
    [Tooltip("Camera offset in world metres while following the arriving bus: along its length, height, side.")]
    public Vector3 busStopCameraOffset = new Vector3(-8, 4.5f, 7);
    [Tooltip("Exact place under the bus shelter where the boys wait. Optional; otherwise Waiting Offset is used.")]
    public Transform waitingShelterAnchor;
    [Tooltip("Fine adjustment of the waiting row inside the selected shelter, in its local coordinates.")]
    public Vector3 waitingShelterLocalOffset;
    [Tooltip("Optional three empty GameObjects for the waiting row: Ethan/Player, Lucas, Noah. Place them at foot height under the shelter.")]
    public Transform[] waitingBoyPoints = new Transform[3];
    [Min(.4f)] public float waitingRowSpacing = .8f;
    [Tooltip("Fallback waiting place in world metres: X along bus length, Z toward the shelter.")]
    public Vector3 waitingOffset = new Vector3(-3, 0, 3.2f);
    [Tooltip("Optional exact position and rotation of the bus the boys miss outside school.")]
    public Transform missedBusSchoolAnchor;
    [HideInInspector] public Vector3 missedBusSchoolLocalOffset = new Vector3(0, 0, 18); // Legacy fallback.
    [Tooltip("Extra height above the road for the visual bus outside school.")]
    public float missedBusGroundClearance = .05f;
    [Tooltip("Direction of the school road, local to the school exit.")]
    public Vector3 missedBusSchoolTravelLocalDirection = Vector3.forward;
    [Tooltip("Optional exact destination for the bus missed at school; overrides its local travel direction.")]
    public Transform missedBusDepartureTarget;
    [Tooltip("Interior camera position in the bus prefab's local coordinates, ahead of the three seats.")]
    public Vector3 seatedCameraLocal = new Vector3(.23f, .23f, -.08f);
    [Min(0)] public float seatedBlackHoldSeconds = 1;
    [Range(35, 90)] public float seatedShotFieldOfView = 65;
    [Min(.02f)] public float cameraFollowSmoothTime = .28f;
    [Min(.1f)] public float cameraRotationSpeed = 5;
    [Min(4)] public float groupCameraDistance = 13;
    public float speakerLookHeight = .85f;
    public float friendWalkSpeed = 2.5f;
    [Header("Sprite walking bob")]
    [Min(0)] public float walkBobHeight = .025f;
    [Min(.2f)] public float walkBobStride = .85f;
    readonly Dictionary<Transform, Vector3> bobPreviousPosition = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, float> bobPhase = new Dictionary<Transform, float>();
    readonly Dictionary<Transform, float> bobOffset = new Dictionary<Transform, float>();
    MaterialPropertyBlock walkBobProperties;
    [Header("Back sprite colour matching")]
    [Range(0, 1)] public float backSpriteSaturation = .65f;
    [Range(.1f, 1.5f)] public float backSpriteBrightness = .67f;
    public Color backSpriteTint = new Color(.94f, .97f, 1f);

    void GroundStandingFriends()
    {
        foreach (var actor in friends.Values)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy || seatedSpriteActors.Contains(actor)) continue;
            var visual = actor.GetComponentInChildren<Renderer>();
            if (visual == null) continue;
            Vector3 feet = actor.position; feet.y = visual.bounds.min.y;
            float groundY = float.NegativeInfinity;
            // The school porch can sit above the terrain. Probe from the door
            // height, then select the upper walkable surface, not the terrain
            // closest to already-sunken feet.
            float probeY = CurrentScene == 0 && schoolExit != null
                ? Mathf.Max(feet.y + .6f, schoolExit.position.y + 1.5f) : feet.y + .6f;
            Vector3 probe = new Vector3(feet.x, probeY, feet.z);
            foreach (var hit in Physics.RaycastAll(probe, Vector3.down, Mathf.Max(1.8f,probeY-feet.y+1.2f), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .7f || hit.transform.IsChildOf(existingPlayer)) continue;
                bool character = false;
                foreach (var other in friends.Values)
                    if (other != null && hit.transform.IsChildOf(other)) { character = true; break; }
                if (character) continue;
                if (hit.point.y > groundY) groundY = hit.point.y;
            }
            if (!float.IsNegativeInfinity(groundY))
            {
                float correction = groundY + actorGroundClearance - feet.y;
                // Never sink into a step. Ease down from it instead of dropping instantly.
                actor.position += Vector3.up * (correction >= 0 ? correction :
                    Mathf.Max(correction, -2f * Time.deltaTime));
            }
        }
    }

    void UpdateWalkingBob()
    {
        foreach (var actor in friends.Values)
        {
            if (actor == null) continue;
            bobPreviousPosition.TryGetValue(actor, out var previous);
            Vector3 delta = actor.position - previous;
            delta.y = 0;
            float distance = bobPreviousPosition.ContainsKey(actor) ? delta.magnitude : 0;
            bobPreviousPosition[actor] = actor.position;
            bobPhase.TryGetValue(actor, out float phase);
            bobOffset.TryGetValue(actor, out float offset);
            // Ignore staging teleports and animate only actual horizontal travel.
            bool moving = actor.gameObject.activeInHierarchy && !seatedSpriteActors.Contains(actor)
                && !IsDialogueLinePlaying && distance > .0005f && distance < .5f;
            if (moving) phase = Mathf.Repeat(phase + distance / Mathf.Max(.2f, walkBobStride) * Mathf.PI * 2, Mathf.PI * 2);
            float target = moving ? (1 - Mathf.Cos(phase)) * .5f * walkBobHeight : 0;
            offset = Mathf.MoveTowards(offset, target, Time.deltaTime * .3f);
            bobPhase[actor] = phase;
            bobOffset[actor] = offset;
            foreach (var visual in actor.GetComponentsInChildren<SpriteRenderer>())
            {
                visual.GetPropertyBlock(walkBobProperties);
                walkBobProperties.SetFloat("_WalkBob", offset);
                visual.SetPropertyBlock(walkBobProperties);
            }
        }
    }
    [Header("Companion follow")]
    [Min(.1f)] public float friendFollowStartDistance = .55f;
    [Min(.1f)] public float friendTrailingDistance = 2.1f;
    [Min(.1f)] public float friendFollowStopDistance = .25f;
    [Header("Boy visuals")]
    [Tooltip("Optional body renderer of the Player. Boys match this model's width and height, including SpriteRenderers. Defaults to the renderer on the Player.")]
    public Renderer playerSizeReference;
    Vector3 lastPlayerVisualSize;
    [Min(.5f)] public float schoolRowSpacing = 1.3f;
    [Tooltip("Bus movement axis in the selected bus prefab. X is along its length in the original bus model.")]
    public Vector3 busTravelLocalDirection = Vector3.right;
    [Min(1)] public float missedBusDepartureSpeed = 8;
    [Min(0)] public float missedBusDepartureHoldSeconds = 1;
    [Range(20, 65)] public float missedBusShotFieldOfView = 32;
    [Min(1)] public float lastBusArrivalSpeed = 9;
    [Min(1)] public float lastBusApproachDistance = 28;
    [Min(0)] public float crashShakeSeconds = .8f;
    [Min(0)] public float crashShakeStrength = .16f;
    [Header("Crash pacing and safe recovery")]
    [Min(.5f)] public float dramaticCrashSeconds = 2.6f;
    [Min(0)] public float crashRecoveryPause = 1.4f;
    [Tooltip("Optional empty in the clear aisle, at floor level, where the player stands after the crash dialogue.")]
    public Transform postCrashStandPoint;
    Vector3? lastWalkableBusLocal;
    public string busSpottingLine = "Hey, the bus!";
    [Tooltip("Off by default: leave the opening on the seated shot. Enable to play the later chapters too.")]
    public bool continueAfterSeating = true;
    [Min(0)] public float seatedShotHoldSeconds = 2;
    [HideInInspector] public Vector3 glassesLocalPosition = new Vector3(-.31f, .14f, -.09f); // Legacy placement.
    [Header("Crash investigation")]
    [Tooltip("Optional exact point in front of the empty driver's seat.")]
    public Transform driverSeatInspectionAnchor;
    [Tooltip("Fallback inspection point in the bus prefab's local coordinates.")]
    public Vector3 driverSeatInspectionLocal = new Vector3(-.28f, .10f, -.06f);
    [Min(.3f)] public float driverSeatInspectionRadius = .7f;
    [Tooltip("Optional camera target on the driver's seat. Defaults to the placed glasses.")]
    public Transform driverSeatLookTarget;
    [Min(.1f)] public float driverSeatLookSeconds = .6f;

    readonly Dictionary<string, Transform> friends = new Dictionary<string, Transform>();
    readonly Dictionary<string, Vector3> standingScales = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Vector3> friendFollowVelocity = new Dictionary<string, Vector3>();
    readonly List<GameObject> spawnedVisuals = new List<GameObject>();
    AudioSource dialogueAudio;
    AudioClip advanceBlip;
    enum CutsceneCameraMode { Fixed, Group, Speaker, MovingBus, ArrivingBus }
    CutsceneCameraMode cameraMode;
    Transform cameraSpeaker, cameraBus;
    Transform seatedLookTarget;
    bool investigationLookLocked;
    bool investigationLookAtGlasses;

    Vector3 InvestigationLookPoint()
    {
        if (investigationLookAtGlasses && sceneGlassesPickup != null)
        {
            var sprite = sceneGlassesPickup.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null) return sprite.bounds.center;
            return sceneGlassesPickup.transform.position;
        }
        return driverSeatLookTarget != null ? driverSeatLookTarget.position
            : existingDriver != null ? existingDriver.transform.position
            : existingBus.TransformPoint(driverSeatInspectionLocal) + Vector3.up * .65f;
    }

    bool FocusInvestigationLine(string words)
    {
        if (CurrentScene != 3 && CurrentScene != 4) return false;
        string line = words.ToLowerInvariant();
        if (line.Contains("where is the bus driver") || CurrentScene == 4)
            investigationLookLocked = true;
        if (CurrentScene == 4 && (line.Contains("blood") || line.Contains("glasses") || line == "look."))
            investigationLookAtGlasses = true;
        return investigationLookLocked;
    }
    bool holdSeatedView;
    Vector3 desiredCameraPosition, desiredCameraLook, cameraVelocity;
    bool cinematicCamera, glassesCollected, driverSeatFound, seatedDialogueCamera, boardingFirstPerson, exploringFirstPerson;
    float crashShakeUntil;
    bool gameCameraShaking;
    bool companionFollowReady;
    bool companionsFollowing;
    Vector3 lastPlayerFollowFeet, lastFollowDirection = Vector3.forward;
    PickUp glassesPickup;
    GameObject glassesRoot;

    void ResolveSceneGlasses()
    {
        if (sceneGlassesPickup != null && sceneGlassesPickup.scene.IsValid()) return;
        // The BrokenGlasses tag may be on the visual child of the complete pickup.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.gameObject.tag == "BrokenGlasses")
                {
                    var pickup = child.GetComponentInParent<PickUp>(true);
                    sceneGlassesPickup = pickup != null ? pickup.gameObject : child.gameObject;
                    return;
                }
    }

    void HideSceneGlasses()
    {
        ResolveSceneGlasses();
        if (sceneGlassesPickup != null && sceneGlassesPickup.scene.IsValid())
            sceneGlassesPickup.SetActive(false);
    }

    void AttachSceneGlassesToBus()
    {
        ResolveSceneGlasses();
        // Keep the scene-authored seat position while the bus drives to the stop.
        if (sceneGlassesPickup != null && sceneGlassesPickup.scene.IsValid() && existingBus != null &&
            (!sceneGlassesPickup.transform.IsChildOf(existingBus) ||
             (existingDriver != null && sceneGlassesPickup.transform.IsChildOf(existingDriver.transform))))
            sceneGlassesPickup.transform.SetParent(existingBus, true);
    }

    void ActivateSceneGlasses()
    {
        ResolveSceneGlasses();
        if (sceneGlassesPickup == null || !sceneGlassesPickup.scene.IsValid())
        {
            Debug.LogWarning("Assign the complete glasses GameObject FROM THE SCENE (not a prefab asset) in Story Cutscene Director > Sequence and pickups.", this);
            return;
        }
        if (glassesCollected) { sceneGlassesPickup.SetActive(false); return; }
        glassesRoot = sceneGlassesPickup;
        AttachSceneGlassesToBus();
        glassesPickup = glassesRoot.GetComponent<PickUp>();
        if (glassesPickup == null) glassesPickup = glassesRoot.GetComponentInChildren<PickUp>(true);
        if (glassesPickup == null)
        {
            Debug.LogWarning("The scene glasses GameObject needs its existing PickUp component on itself or a child.", glassesRoot);
            return;
        }
        // This prefab variant inherits the flashlight's pickup data. The glasses
        // are a quest item, not a second flashlight inventory slot.
        glassesPickup.questItem = true;
        glassesPickup.itemTag = "BrokenGlasses";
        foreach (var spinner in glassesRoot.GetComponentsInChildren<ItemSpin>(true))
            spinner.enabled = false;
        glassesPickup.Collected -= OnGlassesCollected;
        glassesPickup.Collected += OnGlassesCollected;
        glassesPickup.enabled = driverSeatFound;
        glassesRoot.SetActive(true);
    }
    Vector3 SchoolWalkDirection => schoolExit.TransformDirection(
        schoolWalkLocalDirection.sqrMagnitude > .001f ? schoolWalkLocalDirection.normalized : Vector3.forward);

    void LateUpdate()
    {
        if (musicSource != null) musicSource.volume = Mathf.MoveTowards(musicSource.volume,
            Time.timeScale <= 0 ? 0 : musicVolume * (IsDialogueLinePlaying ? .35f : 1f), Time.unscaledDeltaTime * .3f);
        if (IsPlaying) UpdateNightSounds();
        if (!IsPlaying || Time.timeScale <= 0) return;
        UpdateInteriorAtmosphere();
        UpdateWalkingBob();
        MatchFriendSizes();
        UpdateDirectionalSprites();
        GroundStandingFriends();
        if (!cinematicCamera)
        {
            if ((investigationLookLocked || (seatedDialogueCamera && !holdSeatedView)) && gameCamera != null && gameCamera.enabled && existingBus != null)
            {
                Vector3 lookAt = investigationLookLocked ? InvestigationLookPoint() : seatedLookTarget != null && seatedLookTarget.gameObject.activeInHierarchy
                    ? ActorLookPoint(seatedLookTarget) : SeatedForwardLook();
                Vector3 direction = lookAt - gameCamera.transform.position;
                if (direction.sqrMagnitude > .01f)
                    gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation,
                        Quaternion.LookRotation(direction),
                        1 - Mathf.Exp(-cameraRotationSpeed * Time.unscaledDeltaTime));
            }
            if (gameCamera != null && gameCamera.enabled && Time.unscaledTime < crashShakeUntil)
            {
                float scaleY = Mathf.Max(.01f, Mathf.Abs(existingPlayer.lossyScale.y));
                float remaining = Mathf.Clamp01((crashShakeUntil-Time.unscaledTime) / Mathf.Max(.1f, dramaticCrashSeconds));
                // Low-frequency, decaying movement; never throw the camera through a seat.
                Vector3 shake = new Vector3(Mathf.Sin(Time.unscaledTime*19), Mathf.Sin(Time.unscaledTime*13), 0);
                gameCamera.transform.localPosition = playableCameraLocal + shake * (Mathf.Min(.045f, crashShakeStrength) * remaining / scaleY);
                gameCameraShaking = true;
            }
            else if (gameCameraShaking && gameCamera != null)
            {
                gameCamera.transform.localPosition = playableCameraLocal;
                gameCameraShaking = false;
            }
            return;
        }
        if (storyCamera == null) return;
        if (cameraMode == CutsceneCameraMode.Group) UpdateGroupCameraTarget();
        else if (cameraMode == CutsceneCameraMode.Speaker && cameraSpeaker != null) UpdateSpeakerCameraTarget();
        else if (cameraMode == CutsceneCameraMode.MovingBus && cameraBus != null) UpdateMovingBusCameraTarget();
        else if (cameraMode == CutsceneCameraMode.ArrivingBus && cameraBus != null) UpdateArrivingBusCameraTarget();
        float delta = Time.unscaledDeltaTime;
        float fieldOfView = seatedDialogueCamera ? seatedShotFieldOfView :
            cameraMode == CutsceneCameraMode.MovingBus ? missedBusShotFieldOfView :
            CurrentScene == 0 && cameraMode == CutsceneCameraMode.Speaker ? schoolSpeakerFieldOfView : 55;
        storyCamera.fieldOfView = Mathf.MoveTowards(storyCamera.fieldOfView, fieldOfView, delta * 24);
        storyCamera.transform.position = Vector3.SmoothDamp(storyCamera.transform.position,
            desiredCameraPosition, ref cameraVelocity, cameraFollowSmoothTime, Mathf.Infinity, delta);
        if (Time.unscaledTime < crashShakeUntil)
            storyCamera.transform.position += Random.insideUnitSphere * crashShakeStrength;
        Vector3 aim = desiredCameraLook - storyCamera.transform.position;
        if (aim.sqrMagnitude > .01f)
            storyCamera.transform.rotation = Quaternion.Slerp(storyCamera.transform.rotation,
                Quaternion.LookRotation(aim), 1 - Mathf.Exp(-cameraRotationSpeed * delta));
    }

    void SetCamera(Vector3 position, Vector3 look, bool instant = false)
    {
        desiredCameraPosition = position; desiredCameraLook = look;
        cameraMode = CutsceneCameraMode.Fixed; cameraSpeaker = cameraBus = null;
        cinematicCamera = true;
        storyCamera.enabled = true; gameCamera.enabled = false;
        if (instant) { cameraVelocity = Vector3.zero; storyCamera.transform.position = position; storyCamera.transform.LookAt(look); }
    }

    void FollowGroup(bool instant = false)
    {
        cameraMode = CutsceneCameraMode.Group; cameraSpeaker = cameraBus = null;
        cinematicCamera = true; storyCamera.enabled = true; gameCamera.enabled = false;
        UpdateGroupCameraTarget();
        if (instant) { cameraVelocity = Vector3.zero; storyCamera.transform.position = desiredCameraPosition; storyCamera.transform.LookAt(desiredCameraLook); }
    }

    void FollowMovingBus(Transform target)
    {
        cameraBus = target; cameraSpeaker = null; cameraMode = CutsceneCameraMode.MovingBus;
        cinematicCamera = true; storyCamera.enabled = true; gameCamera.enabled = false;
        UpdateMovingBusCameraTarget();
    }

    void FollowArrivingBus(bool instant = false)
    {
        cameraBus = existingBus; cameraSpeaker = null; cameraMode = CutsceneCameraMode.ArrivingBus;
        cinematicCamera = true; storyCamera.enabled = true; gameCamera.enabled = false;
        UpdateArrivingBusCameraTarget();
        if (instant)
        {
            cameraVelocity = Vector3.zero;
            storyCamera.transform.position = desiredCameraPosition;
            storyCamera.transform.LookAt(desiredCameraLook);
        }
    }

    void UpdateArrivingBusCameraTarget()
    {
        desiredCameraPosition = cameraBus.position + existingBus.right * busStopCameraOffset.x +
            Vector3.up * busStopCameraOffset.y + existingBus.forward * busStopCameraOffset.z;
        desiredCameraLook = cameraBus.position + Vector3.up * 1.7f;
    }

    void UpdateMovingBusCameraTarget()
    {
        Vector3 look = cameraBus.position + Vector3.up * 1.5f;
        desiredCameraLook = look;
        // Pan from the established street-side school shot; following the bus itself
        // could put the camera through the school facade or the bus body.
        desiredCameraPosition = schoolExit.position + schoolExit.TransformDirection(schoolWideCameraOffset);
    }

    void GroundMissedBus(GameObject visualBus)
    {
        if (missedBusSchoolAnchor != null) return; // A placed anchor is fully user-controlled.
        bool foundRenderer = false;
        Bounds visibleBounds = default;
        foreach (var renderer in visualBus.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is SpriteRenderer) continue;
            if (foundRenderer) visibleBounds.Encapsulate(renderer.bounds);
            else { visibleBounds = renderer.bounds; foundRenderer = true; }
        }
        if (!foundRenderer) return;
        float groundY = schoolExit.position.y - .52f;
        RaycastHit[] hits = Physics.RaycastAll(visualBus.transform.position + Vector3.up * 30,
            Vector3.down, 80);
        float bestY = float.NegativeInfinity;
        foreach (var hit in hits)
            if (!hit.transform.IsChildOf(visualBus.transform) && hit.normal.y > .65f &&
                hit.point.y > bestY)
                bestY = hit.point.y;
        if (bestY > float.NegativeInfinity) groundY = bestY;
        visualBus.transform.position += Vector3.up * (groundY + missedBusGroundClearance - visibleBounds.min.y);
    }

    void UpdateGroupCameraTarget()
    {
        bool hasActor = false;
        Bounds group = default;
        foreach (var friend in friends.Values)
        {
            if (friend == null || !friend.gameObject.activeInHierarchy) continue;
            Vector3 center = ActorLookPoint(friend);
            if (!hasActor) { group = new Bounds(center, Vector3.one); hasActor = true; }
            else group.Encapsulate(center);
        }
        if (!hasActor) return;
        float distance = Mathf.Max(groupCameraDistance, group.extents.magnitude * 2.7f);
        Vector3 forward = CurrentScene == 0 && schoolExit != null ? schoolExit.forward : existingBus.forward;
        Vector3 right = CurrentScene == 0 && schoolExit != null ? schoolExit.right : existingBus.right;
        desiredCameraLook = group.center;
        desiredCameraPosition = CurrentScene == 0 && schoolExit != null
            ? group.center + schoolExit.TransformDirection(schoolWideCameraOffset)
            : group.center + (forward - right * .45f).normalized * distance + Vector3.up * distance * .35f;
    }

    Vector3 ActorLookPoint(Transform actor)
    {
        var renderer = actor.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center + Vector3.up * renderer.bounds.size.y * .28f
            : actor.position + Vector3.up * speakerLookHeight;
    }

    void UpdateSpeakerCameraTarget()
    {
        Vector3 look = ActorLookPoint(cameraSpeaker);
        if (seatedDialogueCamera)
        {
            // First-person seat view: face the front by default and glance at
            // whichever nearby passenger or the driver is speaking.
            desiredCameraPosition = existingBus.TransformPoint(seatedCameraLocal);
            if (cameraSpeaker == existingDriver.transform &&
                Vector3.Distance(look, desiredCameraPosition) > 12)
                look = existingBus.TransformPoint(new Vector3(-.34f, .20f, 0));
            desiredCameraLook = cameraSpeaker == friends["Ethan"]
                ? SeatedForwardLook() : look;
            return;
        }
        Vector3 offset = speakerCameraOffset;
        if (CurrentScene < shots.Length && shots[CurrentScene] != null)
            offset = shots[CurrentScene].cameraOffset;
        // Never put an opening dialogue camera behind the school facade.
        if (CurrentScene == 0)
        {
            offset = schoolSpeakerCloseOffset;
            offset.z = Mathf.Max(.5f, Mathf.Abs(offset.z));
        }
        Vector3 right = CurrentScene == 0 && schoolExit != null ? schoolExit.right : existingBus.right;
        Vector3 forward = CurrentScene == 0 && schoolExit != null ? schoolExit.forward : existingBus.forward;
        desiredCameraLook = look;
        desiredCameraPosition = look + right * offset.x + Vector3.up * offset.y + forward * offset.z;
    }

    void FocusSpeaker(string who)
    {
        Transform target = null;
        string role = ActorRole(who);
        if (role == "Bus Driver" && existingDriver != null && existingDriver.gameObject.activeSelf) target = existingDriver.transform;
        else friends.TryGetValue(role, out target);
        if (seatedDialogueCamera)
        {
            // Ethan is the first-person player. When he answers, keep looking at
            // the person who was speaking instead of snapping back down the aisle.
            holdSeatedView = role == "Ethan";
            if (!holdSeatedView && target != null) seatedLookTarget = target;
            return;
        }
        // After the crash exploration is already flagged, but seated dialogue
        // must still resolve its speaker before the exploration early-out.
        if (boardingFirstPerson || exploringFirstPerson) return;
        if (target == null) return;
        if (cameraSpeaker != target) cameraVelocity = Vector3.zero;
        cameraSpeaker = target;
        cameraMode = CutsceneCameraMode.Speaker;
        cinematicCamera = true;
        storyCamera.enabled = true; gameCamera.enabled = false;
        UpdateSpeakerCameraTarget();
    }

    Vector3 SeatedForwardLook()
    {
        return existingBus.TransformPoint(seatedCameraLocal + new Vector3(-.55f, -.025f, .025f));
    }

    readonly List<Sprite> winterSprites = new List<Sprite>();
    SpriteRenderer playerSpriteVisual;
    readonly Dictionary<Transform, Sprite[]> directionalSprites = new Dictionary<Transform, Sprite[]>();
    readonly Dictionary<Transform, Vector3> spriteLastPositions = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> spriteHeadings = new Dictionary<Transform, Vector3>();
    readonly HashSet<Transform> seatedSpriteActors = new HashSet<Transform>();

    Sprite PoseSprite(int role, bool seated, Sprite front)
    {
        var sheet = Resources.Load<Texture2D>("Characters/WinterBoysPoses");
        if (sheet == null || !sheet.isReadable) return null;
        int column = role == 0 ? 0 : role == 1 ? 2 : 1;
        int cellWidth = sheet.width / 3;
        int split = Mathf.RoundToInt(sheet.height * .46875f);
        int minX = (column + 1) * cellWidth, maxX = column * cellWidth;
        int minY = sheet.height, maxY = 0;
        var pixels = sheet.GetPixels32();
        for (int y = seated ? 0 : split; y < (seated ? split : sheet.height); y++)
            for (int x = column * cellWidth; x < (column + 1) * cellWidth; x++)
                if (pixels[y * sheet.width + x].a > 128)
                { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        if (maxX <= minX || maxY <= minY) return null;
        float height = maxY - minY + 1;
        var pose = Sprite.Create(sheet, new Rect(minX, minY, maxX-minX+1, height),
            new Vector2(.5f, seated ? .24f : 0), height / (front.bounds.size.y * (seated ? .78f : 1f)), 0, SpriteMeshType.FullRect);
        winterSprites.Add(pose);
        return pose;
    }

    void UpdateDirectionalSprites()
    {
        Camera viewer = cinematicCamera ? storyCamera : gameCamera;
        if (viewer == null) return;
        foreach (var pair in directionalSprites)
        {
            var actor = pair.Key;
            if (actor == null) continue;
            var visual = actor.GetComponent<SpriteRenderer>();
            if (visual == null) continue;
            Vector3 delta = spriteLastPositions.TryGetValue(actor, out var last) ? actor.position-last : Vector3.zero;
            spriteLastPositions[actor] = actor.position;
            delta.y = 0;
            if (delta.sqrMagnitude > .000001f && delta.sqrMagnitude < .25f) spriteHeadings[actor] = delta.normalized;
            bool seated = seatedSpriteActors.Contains(actor);
            Vector3 towardCamera = viewer.transform.position-actor.position; towardCamera.y = 0;
            bool actuallyWalking = Time.deltaTime > 0 && delta.magnitude > .08f * Time.deltaTime && delta.magnitude < .5f;
            bool gameplay = !cinematicCamera && !seatedDialogueCamera && !IsDialogueLinePlaying &&
                playerMovement != null && playerMovement.isActiveAndEnabled && Time.timeScale > 0;
            bool back = gameplay && !seated && actuallyWalking && Vector3.Dot(delta.normalized, towardCamera.normalized) < -.35f;
            visual.sprite = pair.Value[seated ? 2 : back ? 1 : 0] ?? pair.Value[0];
            if (visual.sharedMaterial != null && visual.sharedMaterial.HasProperty("_BackGrade"))
            {
                bool usingBack = back && pair.Value[1] != null;
                visual.sharedMaterial.SetFloat("_BackGrade", usingBack ? 1 : 0);
                visual.sharedMaterial.SetFloat("_BackSaturation", backSpriteSaturation);
                visual.sharedMaterial.SetFloat("_BackBrightness", backSpriteBrightness);
                visual.sharedMaterial.SetColor("_BackTint", backSpriteTint);
            }
            // Only the original front sheet needs the olive-to-purple conversion.
            // Back artwork already has its authored purple palette.
            if (visual.sharedMaterial != null && visual.sharedMaterial.HasProperty("_Purple"))
                visual.sharedMaterial.SetFloat("_Purple", visual.sprite == pair.Value[0] &&
                    friends.TryGetValue("Lucas", out var dimash) && dimash == actor &&
                    actors != null && actors.Length > 1 && actors[1].bodySprite == null ? 1 : 0);
            // Seated portraits look toward the viewer; mirror the profile on the other side of the aisle.
            visual.flipX = seated && existingBus != null && Vector3.Dot(towardCamera, existingBus.forward) < 0;
        }
    }
    Renderer originalPlayerVisual;
    bool originalPlayerVisualEnabled;

    Sprite WinterSprite(int role)
    {
        var actor = actors != null && role < actors.Length ? actors[role] : null;
        if (actor != null && actor.bodySprite != null) return actor.bodySprite;
        var sheet = Resources.Load<Texture2D>("Characters/WinterBoys");
        if (sheet == null) return null;
        // Source order: Mass (left), Aremi (middle), Dimash (right).
        Rect rect = role == 0 ? new Rect(12, 16, 490, 996)
            : role == 1 ? new Rect(1034, 16, 490, 954) : new Rect(514, 16, 508, 984);
        var sprite = Sprite.Create(sheet, rect, new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
        sprite.name = DisplayName(BoyRoles[role]);
        winterSprites.Add(sprite);
        return sprite;
    }

    SpriteRenderer AddWinterVisual(GameObject target, int role, Sprite sprite)
    {
        var visual = target.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        var shader = Resources.Load<Shader>("Characters/WinterSprite");
        if (shader != null)
        {
            var material = new Material(shader);
            material.SetFloat("_Purple", role == 1 && actors[role].bodySprite == null ? 1 : 0);
            visual.sharedMaterial = material;
            materials.Add(material);
        }
        // The new winter sprites use -Z; do not change existing sprites such as the driver.
        target.AddComponent<YBillboard>().frontFacesNegativeZ = true;
        // Use the original front portrait in bus seats, retaining walking back views.
        directionalSprites[target.transform] = new[] { sprite, PoseSprite(role, false, sprite), (Sprite)null };
        return visual;
    }

    void CreateFriends()
    {
        // Keep the authored controller and scale; the original mesh still defines body size.
        originalPlayerVisual = existingPlayer.GetComponent<Renderer>();
        var mainSprite = WinterSprite(0);
        if (mainSprite != null && originalPlayerVisual != null)
        {
            originalPlayerVisualEnabled = originalPlayerVisual.enabled;
            originalPlayerVisual.enabled = false;
            playerSizeReference = originalPlayerVisual;
            var body = new GameObject("Mass - Player Sprite");
            body.transform.SetParent(existingPlayer, false);
            playerSpriteVisual = AddWinterVisual(body, 0, mainSprite);
            Bounds bounds = originalPlayerVisual.localBounds;
            body.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            body.transform.localScale = new Vector3(bounds.size.x / mainSprite.bounds.size.x,
                bounds.size.y / mainSprite.bounds.size.y, 1);
            // First-person uses the real camera, not a sprite in front of the lens.
            // The story's Mass stand-in supplies the same artwork in group shots.
            playerSpriteVisual.enabled = false;
            spawnedVisuals.Add(body);
        }
        for (int i = 0; i < 3; i++)
        {
            Actor actor = actors != null && i < actors.Length ? actors[i] : null;
            string role = BoyRoles[i];
            GameObject go;
            if (actor != null && actor.artPrefab != null) go = Instantiate(actor.artPrefab);
            else if (WinterSprite(i) is Sprite sprite)
            {
                go = new GameObject(DisplayName(role));
                AddWinterVisual(go, i, sprite);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = actor != null ? actor.color : Color.white;
                materials.Add(material); go.GetComponent<Renderer>().sharedMaterial = material;
                // A Unity capsule is already twice as tall as it is wide.
                // Keep its transform uniform so the boys do not become needles.
                go.transform.localScale = Vector3.one * .85f;
            }
            go.name = "Story " + DisplayName(role);
            spawnedVisuals.Add(go); friends[role] = go.transform; standingScales[role] = go.transform.localScale;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (go.GetComponentInChildren<SpriteRenderer>() != null && go.GetComponentInChildren<YBillboard>() == null)
                go.AddComponent<YBillboard>();
        }
        lastPlayerVisualSize = Vector3.zero;
        MatchFriendSizes();
    }

    void MatchFriendSizes()
    {
        if (existingPlayer == null) return;
        if (playerSizeReference == null)
        {
            playerSizeReference = existingPlayer.GetComponent<Renderer>();
            if (playerSizeReference == null)
                foreach (var body in existingPlayer.GetComponentsInChildren<Renderer>(true))
                    if (body.GetComponentInParent<Camera>() == null && body.GetComponentInParent<Canvas>() == null)
                    { playerSizeReference = body; break; }
        }
        if (playerSizeReference == null) return;
        Vector3 size = Vector3.Scale(playerSizeReference.localBounds.size, playerSizeReference.transform.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        if (size.x < .001f || size.y < .001f || (size - lastPlayerVisualSize).sqrMagnitude < .000001f) return;
        lastPlayerVisualSize = size;
        foreach (var pair in friends)
        {
            Transform actor = pair.Value;
            if (actor == null) continue;
            bool found = false;
            Bounds bounds = default;
            float feet = float.PositiveInfinity;
            bool sprite = false;
            foreach (var visual in actor.GetComponentsInChildren<Renderer>(true))
            {
                if (visual.GetComponentInParent<Canvas>() != null) continue;
                sprite |= visual is SpriteRenderer;
                feet = Mathf.Min(feet, visual.bounds.min.y);
                Bounds local = visual.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = actor.InverseTransformPoint(visual.transform.TransformPoint(point));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found) continue;
            Vector3 current = Vector3.Scale(bounds.size, actor.lossyScale);
            Vector3 scale = actor.localScale;
            if (Mathf.Abs(current.x) > .001f) scale.x *= size.x / Mathf.Abs(current.x);
            if (Mathf.Abs(current.y) > .001f) scale.y *= size.y / Mathf.Abs(current.y);
            if (!sprite && Mathf.Abs(current.z) > .001f) scale.z *= (size.z > .001f ? size.z : size.x) / Mathf.Abs(current.z);
            actor.localScale = scale;
            standingScales[pair.Key] = scale;
            float resizedFeet = float.PositiveInfinity;
            foreach (var visual in actor.GetComponentsInChildren<Renderer>(true))
                if (visual.GetComponentInParent<Canvas>() == null) resizedFeet = Mathf.Min(resizedFeet, visual.bounds.min.y);
            if (!float.IsInfinity(resizedFeet)) actor.position += Vector3.up * (feet - resizedFeet);
        }
    }

    void PlaceFriends(int scene, Vector3 anchor)
    {
        seatedSpriteActors.Clear();
        foreach (var pair in directionalSprites)
            if (pair.Key != null) pair.Key.GetComponent<SpriteRenderer>().sprite = pair.Value[0];
        companionFollowReady = companionsFollowing = false;
        friendFollowVelocity.Clear();
        string[] names = { "Ethan", "Lucas", "Noah" };
        for (int i = 0; i < names.Length; i++)
        {
            if (!friends.TryGetValue(names[i], out var friend)) continue;
            Vector3 rowRight = scene == 0 && schoolExit != null ? schoolExit.right
                : scene == 1 && waitingShelterAnchor != null ? waitingShelterAnchor.right
                : existingBus.right;
            Vector3 position = anchor + rowRight * (i - 1) * schoolRowSpacing;
            Quaternion rotation = Quaternion.identity;
            Shot shot = scene < shots.Length ? shots[scene] : null;
            if (scene != 1 && shot != null && shot.actorPositions != null)
                foreach (var placement in shot.actorPositions)
                    if (placement != null && ActorRole(placement.actorName) == names[i])
                    {
                        Transform stage = scene == 0 && schoolExit != null ? schoolExit : shot.stage != null ? shot.stage : existingBus;
                        position = scene >= 1 && stage.IsChildOf(transform)
                            ? anchor + stage.TransformDirection(placement.localPosition)
                            : stage.TransformPoint(placement.localPosition);
                        rotation = stage.rotation * Quaternion.Euler(placement.localEulerAngles);
                        break;
                    }
            if (standingScales.TryGetValue(names[i], out var scale)) friend.localScale = scale;
            if (scene == 1)
            {
                position = WaitingBoyFeet(i);
                Vector3 facing = doorOutside - position;
                facing.y = 0;
                if (facing.sqrMagnitude > .01f) rotation = Quaternion.LookRotation(facing);
            }
            if (scene != 1 && TryWalkableGroundHeight(position, out float floorHeight)) position.y = floorHeight;
            friend.SetPositionAndRotation(position, rotation);
            var renderer = friend.GetComponentInChildren<Renderer>();
            if (renderer != null) friend.position += Vector3.up * (position.y + actorGroundClearance - renderer.bounds.min.y);
        }
    }

    float WaitingGroundHeight(Vector3 position)
    {
        return TryWalkableGroundHeight(position, out float height) ? height : position.y;
    }

    Vector3 WaitingBoyFeet(int index)
    {
        if (waitingBoyPoints != null && index < waitingBoyPoints.Length && waitingBoyPoints[index] != null)
            return waitingBoyPoints[index].position;

        Vector3 center = doorOutside + existingBus.right * waitingOffset.x +
            Vector3.up * waitingOffset.y + existingBus.forward * waitingOffset.z;
        Vector3 row = existingBus.right;
        float spacing = waitingRowSpacing;
        if (waitingShelterAnchor != null)
        {
            center = waitingShelterAnchor.position;
            row = waitingShelterAnchor.right;
            var renderers = waitingShelterAnchor.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds shelter = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) shelter.Encapsulate(renderers[i].bounds);
                // The imported shelter's pivot and local right axis are not the
                // centre and width of its usable space. Use the actual geometry.
                bool alongX = shelter.size.x >= shelter.size.z;
                row = alongX ? Vector3.right : Vector3.forward;
                Vector3 front = alongX ? Vector3.forward : Vector3.right;
                if (Vector3.Dot(front, doorOutside - shelter.center) < 0) front = -front;
                float halfWidth = alongX ? shelter.extents.x : shelter.extents.z;
                float halfDepth = alongX ? shelter.extents.z : shelter.extents.x;
                center = shelter.center + front * (halfDepth * .55f);
                center.y = waitingShelterAnchor.position.y;
                spacing = Mathf.Min(spacing, Mathf.Max(.4f, halfWidth - .55f));
            }
            center += waitingShelterAnchor.TransformVector(waitingShelterLocalOffset);
        }
        Vector3 feet = center + row * ((index - 1) * spacing);
        feet.y = WaitingGroundHeight(feet);
        return feet;
    }

    bool TryWalkableGroundHeight(Vector3 position, out float height)
    {
        float best = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(position + Vector3.up * 6, Vector3.down, 12))
            if (!hit.transform.IsChildOf(existingBus) && !hit.transform.IsChildOf(existingPlayer) &&
                (waitingShelterAnchor == null || !hit.transform.IsChildOf(waitingShelterAnchor)) &&
                hit.collider != null && !hit.collider.isTrigger && hit.normal.y > .7f &&
                hit.point.y <= position.y + 1.5f && hit.point.y > best)
                best = hit.point.y;
        height = best;
        return best > float.NegativeInfinity;
    }

    IEnumerator MoveFriend(Transform friend, Vector3 destination)
    {
        if (friend == null) yield break;
        while (Vector3.ProjectOnPlane(friend.position - destination, Vector3.up).sqrMagnitude > .04f)
        {
            Vector3 direction = destination - friend.position;
            friend.position = Vector3.MoveTowards(friend.position, destination, friendWalkSpeed * Time.deltaTime);
            direction.y = 0;
            if (direction.sqrMagnitude > .01f) friend.rotation = Quaternion.LookRotation(direction);
            yield return null;
        }
    }

    IEnumerator WalkSchoolRow()
    {
        // Imported school meshes may have no collision, so the terrain below
        // was being selected even when a visible porch was above it.
        if (schoolExit.parent != null)
            foreach (var mesh in schoolExit.parent.GetComponentsInChildren<MeshFilter>())
                if (mesh.sharedMesh != null && mesh.GetComponent<Collider>() == null)
                {
                    var surface = mesh.gameObject.AddComponent<MeshCollider>();
                    surface.sharedMesh = mesh.sharedMesh;
                }
        Physics.SyncTransforms();
        string[] names = { "Ethan", "Lucas", "Noah" };
        Transform[] boys = new Transform[names.Length];
        Vector3[] doorway = new Vector3[names.Length];
        Vector3[] line = new Vector3[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            if (!friends.TryGetValue(names[i], out boys[i])) continue;
            line[i] = boys[i].position + SchoolWalkDirection * schoolWalkDistance;
            doorway[i] = schoolExit.position - SchoolWalkDirection * (.55f + i * .15f);
            doorway[i].y = boys[i].position.y;
            boys[i].position = doorway[i];
            boys[i].gameObject.SetActive(false);
        }
        float total = schoolWalkDuration + schoolExitStaggerSeconds * (names.Length - 1);
        for (float elapsed = 0; elapsed < total; elapsed += Time.deltaTime)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (boys[i] == null) continue;
                float local = elapsed - i * schoolExitStaggerSeconds;
                if (local < 0) continue;
                if (!boys[i].gameObject.activeSelf) boys[i].gameObject.SetActive(true);
                float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(local / schoolWalkDuration));
                Vector3 next = Vector3.Lerp(doorway[i], line[i], progress);
                // Animate the route horizontally; keep the previous grounded
                // height so each frame does not push the feet under the steps.
                next.y = boys[i].position.y;
                boys[i].position = next;
            }
            GroundStandingFriends();
            if (Mathf.FloorToInt(elapsed * 2.5f) != Mathf.FloorToInt((elapsed - Time.deltaTime) * 2.5f))
                PlayEffect(footstepsSound, 2);
            yield return null;
        }
        for (int i = 0; i < boys.Length; i++) if (boys[i] != null)
        {
            boys[i].gameObject.SetActive(true);
            Vector3 end = line[i]; end.y = boys[i].position.y;
            boys[i].position = end;
        }
        GroundStandingFriends();
    }

    IEnumerator DriveVisualBus(Transform bus, Vector3 destination, float speed)
    {
        while (bus != null && (bus.position - destination).sqrMagnitude > .04f)
        { Vector3 previous = bus.position; bus.position = Vector3.MoveTowards(bus.position, destination, speed * Time.deltaTime); RollBusWheels(bus, bus.position - previous); yield return null; }
    }

    IEnumerator DepartMissedBus(Transform bus, Vector3 destination)
    {
        PlayEffect(busDepartureSound, 3);
        yield return WaitUnpaused(missedBusDepartureHoldSeconds);
        yield return DriveVisualBus(bus, destination, missedBusDepartureSpeed);
    }

    IEnumerator DriveBus(Vector3 destination, float speed)
    {
        while ((existingBus.position - destination).sqrMagnitude > .04f)
        { Vector3 previous = existingBus.position; existingBus.position = Vector3.MoveTowards(existingBus.position, destination, speed * Time.deltaTime); RollBusWheels(existingBus, existingBus.position - previous); yield return null; }
    }

    IEnumerator WaitUnpaused(float seconds)
    {
        float elapsed = 0;
        while (elapsed < seconds)
        { if (Time.timeScale > 0) elapsed += Time.unscaledDeltaTime; yield return null; }
    }

    void SeatFriends(bool includeEthan)
    {
        string[] names = { "Ethan", "Lucas", "Noah" };
        for (int i = includeEthan ? 0 : 1; i < names.Length; i++)
        {
            if (!friends.TryGetValue(names[i], out var friend)) continue;
            if (standingScales.TryGetValue(names[i], out var standing)) friend.localScale = standing;
            friend.position = CompanionSeatPosition(i);
            friend.rotation = SeatedActorRotation(i);
            // Being seated is independent of having a separate seated artwork.
            seatedSpriteActors.Add(friend);
            bool hasSeatedPose = directionalSprites.TryGetValue(friend, out var poses) && poses[2] != null;
            if (hasSeatedPose)
            {
                seatedSpriteActors.Add(friend);
                friend.GetComponent<SpriteRenderer>().sprite = poses[2];
            }
            // Correct non-foot sprite pivots, so the artwork sits above the seat surface.
            var renderer = friend.GetComponentInChildren<Renderer>();
            if (renderer != null && !hasSeatedPose) friend.position += Vector3.up * (CompanionSeatPosition(i).y - renderer.bounds.min.y);
        }
    }

    void KeepFriendsSeatedAfterCrash()
    {
        companionFollowReady = companionsFollowing = false;
        friendFollowVelocity.Clear();
        for (int i = 1; i <= 2; i++)
        {
            string name = i == 1 ? "Lucas" : "Noah";
            if (!friends.TryGetValue(name, out var friend) || friend == null) continue;
            friend.gameObject.SetActive(true);
        }
        // Keep the same configured seats and seat-surface pivot correction as before impact.
        // Only the playable character gets up to investigate the driver's seat.
        SeatFriends(false);
        if (friends.TryGetValue("Ethan", out var playerVisual) && playerVisual != null)
            playerVisual.gameObject.SetActive(false);
    }

    IEnumerator WaitForDriverSeat()
    {
        while (true)
        {
            Vector3 target = driverSeatInspectionAnchor != null ? driverSeatInspectionAnchor.position
                : existingBus.TransformPoint(driverSeatInspectionLocal);
            Vector3 delta = target - existingPlayer.position;
            delta.y = 0;
            if (Time.timeScale > 0 && delta.magnitude <= driverSeatInspectionRadius)
            {
                Vector3 view = target + Vector3.up * .65f - gameCamera.transform.position;
                if (view.sqrMagnitude < .04f || Vector3.Dot(gameCamera.transform.forward, view.normalized) > .2f ||
                    delta.magnitude < driverSeatInspectionRadius * .7f) break;
            }
            yield return null;
        }
    }

    IEnumerator LookAtDriverSeat()
    {
        Vector3 target = driverSeatLookTarget != null ? driverSeatLookTarget.position
            : sceneGlassesPickup != null ? sceneGlassesPickup.transform.position
            : existingBus.TransformPoint(driverSeatInspectionLocal) + Vector3.up * .35f;
        Vector3 direction = target - gameCamera.transform.position;
        if (direction.sqrMagnitude < .001f) yield break;
        Quaternion start = gameCamera.transform.rotation;
        Quaternion end = Quaternion.LookRotation(direction, Vector3.up);
        float duration = Mathf.Max(.1f, driverSeatLookSeconds);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            gameCamera.transform.rotation = Quaternion.Slerp(start, end,
                Mathf.SmoothStep(0, 1, elapsed / duration));
            yield return null;
        }
        gameCamera.transform.rotation = end;
    }

    IEnumerator WaitForAisleMidpoint()
    {
        SetObjective("Loop naar je vrienden achterin de bus.");
        // Use the authored bus body, not the generated floor's guessed height/width.
        // The latter can reject a player visibly standing inside an imported bus.
        MeshRenderer busBody = null;
        foreach (var mesh in existingBus.GetComponentsInChildren<MeshRenderer>(true))
            if (mesh.name == "bus_outer_body") { busBody = mesh; break; }
        while (true)
        {
            Vector3 playerCenter = playerController.bounds.center;
            bool inside = busBody == null || busBody.localBounds.Contains(
                busBody.transform.InverseTransformPoint(playerCenter));
            bool nearFriend = false;
            for (int i = 1; i <= 2 && inside; i++)
            {
                Vector3 target = friends.TryGetValue(BoyRoles[i], out var friend) && friend != null
                    ? friend.position : BackSeatFeet(i);
                // Measure to the visible body, not a pivot that may sit above the seat.
                float distance = Vector3.Distance(playerCenter, target);
                if (friend != null)
                    foreach (var visual in friend.GetComponentsInChildren<Renderer>())
                        if (visual.enabled && visual.GetComponentInParent<Canvas>() == null)
                            distance = Mathf.Min(distance, Vector3.Distance(playerCenter,
                                visual.bounds.ClosestPoint(playerCenter)));
                if (distance <= Mathf.Max(.3f, sitNearFriendsDistance))
                { nearFriend = true; break; }
            }
            if (nearFriend && Time.timeScale > 0) break;
            yield return null;
        }
        SetObjective("");
    }

    IEnumerator RunFirstPerson(int startScene)
    {
        driverSeatFound = false;
        CreateFriends();
        dialogueAudio = gameObject.AddComponent<AudioSource>();
        int samples = 2205; float[] wave = new float[samples];
        for (int i = 0; i < samples; i++) wave[i] = Mathf.Sin(i * .28f) * (1f - (float)i / samples) * .2f;
        advanceBlip = AudioClip.Create("Dialogue advance", samples, 1, 22050, false); advanceBlip.SetData(wave, 0);
        PrepareStoryAudio();
        if (dialogueBlipClip != null) { Destroy(advanceBlip); advanceBlip = dialogueBlipClip; }
        PlayAmbience(schoolAmbience);
        SetPlayerControl(false);
        if (startScene > 0)
        {
            PlaceFriends(startScene, DestinationForScene(startScene));
            FocusSpeaker("Ethan");
            yield return SpeakScene(startScene);
            Restore();
            yield break;
        }
        Vector3 travelDirection = busTravelLocalDirection.sqrMagnitude > .001f
            ? existingBus.TransformDirection(busTravelLocalDirection.normalized) : existingBus.right;
        Vector3 stop = waitingShelterAnchor != null ? waitingShelterAnchor.TransformPoint(waitingShelterLocalOffset)
            : doorOutside + existingBus.right * waitingOffset.x +
              Vector3.up * waitingOffset.y + existingBus.forward * waitingOffset.z;
        if (waitingShelterAnchor == null) stop.y = WaitingGroundHeight(stop) + .02f;
        string checkpoint = resumeState != null ? resumeState.checkpoint : "school";
        if (checkpoint == "lastStop" || checkpoint == "exploring")
        {
            existingBus.position = parkedBusPosition;
            arrivedAtLastStop = true;
            existingDriver.gameObject.SetActive(false);
            BuildLastStop(); ApplyAtmosphere(true);
            PlayAmbience(horrorAmbience);
            glassesCollected = resumeState.glassesCollected;
            driverSeatFound = resumeState.driverSeatFound;
            ActivateSceneGlasses();
            if (glassesCollected)
            {
                var inventory = existingPlayer.GetComponent<InventorySystem>();
                var configuredPickup = sceneGlassesPickup != null
                    ? sceneGlassesPickup.GetComponentInChildren<PickUp>(true) : null;
                if (inventory != null)
                {
                    string itemTag = configuredPickup != null && !string.IsNullOrEmpty(configuredPickup.itemTag)
                        ? configuredPickup.itemTag : "BrokenGlasses";
                    if (configuredPickup == null || configuredPickup.questItem) inventory.AddQuestItem(itemTag);
                    else inventory.AddItem(itemTag);
                }
            }
            Vector3 savedFeet = resumeState.hasPlayerPosition
                ? resumeState.playerPosition - Vector3.up * PlayerFeetOffset : aisleFeet;
            TeleportPlayer(savedFeet, resumeState.playerRotation * Vector3.forward);
            if (checkpoint == "lastStop") KeepFriendsSeatedAfterCrash();
            else
            {
                exploringFirstPerson = true;
                PlaceFriends(5, existingPlayer.position + existingBus.forward * 2);
            }
            storyCamera.enabled = false; gameCamera.enabled = true; cinematicCamera = false;
            if (checkpoint == "lastStop") yield return ContinueLastStop(true);
            else
            {
                OpeningCheckpoint = "exploring";
                yield return ExploreLaterScenes(Mathf.Clamp(resumeState.storyScene, 5, shots.Length - 1), true);
            }
            yield break;
        }
        if (checkpoint == "boarding" || checkpoint == "seated")
        {
            existingBus.position = parkedBusPosition;
            PlaceFriends(1, stop);
            yield return OpenRealDoors(); BuildBoardingRamp(); SeatFriends(false);
            if (checkpoint == "boarding")
            {
                OpeningCheckpoint = "boarding";
                existingPlayer.gameObject.layer = boardingLayer;
                TeleportPlayer(resumeState.hasPlayerPosition ? resumeState.playerPosition - Vector3.up * PlayerFeetOffset : doorOutside,
                    aisleFeet - doorOutside);
                storyCamera.enabled = false; gameCamera.enabled = true; cinematicCamera = false;
                SetPlayerControl(true);
                yield return WaitForAisleMidpoint();
                yield return RunSeatedSequence(false);
            }
            else yield return RunSeatedSequence(true);
            yield break;
        }
        existingBus.position = parkedBusPosition + travelDirection * lastBusApproachDistance;
        if (checkpoint != "busStop") HideLastBus();
        if (checkpoint != "busStop")
        {
            OpeningCheckpoint = "school";
            PlaceFriends(0, schoolExit.position);
            foreach (var boy in friends.Values) if (boy != null) boy.gameObject.SetActive(false);
            SetCamera(schoolExit.position + schoolExit.TransformDirection(schoolEstablishingCameraOffset),
                schoolExit.position + Vector3.up * schoolEstablishingLookHeight, true);
            if (showOpeningChapter) yield return ShowChapterIntro(1);
            yield return FadeTo(0, openingFadeInSeconds);
            yield return WaitUnpaused(openingSchoolHoldSeconds);
            FollowGroup();
            // Stagger the doorway exit, then let all three walk side-by-side during dialogue.
            Coroutine schoolWalk = StartCoroutine(WalkSchoolRow());
            // The spotting line is the final line before the bus shot, and is spoken only once.
            yield return SpeakScenePart(0, false);
            SetObjective("");
            Vector3 missedPosition = missedBusSchoolAnchor != null ? missedBusSchoolAnchor.position
                : parkedBusPosition;
            Vector3 missedDirection = missedBusDepartureTarget != null
                ? missedBusDepartureTarget.position - missedPosition
                : missedBusSchoolAnchor != null ? missedBusSchoolAnchor.forward
                : schoolExit.TransformDirection(missedBusSchoolTravelLocalDirection.normalized);
            missedDirection.y = 0;
            if (missedDirection.sqrMagnitude < .001f) missedDirection = schoolExit.forward;
            missedDirection.Normalize();
            Quaternion missedRotation = missedBusSchoolAnchor != null ? missedBusSchoolAnchor.rotation
                : Quaternion.FromToRotation(-existingBus.right, missedDirection) * existingBus.rotation;
            GameObject missedBus = Instantiate(existingBus.gameObject, missedPosition, missedRotation);
            missedBus.name = "Missed bus (cutscene visual)";
            spawnedVisuals.Add(missedBus);
            foreach (var collider in missedBus.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var behaviour in missedBus.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var originalRenderers = existingBus.GetComponentsInChildren<Renderer>(true);
            var visualRenderers = missedBus.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                bool wasEnabled = i < originalRenderers.Length &&
                    lastBusRendererStates.TryGetValue(originalRenderers[i], out bool originalState) && originalState;
                visualRenderers[i].enabled = wasEnabled && !(visualRenderers[i] is SpriteRenderer);
            }
            GroundMissedBus(missedBus);
            // After "Hey, the bus!", turn toward it and let the departure play without subtitles.
            FollowMovingBus(missedBus.transform);
            Vector3 missedDestination = missedBusDepartureTarget != null
                ? new Vector3(missedBusDepartureTarget.position.x, missedBus.transform.position.y,
                    missedBusDepartureTarget.position.z)
                : missedBus.transform.position + missedDirection * busTravelDistance;
            yield return DepartMissedBus(missedBus.transform, missedDestination);
            yield return schoolWalk;
            Destroy(missedBus);
            FollowGroup();
            yield return SpeakScenePart(0, true);
            SetObjective("");
            // Only the walk to the bus stop is hidden. The departure stays visible.
            yield return FadeTo(1, busStopFadeOutSeconds);
        }
        // The last bus is only revealed after the boys have mentioned it, under the fade.
        ShowLastBus();
        PlaceFriends(1, stop);
        PlayAmbience(streetAmbience);
        Vector3 waitingPlayerFeet = WaitingBoyFeet(0);
        TeleportPlayer(waitingPlayerFeet, doorOutside - waitingPlayerFeet);
        FollowArrivingBus(true);
        OpeningCheckpoint = "busStop";
        SetObjective("");
        if (checkpoint != "busStop") yield return FadeTo(0, busStopFadeInSeconds);
        PlayEngine(true);
        yield return DriveBus(parkedBusPosition, lastBusArrivalSpeed);
        PlayEngine(false);
        // Cut immediately from the tracking shot to the waiting Player's eyes.
        boardingFirstPerson = true;
        cinematicCamera = false; storyCamera.enabled = false; gameCamera.enabled = true;
        SetPlayerControl(false);
        if (friends.TryGetValue("Ethan", out var waitingPlayer)) waitingPlayer.gameObject.SetActive(false);
        PlayEffect(busDoorSound, 3);
        yield return OpenRealDoors(); BuildBoardingRamp();
        // From arrival onward the player sees each friend board from their own POV.
        yield return SpeakScenePart(1, false);
        // Keep the Player camera active throughout boarding. FollowGroup would
        // silently switch back to the distant third-person cutscene camera.
        foreach (string name in new[] { "Lucas", "Noah" })
            if (friends.TryGetValue(name, out var friend))
            {
                yield return MoveFriend(friend, doorOutside);
                yield return MoveFriend(friend, doorThreshold);
                yield return MoveFriend(friend, aisleFeet);
                yield return MoveFriend(friend, BackSeatFeet(name == "Lucas" ? 1 : 2));
            }
        SeatFriends(false);
        yield return SpeakScenePart(1, true);
        existingPlayer.gameObject.layer = boardingLayer;
        SetPlayerControl(true);
        OpeningCheckpoint = "boarding";
        PlayAmbience(busInteriorAmbience);
        yield return WaitForAisleMidpoint();
        boardingFirstPerson = false;
        yield return RunSeatedSequence(false);
    }

    IEnumerator RunSeatedSequence(bool alreadySeated)
    {
        if (!alreadySeated && playerController != null && playerController.enabled)
        {
            Vector3 feet = existingPlayer.position;
            feet.y = playerController.bounds.min.y;
            lastWalkableBusLocal = existingBus.InverseTransformPoint(feet);
        }
        if (!alreadySeated)
        {
            SetPlayerControl(false);
            yield return FadeTo(1, seatingFadeOutSeconds);
            if (seatedBlackHoldSeconds > 0) yield return WaitUnpaused(seatedBlackHoldSeconds);
        }
        hasSatDown = true;
        seatedDialogueCamera = true;
        seatedLookTarget = null;
        holdSeatedView = false;
        OpeningCheckpoint = "seated";
        PlayAmbience(busInteriorAmbience);
        TeleportPlayer(BackSeatFeet(0), -existingBus.right);
        // Seated characters are staged, not simulated against the seat collider.
        playerController.enabled = false;
        gameCamera.transform.localPosition = playableCameraLocal;
        SeatFriends(true);
        if (friends.TryGetValue("Ethan", out var seatedPlayer)) seatedPlayer.gameObject.SetActive(false);
        // Keep the seated dialogue and crash in the actual Player camera.
        cinematicCamera = false;
        storyCamera.enabled = false;
        gameCamera.enabled = true;
        if (!alreadySeated) yield return FadeTo(0, seatingFadeInSeconds);
        SetObjective("");
        if (seatedShotHoldSeconds > 0) yield return WaitUnpaused(seatedShotHoldSeconds);
        if (!continueAfterSeating)
        {
            SetObjective("Hoofdstuk voltooid. Open het menu met Escape.");
            yield break;
        }
        PlayEngine(true);
        yield return SpeakScene(2);
        // Let the final sleep line finish, then hide the entire transition before impact.
        SetObjective("");
        // Finish looking forward before closing the eyes, even when the last
        // reply held the view on a friend or the pre-fade pause is set to zero.
        holdSeatedView = true;
        seatedLookTarget = null;
        Quaternion sleepStartRotation = gameCamera.transform.rotation;
        Quaternion sleepForwardRotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(-existingBus.right, Vector3.up).normalized, Vector3.up);
        float lookDuration = Mathf.Max(.1f, sleepLookForwardSeconds);
        for (float elapsed = 0; elapsed < lookDuration;)
        {
            if (Time.timeScale > 0)
            {
                elapsed += Time.unscaledDeltaTime;
                gameCamera.transform.rotation = Quaternion.Slerp(sleepStartRotation,
                    sleepForwardRotation, Mathf.SmoothStep(0, 1, elapsed / lookDuration));
            }
            yield return null;
        }
        gameCamera.transform.rotation = sleepForwardRotation;
        if (sleepBeforeFadeSeconds > 0) yield return WaitUnpaused(sleepBeforeFadeSeconds);
        yield return FadeTo(1, crashFadeOutSeconds);
        yield return WaitUnpaused(sleepToCrashBlackHoldSeconds);
        StopEngine(); PlayEffect(crashSound, 4);
        PlayAmbience(horrorAmbience);
        // Never teleport the bus after impact: player, seats and pickup remain in
        // the same playable interior, rather than an unloaded patch of terrain.
        arrivedAtLastStop = true;
        OpeningCheckpoint = "lastStop";
        existingDriver.gameObject.SetActive(false);
        ActivateSceneGlasses();
        KeepFriendsSeatedAfterCrash();
        gameCamera.transform.localPosition = playableCameraLocal;
        seatedDialogueCamera = true;
        holdSeatedView = false;
        seatedLookTarget = null;
        exploringFirstPerson = true;
        cinematicCamera = false; storyCamera.enabled = false; gameCamera.enabled = true;
        SetPlayerControl(false);
        BuildLastStop(); ApplyAtmosphere(true);
        crashShakeUntil = Time.unscaledTime + dramaticCrashSeconds;
        yield return FadeTo(0, Mathf.Max(2f, crashFadeInSeconds));
        yield return WaitUnpaused(crashRecoveryPause);
        yield return ContinueLastStop(false);
    }

    IEnumerator ContinueLastStop(bool resumed)
    {
        OpeningCheckpoint = "lastStop";
        if (!resumed)
        {
            SetPlayerControl(false);
            yield return SpeakScene(3);
            investigationLookLocked = false;
            investigationLookAtGlasses = false;
            // Stay in the seat until the player deliberately stands up.
            // Require a fresh press: a held sprint key must not skip this beat.
            holdSeatedView = true;
            playerController.enabled = false;
            SetObjective("Druk op Shift om op te staan.");
            yield return null;
            while (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                yield return null;
            while (Time.timeScale <= 0 ||
                !(Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)))
                yield return null;
            SetObjective("");
            while (!TryStandInBusAisle())
            {
                SetObjective("Geen vrije staplek: plaats Post Crash Stand Point in het gangpad.");
                yield return null;
            }
        }
        seatedDialogueCamera = false;
        exploringFirstPerson = true;
        cinematicCamera = false;
        storyCamera.enabled = false;
        gameCamera.enabled = true;
        SetPlayerControl(true);
        if (!driverSeatFound)
        {
            SetObjective("Kijk voorin de bus naar de chauffeursstoel.");
            yield return WaitForDriverSeat();
            SetPlayerControl(false);
            SetObjective("");
            // Friends join the investigation in the aisle, rather than talking
            // through rows of seat backs from the rear of the bus.
            for (int i = 1; i <= 2; i++)
            {
                if (!friends.TryGetValue(BoyRoles[i], out var friend) || friend == null) continue;
                seatedSpriteActors.Remove(friend);
                if (directionalSprites.TryGetValue(friend, out var poses)) friend.GetComponent<SpriteRenderer>().sprite = poses[0];
                Vector3 feet = existingPlayer.position + existingBus.right * (i == 1 ? 1.0f : 1.7f)
                    + existingBus.forward * (i == 1 ? -.2f : .2f);
                feet.y = playerController.bounds.min.y;
                friend.position = feet;
                var visual = friend.GetComponentInChildren<Renderer>();
                if (visual != null) friend.position += Vector3.up * (feet.y - visual.bounds.min.y);
            }
            seatedDialogueCamera = true;
            holdSeatedView = true;
            yield return SpeakScene(4);
            investigationLookLocked = false;
            investigationLookAtGlasses = false;
            seatedDialogueCamera = false;
            seatedLookTarget = null;
            driverSeatFound = true;
            // Preserve the locked view when mouse control resumes, without a snap.
            Vector3 viewAngles = gameCamera.transform.eulerAngles;
            existingPlayer.rotation = Quaternion.Euler(0, viewAngles.y, 0);
            playerLook.SetPitch(Mathf.DeltaAngle(0, viewAngles.x));
            SetPlayerControl(true);
        }
        if (!glassesCollected)
        {
            if (glassesPickup == null)
            {
                Debug.LogError("BrokenGlasses is missing or has no PickUp component. The player cannot leave the bus until this scene pickup is assigned.", this);
                SetObjective("BrokenGlasses ontbreekt: koppel het volledige scène-object in de Inspector.");
                while (glassesPickup == null)
                {
                    ResolveSceneGlasses();
                    if (sceneGlassesPickup != null && sceneGlassesPickup.GetComponentInChildren<PickUp>(true) != null)
                        ActivateSceneGlasses();
                    yield return new WaitForSeconds(.5f);
                }
            }
            glassesPickup.enabled = true;
            SetObjective("Pak de bril van de chauffeur op met E.");
            while (!glassesCollected) yield return null;
        }
        SetObjective("Stap uit de bus.");
        Vector3 exit = doorOutside;
        while (Vector3.Distance(existingPlayer.position, exit + Vector3.up * PlayerFeetOffset) > 1.7f)
        {
            // Friends remain seated until the exterior transition places the group outside.
            yield return null;
        }
        yield return FadeTo(1);
        TeleportPlayer(exit, existingBus.forward);
        PlaceFriends(5, existingPlayer.position + existingBus.forward * 2);
        yield return FadeTo(0);
        SetPlayerControl(false);
        SetObjective("");
        OpeningCheckpoint = "exploring";
        yield return ExploreLaterScenes(5, false);
    }

    IEnumerator ExploreLaterScenes(int firstScene, bool resumeAtScene)
    {
        if (firstScene <= 7)
        {
            // Stop at the playable cabin until its ranger/dog interactions exist.
            // Do not auto-play the remaining chapters against placeholder scenery.
            for(int scene=Mathf.Max(5,firstScene);scene<=7 && scene<shots.Length;scene++)
            {
                if(scene==6 && !(resumeAtScene && scene==firstScene))
                { SetPlayerControl(false); yield return ShowChapterIntro(2); }
                SetPlayerControl(false); seatedDialogueCamera=true; holdSeatedView=true;
                yield return SpeakScene(scene);
                seatedDialogueCamera=false;
            }
            SetPlayerControl(true); SetObjective("Verken het huis van de boswachter.");
            while(IsPlaying) { FollowFriendsNearPlayer(); yield return null; }
            yield break;
        }
        for (int i = firstScene; i < shots.Length; i++)
        {
            if (i > 5 && !(resumeAtScene && i == firstScene))
            {
                Vector3 destination = DestinationForScene(i);
                SetPlayerControl(true);
                SetObjective(i < 9 ? "Volg het pad door het bos." : "Zoek verder naar aanwijzingen.");
                while (Vector3.Distance(existingPlayer.position, destination) > 3)
                {
                    FollowFriendsNearPlayer();
                    yield return null;
                }
                SetPlayerControl(false);
                SetObjective("");
                PlaceFriends(i, destination);
            }
            FocusSpeaker("Ethan");
            yield return SpeakScene(i);
        }
        Restore();
    }

    void OnGlassesCollected(PickUp pickup)
    {
        glassesCollected = true;
        if (glassesRoot != null && pickup.gameObject != glassesRoot) Destroy(glassesRoot);
    }

    void FollowFriendsNearPlayer()
    {
        if (IsDialogueLinePlaying || playerMovement == null || !playerMovement.enabled) return;
        // Use the authored standing scale for both meshes and future billboard sprites.
        foreach (string role in new[] { "Lucas", "Noah" })
        {
            if (!friends.TryGetValue(role, out var actor) || actor == null ||
                !standingScales.TryGetValue(role, out var standing)) continue;
            var visual = actor.GetComponentInChildren<Renderer>();
            float feet = visual != null ? visual.bounds.min.y : actor.position.y;
            Vector3 targetScale = standing;
            targetScale.y *= playerMovement.IsCrouching ? playerMovement.crouchFraction : 1f;
            actor.localScale = Vector3.Lerp(actor.localScale, targetScale, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (visual != null) actor.position += Vector3.up * (feet - visual.bounds.min.y);
        }
        Vector3 playerFeet = existingPlayer.position - Vector3.up * PlayerFeetOffset;
        if (!companionFollowReady)
        {
            lastPlayerFollowFeet = playerFeet;
            companionFollowReady = true;
            return;
        }
        Vector3 travel = playerFeet - lastPlayerFollowFeet;
        travel.y = 0;
        if (!companionsFollowing && travel.magnitude < friendFollowStartDistance)
            return;
        companionsFollowing = true;
        if (travel.sqrMagnitude > .0025f)
        {
            lastFollowDirection = travel.normalized;
            lastPlayerFollowFeet = playerFeet;
        }
        int index = 0;
        foreach (string name in new[] { "Lucas", "Noah" })
        {
            if (!friends.TryGetValue(name, out var friend) || friend == null) continue;
            Vector3 side = Vector3.Cross(Vector3.up, lastFollowDirection).normalized;
            Vector3 target = playerFeet - lastFollowDirection * (friendTrailingDistance + index * .5f)
                + side * (index == 0 ? -.7f : .7f);
            Vector3 playerBusLocal = existingBus.InverseTransformPoint(playerFeet);
            bool insideBus = OpeningCheckpoint == "lastStop" &&
                playerBusLocal.x > -.05f - busInteriorFloorLength * .5f &&
                playerBusLocal.x < -.05f + busInteriorFloorLength * .5f &&
                Mathf.Abs(playerBusLocal.z) < busInteriorFloorWidth * .65f;
            if (insideBus)
            {
                Vector3 localTarget = existingBus.InverseTransformPoint(target);
                localTarget.x = Mathf.Clamp(localTarget.x,
                    -.05f - busInteriorFloorLength * .42f, -.05f + busInteriorFloorLength * .42f);
                localTarget.z = Mathf.Clamp(localTarget.z,
                    -busInteriorFloorWidth * .32f, busInteriorFloorWidth * .32f);
                target = existingBus.TransformPoint(localTarget);
            }
            float floorY;
            if (insideBus) floorY = existingBus.TransformPoint(busAisleLocal).y;
            else if (!TryWalkableGroundHeight(friend.position, out floorY)) continue;
            var renderer = friend.GetComponentInChildren<Renderer>();
            target.y = floorY + actorGroundClearance +
                (renderer != null ? friend.position.y - renderer.bounds.min.y : 0);
            if ((friend.position - target).sqrMagnitude > friendFollowStopDistance * friendFollowStopDistance)
            {
                friendFollowVelocity.TryGetValue(name, out Vector3 velocity);
                Vector3 next = Vector3.SmoothDamp(friend.position, target, ref velocity,
                    .3f, playerMovement.CurrentMoveSpeed, Time.deltaTime);
                Vector3 fromPlayer = next - playerFeet;
                fromPlayer.y = 0;
                if (fromPlayer.magnitude < 1.15f) next = friend.position;
                // Grounding owns vertical movement; a distant target must not
                // pull companions through slopes or lift them over obstacles.
                next.y = friend.position.y;
                Vector3 step = next - friend.position;
                float feetY = renderer != null ? renderer.bounds.min.y : friend.position.y;
                Vector3 obstacleProbe = new Vector3(friend.position.x, feetY + .5f, friend.position.z);
                foreach (var hit in Physics.SphereCastAll(obstacleProbe, .15f, step.normalized,
                    step.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(friend) || hit.transform.IsChildOf(existingPlayer)) continue;
                    if (hit.normal.y > .7f) continue;
                    next = friend.position;
                    velocity = Vector3.zero;
                    break;
                }
                friend.position = next;
                friendFollowVelocity[name] = velocity;
            }
            else friendFollowVelocity[name] = Vector3.zero;
            Vector3 direction = target - friend.position; direction.y = 0;
            if (direction.sqrMagnitude > .01f) friend.rotation = Quaternion.LookRotation(direction);
            index++;
        }
    }

    IEnumerator SpeakScenePart(int scene, bool afterFade)
    {
        CurrentScene = scene;
        bool second = false;
        foreach (string raw in shots[scene].dialogue.Split('\n'))
        {
            int separator = raw.IndexOf('|'); if (separator < 0) continue;
            string who = raw.Substring(0, separator).Trim();
            string role = ActorRole(who);
            string words = raw.Substring(separator + 1).Trim();
            bool spotsBus = scene == 0 && role == "Lucas" &&
                (string.Equals(words, busSpottingLine, System.StringComparison.OrdinalIgnoreCase) ||
                 (words.IndexOf("hey", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                  words.IndexOf("bus", System.StringComparison.OrdinalIgnoreCase) >= 0));
            if (spotsBus)
            {
                if (!afterFade) yield return Say(who, words);
                second = true;
                continue;
            }
            if (scene == 1 && who == "ACTION" && words.Contains("get on the bus"))
            { second = true; continue; }
            if (second != afterFade || who == "ACTION" || who == "SFX") continue;
            yield return Say(who, words);
        }
    }

    void CleanupFirstPerson()
    {
        bobPreviousPosition.Clear(); bobPhase.Clear(); bobOffset.Clear();
        directionalSprites.Clear(); spriteLastPositions.Clear(); spriteHeadings.Clear(); seatedSpriteActors.Clear();
        if (originalPlayerVisual != null) originalPlayerVisual.enabled = originalPlayerVisualEnabled;
        foreach (var sprite in winterSprites) if (sprite != null) Destroy(sprite);
        winterSprites.Clear();
        if (glassesPickup != null) glassesPickup.Collected -= OnGlassesCollected;
        foreach (var visual in spawnedVisuals) if (visual != null) Destroy(visual);
        spawnedVisuals.Clear(); friends.Clear();
        if (dialogueAudio != null) Destroy(dialogueAudio);
        if (advanceBlip != null && advanceBlip != dialogueBlipClip) Destroy(advanceBlip);
        CleanupStoryAudio();
        cinematicCamera = false;
        seatedDialogueCamera = boardingFirstPerson = exploringFirstPerson = false;
        gameCameraShaking = false;
        if (arrivedAtLastStop) CleanupAtmosphere();
    }
}
