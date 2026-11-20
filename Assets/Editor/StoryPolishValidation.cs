using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Explicitly requested regression checks; never saves or replaces the open scene.
[InitializeOnLoad]
public static class StoryPolishValidation
{
    const string Request = "Temp/run-story-polish-validation";
    static StoryPolishValidation() { EditorApplication.delayCall += RequestedRun; }
    static void RequestedRun()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Run();
    }
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target,value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,Flags).Invoke(target,args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [MenuItem("Tools/Never Miss The Bus/Validate Story Polish")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before running validation."); return; }
        var objects = new List<GameObject>();
        GameObject Make(string name) { var go=new GameObject(name); objects.Add(go); return go; }
        try
        {
            SchoolGroundTest.Run();
            var backTest=typeof(StoryCutsceneDirector).GetMethod("ShouldShowWalkingBack",BindingFlags.Static|BindingFlags.NonPublic);
            bool Back(Vector3 travel, Vector3 viewer, float dt=.02f) => (bool)backTest.Invoke(null,new object[]{travel,viewer,dt,false});
            Check(Back(Vector3.back*.04f,Vector3.forward),"Walking away has no back sprite");
            Check(!Back(Vector3.forward*.04f,Vector3.forward),"Walking toward viewer shows back");
            Check(!Back(Vector3.right*.04f,Vector3.forward),"Sideways travel shows back");
            Check(!Back(Vector3.up*.04f,Vector3.forward),"Grounding shows back");
            Check(!Back(Vector3.zero,Vector3.forward),"Standing actor shows back");
            Check(!Back(Vector3.back*100,Vector3.forward),"Teleport shows back");
            var holder=Make("Validation director"); holder.SetActive(false);
            var director=holder.AddComponent<StoryCutsceneDirector>();
            Set(director,"lastPlayerVisualSize",new Vector3(.5f,1.2f,.5f));
            Vector3 origin=new Vector3(4000,50,4000);
            var floor=Make("Validation floor"); floor.transform.position=origin-Vector3.up*.5f;
            floor.AddComponent<BoxCollider>().size=new Vector3(60,1,60);
            var wall=Make("Validation obstacle"); wall.transform.position=origin+new Vector3(2,1,0);
            wall.AddComponent<BoxCollider>().size=new Vector3(1,2,2);
            Physics.SyncTransforms();
            var path=(List<Vector3>)Call(director,"FindCompanionPath",origin,origin+Vector3.right*5);
            Check(path.Count>0 && Vector3.Distance(path[path.Count-1],origin+Vector3.right*5)<.7f,"No obstacle detour");
            Vector3 previous=origin;
            foreach(var point in path) { Check((bool)Call(director,"NavigationEdge",previous,point),"Path crosses obstacle"); previous=point; }
            var distant=(List<Vector3>)Call(director,"FindCompanionPath",origin,origin+Vector3.right*25);
            Check(distant.Count>0,"Follower stalls beyond local search grid");

            var bus=Make("Validation bus"); bus.transform.position=new Vector3(10,0,10);
            var player=Make("Validation player"); player.transform.position=bus.transform.position+Vector3.up;
            var controller=player.AddComponent<CharacterController>();
            var glasses=Make("Validation glasses"); glasses.transform.SetParent(bus.transform); glasses.transform.localPosition=Vector3.right;
            Set(director,"existingBus",bus.transform); Set(director,"existingPlayer",player.transform);
            Set(director,"playerController",controller); Set(director,"doorOutside",bus.transform.position);
            Vector3 relative=player.transform.position-bus.transform.position;
            Call(director,"MoveToCrashLocation");
            Check(Vector3.Distance(bus.transform.position,director.crashLocation)<.01f,"Crash stayed in city");
            Check(Vector3.Distance(player.transform.position-bus.transform.position,relative)<.01f,"Crash changed player seat offset");
            Check(glasses.transform.localPosition==Vector3.right,"Crash moved authored glasses position");
            Call(director,"MoveToCrashLocation");
            Check(Vector3.Distance(bus.transform.position,director.crashLocation)<.01f,"Crash relocation ran twice");

            var pickup=glasses.AddComponent<PickUp>(); pickup.itemTag="BrokenGlasses"; pickup.pickupRange=4;
            Check(Mathf.Approximately(pickup.EffectivePickupRange,1),"Glasses range exceeds one metre");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BrokenGlassas Variant.prefab");
            Check(prefab.GetComponentInChildren<PromptFade>(true)!=null,"Glasses has no authored pickup prompt");

            // Exercise the same runtime eligibility/readability methods as E.
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(item);
            item.transform.position=origin+Vector3.up*5; item.transform.localScale=Vector3.one*.1f;
            var probe=item.AddComponent<PickUp>(); probe.itemTag="BrokenGlasses"; probe.pickupRange=4;
            var viewer=Make("Validation pickup viewer");
            var camera=viewer.AddComponent<Camera>(); camera.nearClipPlane=.01f;
            var ui=Make("Validation pickup prompt"); ui.transform.SetParent(item.transform,false);
            ui.transform.localPosition=Vector3.up*2;
            ui.AddComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var textObject=Make("Validation pickup text"); textObject.transform.SetParent(ui.transform,false);
            var text=textObject.AddComponent<TMPro.TextMeshProUGUI>(); text.text="Press E";
            text.rectTransform.sizeDelta=new Vector2(2,1);
            var prompt=ui.AddComponent<PromptFade>(); prompt.uiContainer=ui;
            Call(prompt,"Awake"); Set(probe,"player",viewer);
            viewer.transform.position=item.transform.position-Vector3.forward*1.6f;
            Physics.SyncTransforms(); Call(probe,"Update");
            Check(!probe.CanOfferPickup,"Glasses offered outside one metre");
            viewer.transform.position=item.transform.position-Vector3.forward*.7f;
            Physics.SyncTransforms(); Call(probe,"Update");
            Check(probe.CanOfferPickup,"Nearby visible glasses cannot be offered");
            Check(!prompt.IsReadable(camera),"Hidden prompt allows pickup");
            Call(prompt,"SetAlphaImmediate",1f);
            Check(prompt.IsReadable(camera),"Visible prompt is not readable");
            var occluder=Make("Validation pickup obstruction");
            occluder.transform.position=item.transform.position-Vector3.forward*.35f;
            occluder.AddComponent<BoxCollider>().size=Vector3.one*.4f;
            Physics.SyncTransforms(); Call(probe,"Update");
            Check(!probe.CanOfferPickup && !prompt.IsReadable(camera),"Occluded item/prompt allows pickup");
            Directory.CreateDirectory("output");
            File.WriteAllText("output/story-polish-validation.txt","PASS: directional sprites (away/toward/sideways/stationary/vertical/teleport); 12 school porch rays; obstacle detour; distant follower path; isolated crash relocation; preserved player and glasses offsets; idempotent relocation; 1m glasses range; authored pickup prompt; near/far/hidden/visible/occluded pickup gating.\n");
            Debug.Log("STORY_POLISH_VALIDATION: PASS");
        }
        catch(Exception exception)
        {
            Directory.CreateDirectory("output"); File.WriteAllText("output/story-polish-validation.txt",exception.ToString());
            Debug.LogException(exception);
        }
        finally { for(int i=objects.Count-1;i>=0;i--) if(objects[i]!=null) UnityEngine.Object.DestroyImmediate(objects[i]); }
    }
}
