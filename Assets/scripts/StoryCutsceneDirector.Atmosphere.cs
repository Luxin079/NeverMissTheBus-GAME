using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class StoryCutsceneDirector
{
    [Header("Last stop atmosphere")]
    public Color normalFog = new Color(.52f, .59f, .62f);
    public Color horrorFog = new Color(.13f, .19f, .20f);
    [Range(0, .08f)] public float horrorFogDensity = .027f;
    public float forestSpacing = 8;
    [Header("Winter dusk atmosphere")]
    public Color winterDuskFog = new Color(.18f, .24f, .32f);
    public Color winterNightFog = new Color(.055f, .18f, .26f);
    [Range(0, .08f)] public float winterFogDensity = .018f;
    public Color winterAmbient = new Color(.26f, .32f, .43f);
    public Color streetLampColor = new Color(1f, .55f, .25f);
    readonly System.Collections.Generic.Dictionary<Light, Color> originalLampColors = new System.Collections.Generic.Dictionary<Light, Color>();
    bool atmosphereCaptured;
    FogMode previousFogMode;
    AmbientMode previousAmbientMode;
    public int forestTreeCount = 22;
    [Header("Terrain surface")]
    [Tooltip("Applied only when the terrain has no painted layers; the original asset is not changed.")]
    public TerrainLayer terrainSurfaceLayer;
    [Header("City outskirts - existing building assets")]
    public bool extendCity = true;
    [Range(0, 16)] public int extraCityBuildings = 8;
    [Min(8)] public float outskirtsGap = 18;
    GameObject cityExtension;
    [Header("Interior darkness")]
    [Range(.05f, 1)] public float interiorAmbientMultiplier = .32f;
    [Min(.1f)] public float interiorBlendSeconds = 1.8f;
    [Tooltip("Optional explicit indoor volumes. Use BoxColliders marked Is Trigger.")]
    public BoxCollider[] interiorVolumes;
    float interiorBlend, nextInteriorCheck;
    bool cameraIndoors;
    [Header("Forest route - optional authored markers and house")]
    public Transform forestSignPoint;
    public Transform rangerHousePoint;
    public GameObject rangerHousePrefab;
    GameObject rangerDoor;
    bool rangerDoorOpened;
    Vector3 routeSign, routeMiddle, routeHouse;
    [Header("Separate crash location")]
    public Vector3 crashLocation = new Vector3(1800, 0, 1800);
    bool crashLocationReady;
    Vector3 crashLocationDelta;
    readonly System.Collections.Generic.Dictionary<Light, float> daylightIntensity = new System.Collections.Generic.Dictionary<Light, float>();
    readonly System.Collections.Generic.Dictionary<Light, LightShadows> daylightShadows = new System.Collections.Generic.Dictionary<Light, LightShadows>();

    void MoveToCrashLocation()
    {
        if (crashLocationReady) return;
        crashLocationReady = true;
        crashLocationDelta = crashLocation - doorOutside;
        bool controllerOn = playerController.enabled;
        playerController.enabled = false;
        existingBus.position += crashLocationDelta;
        var movedMarkers = new System.Collections.Generic.HashSet<Transform>();
        foreach (var marker in new[] { postCrashStandPoint, driverSeatInspectionAnchor, driverSeatLookTarget })
            if (marker != null && !marker.IsChildOf(existingBus) && movedMarkers.Add(marker))
                marker.position += crashLocationDelta;
        if (!existingPlayer.IsChildOf(existingBus)) existingPlayer.position += crashLocationDelta;
        foreach (var boy in friends.Values)
            if (boy != null && !boy.IsChildOf(existingBus)) boy.position += crashLocationDelta;
        doorOutside += crashLocationDelta; doorThreshold += crashLocationDelta;
        aisleFeet += crashLocationDelta; seatFeet += crashLocationDelta;
        parkedBusPosition += crashLocationDelta;
        playerController.enabled = controllerOn;
        companionRoutes.Clear();
        Physics.SyncTransforms();
    }

    void UpdateInteriorAtmosphere()
    {
        if (!atmosphereCaptured || gameCamera == null) return;
        Vector3 eye = (cinematicCamera && storyCamera != null ? storyCamera : gameCamera).transform.position;
        if (Time.time > nextInteriorCheck)
        {
            nextInteriorCheck = Time.time + .25f;
            cameraIndoors = false;
            if (interiorVolumes != null) foreach (var volume in interiorVolumes)
                if (volume != null && volume.gameObject.activeInHierarchy &&
                    (volume.ClosestPoint(eye)-eye).sqrMagnitude < .001f) cameraIndoors = true;
            // A roof plus walls distinguishes buildings/bus from a tree canopy.
            bool roof = Physics.Raycast(eye, Vector3.up, 5, ~0, QueryTriggerInteraction.Ignore);
            int walls = 0;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (Physics.Raycast(eye, direction, 4, ~0, QueryTriggerInteraction.Ignore)) walls++;
            cameraIndoors |= roof && walls >= 2;
        }
        interiorBlend = Mathf.MoveTowards(interiorBlend, cameraIndoors ? 1 : 0, Time.deltaTime / Mathf.Max(.1f, interiorBlendSeconds));
        RenderSettings.ambientLight = winterAmbient * (arrivedAtLastStop ? .35f : 1f) * Mathf.Lerp(1, interiorAmbientMultiplier, interiorBlend);
        Shader.SetGlobalFloat("_StoryInteriorDarkness", interiorBlend);
        if (ambienceSource != null) ambienceSource.volume = ambientVolume * Mathf.Lerp(1, .4f, interiorBlend);
    }

    Vector3 RouteGround(Vector3 point)
    {
        if (TryWalkableGroundHeight(point, out float y)) point.y = y;
        else if (crashLocationReady) point.y = crashLocation.y;
        else if (Terrain.activeTerrain != null)
            point.y = Terrain.activeTerrain.SampleHeight(point) + Terrain.activeTerrain.transform.position.y;
        return point;
    }

    void BuildForestRoute()
    {
        routeSign = RouteGround(ForestMarkerPosition(forestSignPoint, 6));
        routeHouse = RouteGround(ForestMarkerPosition(rangerHousePoint, 7));
        routeMiddle = RouteGround(Vector3.Lerp(routeSign, routeHouse, .5f));
        Vector3 direction = routeHouse-routeSign; direction.y=0; direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        Vector3 sign = routeSign + side * 2;
        Shape(lastStopSet.transform, "Ranger sign post", PrimitiveType.Cube, sign+Vector3.up, new Vector3(.15f,2,.15f), new Color(.16f,.12f,.09f));
        Shape(lastStopSet.transform, "Ranger sign board", PrimitiveType.Cube, sign+Vector3.up*1.7f, new Vector3(2,.65f,.12f), new Color(.23f,.18f,.12f));
        Label(lastStopSet.transform,"FOREST RANGER STATION →", sign+Vector3.up*1.7f-Vector3.forward*.08f);
        var label = lastStopSet.transform.GetChild(lastStopSet.transform.childCount-1);
        label.localScale=Vector3.one*.14f;
        label.gameObject.AddComponent<YBillboard>().frontFacesNegativeZ=true;
        // A continuous walkable path; existing terrain/buildings are untouched.
        Vector3 start = RouteGround(doorOutside + existingBus.forward * 2);
        Vector3[] stops = { start, routeSign, routeMiddle, routeHouse };
        for (int segment=0; segment<stops.Length-1; segment++)
        {
            Vector3 a=stops[segment], b=stops[segment+1];
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/2));
            for(int n=0;n<count;n++)
            {
                Vector3 p=RouteGround(Vector3.Lerp(a,b,(n+.5f)/count));
                Shape(lastStopSet.transform,"Forest path",PrimitiveType.Cube,p-Vector3.up*.08f,new Vector3(3.4f,.16f,Vector3.Distance(a,b)/count+.15f),new Color(.19f,.22f,.24f));
                var tile=lastStopSet.transform.GetChild(lastStopSet.transform.childCount-1);
                Vector3 forward=b-a;forward.y=0;tile.rotation=Quaternion.LookRotation(forward);
            }
        }
        if(rangerHousePrefab!=null) { Instantiate(rangerHousePrefab,routeHouse,Quaternion.LookRotation(direction),lastStopSet.transform);return; }
        // Placeholder room with a real doorway and collision, not a solid cube.
        var house=new GameObject("Ranger cabin - replace with asset");house.transform.SetParent(lastStopSet.transform);
        house.transform.SetPositionAndRotation(routeHouse,Quaternion.LookRotation(direction));
        CabinPart(house.transform,"Floor",new Vector3(0,-.1f,3),new Vector3(7,.2f,7));
        CabinPart(house.transform,"Roof",new Vector3(0,3.2f,3),new Vector3(7,.2f,7));
        CabinPart(house.transform,"Left wall",new Vector3(-3.4f,1.5f,3),new Vector3(.2f,3,7));
        CabinPart(house.transform,"Right wall",new Vector3(3.4f,1.5f,3),new Vector3(.2f,3,7));
        CabinPart(house.transform,"Rear wall",new Vector3(0,1.5f,6.4f),new Vector3(7,3,.2f));
        CabinPart(house.transform,"Front left",new Vector3(-2.15f,1.5f,-.4f),new Vector3(2.7f,3,.2f));
        CabinPart(house.transform,"Front right",new Vector3(2.15f,1.5f,-.4f),new Vector3(2.7f,3,.2f));
        CabinPart(house.transform,"Door",new Vector3(0,1.3f,-.4f),new Vector3(1.5f,2.6f,.15f));
        rangerDoor=house.transform.GetChild(house.transform.childCount-1).gameObject;
    }

    Vector3 ForestMarkerPosition(Transform marker, int scene)
    {
        if (marker == null) return DestinationForScene(scene);
        Vector3 position = marker.position;
        // Existing authored city markers retain their offsets at the new stop.
        if (crashLocationReady && Vector3.Distance(position, crashLocation) > 300)
            position += crashLocationDelta;
        return position;
    }
    void CabinPart(Transform house,string name,Vector3 position,Vector3 size)
    {
        Shape(house,name,PrimitiveType.Cube,house.TransformPoint(position),size,new Color(.18f,.14f,.11f));
        house.GetChild(house.childCount-1).rotation=house.rotation;
    }

    System.Collections.IEnumerator ForestStoryAction(int scene,string action)
    {
        Vector3 target; string objective;
        if(scene==5 && action.Contains("walk through the village"))
        { target=RouteGround(Vector3.Lerp(doorOutside,routeSign,.45f)); objective="Zoek met je vrienden hulp in het dorp."; }
        else if(scene==5 && action.Contains("edge of the village"))
        { target=routeSign; objective="Loop naar het bord bij de bosrand."; }
        else if(scene==5 && action.Contains("closer to the sign"))
        { target=routeSign; objective="Bekijk het bord van de boswachter."; }
        else if(scene==6 && action.Contains("walk into the forest"))
        { target=routeMiddle; objective="Volg het pad het bos in."; }
        else if(scene==6 && action.Contains("deeper into the forest"))
        { target=RouteGround(Vector3.Lerp(routeMiddle,routeHouse,.65f)); objective="Volg het bospad naar het huis."; }
        else if(scene==6 && action.Contains("walk toward it"))
        { target=routeHouse; objective="Loop naar het huis van de boswachter."; }
        else if(scene==7 && action.Contains("opens the door"))
        {
            seatedDialogueCamera=false; SetPlayerControl(true); SetObjective("Druk op E om de deur te openen.");
            while(Time.timeScale<=0 || !Input.GetKeyDown(KeyCode.E)) yield return null;
            if(!rangerDoorOpened)
            {
                PlayEffect(busDoorSound,3);
                if(rangerDoor!=null) rangerDoor.SetActive(false);
                rangerDoorOpened=true;
            }
            SetPlayerControl(false); seatedDialogueCamera=true;holdSeatedView=true;SetObjective("");yield break;
        }
        else if(scene==7 && action.Contains("enter the house"))
        { target=routeHouse+(routeHouse-routeSign).normalized*2.2f; objective="Ga voorzichtig naar binnen."; }
        else yield break;
        seatedDialogueCamera=false;SetPlayerControl(true);SetObjective(objective);
        while(Vector3.ProjectOnPlane(existingPlayer.position-target,Vector3.up).sqrMagnitude>2.25f)
        { FollowFriendsNearPlayer();yield return null; }
        SetPlayerControl(false);SetObjective("");
        seatedDialogueCamera=true;holdSeatedView=true;
    }

    void BuildCityOutskirts()
    {
        if (!extendCity || townSource == null || cityExtension != null) return;
        Transform buildings = townSource.Find("Buildings");
        if (buildings == null || buildings.childCount == 0) return;
        var templates = new System.Collections.Generic.List<Transform>();
        Bounds city = default;
        bool found = false;
        foreach (var visual in townSource.GetComponentsInChildren<MeshRenderer>())
        {
            if (!found) { city = visual.bounds; found = true; }
            else city.Encapsulate(visual.bounds);
        }
        if (!found) return;
        foreach (Transform candidate in buildings)
            if (candidate.gameObject.activeSelf && candidate.GetComponentInChildren<MeshRenderer>() != null)
                templates.Add(candidate);
        if (templates.Count == 0) return;
        cityExtension = new GameObject("City outskirts - reused buildings");
        cityExtension.transform.SetParent(townSource, false);
        float[] nextZ = { city.min.z, city.min.z };
        for (int i = 0; i < extraCityBuildings; i++)
        {
            Transform template = templates[i % templates.Count];
            var renderers = template.GetComponentsInChildren<MeshRenderer>();
            Bounds shape = renderers[0].bounds;
            foreach (var visual in renderers) shape.Encapsulate(visual.bounds);
            int side = i % 2;
            Vector3 destination = new Vector3(side == 0 ? city.min.x - outskirtsGap - shape.extents.x
                : city.max.x + outskirtsGap + shape.extents.x, shape.min.y, nextZ[side] + shape.extents.z);
            nextZ[side] += shape.size.z + outskirtsGap;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 local = destination - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < shape.extents.x || local.z < shape.extents.z ||
                    local.x + shape.extents.x > size.x || local.z + shape.extents.z > size.z) continue;
                destination.y = terrain.SampleHeight(destination) + terrain.transform.position.y;
            }
            var clone = Instantiate(template.gameObject, cityExtension.transform);
            clone.name = template.name + " - outskirts " + (i + 1);
            clone.transform.SetPositionAndRotation(template.position, template.rotation);
            clone.transform.localScale = template.lossyScale;
            // Account for a scaled City root without modifying the user's original layout.
            Vector3 parentScale = cityExtension.transform.lossyScale;
            clone.transform.localScale = new Vector3(template.lossyScale.x / parentScale.x,
                template.lossyScale.y / parentScale.y, template.lossyScale.z / parentScale.z);
            clone.transform.position += destination - new Vector3(shape.center.x, shape.min.y, shape.center.z);
        }
    }
    GameObject lastStopSet;
    Terrain runtimeTerrain;
    TerrainCollider runtimeTerrainCollider;
    TerrainData originalTerrainData, temporaryTerrainData;
    bool previousFog;
    Color previousFogColor, previousAmbient;
    float previousFogDensity;
    float previousPostExposure;
    bool hadPostExposure;

    void PrepareTerrainSurface()
    {
        runtimeTerrain = FindFirstObjectByType<Terrain>();
        if (runtimeTerrain == null || runtimeTerrain.terrainData == null || terrainSurfaceLayer == null ||
            runtimeTerrain.terrainData.terrainLayers.Length > 0) return;
        originalTerrainData = runtimeTerrain.terrainData;
        temporaryTerrainData = Instantiate(originalTerrainData);
        temporaryTerrainData.name = originalTerrainData.name + " (runtime surface)";
        temporaryTerrainData.terrainLayers = new[] { terrainSurfaceLayer };
        runtimeTerrain.terrainData = temporaryTerrainData;
        runtimeTerrainCollider = runtimeTerrain.GetComponent<TerrainCollider>();
        if (runtimeTerrainCollider != null) runtimeTerrainCollider.terrainData = temporaryTerrainData;
    }

    void RestoreTerrainSurface()
    {
        if (cityExtension != null) Destroy(cityExtension);
        if (temporaryTerrainData == null) return;
        if (runtimeTerrain != null) runtimeTerrain.terrainData = originalTerrainData;
        if (runtimeTerrainCollider != null) runtimeTerrainCollider.terrainData = originalTerrainData;
        Destroy(temporaryTerrainData);
        temporaryTerrainData = null;
    }

    void ApplyAtmosphere(bool horror)
    {
        if (!atmosphereCaptured)
        {
            atmosphereCaptured = true;
            previousFogMode = RenderSettings.fogMode;
            previousAmbientMode = RenderSettings.ambientMode;
            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogDensity = RenderSettings.fogDensity;
            previousAmbient = RenderSettings.ambientLight;
        }
        RenderSettings.fog = true;
        RenderSettings.fogColor = horror ? winterNightFog : winterDuskFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = horror ? horrorFogDensity : winterFogDensity;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = horror ? winterAmbient * .35f : winterAmbient;
        Shader.SetGlobalFloat("_StoryNightDarkness", horror ? 1 : 0);
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            if (!daylightIntensity.ContainsKey(light))
            { daylightIntensity.Add(light,light.intensity); daylightShadows.Add(light,light.shadows); }
            light.intensity = horror ? Mathf.Min(daylightIntensity[light]*.2f,.22f) : daylightIntensity[light];
            light.shadows = horror ? LightShadows.Soft : daylightShadows[light];
        }
        if (townSource != null)
            foreach (var lamp in townSource.GetComponentsInChildren<Light>(true))
            {
                if (lamp.type == LightType.Directional || (existingBus != null && lamp.transform.IsChildOf(existingBus))) continue;
                if (!originalLampColors.ContainsKey(lamp)) originalLampColors.Add(lamp, lamp.color);
                lamp.color = streetLampColor;
            }
        var volume = FindFirstObjectByType<Volume>();
        if (volume != null && volume.profile != null && volume.profile.TryGet<ColorAdjustments>(out var adjustment))
        {
            if (!hadPostExposure) { previousPostExposure = adjustment.postExposure.value; hadPostExposure = true; }
            adjustment.postExposure.value = horror ? -1.1f : -.15f;
        }
    }

    void BuildLastStop()
    {
        if (lastStopSet != null) return;
        MoveToCrashLocation();
        lastStopSet = new GameObject("Last stop - inspector configurable");
        // Keep the crash in the actual playable bus. Duplicating the entire City
        // here caused long editor stalls and an empty, disconnected destination.
        lastStopSet.transform.position = existingBus.position;
        Vector3 center = lastStopSet.transform.position;
        Shape(lastStopSet.transform, "Isolated forest placeholder ground", PrimitiveType.Cube,
            crashLocation + existingBus.forward * 35 - Vector3.up * .5f,
            new Vector3(180, 1, 180), new Color(.16f,.19f,.22f));
        Physics.SyncTransforms();
        BuildForestRoute();
        for (int i = 0; i < forestTreeCount; i++)
        {
            Vector3 along = Vector3.Lerp(routeSign, routeHouse, (i/2f)/Mathf.Max(1,forestTreeCount/2f));
            Vector3 side = Vector3.Cross(Vector3.up,(routeHouse-routeSign).normalized);
            Vector3 position = RouteGround(along + side * (i % 2 == 0 ? -5 : 5));
            if (forestTreePrefab != null)
            {
                var tree = Instantiate(forestTreePrefab, position, Quaternion.Euler(0, i * 83, 0), lastStopSet.transform);
                PrepareForestMaterials(tree);
            }
            else Shape(lastStopSet.transform, "Forest tree", PrimitiveType.Cylinder, position + Vector3.up * 2.5f, new Vector3(.7f, 5, .7f), new Color(.07f, .12f, .09f));
        }
    }

    void PrepareForestMaterials(GameObject tree)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || GraphicsSettings.currentRenderPipeline == null) return;
        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var source = slots[i];
                if (source == null || (source.shader != null && source.shader.isSupported &&
                    source.shader.name.StartsWith("Universal Render Pipeline/"))) continue;
                // Imported foliage uses legacy shaders. Convert runtime instances,
                // keeping their texture/alpha; never rewrite the asset pack.
                var material = new Material(shader) { name = source.name + " (forest URP)" };
                material.SetTexture("_BaseMap", source.mainTexture);
                material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : Color.white);
                material.SetFloat("_Smoothness", 0);
                material.SetFloat("_AlphaClip", 1); material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .4f);
                material.SetFloat("_Cull", 0); material.renderQueue = 2450;
                materials.Add(material); slots[i] = material;
            }
            renderer.sharedMaterials = slots;
        }
    }

    Vector3 DestinationForScene(int index)
    {
        if (crashLocationReady && index >= 5)
            return crashLocation + existingBus.forward * (index == 6 ? 30 : index >= 7 ? 60 : 8);
        if (index < shots.Length && shots[index] != null && shots[index].stage != null &&
            !shots[index].stage.IsChildOf(transform))
            return shots[index].stage.position;
        Vector3 local;
        if (index == 6) local = new Vector3(0, 0, 39);
        else if (index == 7) local = new Vector3(0, 0, 63);
        else if (index == 8) local = new Vector3(0, 0, 68);
        else if (index == 9) local = new Vector3(0, 0, 28);
        else if (index == 10) local = new Vector3(-22, 0, 10);
        else local = new Vector3(-22 + (index % 2 == 0 ? 2 : -2), 0, 15 + index % 3);
        return existingBus.position + existingBus.right * local.x + existingBus.forward * local.z + Vector3.up * .7f;
    }

    void CleanupAtmosphere()
    {
        crashLocationReady = false;
        Shader.SetGlobalFloat("_StoryNightDarkness",0);
        foreach(var pair in daylightIntensity) if(pair.Key!=null)
        { pair.Key.intensity=pair.Value;pair.Key.shadows=daylightShadows[pair.Key]; }
        daylightIntensity.Clear(); daylightShadows.Clear();
        Shader.SetGlobalFloat("_StoryInteriorDarkness",0);
        interiorBlend=0; rangerDoorOpened=false; rangerDoor=null;
        if (lastStopSet != null) Destroy(lastStopSet);
        if (!atmosphereCaptured) return;
        atmosphereCaptured = false;
        RenderSettings.fogMode = previousFogMode;
        RenderSettings.ambientMode = previousAmbientMode;
        foreach (var pair in originalLampColors) if (pair.Key != null) pair.Key.color = pair.Value;
        originalLampColors.Clear();
        RenderSettings.fog = previousFog;
        RenderSettings.fogColor = previousFogColor;
        RenderSettings.fogDensity = previousFogDensity;
        RenderSettings.ambientLight = previousAmbient;
        if (hadPostExposure)
        {
            var volume = FindFirstObjectByType<Volume>();
            if (volume != null && volume.profile != null && volume.profile.TryGet<ColorAdjustments>(out var adjustment))
                adjustment.postExposure.value = previousPostExposure;
            hadPostExposure = false;
        }
    }
}
