#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhysicsLab.Lab.Rope;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 3 Repair lab physics.
    /// Puts every lab part into a known good physics state, whatever state the scene is in now.
    /// Static parts (table, centre pin) lose their Rigidbody. Moving parts get the right locks.
    /// Colliders are rebuilt to fit each mesh. Parts are lowered onto the table.
    /// Every change is written to the Console. Ctrl+Z undoes it.
    /// </summary>
    public static class LabPhysicsRepair
    {
        private const float RestGapMetres = 0.002f;   // parts hover this far above the table top, so nothing rubs
        private const float RingMassKg = 0.2f;
        private const float RingDamping = 6f;
        private const float MassDamping = 0.3f;

        [MenuItem("Lab/3 Repair lab physics")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabPhysicsRepair]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject("LabEquipments");
            if (equipment == null) return Fail(log, "no object named LabEquipments.");

            Transform table = equipment.Find("Table");
            Transform pin = equipment.Find("CenterPin");
            Transform ring = equipment.Find("Ring");
            Transform center = equipment.Find("TableCenter");
            Pulley[] pulleys = { GetPulley(equipment, "Pulley 1"), GetPulley(equipment, "Pulley 2") };
            if (table == null || pin == null || ring == null || center == null || pulleys[0] == null || pulleys[1] == null)
            {
                return Fail(log, "LabEquipments must hold Table, CenterPin, Ring, TableCenter, Pulley 1 and Pulley 2.");
            }

            // 1. The table and the centre pin never move, so they must not have a Rigidbody.
            MakeStatic(table.gameObject, log);
            FitBox(table.gameObject, log);
            float tableTop = table.GetComponent<Renderer>().bounds.max.y;
            Undo.RecordObject(center, "Table centre height");
            center.position = new Vector3(center.position.x, tableTop, center.position.z);
            log.AppendLine($"  Table top is {tableTop:F3} m. TableCenter moved to that height.");

            MakeStatic(pin.gameObject, log);
            RemoveColliders(pin.gameObject, log, "the ring is held near the pin by TableBounds, so a pin collider would only make the ring jitter");
            PlaceOnTable(pin, tableTop, 0f, center.position);

            // 2. The ring slides on the table: Y and rotation frozen.
            RepairRing(ring, tableTop, center.position, log);

            // 3. Pulleys sit on the table edge and stay frozen until a hand takes them. Their mass hangs from the string.
            //    The rim radius is set to the table radius, or the mass would hang inside the table.
            float tableRadius = table.GetComponent<Renderer>().bounds.extents.x;
            foreach (Pulley pulley in pulleys)
            {
                SetFloat(pulley, "rimRadiusMetres", tableRadius);
                RepairPulleyAndMass(pulley, equipment, center, log);
            }
            log.AppendLine($"  Pulley rim radius set to the table radius, {tableRadius:F3} m.");

            // 4. The spring scale: collider, locks, lowered to the table.
            SpringScale scale = LabSceneFixes.PrepareSpringScale(log);
            if (scale != null) LowerScale(scale, tableTop, log);

            log.AppendLine("  Done. Next: Lab > 2 Build real string, then Lab > 4 Build connect system.");
            return true;
        }

        // ---------- parts ----------

        private static void RepairRing(Transform ring, float tableTop, Vector3 center, StringBuilder log)
        {
            RemoveIfPresent<HoldInPlaceOnRelease>(ring.gameObject, log);
            Rigidbody body = ring.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(ring.gameObject);
            Undo.RecordObject(body, "Repair ring body");
            body.mass = RingMassKg;
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.linearDamping = RingDamping;
            body.interpolation = RigidbodyInterpolation.None;
            FitBox(ring.gameObject, log);
            PlaceOnTable(ring, tableTop, RestGapMetres, center);
            log.AppendLine("  Ring: Y and rotation frozen, damping 6, centred on the pin, lowered onto the table.");
        }

        private static void RepairPulleyAndMass(Pulley pulley, Transform equipment, Transform center, StringBuilder log)
        {
            GameObject go = pulley.gameObject;
            RemoveIfPresent<HoldInPlaceOnRelease>(go, log);
            RemoveIfPresent<ReturnIfFallen>(go, log);

            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(go);
            Undo.RecordObject(body, "Repair pulley body");
            body.mass = 1f;
            body.useGravity = false;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeAll; // clamped: it moves only when a hand takes it
            body.interpolation = RigidbodyInterpolation.None;
            FitBox(go, log);
            PlaceLikePulleyScript(pulley, center);

            LabObject massObject = ReadReference<LabObject>(pulley, "massObject");
            if (massObject == null)
            {
                log.AppendLine($"  Warning: {pulley.name} has no mass object assigned.");
                return;
            }
            RepairMass(massObject, pulley, equipment, log);
            log.AppendLine($"  {pulley.name}: clamped (frozen) on the rim, collider fitted.");
        }

        // The mass hangs from the string, so it must not be a child of the pulley (a child with its own Rigidbody
        // would be dragged by the parent's movement and fight the physics).
        private static void RepairMass(LabObject massObject, Pulley pulley, Transform equipment, StringBuilder log)
        {
            GameObject go = massObject.gameObject;
            if (massObject.transform.parent == pulley.transform)
            {
                Undo.SetTransformParent(massObject.transform, equipment, "Unparent mass");
                log.AppendLine($"  {go.name}: moved out of {pulley.name} (keeps its world position and size).");
            }

            RemoveIfPresent<HoldInPlaceOnRelease>(go, log);
            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(go);
            Undo.RecordObject(body, "Repair mass body");
            body.mass = Mathf.Max(0.001f, ReadFloat(pulley, "massKg"));
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.linearDamping = MassDamping;
            body.interpolation = RigidbodyInterpolation.None;
            FitBox(go, log);
            if (go.GetComponent<ReturnIfFallen>() == null) Undo.AddComponent<ReturnIfFallen>(go);
        }

        private static void LowerScale(SpringScale scale, float tableTop, StringBuilder log)
        {
            Collider box = scale.GetComponentInChildren<Collider>();
            if (box == null) return;
            Undo.RecordObject(scale.transform, "Lower scale");
            float lift = tableTop + RestGapMetres - box.bounds.min.y;
            scale.transform.position += Vector3.up * lift;
            log.AppendLine($"  Spring scale lowered by {-lift:F3} m onto the table.");
        }

        // ---------- helpers ----------

        // Same placement as Pulley.PlaceOnRim, so the string builder reads the real run-time position.
        private static void PlaceLikePulleyScript(Pulley pulley, Transform center)
        {
            float degrees = ReadFloat(pulley, "angleDegrees");
            float rim = ReadFloat(pulley, "rimRadiusMetres");
            float radians = degrees * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            Undo.RecordObject(pulley.transform, "Place pulley");
            pulley.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            pulley.transform.position = center.position + outward * rim;
        }

        private static void PlaceOnTable(Transform part, float tableTop, float gap, Vector3 center)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            float half = renderer != null ? renderer.bounds.extents.y : 0f;
            Undo.RecordObject(part, "Lower onto table");
            part.position = new Vector3(center.x, tableTop + gap + half, center.z);
        }

        private static void MakeStatic(GameObject go, StringBuilder log)
        {
            RemoveIfPresent<ReturnIfFallen>(go, log);       // these two need a Rigidbody, so they go first
            RemoveIfPresent<HoldInPlaceOnRelease>(go, log);
            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null) return;
            Undo.DestroyObjectImmediate(body);
            log.AppendLine($"  {go.name}: Rigidbody removed (it never moves, and a free body made it fall and shove other parts).");
        }

        // One Box Collider that matches the mesh, in the object's own space. Replaces whatever collider was there.
        private static void FitBox(GameObject go, StringBuilder log)
        {
            foreach (Collider old in go.GetComponents<Collider>()) Undo.DestroyObjectImmediate(old);
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);

            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }
            else
            {
                log.AppendLine($"  Warning: {go.name} has no mesh. Its Box Collider keeps the default size.");
            }
        }

        private static void RemoveColliders(GameObject go, StringBuilder log, string reason)
        {
            Collider[] colliders = go.GetComponents<Collider>();
            if (colliders.Length == 0) return;
            foreach (Collider c in colliders) Undo.DestroyObjectImmediate(c);
            log.AppendLine($"  {go.name}: collider removed ({reason}).");
        }

        private static void RemoveIfPresent<T>(GameObject go, StringBuilder log) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null) return;
            Undo.DestroyObjectImmediate(component);
            log.AppendLine($"  {go.name}: {typeof(T).Name} removed.");
        }

        private static Pulley GetPulley(Transform equipment, string pulleyName)
        {
            Transform t = equipment.Find(pulleyName);
            return t != null ? t.GetComponent<Pulley>() : null;
        }

        private static T ReadReference<T>(Object owner, string field) where T : Object
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static float ReadFloat(Object owner, string field)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            return property != null ? property.floatValue : 0f;
        }

        private static void SetFloat(Object owner, string field, float value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;
            property.floatValue = value;
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
