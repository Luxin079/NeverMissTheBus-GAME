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
    [Tooltip("Player reaches this fraction of the aisle before the seating fade.")]
    [Range(.1f,.9f)] public float aisleMidpoint=.5f;
    [Tooltip("Player root height above the bus floor after the seating fade.")]
    public float seatedPlayerHeight=0;
    [Tooltip("Visual origin height above a bus seat. Raise this when a sprite sinks into the floor.")]
    public float companionSeatHeight=0;
    [Tooltip("Bus layer when the player boards. Unity layer 1 is TransparentFX.")]
    [Range(0,31)] public int boardingLayer=1;
    public float seatUseDistance=1.1f; // Serialized legacy setting.
    public float storyWalkSpeed=3; // Serialized legacy setting.
    [Tooltip("Optional exact bus stopping point in front of the shelter. An empty Transform in the scene works well.")]
    public Transform busStopParkingAnchor;
    [Tooltip("Eye height above the ground for the existing Player camera during first-person play.")]
    [Min(.5f)] public float firstPersonEyeHeight=1.12f;
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
    readonly List<Renderer> hiddenPlayerRenderers=new List<Renderer>();
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
        foreach (var seatAnchor in new[] { ethanSeatAnchor, lucasSeatAnchor, noahSeatAnchor })
            if (seatAnchor != null && !seatAnchor.IsChildOf(existingBus))
                seatAnchor.SetParent(existingBus, true);
        playerStartLayer=existingPlayer.gameObject.layer;
        playerCameraStartLocal=gameCamera.transform.localPosition;
        playerCameraStartNearClip=gameCamera.nearClipPlane;
        float scaleY=Mathf.Max(.01f,Mathf.Abs(existingPlayer.lossyScale.y));
        Vector3 eyeLocal=playerCameraStartLocal;
        eyeLocal.y=(firstPersonEyeHeight-PlayerFeetOffset)/scaleY;
        gameCamera.transform.localPosition=eyeLocal;
        playableCameraLocal=eyeLocal;
        gameCamera.nearClipPlane=.08f;
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
        foreach(var renderer in existingPlayer.GetComponents<Renderer>())
            if(renderer.enabled){hiddenPlayerRenderers.Add(renderer);renderer.enabled=false;}
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
        playerMovement.enabled=enabled && suspended.Contains(playerMovement);
        playerLook.enabled=enabled && suspended.Contains(playerLook);
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
        foreach(var renderer in hiddenPlayerRenderers)if(renderer!=null)renderer.enabled=true;
        hiddenPlayerRenderers.Clear();
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
        Gizmos.color=Color.yellow;
        Gizmos.DrawWireSphere(driverSeatInspectionAnchor!=null ? driverSeatInspectionAnchor.position
            : existingBus.TransformPoint(driverSeatInspectionLocal),driverSeatInspectionRadius);
    }
}
