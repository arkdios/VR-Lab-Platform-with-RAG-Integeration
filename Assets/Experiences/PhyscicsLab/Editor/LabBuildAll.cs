#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 0 Build working lab. Runs the four steps in the right order:
    /// 1 floor and UI fixes, 3 physics repair, 2 real string, 4 connect system.
    /// Stops at the first step that fails. Safe to run again.
    /// </summary>
    public static class LabBuildAll
    {
        [MenuItem("Lab/0 Build working lab")]
        private static void Run()
        {
            var log = new StringBuilder("[LabBuildAll]\n");
            bool ok = LabSceneFixes.RunAll(log)
                      && LabPhysicsRepair.Run(log)
                      && LabStringBuilder.Run(log)
                      && LabConnectBuilder.Run(log);

            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                log.AppendLine("All steps done. Save the scene (Ctrl+S) and press Play.");
                Debug.Log(log.ToString());
            }
            else
            {
                Debug.LogError(log.ToString());
            }
        }
    }
}
#endif
