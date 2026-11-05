using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSGICore.Editor
{
    /// <summary>Builds a small Cornell-box style test room in the open scene plus a global Volume with SSGICore enabled.</summary>
    public static class SSGICoreDemoSceneBuilder
    {
        const string kDir = "Assets/SSGICore_Demo";

        [MenuItem("Tools/SSGICore/Create Demo Content")]
        public static void Create()
        {
            Directory.CreateDirectory(kDir);

            var root = new GameObject("SSGICore Demo");
            Build(root.transform, PrimitiveType.Cube, "Floor", new Vector3(0, -0.05f, 0), new Vector3(8, 0.1f, 8), new Color(0.8f, 0.8f, 0.8f));
            Build(root.transform, PrimitiveType.Cube, "Back Wall", new Vector3(0, 2, 4), new Vector3(8, 4, 0.1f), new Color(0.8f, 0.8f, 0.8f));
            Build(root.transform, PrimitiveType.Cube, "Left Wall (red)", new Vector3(-4, 2, 0), new Vector3(0.1f, 4, 8), new Color(0.85f, 0.1f, 0.1f));
            Build(root.transform, PrimitiveType.Cube, "Right Wall (green)", new Vector3(4, 2, 0), new Vector3(0.1f, 4, 8), new Color(0.1f, 0.75f, 0.15f));
            Build(root.transform, PrimitiveType.Cube, "Box", new Vector3(-1.2f, 0.75f, 1.2f), new Vector3(1.5f, 1.5f, 1.5f), new Color(0.9f, 0.9f, 0.9f));
            Build(root.transform, PrimitiveType.Sphere, "Sphere", new Vector3(1.4f, 0.8f, 0.2f), new Vector3(1.6f, 1.6f, 1.6f), new Color(0.9f, 0.9f, 0.9f));

            var sun = new GameObject("Sun");
            sun.transform.SetParent(root.transform);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            string profilePath = kDir + "/SSGICoreDemoProfile.asset";
            AssetDatabase.CreateAsset(profile, profilePath);
            var ssgi = profile.Add<SSGICoreVolume>(true);
            ssgi.enable.Override(true);
            AssetDatabase.AddObjectToAsset(ssgi, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volGo = new GameObject("SSGICore Volume");
            volGo.transform.SetParent(root.transform);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            Selection.activeGameObject = root;
            Debug.Log("[SSGICore] Demo content created. Make sure the SSGICore renderer feature is on your Forward+ renderer.");
        }

        static void Build(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            string matPath = kDir + "/" + name.Replace(" ", "_").Replace("(", "").Replace(")", "") + ".mat";
            AssetDatabase.CreateAsset(mat, matPath);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
