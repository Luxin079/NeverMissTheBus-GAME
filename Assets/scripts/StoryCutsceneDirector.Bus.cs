using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class StoryCutsceneDirector
{
    [Header("Existing scene objects")]
    public Transform existingBus;
    public Transform existingPlayer;
    public SpriteRenderer existingDriver;
    public Transform[] busApproachWaypoints;
    [Header("Bus coordinates (original prefab units)")]
    public Vector3 busAisleLocal=new Vector3(-.23f,.075f,0);
    public Vector3 busSeatLocal=new Vector3(.10f,.075f,-.075f);
    [Tooltip("Three seats beside each other: Player/Ethan, Lucas, Noah. Coordinates are in the selected bus prefab.")]
    public Vector3[] seatedCharacterLocals = {
        new Vector3(.23f,.11f,-.08f),
        new Vector3(.23f,.11f,0),
        new Vector3(.23f,.11f,.08f)
    };
    [Header("Seated boy positions")]
    [Tooltip("Empty GameObject at Ethan/the Player's seat. This point is the seat surface; make it a child of the bus for easy editing.")]
    public Transform ethanSeatAnchor;
    [Tooltip("Empty GameObject at Lucas's seat.")]
    public Transform lucasSeatAnchor;
    [Tooltip("Empty GameObject at Noah's seat.")]
    public Transform noahSeatAnchor;
    [HideInInspector] public float aisleMidpoint=.5f;
    [HideInInspector] public Transform seatingTransitionPoint;
    [HideInInspector] public float seatingTransitionRadius=.8f;
    [Tooltip("Start the seating fade when the Player is inside the bus and this close to either friend, in world metres.")]
    [Min(.3f)] public float sitNearFriendsDistance = 1.5f;
    [Tooltip("Visual origin height above a bus seat. Raise this when a sprite sinks into the floor.")]
    public float companionSeatHeight=0;
    [Tooltip("Bus layer when the player boards. Unity layer 1 is TransparentFX.")]
    [Range(0,31)] public int boardingLayer=1;
    public float seatUseDistance=1.1f; // Serialized legacy setting.
    public float storyWalkSpeed=3; // Serialized legacy setting.
    [Tooltip("Optional exact bus stopping point in front of the shelter. An empty Transform in the scene works well.")]
    public Transform busStopParkingAnchor;
    [Header("Bus interior collision (local prefab units)")]
    [Min(.2f)] public float busInteriorFloorLength=.86f;
    [Min(.12f)] public float busInteriorFloorWidth=.28f;
    [Min(.08f)] public float busInteriorWallHeight=.22f;
    [Tooltip("Shift the final parking point along the bus length to align its front door with the shelter (world metres).")]
    public float parkingShiftAlongBus = -6.5f;

    PlayerMovement playerMovement;
    PlayerCrosshair playerCrosshair;
    MouseLook playerLook;
    CharacterController playerController;
    bool controllerInitiallyEnabled, busReady, driverInitiallyActive, hasSatDown, arrivedAtLastStop;
    Vector3 parkedBusPosition, initialBusPosition, driverStartPosition;
    Quaternion driverStartRotation;
    Transform driverStartParent;
    Vector3 playerCameraStartLocal;
    Vector3 playableCameraLocal;
    float playerCameraStartNearClip;
    int playerStartLayer;
    readonly Dictionary<Renderer,bool> lastBusRendererStates=new Dictionary<Renderer,bool>();
    readonly List<Transform> busDoors=new List<Transform>();
    readonly List<Vector3> doorStartPositions=new List<Vector3>();
    readonly List<Collider> doorColliders=new List<Collider>();
    readonly List<bool> doorColliderStates=new List<bool>();
    Vector3 doorOutside, doorThreshold, aisleFeet, seatFeet;
    float busDoorLocalX;
    GameObject boardingRamp;
    GameObject busInteriorCollision;
    bool BusReady=>busReady;
    float PlayerFeetOffset => (playerController.height*.5f-playerController.center.y)*existingPlayer.lossyScale.y+.04f;

    void PrepareBusSequence()
    {
        if(existingPlayer==null)
        { var found=FindFirstObjectByType<PlayerMovement>();if(found!=null)existingPlayer=found.transform; }
        if(existingBus==null || existingPlayer==null || existingDriver==null)return;
        playerMovement=existingPlayer.GetComponent<PlayerMovement>();
        playerCrosshair=existingPlayer.GetComponent<PlayerCrosshair>();
        playerLook=existingPlayer.GetComponentInChildren<MouseLook>();
        playerController=existingPlayer.GetComponent<CharacterController>();
        if(playerMovement==null || playerLook==null || playerController==null)return;
        hasSatDown=false;
        arrivedAtLastStop=false;
        controllerInitiallyEnabled=playerController.enabled;
        initialBusPosition=existingBus.position;
        parkedBusPosition=busStopParkingAnchor!=null ? busStopParkingAnchor.position
            : initialBusPosition+existingBus.right*parkingShiftAlongBus;
        driverInitiallyActive=existingDriver.gameObject.activeSelf;
        driverStartPosition=existingDriver.transform.position;driverStartRotation=existingDriver.transform.rotation;
        driverStartParent=existingDriver.transform.parent;
        existingDriver.transform.SetParent(existingBus,true);
        foreach (var seatAnchor in new[] { ethanSeatAnchor, lucasSeatAnchor, noahSeatAnchor, seatingTransitionPoint })
            if (seatAnchor != null && !seatAnchor.IsChildOf(existingBus))
                seatAnchor.SetParent(existingBus, true);
        playerStartLayer=existingPlayer.gameObject.layer;
        playerCameraStartLocal=gameCamera.transform.localPosition;
        playerCameraStartNearClip=gameCamera.nearClipPlane;
        playableCameraLocal=playerCameraStartLocal;
        Bounds doorBounds=default; bool foundDoor=false;
        foreach(var child in existingBus.GetComponentsInChildren<Transform>(true))
        {
            if(child.name!="bus_door_01" && child.name!="bus_door_02")continue;
            busDoors.Add(child);doorStartPositions.Add(child.localPosition);
            var mesh=child.GetComponent<Renderer>();
            if(mesh!=null) { if(foundDoor)doorBounds.Encapsulate(mesh.bounds);else {doorBounds=mesh.bounds;foundDoor=true;} }
            foreach(var collider in child.GetComponentsInChildren<Collider>())
            {doorColliders.Add(collider);doorColliderStates.Add(collider.enabled);}
        }
        AttachDoorGlass();
        aisleFeet=existingBus.TransformPoint(busAisleLocal);
        seatFeet=existingBus.TransformPoint(busSeatLocal);
        doorThreshold=foundDoor?doorBounds.center:existingBus.TransformPoint(new Vector3(-.33f,.075f,.13f));
        doorThreshold.y=aisleFeet.y;
        busDoorLocalX = existingBus.InverseTransformPoint(doorThreshold).x;
        doorOutside=doorThreshold+existingBus.forward*1.8f;
        foreach(var hit in Physics.RaycastAll(doorOutside+Vector3.up*3,Vector3.down,8))
            if(!hit.transform.IsChildOf(existingBus) && !hit.transform.IsChildOf(existingPlayer) && hit.normal.y>.8f &&
                (hit.collider is TerrainCollider || hit.transform.name.ToLowerInvariant().Contains("road")))
            {doorOutside.y=hit.point.y+.02f;break;}
        Vector3 parkingDelta=parkedBusPosition-initialBusPosition;
        doorOutside+=parkingDelta;doorThreshold+=parkingDelta;
        aisleFeet+=parkingDelta;seatFeet+=parkingDelta;
        BuildBusInteriorCollision();
        busReady=true;
    }

    void BuildBusInteriorCollision()
    {
        busInteriorCollision = new GameObject("Playable bus interior collision");
        busInteriorCollision.transform.SetParent(existingBus, false);
        busInteriorCollision.layer = boardingLayer;
        float middleX = -.05f;
        float floorY = busAisleLocal.y;
        AddBusBox("Floor", new Vector3(middleX, floorY - .012f, 0),
            new Vector3(busInteriorFloorLength, .024f, busInteriorFloorWidth));
        AddBusBox("Left wall", new Vector3(middleX, floorY + busInteriorWallHeight * .5f,
                -(busInteriorFloorWidth * .5f + .012f)),
            new Vector3(busInteriorFloorLength, busInteriorWallHeight, .024f));
        float minX = middleX - busInteriorFloorLength * .5f;
        float maxX = middleX + busInteriorFloorLength * .5f;
        float gapMin = Mathf.Clamp(busDoorLocalX - .10f, minX, maxX);
        float gapMax = Mathf.Clamp(busDoorLocalX + .10f, minX, maxX);
        float rightZ = busInteriorFloorWidth * .5f + .012f;
        if (gapMin - minX > .03f)
            AddBusBox("Door-side wall before entrance",
                new Vector3((minX + gapMin) * .5f, floorY + busInteriorWallHeight * .5f, rightZ),
                new Vector3(gapMin - minX, busInteriorWallHeight, .024f));
        if (maxX - gapMax > .03f)
            AddBusBox("Door-side wall after entrance",
                new Vector3((gapMax + maxX) * .5f, floorY + busInteriorWallHeight * .5f, rightZ),
                new Vector3(maxX - gapMax, busInteriorWallHeight, .024f));
        AddBusBox("Front wall", new Vector3(minX, floorY + busInteriorWallHeight * .5f, 0),
            new Vector3(.024f, busInteriorWallHeight, busInteriorFloorWidth));
        AddBusBox("Rear wall", new Vector3(middleX + busInteriorFloorLength * .5f,
                floorY + busInteriorWallHeight * .5f, 0),
            new Vector3(.024f, busInteriorWallHeight, busInteriorFloorWidth));
    }

    void AddBusBox(string name, Vector3 center, Vector3 size)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(busInteriorCollision.transform, false);
        part.layer = boardingLayer;
        part.transform.localPosition = center;
        part.AddComponent<BoxCollider>().size = size;
    }
    void SetPlayerControl(bool enabled)
    {
        playerCrosshair?.SetStoryControl(enabled);
        if(playerMovement==null || playerLook==null)return;
        playerMovement.ResetMotion();
        if (enabled && !playerLook.enabled && gameCamera != null)
        {
            Vector3 view = gameCamera.transform.eulerAngles;
            existingPlayer.rotation = Quaternion.Euler(0,view.y,0);
            playerLook.SetPitch(Mathf.DeltaAngle(0,view.x));
        }
        playerMovement.enabled=enabled && suspended.Contains(playerMovement);
        playerLook.enabled=enabled && suspended.Contains(playerLook);
        var inventory = existingPlayer.GetComponent<InventorySystem>();
        if (inventory != null && suspended.Contains(inventory)) inventory.enabled = enabled;
        if(friends.TryGetValue("Ethan",out var ethan) && ethan!=null)
            ethan.gameObject.SetActive(!enabled && !boardingFirstPerson && !seatedDialogueCamera && !exploringFirstPerson);
        Cursor.lockState=enabled?CursorLockMode.Locked:CursorLockMode.None;
        Cursor.visible=!enabled;
    }
    void TeleportPlayer(Vector3 feet,Vector3 forward)
    {
        if(playerController==null || existingPlayer==null || playerMovement==null || playerLook==null)return;
        playerController.enabled=false;
        existingPlayer.position=feet+Vector3.up*PlayerFeetOffset;
        forward.y=0;if(forward.sqrMagnitude>.001f)existingPlayer.rotation=Quaternion.LookRotation(forward);
        playerMovement.ResetMotion();playerLook.SetPitch(0);
        playerController.enabled=controllerInitiallyEnabled;
        Physics.SyncTransforms();
    }

    bool TryStandInBusAisle()
    {
        // Return to the proven walkable position recorded just before sitting.
        // Imported bus colliders can report broad overlaps even inside the aisle.
        if (lastWalkableBusLocal.HasValue && postCrashStandPoint == null)
        {
            TeleportPlayer(existingBus.TransformPoint(lastWalkableBusLocal.Value) + Vector3.up * .03f, -existingBus.right);
            return true;
        }
        float radius = playerController.radius * Mathf.Max(Mathf.Abs(existingPlayer.lossyScale.x), Mathf.Abs(existingPlayer.lossyScale.z));
        float height = Mathf.Max(radius * 2, playerController.height * Mathf.Abs(existingPlayer.lossyScale.y));
        Vector3 baseFeet = postCrashStandPoint != null ? postCrashStandPoint.position : existingBus.TransformPoint(busAisleLocal);
        Physics.SyncTransforms();
        for (int i = 0; i < 21; i++)
        {
            float offset = i == 0 ? 0 : ((i+1)/2) * .25f * (i%2 == 0 ? -1 : 1);
            Vector3 feet = baseFeet + existingBus.right * offset + Vector3.up * .04f;
            bool blocked = false;
            foreach (var hit in Physics.OverlapCapsule(feet + Vector3.up * radius,
                feet + Vector3.up * (height-radius), radius * .95f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(existingPlayer)) { blocked = true; break; }
            // Ignore the player's own collider and accept any bus floor hit,
            // not only the first overlapping collider returned by a raycast.
            bool supported = false;
            foreach (var ground in Physics.RaycastAll(feet + Vector3.up * .15f, Vector3.down, .4f, ~0, QueryTriggerInteraction.Ignore))
                if (ground.transform.IsChildOf(existingBus) && !ground.transform.IsChildOf(existingPlayer) && ground.normal.y > .5f)
                    supported = true;
            if (blocked || !supported) continue;
            TeleportPlayer(feet, -existingBus.right);
            return true;
        }
        return false;
    }
    Vector3 BackSeatFeet(int index)
    {
        Transform anchor = SeatAnchor(index);
        if (anchor != null) return anchor.position;
        Vector3 offset=seatedCharacterLocals!=null && index>=0 && index<seatedCharacterLocals.Length
            ?seatedCharacterLocals[index]
            :busSeatLocal+new Vector3(0,0,(index-1)*.08f);
        return existingBus.TransformPoint(offset);
    }
    Transform SeatAnchor(int index)
    {
        return index == 0 ? ethanSeatAnchor : index == 1 ? lucasSeatAnchor : index == 2 ? noahSeatAnchor : null;
    }
    Quaternion SeatedActorRotation(int index)
    {
        Transform anchor = SeatAnchor(index);
        return anchor != null ? anchor.rotation : existingBus.rotation;
    }
    Vector3 CompanionSeatPosition(int index)
    {
        var seat=BackSeatFeet(index);
        // Character visual pivots can differ; the Inspector exposes this height and per-scene overrides.
        return seat+Vector3.up*companionSeatHeight;
    }
    IEnumerator OpenRealDoors()
    {
        foreach(var collider in doorColliders)if(collider!=null)collider.enabled=false;
        for(float t=0;t<1;t+=Time.unscaledDeltaTime*2)
        {for(int i=0;i<busDoors.Count;i++)if(busDoors[i]!=null)busDoors[i].localPosition=doorStartPositions[i]+Vector3.right*(i==0?-.07f:.07f)*Mathf.SmoothStep(0,1,t);yield return null;}
        for(int i=0;i<busDoors.Count;i++)if(busDoors[i]!=null)busDoors[i].localPosition=doorStartPositions[i]+Vector3.right*(i==0?-.07f:.07f);
    }

    readonly Dictionary<MeshFilter, Mesh> originalGlassMeshes = new Dictionary<MeshFilter, Mesh>();
    readonly List<Mesh> generatedGlassMeshes = new List<Mesh>();
    readonly List<GameObject> movingDoorGlass = new List<GameObject>();
    readonly Dictionary<Transform, Vector3> wheelStartPositions = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Quaternion> wheelStartRotations = new Dictionary<Transform, Quaternion>();

    void AttachDoorGlass()
    {
        // Imported glass can contain every window in one mesh. Move only the
        // triangles inside each door, never the windshield or side windows.
        foreach (var filter in existingBus.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.name.ToLowerInvariant().Contains("glass") || filter.sharedMesh == null) continue;
            if (busDoors.Exists(door => filter.transform.IsChildOf(door))) continue;
            Mesh source = filter.sharedMesh;
            if (!source.isReadable) { Debug.LogWarning("Enable Read/Write for bus glass: " + filter.name, filter); continue; }
            var vertices = source.vertices;
            Mesh remaining = Instantiate(source);
            bool changed = false;
            for (int d = 0; d < busDoors.Count; d++)
            {
                var doorRenderer = busDoors[d].GetComponent<Renderer>();
                if (doorRenderer == null) continue;
                Bounds bounds = doorRenderer.bounds;
                bounds.Expand(.06f);
                Mesh pane = Instantiate(source);
                bool hasPane = false;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    var keep = new List<int>(); var move = new List<int>();
                    var indices = remaining.GetTriangles(sub);
                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        Vector3 center = filter.transform.TransformPoint((vertices[indices[t]] + vertices[indices[t+1]] + vertices[indices[t+2]]) / 3f);
                        var destination = bounds.Contains(center) ? move : keep;
                        destination.Add(indices[t]); destination.Add(indices[t+1]); destination.Add(indices[t+2]);
                    }
                    pane.SetTriangles(move, sub); remaining.SetTriangles(keep, sub);
                    hasPane |= move.Count > 0;
                }
                if (!hasPane) { Destroy(pane); continue; }
                changed = true;
                var part = new GameObject("Moving door glass");
                part.layer = filter.gameObject.layer;
                part.transform.SetParent(filter.transform, false);
                part.transform.SetParent(busDoors[d], true);
                part.AddComponent<MeshFilter>().sharedMesh = pane;
                part.AddComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials;
                movingDoorGlass.Add(part); generatedGlassMeshes.Add(pane);
            }
            if (changed) { originalGlassMeshes.Add(filter, source); filter.sharedMesh = remaining; generatedGlassMeshes.Add(remaining); }
            else Destroy(remaining);
        }
    }

    void RollBusWheels(Transform bus, Vector3 displacement)
    {
        foreach (var wheel in bus.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!wheel.name.Contains("tire")) continue;
            if (!wheelStartPositions.ContainsKey(wheel.transform))
            {
                wheelStartPositions.Add(wheel.transform, wheel.transform.localPosition);
                wheelStartRotations.Add(wheel.transform, wheel.transform.localRotation);
            }
            float radius = Mathf.Max(.05f, wheel.bounds.extents.y);
            Vector3 axle = bus.forward;
            float distance = Vector3.Dot(displacement, Vector3.Cross(axle, Vector3.up));
            wheel.transform.RotateAround(wheel.bounds.center, axle, distance / radius * Mathf.Rad2Deg);
        }
    }
    void BuildBoardingRamp()
    {
        Vector3 delta=aisleFeet-doorOutside;
        boardingRamp=new GameObject("Boarding ramp (edit via bus Inspector)");
        boardingRamp.transform.SetParent(existingBus,true);
        boardingRamp.transform.position=(aisleFeet+doorOutside)*.5f-Vector3.up*.06f;
        boardingRamp.transform.rotation=Quaternion.LookRotation(delta,Vector3.up);
        var collider=boardingRamp.AddComponent<BoxCollider>();collider.size=new Vector3(.85f,.12f,delta.magnitude+.25f);
        boardingRamp.layer=boardingLayer;
    }
    void RestoreBusSequence()
    {
        ShowLastBus();
        if(!busReady)return;
        busReady=false;
        investigationLookLocked = false;
        investigationLookAtGlasses = false;
        foreach (var pair in wheelStartPositions)
            if (pair.Key != null) { pair.Key.localPosition = pair.Value; pair.Key.localRotation = wheelStartRotations[pair.Key]; }
        wheelStartPositions.Clear(); wheelStartRotations.Clear();
        foreach (var pair in originalGlassMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
        foreach (var part in movingDoorGlass) if (part != null) Destroy(part);
        foreach (var mesh in generatedGlassMeshes) Destroy(mesh);
        originalGlassMeshes.Clear(); movingDoorGlass.Clear(); generatedGlassMeshes.Clear();
        if(existingBus!=null && !arrivedAtLastStop)existingBus.position=initialBusPosition;
        if(existingBus!=null && existingPlayer!=null && playerController!=null && hasSatDown && !arrivedAtLastStop)
            TeleportPlayer(existingBus.TransformPoint(busAisleLocal),existingBus.right);
        if(playerController!=null)playerController.enabled=controllerInitiallyEnabled;
        if(existingPlayer!=null)existingPlayer.gameObject.layer=playerStartLayer;
        if(gameCamera!=null)
        {gameCamera.transform.localPosition=playerCameraStartLocal;gameCamera.nearClipPlane=playerCameraStartNearClip;}
        for(int i=0;i<busDoors.Count;i++)if(busDoors[i]!=null)busDoors[i].localPosition=doorStartPositions[i];
        for(int i=0;i<doorColliders.Count;i++)if(doorColliders[i]!=null)doorColliders[i].enabled=doorColliderStates[i];
        busDoors.Clear();doorStartPositions.Clear();doorColliders.Clear();doorColliderStates.Clear();
        if(boardingRamp!=null)Destroy(boardingRamp);
        if(busInteriorCollision!=null)Destroy(busInteriorCollision);
        if(existingDriver!=null)existingDriver.transform.SetParent(driverStartParent,true);
        if(existingDriver!=null && !arrivedAtLastStop)
        {existingDriver.transform.SetPositionAndRotation(driverStartPosition,driverStartRotation);existingDriver.gameObject.SetActive(driverInitiallyActive);}
    }
    void HideLastBus()
    {
        if(existingBus==null)return;
        foreach(var renderer in existingBus.GetComponentsInChildren<Renderer>(true))
        {
            if(renderer==null)continue;
            if(!lastBusRendererStates.ContainsKey(renderer))lastBusRendererStates.Add(renderer,renderer.enabled);
            renderer.enabled=false;
        }
    }
    void ShowLastBus()
    {
        foreach(var pair in lastBusRendererStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        lastBusRendererStates.Clear();
    }
    void OnDrawGizmosSelected()
    {
        if(existingBus==null)return;
        Gizmos.color=Color.green;Gizmos.DrawWireSphere(existingBus.TransformPoint(busSeatLocal),.3f);
        Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(existingBus.TransformPoint(busAisleLocal),.25f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(BackSeatFeet(1), sitNearFriendsDistance);
        Gizmos.DrawWireSphere(BackSeatFeet(2), sitNearFriendsDistance);
        Gizmos.color=Color.yellow;
        Gizmos.DrawWireSphere(driverSeatInspectionAnchor!=null ? driverSeatInspectionAnchor.position
            : existingBus.TransformPoint(driverSeatInspectionLocal),driverSeatInspectionRadius);
    }
}
