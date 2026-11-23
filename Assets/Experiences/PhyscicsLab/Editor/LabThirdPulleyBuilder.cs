#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Add third pulley (Case D).
    /// Case D of Table 1 needs three pulleys and three hanging masses. This makes "Pulley 3" and "Mass 3" as copies
    /// of Pulley 2 and Mass 2, so they have the same model, size, collider and grab setup. Then it gives the copies
    /// their own ids, points every saved link of the copies at the copies (not at Pulley 2 or Mass 2), and adds the
    /// new pulley to the state tracker and the progress store.
    /// Safe to run twice: when Pulley 3 exists, it only checks the lists again.
    /// The string, the clamp spots and the loop are made by the next steps of Lab > 0.
    /// </summary>
    public static class LabThirdPulleyBuilder
    {
        private const string EquipmentName = "LabEquipments";
        private const int CopyFromNumber = 2;
        private const int NewNumber = 3;
        private const float StartAngleDegrees = 270f;   // the far side of the table: the spare masses stand at 180 degrees

        [MenuItem("Lab/Add third pulley (Case D)")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabThirdPulleyBuilder]\n");
            bool ok = Run(log);
            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                log.AppendLine("  Done. Now run Lab > 0 Build working lab, then Ctrl+S.");
                Debug.Log(log.ToString());
            }
            else
            {
                Debug.LogError(log.ToString());
            }
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentName);
            if (equipment == null) return Fail(log, $"no object named {EquipmentName}.");

            Pulley pulley = FindOrCopyPulley(equipment, log);
            if (pulley == null) return false;

            AddToTrackers(pulley, log);
            return true;
        }

        // ---------- the copy ----------

        private static Pulley FindOrCopyPulley(Transform equipment, StringBuilder log)
        {
            Transform existing = equipment.Find(LabPulleyRig.PulleyName(NewNumber));
            if (existing != null)
            {
                log.AppendLine("  Pulley 3 is already there.");
                return existing.GetComponent<Pulley>();
            }

            Transform pulleyFrom = equipment.Find(LabPulleyRig.PulleyName(CopyFromNumber));
            Transform massFrom = equipment.Find(LabPulleyRig.MassName(CopyFromNumber));
            if (pulleyFrom == null || massFrom == null)
            {
                Fail(log, "LabEquipments must hold Pulley 2 and Mass 2, so Pulley 3 can be copied from them.");
                return null;
            }

            GameObject pulleyCopy = Copy(pulleyFrom.gameObject, LabPulleyRig.PulleyName(NewNumber), equipment);
            GameObject massCopy = Copy(massFrom.gameObject, LabPulleyRig.MassName(NewNumber), equipment);
            PointLinksAtCopies(new[] { pulleyFrom.gameObject, massFrom.gameObject }, new[] { pulleyCopy, massCopy }, log);

            SetString(pulleyCopy.GetComponent<LabObject>(), "objectId", "pulley-" + NewNumber);
            SetString(massCopy.GetComponent<LabObject>(), "objectId", "mass-" + NewNumber);

            Pulley pulley = pulleyCopy.GetComponent<Pulley>();
            SetFloat(pulley, "angleDegrees", StartAngleDegrees);
            log.AppendLine($"  Pulley 3 and Mass 3 made as copies of Pulley 2 and Mass 2. Pulley 3 starts at {StartAngleDegrees:0} degrees.");
            return pulley;
        }

        private static GameObject Copy(GameObject original, string copyName, Transform parent)
        {
            GameObject copy = Object.Instantiate(original, parent);   // same local place under the same parent
            Undo.RegisterCreatedObjectUndo(copy, "Copy " + original.name);
            copy.name = copyName;
            return copy;
        }

        // A copy still holds links to the originals (Pulley 3 -> Mass 2, the loop -> Pulley 2, and so on).
        // The map says which original part became which copied part, and every link found in the copies is swapped.
        // The two lists run in step: originals[i] was copied to copies[i]. A last count shows what was missed.
        private static void PointLinksAtCopies(GameObject[] originals, GameObject[] copies, StringBuilder log)
        {
            var map = new Dictionary<Object, Object>();
            for (int i = 0; i < originals.Length; i++) MapParts(originals[i], copies[i], map);
            for (int i = 0; i < copies.Length; i++) RedirectLinks(copies[i], map);

            int missed = 0;
            for (int i = 0; i < copies.Length; i++) missed += CountLinksInto(copies[i], originals);
            log.AppendLine(missed == 0
                ? "  Links of the copies: all of them point at the copies."
                : $"  Warning: {missed} link(s) of the copies still point at Pulley 2 or Mass 2. Check Pulley 3 and Mass 3 in the Inspector.");
        }

        // How many saved links in the copy still point into one of the original objects. It should be 0.
        private static int CountLinksInto(GameObject root, GameObject[] originals)
        {
            int count = 0;
            foreach (Component part in root.GetComponentsInChildren<Component>(true))
            {
                if (part == null || part is Transform) continue;

                SerializedProperty property = new SerializedObject(part).GetIterator();
                while (property.NextVisible(true))
                {
                    bool link = property.propertyType == SerializedPropertyType.ObjectReference;
                    if (link && IsInsideAny(property.objectReferenceValue, originals)) count++;
                }
            }
            return count;
        }

        private static bool IsInsideAny(Object target, GameObject[] places)
        {
            var component = target as Component;
            var gameObject = target as GameObject;
            Transform spot = component != null ? component.transform : gameObject != null ? gameObject.transform : null;
            if (spot == null) return false;

            for (int i = 0; i < places.Length; i++)
            {
                if (spot.IsChildOf(places[i].transform)) return true;
            }
            return false;
        }

        // Walks the original and the copy side by side. A copy keeps its children and components in the same order.
        private static void MapParts(GameObject original, GameObject copy, Dictionary<Object, Object> map)
        {
            map[original] = copy;
            Component[] fromParts = original.GetComponents<Component>();
            Component[] toParts = copy.GetComponents<Component>();
            int shared = Mathf.Min(fromParts.Length, toParts.Length);
            for (int i = 0; i < shared; i++)
            {
                bool pair = fromParts[i] != null && toParts[i] != null && fromParts[i].GetType() == toParts[i].GetType();
                if (pair) map[fromParts[i]] = toParts[i];
            }

            int children = Mathf.Min(original.transform.childCount, copy.transform.childCount);
            for (int i = 0; i < children; i++)
            {
                MapParts(original.transform.GetChild(i).gameObject, copy.transform.GetChild(i).gameObject, map);
            }
        }

        private static void RedirectLinks(GameObject root, Dictionary<Object, Object> map)
        {
            foreach (Component part in root.GetComponentsInChildren<Component>(true))
            {
                if (part == null || part is Transform) continue;   // the parent link of a Transform must stay as it is
                RedirectLinksOf(part, map);
            }
        }

        private static void RedirectLinksOf(Component part, Dictionary<Object, Object> map)
        {
            var serialized = new SerializedObject(part);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                Object target = property.objectReferenceValue;
                if (target != null && map.TryGetValue(target, out Object replacement))
                {
                    property.objectReferenceValue = replacement;
                    changed = true;
                }
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- the lists that must know the new pulley ----------

        private static void AddToTrackers(Pulley pulley, StringBuilder log)
        {
            var state = Object.FindAnyObjectByType<LabStateTracker>(FindObjectsInactive.Include);
            var store = Object.FindAnyObjectByType<LabProgressStore>(FindObjectsInactive.Include);

            AppendOnce(state, "pulleys", pulley, log);
            AppendOnce(state, "labObjects", pulley.GetComponent<LabObject>(), log);
            AppendOnce(state, "labObjects", pulley.MassObject, log);
            AppendOnce(store, "pulleys", pulley, log);
        }

        // Adds the object to a saved list of the owner, unless it is in the list already.
        private static void AppendOnce(Object owner, string listName, Object value, StringBuilder log)
        {
            if (owner == null || value == null)
            {
                log.AppendLine($"  Warning: could not add to '{listName}' (the owner or the value is missing).");
                return;
            }

            var serialized = new SerializedObject(owner);
            SerializedProperty list = serialized.FindProperty(listName);
            if (list == null || !list.isArray)
            {
                log.AppendLine($"  Warning: {owner.GetType().Name} has no list named '{listName}'.");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            log.AppendLine($"  {value.name}: added to {owner.GetType().Name}.{listName}.");
        }

        // ---------- saved fields ----------

        private static void SetString(Object owner, string field, string value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;

            property.stringValue = value;
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

        private static bool Fail(StringBuilder log, string reason)
        {
            log.AppendLine("  ERROR: " + reason);
            return false;
        }
    }
}
#endif
