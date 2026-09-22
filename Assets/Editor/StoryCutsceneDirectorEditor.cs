using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(StoryCutsceneDirector))]
public class StoryCutsceneDirectorEditor : Editor
{
    int sceneIndex;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var director = (StoryCutsceneDirector)target;
        EditorGUILayout.HelpBox("Positions: open Shots > Actor Positions for each scene. Actors > Art Prefab accepts your 2D billboard sprites. Glasses Pickup Prefab must contain both the item and its Press E prompt as ONE prefab; set PickUp > Prompt Object to that prefab's child.", MessageType.Info);
        if (!Application.isPlaying && GUILayout.Button("Refresh dialogue from story text"))
        {
            Undo.RecordObject(director,"Refresh story dialogue");
            director.BuildPlaceholderStory();
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        }
        if (!Application.isPlaying && GUILayout.Button("Add editable positions for all boys in each scene"))
        {
            Undo.RecordObject(director,"Create actor placements");
            director.EnsureActorPlacements();
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            sceneIndex = EditorGUILayout.IntSlider("Preview scene",sceneIndex,1,18);
            if (GUILayout.Button("Play selected scene")) { director.Stop(); director.PlayFrom(sceneIndex-1); }
            if (GUILayout.Button("Return to gameplay")) director.Stop();
        }
    }
}
