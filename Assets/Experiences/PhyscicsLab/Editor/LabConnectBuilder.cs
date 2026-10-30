#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using PhysicsLab.Lab.Connect;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 4 Build connect system.
    /// Adds the glowing clamp spots, the connect rules, grab support for pulleys and masses, and a reach limit.
    /// Run Lab > 3 Repair lab physics first. Safe to run twice.
    /// </summary>
    public static class LabConnectBuilder
    {
        private const string MaterialFolderParent = "Assets/Experiences/PhyscicsLab";
        private const string MaterialFolder = MaterialFolderParent + "/Materials";
        private const string GlowMaterialPath = MaterialFolder + "/SlotGlow.mat";
        private const float PulleyReachMetres = 0.6f;   // a held pulley cannot be pulled farther than this from the table centre

        [MenuItem("Lab/4 Build connect system")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabConnectBuilder]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject("LabEquipments");
            if (equipment == null) return Fail(log, "no object named LabEquipments.");

            Transform center = equipment.Find("TableCenter");
            Pulley[] pulleys = { GetPulley(equipment, "Pulley 1"), GetPulley(equipment, "Pulley 2") };
            if (center == null || pulleys[0] == null || pulleys[1] == null)
            {
                return Fail(log, "LabEquipments must hold TableCenter, Pulley 1 and Pulley 2.");
            }

            Material glow = GetOrCreateGlowMaterial(log);
            if (glow == null) return Fail(log, "could not make the glow material.");

            Transform root = GetOrCreateChild(equipment, "Connect");
            TableSlotRing slotRing = GetOrAdd<TableSlotRing>(root.gameObject);
            SetObject(slotRing, "tableCenter", center);
            SetFloat(slotRing, "radiusMetres", ReadFloat(pulleys[0], "rimRadiusMetres"));
            SetObject(slotRing, "glowMaterial", glow);

            var parts = new Object[pulleys.Length];
            for (int i = 0; i < pulleys.Length; i++) parts[i] = PreparePulley(pulleys[i], center, log);

            foreach (Pulley pulley in pulleys)
            {
                LabObject mass = ReadReference<LabObject>(pulley, "massObject");
                if (mass != null) PrepareMass(mass, log);
            }

            ConnectManager manager = GetOrAdd<ConnectManager>(root.gameObject);
            SetObject(manager, "slotRing", slotRing);
            SetObjectArray(manager, "parts", parts);
            SetObject(manager, "stateTracker", Object.FindFirstObjectByType<LabStateTracker>());
            SetFloat(manager, "snapRadiusMetres", 0.10f);

            log.AppendLine("  Done. 24 clamp spots are made when Play starts. Grab a pulley and they glow.");
            return true;
        }

        // ---------- parts ----------

        private static ConnectPart PreparePulley(Pulley pulley, Transform center, StringBuilder log)
        {
            GameObject go = pulley.gameObject;
            ConnectPart part = GetOrAdd<ConnectPart>(go);
            SetObject(part, "pulley", pulley);

            TableBounds reach = GetOrAdd<TableBounds>(go);
            SetObject(reach, "tableCenter", center);
            SetFloat(reach, "maxRadiusMetres", PulleyReachMetres);

            ConfigureGrab(go, log);
            log.AppendLine($"  {pulley.name}: can be grabbed and clamped. Reach limit {PulleyReachMetres} m from the table centre.");
            return part;
        }

        private static void PrepareMass(LabObject mass, StringBuilder log)
        {
            ConfigureGrab(mass.gameObject, log);
            if (!TrySetBool(mass, "freeDrag", true))
            {
                log.AppendLine("  Warning: LabObject has no 'freeDrag' field. Apply the LabObject patch from the guide first.");
            }
            log.AppendLine($"  {mass.name}: can be grabbed and dragged in 3D (mouse wheel pushes and pulls).");
        }

        // Velocity tracking pushes the part with speeds, which works with the string maths.
        // Rotation tracking is off, so a held part stays upright.
        private static void ConfigureGrab(GameObject go, StringBuilder log)
        {
            RemovePlainInteractable(go, log);
            XRGrabInteractable grab = go.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = Undo.AddComponent<XRGrabInteractable>(go);

            var serialized = new SerializedObject(grab);
            bool movement = SetEnum(serialized, "m_MovementType", 2);   // 0 Instantaneous, 1 Kinematic, 2 VelocityTracking
            bool rotation = SetBool(serialized, "m_TrackRotation", false);
            bool throwing = SetBool(serialized, "m_ThrowOnDetach", false);
            serialized.ApplyModifiedProperties();
            if (!movement || !rotation || !throwing)
            {
                log.AppendLine($"  Warning: {go.name}: some XRGrabInteractable settings were not found. Set Movement Type = Velocity Tracking, Track Rotation off, Throw On Detach off by hand.");
            }
        }

        // Two interactables on one collider conflict, and LabObject would listen to the old one.
        // So the old plain interactable goes before the grab interactable is added.
        private static void RemovePlainInteractable(GameObject go, StringBuilder log)
        {
            XRSimpleInteractable plain = go.GetComponent<XRSimpleInteractable>();
            if (plain == null) return;
            Undo.DestroyObjectImmediate(plain);
            log.AppendLine($"  {go.name}: old XRSimpleInteractable removed. The grab interactable replaces it.");
        }

        private static Material GetOrCreateGlowMaterial(StringBuilder log)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(MaterialFolderParent, "Materials");
            var material = new Material(shader) { color = new Color(0.2f, 1f, 0.9f, 1f) };
            AssetDatabase.CreateAsset(material, GlowMaterialPath);
            log.AppendLine($"  Glow material created at {GlowMaterialPath}.");
            return material;
        }

        // ---------- helpers ----------

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

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
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

        private static void SetObject(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[LabConnectBuilder] {owner.GetType().Name} has no serialized field '{field}'.", owner);
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

        private static bool TrySetBool(Object owner, string field, bool value)
        {
            var serialized = new SerializedObject(owner);
            bool found = SetBool(serialized, field, value);
            serialized.ApplyModifiedProperties();
            return found;
        }

        private static bool SetBool(SerializedObject serialized, string field, bool value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return false;
            property.boolValue = value;
            return true;
        }

        private static bool SetEnum(SerializedObject serialized, string field, int index)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return false;
            property.enumValueIndex = index;
            return true;
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
