using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Opt-in playtest checkpoint override: does not read/write the player's save.
[InitializeOnLoad]
public static class StoryPlaytest
{
    const string Key="NeverMissTheBus.Playtest";
    static double next;
    static bool started;
    static StoryPlaytest()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if(state==PlayModeStateChange.ExitingPlayMode) SessionState.EraseString(Key);
        };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Prepare()
    {
        if(string.IsNullOrEmpty(SessionState.GetString(Key,""))) return;
        var director=Object.FindFirstObjectByType<StoryCutsceneDirector>();
        if(director!=null) director.playOnStart=false;
        next=EditorApplication.timeSinceStartup+3; started=false;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/story-playtest"))
        {
            SessionState.SetString(Key,File.ReadAllText("Temp/story-playtest").Trim());
            File.Delete("Temp/story-playtest"); EditorApplication.isPlaying=true; return;
        }
        string checkpoint=SessionState.GetString(Key,"");
        if(string.IsNullOrEmpty(checkpoint)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next) return;
        var director=Object.FindFirstObjectByType<StoryCutsceneDirector>();
        if(director==null) return;
        if(!started)
        {
            typeof(StoryCutsceneDirector).GetField("resumeState",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(director,new GameSaveState{checkpoint=checkpoint,
                    glassesCollected=checkpoint=="exploring",driverSeatFound=checkpoint=="exploring"});
            director.Play(); started=true;
        }
        Directory.CreateDirectory("output");
        File.WriteAllText("output/story-playtest-state.json",JsonUtility.ToJson(director.CaptureSaveState(),true));
        next=EditorApplication.timeSinceStartup+1;
    }
    [MenuItem("Tools/Never Miss The Bus/End Checkpoint Playtest")]
    static void End() { SessionState.EraseString(Key); EditorApplication.isPlaying=false; }
}
