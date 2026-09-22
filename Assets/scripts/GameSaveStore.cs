using System;
using System.IO;
using UnityEngine;

[Serializable]
public class GameSaveState
{
    public int version = 1;
    public string sceneName = "SampleScene";
    public string checkpoint = "school";
    public Vector3 playerPosition;
    public Quaternion playerRotation = Quaternion.identity;
    public bool hasPlayerPosition;
    public int storyScene;
    public bool glassesCollected;
    public bool driverSeatFound;
}

[Serializable]
public class GameSettingsState
{
    public int version = 1;
    public float mouseSensitivity = 2;
}

/// <summary>Small JSON save files in Application.persistentDataPath.</summary>
public static class GameSaveStore
{
    public static string ProgressPath => Path.Combine(Application.persistentDataPath, "nevermissthebus-save.json");
    public static string SettingsPath => Path.Combine(Application.persistentDataPath, "nevermissthebus-settings.json");

    public static GameSaveState LoadGame()
    {
        try
        {
            if (!File.Exists(ProgressPath)) return null;
            var save = JsonUtility.FromJson<GameSaveState>(File.ReadAllText(ProgressPath));
            return save != null && save.version == 1 ? save : null;
        }
        catch (Exception error) { Debug.LogWarning("Cannot load game save: " + error.Message); return null; }
    }

    public static bool SaveGame(GameSaveState state)
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(ProgressPath, JsonUtility.ToJson(state, true));
            return true;
        }
        catch (Exception error) { Debug.LogError("Cannot save game: " + error.Message); return false; }
    }

    public static float LoadSensitivity(float fallback = 2)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return fallback;
            var settings = JsonUtility.FromJson<GameSettingsState>(File.ReadAllText(SettingsPath));
            return settings != null && settings.version == 1
                ? Mathf.Clamp(settings.mouseSensitivity, .1f, 5) : fallback;
        }
        catch (Exception error) { Debug.LogWarning("Cannot load settings: " + error.Message); return fallback; }
    }

    public static void SaveSensitivity(float value)
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(new GameSettingsState {
                mouseSensitivity = Mathf.Clamp(value, .1f, 5)
            }, true));
        }
        catch (Exception error) { Debug.LogError("Cannot save settings: " + error.Message); }
    }

    public static void ResetProgress()
    {
        try { if (File.Exists(ProgressPath)) File.Delete(ProgressPath); }
        catch (Exception error) { Debug.LogError("Cannot reset game save: " + error.Message); }
    }
}
