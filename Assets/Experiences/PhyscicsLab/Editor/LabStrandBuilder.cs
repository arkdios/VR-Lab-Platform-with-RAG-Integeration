#if UNITY_EDITOR
using System.Text;
using PhysicsLab.Lab.Rope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Bake strings. Also the last step of Lab > 0.
    /// Gives every string arm a RopeStrand (the simulated string you see), gives every pulley a round wheel
    /// for the string to run over, then lets each string settle on the table and saves its shape in the scene.
    /// After this the strings are visible in Edit Mode, not only in Play Mode.
    /// Run it again after you move a part by hand in Edit Mode, because the saved shape does not follow.
    /// Safe to run twice.
    /// </summary>
    public static class LabStrandBuilder
    {
        private const string WheelObjectName = "RopeWheel";

        [MenuItem("Lab/Bake strings")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabStrandBuilder]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject("LabEquipments");
            Transform strings = equipment != null ? equipment.Find("Strings") : null;
            if (strings == null)
            {
                log.AppendLine("  ERROR: no Strings object. Run Lab > 2 Build real string first.");
                return false;
            }

            RopeArm[] arms = strings.GetComponentsInChildren<RopeArm>();
            var strands = new RopeStrand[arms.Length];
            for (int i = 0; i < arms.Length; i++) strands[i] = PrepareStrand(arms[i], log);

            Physics.SyncTransforms();   // the new wheels must be known to physics before the strings settle
            for (int i = 0; i < arms.Length; i++) BakeStrand(arms[i], strands[i], log);
            return true;
        }

        // ---------- one arm ----------

        private static RopeStrand PrepareStrand(RopeArm arm, StringBuilder log)
        {
            RopeStrand strand = arm.GetComponent<RopeStrand>();
            if (strand == null) strand = Undo.AddComponent<RopeStrand>(arm.gameObject);

            Pulley pulley = arm.Guide != null ? arm.Guide.GetComponentInParent<Pulley>() : null;
            SphereCollider wheel = pulley != null ? MakeWheel(pulley, log) : null;
            SetWheels(strand, wheel);
            return strand;
        }

        private static void BakeStrand(RopeArm arm, RopeStrand strand, StringBuilder log)
        {
            if (!strand.Bake())
            {
                log.AppendLine($"  Warning: {arm.name} has no anchors, so its string was not baked.");
                return;
            }

            EditorUtility.SetDirty(strand);
            EditorUtility.SetDirty(arm.GetComponent<LineRenderer>());
            log.AppendLine($"  {arm.name}: string baked, {strand.PointCount} points.");
        }

        // ---------- the wheel ----------

        // A hidden ball at the centre of the wheel, as big as the wheel. The string cannot go inside it.
        // It is a trigger, so it never pushes any body. It moves with the pulley, so a pulley clamped at another spot works too.
        private static SphereCollider MakeWheel(Pulley pulley, StringBuilder log)
        {
            if (!LabPulleyAxle.TryMeasureWheel(pulley, out Vector3 center, out float radius))
            {
                log.AppendLine($"  Warning: {pulley.name}: wheel mesh not found, so the string cannot run over a wheel.");
                return null;
            }

            Transform ball = GetOrCreateWheelObject(pulley);
            Undo.RecordObject(ball, "Place rope wheel");
            ball.gameObject.layer = pulley.gameObject.layer;
            ball.position = center;

            log.AppendLine($"  {pulley.name}: rope wheel, radius {radius:0.000} m.");
            return FitBall(ball, radius);
        }

        // The ball is a trigger with its centre on the object. Its radius is the wheel radius in metres.
        private static SphereCollider FitBall(Transform ball, float radiusMetres)
        {
            SphereCollider sphere = ball.GetComponent<SphereCollider>();
            if (sphere == null) sphere = Undo.AddComponent<SphereCollider>(ball.gameObject);
            Undo.RecordObject(sphere, "Fit rope wheel");
            sphere.isTrigger = true;
            sphere.center = Vector3.zero;
            sphere.radius = radiusMetres / Mathf.Max(ball.lossyScale.x, 1e-4f);
            return sphere;
        }

        private static Transform GetOrCreateWheelObject(Pulley pulley)
        {
            Transform existing = pulley.transform.Find(WheelObjectName);
            if (existing != null) return existing;

            var created = new GameObject(WheelObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create rope wheel");
            created.transform.SetParent(pulley.transform, false);
            return created.transform;
        }

        private static void SetWheels(RopeStrand strand, SphereCollider wheel)
        {
            var serialized = new SerializedObject(strand);
            SerializedProperty wheels = serialized.FindProperty("wheels");
            if (wheels == null)
            {
                Debug.LogError("[LabStrandBuilder] RopeStrand has no serialized field 'wheels'.", strand);
                return;
            }

            wheels.arraySize = wheel != null ? 1 : 0;
            if (wheel != null) wheels.GetArrayElementAtIndex(0).objectReferenceValue = wheel;
            serialized.ApplyModifiedProperties();
        }
    }
}
#endif