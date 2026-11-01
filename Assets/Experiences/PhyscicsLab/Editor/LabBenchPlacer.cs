#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 8 Put lab on bench.
    /// The working lab (LabEquipments: table, ring, pulleys, strings, spring scale) stands on the floor, so the
    /// hanging masses have no room to hang. The bench in "LabProps (Temp)" has a copy of the table, the ring and
    /// the two pulley clamps on it, but they have no strings. This tool moves the working lab onto the bench, where
    /// the table copy stands, and hides the copies (it deletes nothing). The three spare hooked masses are moved
    /// off the table side so they do not overlap the working pulley and its mass.
    /// Run Lab > 0 afterwards, so the strings, pulleys and masses are placed again at the new height.
    /// Safe to run twice: when the table copy is hidden, it does nothing.
    /// </summary>
    public static class LabBenchPlacer
    {
        private const string EquipmentName = "LabEquipments";
        private const string PropsRootName = "LabProps (Temp)";
        private const string TableCopyName = "ForceTable";
        private const string SpareMassPrefix = "HookedMass";
        private const float SpareMassGapMetres = 0.2f;   // spare masses stand this far outside the table rim

        // The copies that the working lab replaces.
        private static readonly string[] CopiesToHide = { "ForceTable", "Ring", "PulleyClamp1", "PulleyClamp2" };

        [MenuItem("Lab/8 Put lab on bench")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabBenchPlacer]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentName);
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (equipment == null) return Fail(log, $"no object named {EquipmentName}.");
            if (props == null) return Fail(log, $"no object named {PropsRootName}.");

            Transform tableCopy = props.Find(TableCopyName);
            if (tableCopy == null) return Fail(log, $"{PropsRootName} has no child named {TableCopyName}.");
            if (!tableCopy.gameObject.activeSelf)
            {
                log.AppendLine("  The ForceTable copy is hidden, so the lab is already on the bench. Nothing to do.");
                return true;
            }
            if (!TryMeasureWorld(tableCopy, out Bounds table)) return Fail(log, "the ForceTable copy has no mesh to measure.");

            // The working table stands on the floor of LabEquipments, with its middle over the middle of LabEquipments.
            // So LabEquipments goes where the copy's feet are, under the copy's middle.
            Vector3 target = new Vector3(table.center.x, table.min.y, table.center.z);
            Undo.RecordObject(equipment, "Put lab on bench");
            Vector3 before = equipment.position;
            equipment.position = target;
            log.AppendLine($"  {EquipmentName} moved from {Format(before)} to {Format(target)} (the feet of the table copy).");

            MoveSpareMasses(props, table);
            HideCopies(props, log);
            log.AppendLine("  Next: Lab > Delete saved progress, then Lab > 0 Build working lab, then save the scene.");
            return true;
        }

        // The spare hooked masses stand where the working Pulley 1 and its mass will be. Move them as a group
        // to the open side of the table, so nothing overlaps at Play start.
        private static void MoveSpareMasses(Transform props, Bounds table)
        {
            var masses = new System.Collections.Generic.List<Transform>();
            foreach (Transform prop in props)
            {
                if (prop.name.StartsWith(SpareMassPrefix) && prop.gameObject.activeSelf) masses.Add(prop);
            }
            if (masses.Count == 0) return;

            float rightmost = float.MinValue;
            foreach (Transform mass in masses) rightmost = Mathf.Max(rightmost, mass.position.x);

            float shift = (table.min.x - SpareMassGapMetres) - rightmost;   // the group ends up left of the table
            foreach (Transform mass in masses)
            {
                Undo.RecordObject(mass, "Move spare mass");
                mass.position += Vector3.right * shift;
            }
        }

        private static void HideCopies(Transform props, StringBuilder log)
        {
            int hidden = 0;
            foreach (string copyName in CopiesToHide)
            {
                Transform copy = props.Find(copyName);
                if (copy == null) continue;

                Undo.RecordObject(copy.gameObject, "Hide prop copy");
                copy.gameObject.SetActive(false);
                hidden++;
            }
            log.AppendLine($"  {hidden} copies hidden ({string.Join(", ", CopiesToHide)}). Nothing was deleted. The spare masses, the bench and the chair stay.");
        }

        // The box around everything the model draws, in world space. Objects that draw no mesh are skipped.
        private static bool TryMeasureWorld(Transform root, out Bounds bounds)
        {
            bounds = default(Bounds);
            bool started = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!DrawsMesh(renderer)) continue;
                if (!started)
                {
                    bounds = renderer.bounds;
                    started = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return started;
        }

        private static bool DrawsMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh != null;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null;
        }

        private static string Format(Vector3 point) => $"({point.x:0.000}, {point.y:0.000}, {point.z:0.000})";

        private static bool Fail(StringBuilder log, string reason)
        {
            log.AppendLine("  ERROR: " + reason);
            return false;
        }
    }
}
#endif