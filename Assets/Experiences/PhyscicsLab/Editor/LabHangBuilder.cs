#if UNITY_EDITOR
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Set up hand-hung masses. Also two steps of Lab > 0.
    /// The student hangs the masses on the pulley strings by hand, like in the real lab, so the two pulley panels
    /// (angle and mass buttons) are not needed. This tool:
    ///   1. makes a second set of loose hooked masses (50, 100 and 200 g), because Case A needs two 200 g masses,
    ///   2. puts a MassHangPoint (the string loop) on each pulley's Mass object and sets the pulley to 0 kg,
    ///   3. puts a HookedBenchMass on every loose hooked mass,
    ///   4. deletes Pulley1Controls and Pulley2Controls.
    /// Safe to run twice.
    /// </summary>
    public static class LabHangBuilder
    {
        private const string PropsRootName = "LabProps (Temp)";
        private const string TableCentreName = "TableCenter";
        private const string MassPrefix = "HookedMass";
        private const string SparePrefix = "HookedMassB";            // the second set: HookedMassB50g and so on
        private const float SpareOffsetMetres = 0.08f;               // how far the second set stands from the first
        private const float LoopDiameterMetres = 0.012f;
        private const string LoopVisualName = "LoopVisual";
        private const string LoopMaterialPath = "Assets/Experiences/PhyscicsLab/Materials/Lab/StringLoop.mat";
        private static readonly int[] SpareGrams = { 50, 100, 200 };
        private static readonly string[] PanelNames = { "Pulley1Controls", "Pulley2Controls" };

        [MenuItem("Lab/Set up hand-hung masses")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabHangBuilder]\n");
            bool ok = AddSpareMasses(log) && LabPropsBuilder.Run(log) && Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        // ---------- 1. the second set of masses ----------

        // Runs before LabPropsBuilder, which then makes the new masses grabbable like the first set.
        public static bool AddSpareMasses(StringBuilder log)
        {
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (props == null)
            {
                log.AppendLine($"  No object named {PropsRootName}, so no spare masses were made.");
                return true;
            }

            Transform centre = LabSceneFixes.FindSceneObject(TableCentreName);
            foreach (int grams in SpareGrams) AddSpare(props, centre, grams, log);
            return true;
        }

        private static void AddSpare(Transform props, Transform tableCentre, int grams, StringBuilder log)
        {
            Transform original = props.Find($"{MassPrefix}{grams}g");
            if (original == null || !original.gameObject.activeSelf)
            {
                log.AppendLine($"  Warning: no {MassPrefix}{grams}g under {PropsRootName}, so its spare was not made.");
                return;
            }
            if (props.Find($"{SparePrefix}{grams}g") != null) return;   // made on an earlier run

            GameObject copy = Object.Instantiate(original.gameObject, props);
            Undo.RegisterCreatedObjectUndo(copy, "Spare hooked mass");
            copy.name = $"{SparePrefix}{grams}g";
            copy.transform.position = original.position + OutwardFrom(tableCentre, original.position) * SpareOffsetMetres;
            CopyMaterials(original, copy.transform);
            log.AppendLine($"  {copy.name}: made next to {original.name} (move it by hand if it overlaps something).");
        }

        // The flat direction from the table centre to a point. Without a table centre: +X.
        private static Vector3 OutwardFrom(Transform tableCentre, Vector3 point)
        {
            if (tableCentre == null) return Vector3.right;

            Vector3 outward = point - tableCentre.position;
            outward.y = 0f;
            return outward.sqrMagnitude < 1e-6f ? Vector3.right : outward.normalized;
        }

        // A copy keeps the parts in the same order, so the colours are copied one to one.
        private static void CopyMaterials(Transform from, Transform to)
        {
            Renderer[] source = from.GetComponentsInChildren<Renderer>(true);
            Renderer[] target = to.GetComponentsInChildren<Renderer>(true);
            if (source.Length != target.Length) return;

            for (int i = 0; i < source.Length; i++) target[i].sharedMaterials = source[i].sharedMaterials;
        }

        // ---------- 2 to 4. loops, loose masses, panels ----------

        public static bool Run(StringBuilder log)
        {
            DeletePanels(log);

            int loops = 0;
            foreach (Pulley pulley in FindPulleys())
            {
                if (SetUpLoop(pulley, log)) loops++;
            }

            int masses = SetUpLooseMasses(log);
            log.AppendLine($"  Hand-hung masses done: {loops} loop(s), {masses} loose mass(es).");
            return true;
        }

        private static void DeletePanels(StringBuilder log)
        {
            foreach (string panelName in PanelNames)
            {
                Transform panel = LabSceneFixes.FindSceneObject(panelName);
                if (panel == null) continue;

                Undo.DestroyObjectImmediate(panel.gameObject);
                log.AppendLine($"  UI: {panelName} deleted (the student hangs the masses by hand).");
            }
        }

        private static Pulley[] FindPulleys()
        {
            var found = new System.Collections.Generic.List<Pulley>();
            foreach (Pulley pulley in Resources.FindObjectsOfTypeAll<Pulley>())
            {
                if (pulley.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(pulley)) found.Add(pulley);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return found.ToArray();
        }

        private static bool SetUpLoop(Pulley pulley, StringBuilder log)
        {
            LabObject massObject = pulley.MassObject;
            if (massObject == null)
            {
                log.AppendLine($"  Warning: {pulley.name} has no Mass object, so it got no loop.");
                return false;
            }

            GameObject mass = massObject.gameObject;
            MassHangPoint loop = mass.GetComponent<MassHangPoint>();
            if (loop == null) loop = Undo.AddComponent<MassHangPoint>(mass);

            SetReference(loop, "pulley", pulley);
            SetFloat(pulley, "massKg", 0f);   // nothing hangs on the loop at the start
            HideOldMassLook(mass, log);
            AddLoopVisual(mass);
            log.AppendLine($"  {pulley.name}: {mass.name} is a loop now, the pulley starts at 0 kg.");
            return true;
        }

        // The old fixed mass model and the weight stack are replaced by the masses the student hangs.
        private static void HideOldMassLook(GameObject mass, StringBuilder log)
        {
            DisableBehaviour(mass.GetComponent<HookedMassVisual>());
            DisableBehaviour(mass.GetComponent<HangingMassVisual>());

            var ownLook = mass.GetComponent<MeshRenderer>();
            if (ownLook != null && ownLook.enabled)
            {
                Undo.RecordObject(ownLook, "Hide old mass look");
                ownLook.enabled = false;
            }

            foreach (Transform child in mass.transform)
            {
                if (!child.name.StartsWith("RealModel") || !child.gameObject.activeSelf) continue;

                Undo.RecordObject(child.gameObject, "Hide old mass model");
                child.gameObject.SetActive(false);
            }
        }

        private static void DisableBehaviour(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled) return;

            Undo.RecordObject(behaviour, "Switch off old mass look");
            behaviour.enabled = false;
            EditorUtility.SetDirty(behaviour);
        }

        // A small dark ball at the string knot, so the loop can be seen.
        private static void AddLoopVisual(GameObject mass)
        {
            if (mass.transform.Find(LoopVisualName) != null) return;

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(ball, "Loop visual");
            ball.name = LoopVisualName;
            Object.DestroyImmediate(ball.GetComponent<Collider>());   // only to look at, never to bump into

            ball.layer = mass.layer;
            ball.transform.SetParent(mass.transform, false);
            ball.transform.localPosition = Vector3.zero;
            ball.transform.localScale = Vector3.one * (LoopDiameterMetres / Mathf.Max(mass.transform.lossyScale.x, 1e-4f));

            var look = ball.GetComponent<MeshRenderer>();
            look.sharedMaterial = GetLoopMaterial();
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material GetLoopMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LoopMaterialPath);
            if (material != null) return material;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null || !AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(LoopMaterialPath).Replace('\\', '/'))) return null;

            material = new Material(lit) { name = "StringLoop" };
            material.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.055f));
            AssetDatabase.CreateAsset(material, LoopMaterialPath);
            return material;
        }

        // Every loose hooked mass gets the component that lets it hang on a loop.
        private static int SetUpLooseMasses(StringBuilder log)
        {
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (props == null) return 0;

            int done = 0;
            foreach (Transform prop in props)
            {
                if (!prop.name.StartsWith(MassPrefix) || !prop.gameObject.activeSelf) continue;

                if (prop.GetComponent<LabObject>() == null || prop.GetComponent<Rigidbody>() == null)
                {
                    log.AppendLine($"  Warning: {prop.name} is not grabbable yet. Run Lab > 7 Make props grabbable first.");
                    continue;
                }

                HookedBenchMass bench = prop.GetComponent<HookedBenchMass>();
                if (bench == null) bench = Undo.AddComponent<HookedBenchMass>(prop.gameObject);
                SetFloat(bench, "massKg", GramsFromName(prop.name) / 1000f);
                done++;
            }
            return done;
        }

        // "HookedMassB100g" is 100 g. Zero when the name has no number, so the mass of the Rigidbody is used.
        private static float GramsFromName(string propName)
        {
            Match grams = Regex.Match(propName, @"(\d+)g$");
            return grams.Success ? int.Parse(grams.Groups[1].Value) : 0f;
        }

        // ---------- saved fields ----------

        private static void SetReference(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetFloat(Object owner, string field, float value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;

            property.floatValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
#endif