using UnityEngine;
using UnityEditor;

public static class SchoolGroundTest
{
    [MenuItem("Tools/Never Miss The Bus/Test School Porch Collision")]
    public static void Run()
    {
        const string path = "Assets/ModelAssets/Veridian/BuildingBuilder/Demo/Assets/Prefabs/1Story_CleanStone_Shingles.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var collider = root.GetComponentInChildren<MeshCollider>();
            if (collider == null) throw new System.Exception("School porch MeshCollider missing");
            Physics.SyncTransforms();
            foreach (float x in new[] { -1.3f, 0f, 1.3f })
            foreach (float z in new[] { 4.85f, 5.4f, 6f, 7f })
            {
                var ray = new Ray(root.transform.TransformPoint(new Vector3(x, 2, z)), Vector3.down);
                if (!collider.Raycast(ray, out var hit, 2.1f) || hit.normal.y < .7f ||
                    Mathf.Abs(root.transform.InverseTransformPoint(hit.point).y - .510755f) > .03f)
                    throw new System.Exception("School porch collision failed at " + x + ", " + z);
            }
            Debug.Log("SCHOOL_GROUND: all 12 doorway/porch samples passed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
