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
    [Tooltip("Camera offset in world metres while following the arriving bus: along its length, height, side.")]
    public Vector3 busStopCameraOffset = new Vector3(-8, 4.5f, 7);
    [Tooltip("Exact place under the bus shelter where the boys wait. Optional; otherwise Waiting Offset is used.")]
    public Transform waitingShelterAnchor;
    [Tooltip("Fine adjustment of the waiting row inside the selected shelter, in its local coordinates.")]
    public Vector3 waitingShelterLocalOffset;
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
    [Header("Companion follow")]
    [Min(.1f)] public float friendFollowStartDistance = .55f;
    [Min(.1f)] public float friendTrailingDistance = 2.1f;
    [Min(.1f)] public float friendFollowStopDistance = .25f;
    [Header("Boy visuals")]
    [Tooltip("Overall height/width multiplier for the three boys; their individual Visual Scale fields still work.")]
    [Range(.3f, 1f)] public float boyVisualScale = .72f;
    [Range(.25f, 1.5f)] public float seatedActorScale = .65f;
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
    [Min(.5f)] public float driverSeatInspectionRadius = 1.7f;

    readonly Dictionary<string, Transform> friends = new Dictionary<string, Transform>();
    readonly Dictionary<string, Vector3> standingScales = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Vector3> friendFollowVelocity = new Dictionary<string, Vector3>();
    readonly List<GameObject> spawnedVisuals = new List<GameObject>();
    AudioSource dialogueAudio;
    AudioClip advanceBlip;
    enum CutsceneCameraMode { Fixed, Group, Speaker, MovingBus, ArrivingBus }
    CutsceneCameraMode cameraMode;
    Transform cameraSpeaker, cameraBus;
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
        if (!IsPlaying || Time.timeScale <= 0) return;
        if (!cinematicCamera)
        {
            if (gameCamera != null && gameCamera.enabled && Time.unscaledTime < crashShakeUntil)
            {
                float scaleY = Mathf.Max(.01f, Mathf.Abs(existingPlayer.lossyScale.y));
                gameCamera.transform.localPosition = playableCameraLocal +
                    Random.insideUnitSphere * (crashShakeStrength / scaleY);
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
            cameraMode == CutsceneCameraMode.MovingBus ? missedBusShotFieldOfView : 55;
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
        return renderer != null ? renderer.bounds.center : actor.position + Vector3.up * speakerLookHeight;
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
            offset.x *= .55f;
            offset.z = Mathf.Clamp(Mathf.Abs(offset.z) * .65f, 3.5f, 6.5f);
        }
        Vector3 right = CurrentScene == 0 && schoolExit != null ? schoolExit.right : existingBus.right;
        Vector3 forward = CurrentScene == 0 && schoolExit != null ? schoolExit.forward : existingBus.forward;
        desiredCameraLook = look;
        desiredCameraPosition = look + right * offset.x + Vector3.up * offset.y + forward * offset.z;
    }

    void FocusSpeaker(string who)
    {
        if (boardingFirstPerson || exploringFirstPerson) return;
        Transform target = null;
        string role = ActorRole(who);
        if (role == "Bus Driver" && existingDriver != null && existingDriver.gameObject.activeSelf) target = existingDriver.transform;
        else friends.TryGetValue(role, out target);
        if (target == null) return;
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

    void CreateFriends()
    {
        for (int i = 0; i < 3; i++)
        {
            Actor actor = actors != null && i < actors.Length ? actors[i] : null;
            string role = BoyRoles[i];
            GameObject go;
            if (actor != null && actor.artPrefab != null) go = Instantiate(actor.artPrefab);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = actor != null ? actor.color : Color.white;
                materials.Add(material); go.GetComponent<Renderer>().sharedMaterial = material;
                go.transform.localScale = new Vector3(.5f, .85f, .5f);
            }
            go.name = "Story " + DisplayName(role);
            go.transform.localScale *= boyVisualScale * (actor != null ? Mathf.Max(.05f, actor.visualScale) : 1f);
            spawnedVisuals.Add(go); friends[role] = go.transform; standingScales[role] = go.transform.localScale;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (go.GetComponentInChildren<SpriteRenderer>() != null && go.GetComponentInChildren<YBillboard>() == null)
                go.AddComponent<YBillboard>();
        }
    }

    void PlaceFriends(int scene, Vector3 anchor)
    {
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
            if (shot != null && shot.actorPositions != null)
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
            if (scene == 1) position.y = WaitingGroundHeight(position);
            friend.SetPositionAndRotation(position, rotation);
            var renderer = friend.GetComponentInChildren<Renderer>();
            if (renderer != null) friend.position += Vector3.up * (position.y + actorGroundClearance - renderer.bounds.min.y);
        }
    }

    float WaitingGroundHeight(Vector3 position)
    {
        return TryWalkableGroundHeight(position, out float height) ? height : position.y;
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
        while ((friend.position - destination).sqrMagnitude > .04f)
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
                boys[i].position = Vector3.Lerp(doorway[i], line[i], progress);
            }
            if (Mathf.FloorToInt(elapsed * 2.5f) != Mathf.FloorToInt((elapsed - Time.deltaTime) * 2.5f))
                PlayEffect(footstepsSound, 2);
            yield return null;
        }
        for (int i = 0; i < boys.Length; i++) if (boys[i] != null)
        { boys[i].gameObject.SetActive(true); boys[i].position = line[i]; }
    }

    IEnumerator DriveVisualBus(Transform bus, Vector3 destination, float speed)
    {
        while (bus != null && (bus.position - destination).sqrMagnitude > .04f)
        { bus.position = Vector3.MoveTowards(bus.position, destination, speed * Time.deltaTime); yield return null; }
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
        { existingBus.position = Vector3.MoveTowards(existingBus.position, destination, speed * Time.deltaTime); yield return null; }
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
            if (standingScales.TryGetValue(names[i], out var standing)) friend.localScale = standing * seatedActorScale;
            friend.position = CompanionSeatPosition(i);
            friend.rotation = SeatedActorRotation(i);
            // Correct non-foot sprite pivots, so the artwork sits above the seat surface.
            var renderer = friend.GetComponentInChildren<Renderer>();
            if (renderer != null) friend.position += Vector3.up * (CompanionSeatPosition(i).y - renderer.bounds.min.y);
        }
    }

    void StandFriendsAfterCrash()
    {
        companionFollowReady = companionsFollowing = false;
        friendFollowVelocity.Clear();
        for (int i = 1; i <= 2; i++)
        {
            string name = i == 1 ? "Lucas" : "Noah";
            if (!friends.TryGetValue(name, out var friend) || friend == null) continue;
            if (standingScales.TryGetValue(name, out var scale)) friend.localScale = scale;
            friend.gameObject.SetActive(true);
            Vector3 floor = existingBus.TransformPoint(new Vector3(.19f, busAisleLocal.y,
                i == 1 ? -.09f : .09f));
            friend.SetPositionAndRotation(floor, Quaternion.LookRotation(-existingBus.right));
            var renderer = friend.GetComponentInChildren<Renderer>();
            if (renderer != null) friend.position += Vector3.up * (floor.y + actorGroundClearance - renderer.bounds.min.y);
        }
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

    IEnumerator WaitForAisleMidpoint()
    {
        SetObjective("Loop door de bus naar achteren.");
        Vector3 start = existingBus.InverseTransformPoint(doorThreshold);
        Vector3 end = existingBus.InverseTransformPoint(BackSeatFeet(0));
        Vector2 path = new Vector2(end.x - start.x, end.z - start.z);
        float lengthSquared = Mathf.Max(path.sqrMagnitude, .0001f);
        while (true)
        {
            Vector3 local = existingBus.InverseTransformPoint(existingPlayer.position);
            Vector2 walked = new Vector2(local.x - start.x, local.z - start.z);
            if (Vector2.Dot(walked, path) / lengthSquared >= aisleMidpoint) break;
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
            if (checkpoint == "lastStop") StandFriendsAfterCrash();
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
        Vector3 waitingPlayerFeet = stop -
            (waitingShelterAnchor != null ? waitingShelterAnchor.right : existingBus.right) * schoolRowSpacing;
        waitingPlayerFeet.y = WaitingGroundHeight(waitingPlayerFeet);
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
        if (!alreadySeated)
        {
            SetPlayerControl(false);
            yield return FadeTo(1, seatingFadeOutSeconds);
            if (seatedBlackHoldSeconds > 0) yield return WaitUnpaused(seatedBlackHoldSeconds);
        }
        hasSatDown = true;
        seatedDialogueCamera = true;
        OpeningCheckpoint = "seated";
        PlayAmbience(busInteriorAmbience);
        TeleportPlayer(BackSeatFeet(0) + Vector3.up * seatedPlayerHeight, existingBus.forward);
        SeatFriends(true);
        if (friends.TryGetValue("Ethan", out var seatedPlayer)) seatedPlayer.gameObject.SetActive(false);
        storyCamera.fieldOfView = seatedShotFieldOfView;
        SetCamera(existingBus.TransformPoint(seatedCameraLocal), SeatedForwardLook(), true);
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
        StandFriendsAfterCrash();
        TeleportPlayer(existingBus.TransformPoint(new Vector3(.16f, busAisleLocal.y, 0)), -existingBus.right);
        seatedDialogueCamera = false;
        exploringFirstPerson = true;
        cinematicCamera = false; storyCamera.enabled = false; gameCamera.enabled = true;
        SetPlayerControl(false);
        BuildLastStop(); ApplyAtmosphere(true);
        crashShakeUntil = Time.unscaledTime + crashShakeSeconds;
        yield return FadeTo(0, crashFadeInSeconds);
        SetPlayerControl(true);
        yield return ContinueLastStop(false);
    }

    IEnumerator ContinueLastStop(bool resumed)
    {
        OpeningCheckpoint = "lastStop";
        if (!resumed)
            yield return SpeakScene(3);
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
            driverSeatFound = true;
            SetObjective("");
            yield return SpeakScene(4);
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
            FollowFriendsNearPlayer();
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
            else if (!TryWalkableGroundHeight(target, out floorY)) continue;
            var renderer = friend.GetComponentInChildren<Renderer>();
            target.y = floorY + actorGroundClearance +
                (renderer != null ? friend.position.y - renderer.bounds.min.y : 0);
            if ((friend.position - target).sqrMagnitude > friendFollowStopDistance * friendFollowStopDistance)
            {
                friendFollowVelocity.TryGetValue(name, out Vector3 velocity);
                Vector3 next = Vector3.SmoothDamp(friend.position, target, ref velocity,
                    .3f, friendWalkSpeed, Time.deltaTime);
                Vector3 fromPlayer = next - playerFeet;
                fromPlayer.y = 0;
                if (fromPlayer.magnitude < 1.15f)
                    next -= lastFollowDirection * (1.15f - fromPlayer.magnitude);
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
