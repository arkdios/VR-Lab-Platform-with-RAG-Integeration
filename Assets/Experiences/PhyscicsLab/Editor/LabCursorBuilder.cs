#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 6 Add aim cursor. Adds AimCursor next to DesktopPointer and fills it in.
    /// Also runs as the last step of Lab > 0. Safe to run again.
    /// </summary>
    public static class LabCursorBuilder
    {
        private const string HandPrefabPath = "Assets/PolyOne/Free VR Hands/Prefabs/Free Pack - VR Hands ( Rigged ).prefab";

        [MenuItem("Lab/6 Add aim cursor")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabCursorBuilder]\n");
            if (Run(log))
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log(log.ToString());
            }
            else
            {
                Debug.LogError(log.ToString());
            }
        }

        public static bool Run(StringBuilder log)
        {
            DesktopPointer pointer = Object.FindFirstObjectByType<DesktopPointer>(FindObjectsInactive.Include);
            if (pointer == null)
            {
                log.AppendLine("  ERROR: there is no DesktopPointer in the scene.");
                return false;
            }

            AimCursor cursor = pointer.GetComponent<AimCursor>();
            if (cursor == null) cursor = Undo.AddComponent<AimCursor>(pointer.gameObject);
            LabSerialized.SetObject(cursor, "pointer", pointer);
            LabSerialized.SetObject(cursor, "viewCamera", LabSerialized.ReadReference<Camera>(pointer, "viewCamera"));

            GameObject hand = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            if (hand == null) log.AppendLine($"  Warning: no hand model at {HandPrefabPath}. Only the aim dot will show.");
            LabSerialized.SetObject(cursor, "handPrefab", hand);

            log.AppendLine($"  Aim cursor on {pointer.name}: a dot at the screen centre and the right hand at the aim point.");
            return true;
        }
    }
}
#endif
