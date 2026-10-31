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
