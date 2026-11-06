using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with -batchmode -executeMethod ForestSmokeTest.Run (do not pass -quit).
[InitializeOnLoad]
public static class ForestSmokeTest
{
    const string Key="NeverMissTheBus.ForestSmoke";
    static double started;
    static bool checkedRoute;
    static int errors;
    static ForestSmokeTest()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                started=EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
        };
    }
    public static void Run()
    {
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string message,string trace,LogType type)
    { if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) errors++; }
    static void Tick()
    {
        double elapsed=EditorApplication.timeSinceStartup-started;
        if(!checkedRoute && elapsed>12)
        {
            checkedRoute=true;
            try
            {
                var director=UnityEngine.Object.FindFirstObjectByType<StoryCutsceneDirector>();
                if(director==null)throw new Exception("Story director missing");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(StoryCutsceneDirector).GetMethod("BuildLastStop",flags).Invoke(director,null);
                if(GameObject.Find("Ranger sign post")==null)throw new Exception("Forest sign missing");
                if(GameObject.Find("Forest path")==null)throw new Exception("Walkable path missing");
                if(director.rangerHousePrefab==null && GameObject.Find("Ranger cabin - replace with asset")==null)
                    throw new Exception("Placeholder cabin missing");
                var shader=Resources.Load<Shader>("Characters/WinterSprite");
                if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Character shader failed");
                Debug.Log("FOREST_SMOKE: route, cabin, sign and shader validated in Play Mode.");
            }
            catch(Exception ex){ errors++;Debug.LogException(ex); }
        }
        if(elapsed<20)return;
        SessionState.SetBool(Key,false);
        EditorApplication.update-=Tick;
        Debug.Log("FOREST_SMOKE: completed; errors="+errors);
        EditorApplication.Exit(errors==0?0:1);
    }
}
