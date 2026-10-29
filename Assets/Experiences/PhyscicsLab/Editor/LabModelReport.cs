#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Report model sizes. Prints the size, centre, lowest and highest point and the
    /// child names of each lab model, so sockets and colliders can be placed without guessing.
    /// It uses a temporary copy and deletes it. Nothing in your scene is changed.
    /// </summary>
    public static class LabModelReport
    {
        private const string Folder = "Assets/Experiences/PhyscicsLab/Prefabs/";

        private static readonly string[] ModelNames =
        {
            "ForceTable", "LabBench", "LabChair", "PulleyClamp", "Ring",
            "HookedMass50g", "HookedMass100g", "HookedMass200g", "SpringScale (1)"
        };

        [MenuItem("Lab/Report model sizes")]
        private static void Report()
        {
            foreach (string modelName in ModelNames)
            {
                GameObject asset = Load(modelName);
                if (asset == null)
                {
                    Debug.LogWarning($"[LabModelReport] {modelName}: no prefab or fbx found");
                    continue;
                }

                GameObject copy = Object.Instantiate(asset); // temporary copy
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                Renderer[] renderers = copy.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"[LabModelReport] {modelName}: no renderers");
                    Object.DestroyImmediate(copy);
                    continue;
                }

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                var text = new StringBuilder();
                text.Append($"[LabModelReport] {modelName}: size {bounds.size.ToString("F3")} ");
                text.Append($"centre {bounds.center.ToString("F3")} minY {bounds.min.y:F3} maxY {bounds.max.y:F3} ");
                text.Append($"renderers {renderers.Length} colliders {copy.GetComponentsInChildren<Collider>().Length}");
                foreach (Transform child in copy.GetComponentsInChildren<Transform>())
                {
                    text.Append($"\n   {child.name} at {child.localPosition.ToString("F3")}");
                }
                Debug.Log(text.ToString());
                Object.DestroyImmediate(copy);
            }
        }

        private static GameObject Load(string modelName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + modelName + ".prefab");
            return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(Folder + modelName + ".fbx");
        }
    }
}
#endif
