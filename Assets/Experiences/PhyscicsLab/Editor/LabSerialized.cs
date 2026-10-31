#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Reads and writes serialized fields by name, so the builders can fill private [SerializeField] fields.
    /// </summary>
    internal static class LabSerialized
    {
        public static T ReadReference<T>(Object owner, string field) where T : Object
        {
            if (owner == null) return null;
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            return property != null ? property.objectReferenceValue as T : null;
        }

        /// <summary>Sets a bool field by name. Returns false when the field does not exist.</summary>
        public static bool SetBool(Object owner, string field, bool value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return false;

            property.boolValue = value;
            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Sets a text field by name. Returns false when the field does not exist.</summary>
        public static bool SetString(Object owner, string field, string value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return false;

            property.stringValue = value;
            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Sets an enum field by name, by its position in the list. Returns false when the field does not exist.</summary>
        public static bool SetEnum(Object owner, string field, int index)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return false;

            property.enumValueIndex = index;
            serialized.ApplyModifiedProperties();
            return true;
        }

        public static bool SetObject(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[LabSerialized] {owner.GetType().Name} has no serialized field '{field}'.", owner);
                return false;
            }

            Undo.RecordObject(owner, "Set " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            return true;
        }
    }
}
#endif
