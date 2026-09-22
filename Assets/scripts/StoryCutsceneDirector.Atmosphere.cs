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
    public int forestTreeCount = 22;
    [Header("Terrain surface")]
    [Tooltip("Applied only when the terrain has no painted layers; the original asset is not changed.")]
    public TerrainLayer terrainSurfaceLayer;
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
        if (temporaryTerrainData == null) return;
        if (runtimeTerrain != null) runtimeTerrain.terrainData = originalTerrainData;
        if (runtimeTerrainCollider != null) runtimeTerrainCollider.terrainData = originalTerrainData;
        Destroy(temporaryTerrainData);
        temporaryTerrainData = null;
    }

    void ApplyAtmosphere(bool horror)
    {
        if (horror)
        {
            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogDensity = RenderSettings.fogDensity;
            previousAmbient = RenderSettings.ambientLight;
        }
        RenderSettings.fog = horror;
        RenderSettings.fogColor = horror ? horrorFog : normalFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = horror ? horrorFogDensity : .003f;
        if (horror) RenderSettings.ambientLight = new Color(.12f, .16f, .19f);
        var volume = FindFirstObjectByType<Volume>();
        if (volume != null && volume.profile != null && volume.profile.TryGet<ColorAdjustments>(out var adjustment))
        {
            if (horror) { previousPostExposure = adjustment.postExposure.value; hadPostExposure = true; }
            adjustment.postExposure.value = horror ? -.8f : previousPostExposure;
        }
    }

    void BuildLastStop()
    {
        if (lastStopSet != null) return;
        lastStopSet = new GameObject("Last stop - inspector configurable");
        // Keep the crash in the actual playable bus. Duplicating the entire City
        // here caused long editor stalls and an empty, disconnected destination.
        lastStopSet.transform.position = existingBus.position;
        Vector3 center = lastStopSet.transform.position;
        for (int i = 0; i < forestTreeCount; i++)
        {
            Vector3 position = center + existingBus.forward * (18 + i / 2 * forestSpacing) + existingBus.right * (i % 2 == 0 ? -11 : 11);
            if (forestTreePrefab != null) Instantiate(forestTreePrefab, position, Quaternion.Euler(0, i * 83, 0), lastStopSet.transform);
            else Shape(lastStopSet.transform, "Forest tree", PrimitiveType.Cylinder, position + Vector3.up * 2.5f, new Vector3(.7f, 5, .7f), new Color(.07f, .12f, .09f));
        }
    }

    Vector3 DestinationForScene(int index)
    {
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
        if (lastStopSet != null) Destroy(lastStopSet);
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
