#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Small clean-up menu items for the lab.
    /// </summary>
    public static class LabMaintenance
    {
        // Same name as the default File Name on LabProgressStore.
        private const string SavedFileName = "lab_progress.json";

        /// <summary>
        /// LabProgressStore puts the ring and the spring scale back where they were at the end of the last run.
        /// After the lab is rebuilt those old places are wrong, and the string starts too long.
        /// </summary>
        [MenuItem("Lab/Delete saved progress")]
        private static void DeleteSavedProgress()
        {
            string path = Path.Combine(Application.persistentDataPath, SavedFileName);
            if (!File.Exists(path))
            {
                Debug.Log($"[LabMaintenance] No saved progress at {path}. Nothing to delete.");
                return;
            }

            File.Delete(path);
            Debug.Log($"[LabMaintenance] Deleted {path}. The lab starts fresh at the next Play.");
        }
    }
}
#endif
