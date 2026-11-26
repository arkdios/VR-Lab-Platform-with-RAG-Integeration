#if UNITY_EDITOR
using System.Text;
using PhysicsLab.Lab.Connect;
using PhysicsLab.Lab.Rope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Build practice bench. Also a step of Lab > 0 (removed at the start, made again near the end).
    /// Makes the practice bench: a full copy of the lab bench, with its table, ring, pulleys, strings, masses,
    /// spring scale and chair, standing beside the real one. The student learns the controls on it first.
    /// Nothing on the practice bench is judged, recorded or sent to the assistant:
    ///  - the root has a PracticeZone, which the step rules and the status reader use to skip practice parts,
    ///  - the copy is cut off from the lab's LabStateTracker (no events), and its ids start with "practice-".
    /// The copy is made from the finished lab, so run it after the lab is built. Lab > 0 does that for you.
    /// Move the bench by changing OffsetMetres and running Lab > 0 again.
    /// </summary>
    public static class LabPracticeBenchBuilder
    {
        public const string RootName = "PracticeBench";

        /// <summary>How far the practice bench stands from the lab bench: 4.5 m in -X, which is to the right of the lab when you face it from the start.</summary>
        public static readonly Vector3 OffsetMetres = new Vector3(-4.5f, 0f, 0f);

        private const string EquipmentName = "LabEquipments";
        private const string PropsName = "LabProps (Temp)";
        private const string BenchName = "LabBench";
        private const string ChairName = "LabChair";
        private const string LooseMassPrefix = "HookedMass";   // the spare masses that stand on the bench
        private const string TableCentreName = "TableCenter";
        private const string CopyPrefix = "Practice ";
        private const string IdPrefix = "practice-";

        [MenuItem("Lab/Build practice bench")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabPracticeBenchBuilder]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        /// <summary>Deletes the practice bench if there is one. Always returns true, so it can sit in a && chain.</summary>
        public static bool RemoveOld(StringBuilder log)
        {
            foreach (PracticeZone zone in Object.FindObjectsByType<PracticeZone>(FindObjectsInactive.Include))
            {
                log.AppendLine("  Old practice bench removed.");
                Undo.DestroyObjectImmediate(zone.gameObject);
            }
            return true;
        }

        public static bool Run(StringBuilder log)
        {
            RemoveOld(log);

            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentName);
            Transform bench = LabSceneFixes.FindSceneObject(BenchName);
            if (equipment == null || bench == null)
            {
                log.AppendLine("  ERROR: no LabEquipments or LabBench in the scene. Run Lab > 0 first.");
                return false;
            }

            Transform root = MakeRoot();
            Transform copiedEquipment = CopyPart(equipment, root, CopyPrefix + EquipmentName);
            CopyPart(bench, root, CopyPrefix + BenchName);
            CopyPart(LabSceneFixes.FindSceneObject(ChairName), root, CopyPrefix + ChairName);
            int masses = CopyLooseMasses(LabSceneFixes.FindSceneObject(PropsName), root);

            CutLinksToTheLab(root, log);
            AddStrandGate(root, copiedEquipment);
            BakeStrings(root, log);

            log.AppendLine($"  Practice bench made at {OffsetMetres.x:0.0} m from the lab bench: table, ring, pulleys, strings, scale, chair and {masses} loose mass(es).");
            return true;
        }

        // ---------- copying ----------

        private static Transform MakeRoot()
        {
            var created = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(created, "Create practice bench");
            Undo.AddComponent<PracticeZone>(created);
            return created.transform;
        }

        // Unity changes every link that points inside the copied object to point at the copy.
        // So the copy of LabEquipments is a lab of its own, apart from the links that leave it (see CutLinksToTheLab).
        private static Transform CopyPart(Transform source, Transform root, string copyName)
        {
            if (source == null) return null;

            GameObject copy = Object.Instantiate(source.gameObject, source.position + OffsetMetres, source.rotation, root);
            copy.name = copyName;
            Undo.RegisterCreatedObjectUndo(copy, "Copy to practice bench");
            return copy.transform;
        }

        // The loose masses stand on the bench as children of the props object. Only the ones that are switched on.
        private static int CopyLooseMasses(Transform props, Transform root)
        {
            if (props == null) return 0;

            int copied = 0;
            foreach (Transform child in props)
            {
                if (!child.gameObject.activeSelf || !child.name.StartsWith(LooseMassPrefix)) continue;

                CopyPart(child, root, CopyPrefix + child.name);
                copied++;
            }
            return copied;
        }

        // ---------- cutting the copy off from the lab ----------

        private static void CutLinksToTheLab(Transform root, StringBuilder log)
        {
            // 1. A practice part must not share an id with a lab part. The ids go to the backend for the real lab.
            foreach (LabObject part in root.GetComponentsInChildren<LabObject>(true))
            {
                string id = LabSerialized.ReadString(part, "objectId");
                if (!string.IsNullOrEmpty(id) && !id.StartsWith(IdPrefix)) LabSerialized.SetString(part, "objectId", IdPrefix + id);
            }

            // 2. The copy of the connect manager must not report events to the lab's tracker.
            foreach (ConnectManager manager in root.GetComponentsInChildren<ConnectManager>(true))
            {
                LabSerialized.SetObject(manager, "stateTracker", null);
            }

            // 3. One tension box on the screen is enough (Editor and development builds only).
            foreach (RopeNetwork network in root.GetComponentsInChildren<RopeNetwork>(true))
            {
                LabSerialized.SetBool(network, "showTensionsOnScreen", false);
            }
            log.AppendLine("  Practice parts: ids start with 'practice-', no events to the lab tracker.");
        }

        // The strings of the practice bench run only while the player is near it.
        private static void AddStrandGate(Transform root, Transform copiedEquipment)
        {
            Transform centre = copiedEquipment != null ? copiedEquipment.Find(TableCentreName) : null;
            PracticeStrandGate gate = Undo.AddComponent<PracticeStrandGate>(root.gameObject);
            LabSerialized.SetObject(gate, "tableCentre", centre);
        }

        // The drawn strings were baked at the lab. A baked string keeps world positions, so each copy is baked again here.
        private static void BakeStrings(Transform root, StringBuilder log)
        {
            Physics.SyncTransforms();   // the copied table and wheels must be known to physics before the strings settle
            foreach (RopeStrand strand in root.GetComponentsInChildren<RopeStrand>(false))
            {
                if (!strand.Bake())
                {
                    log.AppendLine($"  Warning: {strand.name} on the practice bench has no anchors, so its string was not baked.");
                    continue;
                }

                EditorUtility.SetDirty(strand);
                EditorUtility.SetDirty(strand.GetComponent<LineRenderer>());
            }
        }
    }
}
#endif
