using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Keep Editor Play consistent with the built game's first scene.</summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private const string Preference = "NeverMissTheBus.PlayStartsInMainMenu";
    private const string MenuItem = "Tools/Never Miss The Bus/Play Starts In Main Menu";

    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem(MenuItem)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true));
        Apply();
    }

    [MenuItem(MenuItem, true)]
    private static bool Validate()
    {
        Menu.SetChecked(MenuItem, EditorPrefs.GetBool(Preference, true));
        return true;
    }

    private static void Apply()
    {
        EditorSceneManager.playModeStartScene = EditorPrefs.GetBool(Preference, true)
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene)
            : null;
    }
}
