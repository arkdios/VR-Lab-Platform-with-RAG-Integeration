#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhysicsLab.Lab.Rope;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 2 Build real string.
    /// Builds the real string from the force table slides: four arms, all tied to the ring.
    /// Arm 1 and 2 run over Pulley 1 and 2 to the hanging masses. Arm 3 ends at the spring scale.
    /// Arm 4 is a free end that lies on the table. Safe to run twice: it reuses what it made before.
    /// Nothing is deleted. The old String_* line objects are only switched off.
    /// </summary>
    public static class LabStringBuilder
    {
        private const float MassHangDropMetres = 0.45f;   // how far a mass hangs below the pulley
        private const float ScaleArmMetres = 0.45f;       // string length from the ring to the scale hook
        private const float FreeArmMetres = 0.5f;
        private const float PostClearanceMetres = 0.01f;  // how far the ring can move before it touches the post
        private const float ScaleReachMetres = 0.75f;     // the scale may be held beyond the table edge
        private const float FreeEndAngleDegrees = 270f;
        private const float FreeEndDistanceMetres = 0.3f;

        [MenuItem("Lab/2 Build real string")]
        private static void Build()
        {
            var log = new StringBuilder("[LabStringBuilder]\n");
            bool ok = Run(log);
            if (!ok)
            {
                Debug.LogError(log.ToString());
                return;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        /// <summary>Builds the four arms. Returns false (and logs why) when the scene is missing something.</summary>
        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject("LabEquipments");
            if (equipment == null) return Fail(log, "no object named LabEquipments.");

            Transform ring = equipment.Find("Ring");
            Transform table = equipment.Find("Table");
            Transform pin = equipment.Find("CenterPin");
            Transform center = equipment.Find("TableCenter");
            Pulley pulley1 = GetPulley(equipment, "Pulley 1");
            Pulley pulley2 = GetPulley(equipment, "Pulley 2");
            if (ring == null || table == null || pin == null || center == null || pulley1 == null || pulley2 == null)
            {
                return Fail(log, "LabEquipments must hold Ring, Table, CenterPin, TableCenter, Pulley 1 and Pulley 2.");
            }

            SpringScale scale = LabSceneFixes.PrepareSpringScale(log);
            if (scale == null) return Fail(log, "no SpringScale in the scene.");

            Rigidbody ringBody = ring.GetComponent<Rigidbody>();
            Transform hookEye = ReadReference<Transform>(scale.GetComponent<AimModelAtTarget>(), "hookEye");
            if (ringBody == null || hookEye == null)
            {
                return Fail(log, "the ring needs a Rigidbody, and the scale needs AimModelAtTarget with a hook eye.");
            }

            Material stringMaterial = FindStringMaterial(equipment);
            if (stringMaterial == null) log.AppendLine("  Warning: no material found on String_Pulley1. Assign one to Arm_* by hand.");

            // The ring stays near the post, like the real ring around the post.
            ringBody.interpolation = RigidbodyInterpolation.None;
            SetRing(ring, pin, log);

            // Scale: it can be held a bit past the table edge. Its string has a limited length.
            TableBounds scaleBounds = scale.GetComponent<TableBounds>();
            if (scaleBounds != null) SetFloat(scaleBounds, "maxRadiusMetres", ScaleReachMetres);

            Transform strings = GetOrCreateChild(equipment, "Strings");
            float ringRadius = RingRadius(ring);
            Transform freeEnd = BuildFreeEnd(equipment, ring, stringMaterial, log);

            var ringColliders = new List<Collider>(ring.GetComponentsInChildren<Collider>());
            Collider tableCollider = table.GetComponent<Collider>();

            float tableRadius = table.GetComponent<Renderer>() != null ? table.GetComponent<Renderer>().bounds.extents.x : 0.45f;
            RopeArm arm1 = BuildMassArm(strings, "Arm_Pulley1", pulley1, ring, ringBody, center, ringRadius, tableRadius,
                                        ringColliders, tableCollider, stringMaterial, +1, log);
            RopeArm arm2 = BuildMassArm(strings, "Arm_Pulley2", pulley2, ring, ringBody, center, ringRadius, tableRadius,
                                        ringColliders, tableCollider, stringMaterial, -1, log);

            RopeArm arm3 = MakeArm(strings, "Arm_Scale", stringMaterial);
            Transform hook3 = MakeRingHook(ring, "RingHook_Scale", hookEye.position, ringRadius);
            var scaleIgnore = new List<Collider>(ringColliders);
            if (tableCollider != null) scaleIgnore.Add(tableCollider);
            ConfigureArm(arm3, hook3, null, hookEye, ScaleArmMetres, +1, scaleIgnore);

            RopeArm arm4 = MakeArm(strings, "Arm_Free", stringMaterial);
            Transform hook4 = MakeRingHook(ring, "RingHook_Free", freeEnd.position, ringRadius);
            ConfigureArm(arm4, hook4, null, freeEnd, FreeArmMetres, -1, new List<Collider>());

            SetObject(scale, "tensionArm", arm3);

            RopeNetwork network = strings.GetComponent<RopeNetwork>();
            if (network == null) network = Undo.AddComponent<RopeNetwork>(strings.gameObject);
            SetObjectArray(network, "arms", new Object[] { arm1, arm2, arm3, arm4 });

            SwitchOffOldStrings(equipment, log);
            log.AppendLine("  Done. Four arms tied to the ring. Press Play and run Lab > Run rope tests once.");
            return true;
        }

        // ---------- ring and free end ----------

        private static void SetRing(Transform ring, Transform pin, StringBuilder log)
        {
            TableBounds bounds = ring.GetComponent<TableBounds>();
            if (bounds == null)
            {
                log.AppendLine("  Warning: Ring has no TableBounds, so nothing stops it at the post.");
                return;
            }
            SetObject(bounds, "tableCenter", pin);
            SetFloat(bounds, "maxRadiusMetres", PostClearanceMetres);
            log.AppendLine($"  Ring: held within {PostClearanceMetres * 100f:F1} cm of the post (post clearance).");
        }

        private static float RingRadius(Transform ring)
        {
            Renderer renderer = ring.GetComponentInChildren<Renderer>();
            if (renderer == null) return 0.03f;
            return Mathf.Min(renderer.bounds.extents.x, renderer.bounds.extents.z);
        }

        // The loose loop at the end of the fourth arm. It lies on the table.
        private static Transform BuildFreeEnd(Transform equipment, Transform ring, Material material, StringBuilder log)
        {
            Transform end = GetOrCreateChild(equipment, "FreeEnd");
            float radians = FreeEndAngleDegrees * Mathf.Deg2Rad;
            Vector3 flat = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            end.position = ring.position + flat * FreeEndDistanceMetres;

            Rigidbody body = end.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(end.gameObject);
            body.mass = 0.001f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 20f;   // a loose end on a table does not slide far
            body.interpolation = RigidbodyInterpolation.None;

            if (end.Find("EndLoopVisual") == null)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "EndLoopVisual";
                Object.DestroyImmediate(visual.GetComponent<Collider>()); // a visual only, no collision
                visual.transform.SetParent(end, false);
                visual.transform.localScale = Vector3.one * 0.02f;
                if (material != null) visual.GetComponent<Renderer>().sharedMaterial = material;
                Undo.RegisterCreatedObjectUndo(visual, "End loop visual");
            }
            AddIfMissing<ReturnIfFallen>(end.gameObject);
            log.AppendLine("  Free end: a 1 g body on the table at 270 degrees.");
            return end;
        }

        // ---------- arms ----------

        private static RopeArm BuildMassArm(Transform strings, string armName, Pulley pulley, Transform ring,
            Rigidbody ringBody, Transform center, float ringRadius, float tableRadius, List<Collider> ringColliders,
            Collider tableCollider, Material material, int side, StringBuilder log)
        {
            LabObject massObject = ReadReference<LabObject>(pulley, "massObject");

            // The mass must hang outside the table edge: table radius + half the mass width + 1 cm.
            Renderer massRenderer = massObject.GetComponent<Renderer>();
            float massHalfWidth = massRenderer != null
                ? Mathf.Max(massRenderer.bounds.extents.x, massRenderer.bounds.extents.z)
                : 0.02f;
            Transform groove = MakeGroovePoint(pulley, center, tableRadius + massHalfWidth + 0.01f);
            Rigidbody massBody = PrepareMass(massObject, pulley);
            SetObject(pulley, "massBody", massBody);

            Transform hook = MakeRingHook(ring, "RingHook_" + pulley.name.Replace(" ", ""), groove.position, ringRadius);

            // Hang the mass straight below the groove, so the string starts exactly taut.
            Undo.RecordObject(massObject.transform, "Hang mass");
            massObject.transform.position = groove.position + Vector3.down * MassHangDropMetres;
            float length = Vector3.Distance(hook.position, groove.position) + MassHangDropMetres;

            var ignore = new List<Collider>(ringColliders);
            if (tableCollider != null) ignore.Add(tableCollider);
            Collider pulleyCollider = pulley.GetComponent<Collider>();
            if (pulleyCollider != null) ignore.Add(pulleyCollider);

            RopeArm arm = MakeArm(strings, armName, material);
            ConfigureArm(arm, hook, groove, massObject.transform, length, side, ignore);
            log.AppendLine($"  {armName}: ring to {pulley.name} to its mass, {length:F2} m long.");
            return arm;
        }

        // The top of the pulley wheel, moved outward so the hanging string is at hangRadius from the table centre.
        private static Transform MakeGroovePoint(Pulley pulley, Transform center, float hangRadius)
        {
            Transform groove = GetOrCreateChild(pulley.transform, "GroovePoint");
            Renderer renderer = pulley.GetComponent<Renderer>();
            Vector3 top = renderer != null
                ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z)
                : pulley.transform.position + Vector3.up * 0.02f;

            Vector3 outward = pulley.transform.position - center.position;
            outward.y = 0f;
            Vector3 flatTop = top - center.position;
            flatTop.y = 0f;
            float reach = Mathf.Max(0.005f, hangRadius - flatTop.magnitude); // how far past the wheel top
            groove.position = top + outward.normalized * reach;
            return groove;
        }

        private static Rigidbody PrepareMass(LabObject massObject, Pulley pulley)
        {
            Rigidbody body = massObject.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(massObject.gameObject);
            Undo.RecordObject(body, "Prepare mass body");
            body.mass = Mathf.Max(0.001f, ReadFloat(pulley, "massKg"));
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 0.3f;
            body.interpolation = RigidbodyInterpolation.None;
            AddIfMissing<ReturnIfFallen>(massObject.gameObject);
            return body;
        }

        private static Transform MakeRingHook(Transform ring, string hookName, Vector3 towardWorld, float radius)
        {
            Transform hook = GetOrCreateChild(ring, hookName);
            Vector3 flat = towardWorld - ring.position;
            flat.y = 0f;
            Vector3 direction = flat.sqrMagnitude < 1e-8f ? Vector3.right : flat.normalized;
            hook.position = ring.position + direction * radius; // on the ring edge, facing where the arm goes
            return hook;
        }

        private static RopeArm MakeArm(Transform strings, string armName, Material material)
        {
            Transform arm = GetOrCreateChild(strings, armName);
            LineRenderer line = arm.GetComponent<LineRenderer>();
            if (line == null) line = Undo.AddComponent<LineRenderer>(arm.gameObject);
            if (material != null) line.sharedMaterial = material;
            RopeArm rope = arm.GetComponent<RopeArm>();
            return rope != null ? rope : Undo.AddComponent<RopeArm>(arm.gameObject);
        }

        private static void ConfigureArm(RopeArm arm, Transform ringAnchor, Transform guide, Transform endAnchor,
                                         float length, int side, List<Collider> ignore)
        {
            SetObject(arm, "ringAnchor", ringAnchor);
            SetObject(arm, "guide", guide);
            SetObject(arm, "endAnchor", endAnchor);
            SetFloat(arm, "lengthMetres", length);
            SetInt(arm, "slackSide", side);
            SetObjectArray(arm, "ignoreCollisionWith", ignore.ToArray());
        }

        private static void SwitchOffOldStrings(Transform equipment, StringBuilder log)
        {
            foreach (string oldName in new[] { "String_Pulley1", "String_Pulley2", "String_Scale" })
            {
                Transform old = equipment.Find(oldName);
                if (old == null) continue;
                Undo.RecordObject(old.gameObject, "Switch off old string");
                old.gameObject.SetActive(false);
            }
            log.AppendLine("  Old String_Pulley1, String_Pulley2 and String_Scale lines switched off.");
        }

        // ---------- small helpers ----------

        private static Material FindStringMaterial(Transform equipment)
        {
            Transform old = equipment.Find("String_Pulley1");
            LineRenderer line = old != null ? old.GetComponent<LineRenderer>() : null;
            return line != null ? line.sharedMaterial : null;
        }

        private static Pulley GetPulley(Transform equipment, string pulleyName)
        {
            Transform t = equipment.Find(pulleyName);
            return t != null ? t.GetComponent<Pulley>() : null;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing;

            var created = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(created, "Create " + childName);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static void AddIfMissing<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null) Undo.AddComponent<T>(target);
        }

        private static T ReadReference<T>(Object owner, string field) where T : Object
        {
            if (owner == null) return null;
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static float ReadFloat(Object owner, string field)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            return property != null ? property.floatValue : 0f;
        }

        private static void SetObject(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[LabStringBuilder] {owner.GetType().Name} has no serialized field '{field}'.", owner);
                return;
            }
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

        private static void SetInt(Object owner, string field, int value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;
            property.intValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetObjectArray(Object owner, string field, Object[] values)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedProperties();
        }

        private static bool Fail(StringBuilder log, string reason)
        {
            log.AppendLine("  ERROR: " + reason);
            return false;
        }
    }
}
#endif
