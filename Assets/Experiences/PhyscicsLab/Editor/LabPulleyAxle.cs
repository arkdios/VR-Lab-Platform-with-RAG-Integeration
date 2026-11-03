#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Finds which way the wheel of a pulley model points, so the pulley can be turned correctly on the table.
    /// The wheel is a flat disc, so its axle is the direction in which the wheel mesh is thinnest.
    /// The result is stored on the Pulley component (wheelAxleLocal and clampSideLocal) in the pulley's own space.
    /// Pulley.FacingRotation then turns the pulley so the axle lies along the rim of the table (the tangent),
    /// which puts the wheel plane in line with the string. Used by LabPhysicsRepair, before the pulley is placed.
    /// </summary>
    internal static class LabPulleyAxle
    {
        private const string WheelName = "Pulley_Wheel";
        private const string ClampName = "Pulley_Clamp";

        public static void Measure(Pulley pulley, StringBuilder log)
        {
            MeshFilter wheel = FindPart(pulley.transform, WheelName);
            if (wheel == null || wheel.sharedMesh == null)
            {
                log.AppendLine($"  Warning: {pulley.name} has no {WheelName} mesh, so its wheel turn was not measured (the old turn stays).");
                return;
            }

            Vector3 axle = ThinnestAxis(wheel, pulley.transform, log, pulley.name);
            Vector3 clampSide = DirectionToClamp(pulley.transform, wheel);

            var serialized = new SerializedObject(pulley);
            SetVector(serialized, "wheelAxleLocal", axle);
            SetVector(serialized, "clampSideLocal", clampSide);
            serialized.ApplyModifiedProperties();

            log.AppendLine($"  {pulley.name}: wheel axle {Format(axle)}, clamp side {Format(clampSide)} (in the pulley's own space).");
        }

        // The wheel mesh is a thin disc. Its smallest size (in world metres, so scale is counted) is the axle.
        private static Vector3 ThinnestAxis(MeshFilter wheel, Transform pulley, StringBuilder log, string pulleyName)
        {
            Vector3 meshSize = wheel.sharedMesh.bounds.size;
            Vector3 scale = wheel.transform.lossyScale;
            var size = new Vector3(meshSize.x * Mathf.Abs(scale.x), meshSize.y * Mathf.Abs(scale.y), meshSize.z * Mathf.Abs(scale.z));

            int thin = 0;
            if (size.y < size[thin]) thin = 1;
            if (size.z < size[thin]) thin = 2;
            log.AppendLine($"  {pulleyName}: wheel size {size.x:0.000} x {size.y:0.000} x {size.z:0.000} m (its own x, y, z). The thinnest side is the axle.");

            Vector3 axisInWheel = Vector3.zero;
            axisInWheel[thin] = 1f;
            Vector3 world = wheel.transform.TransformDirection(axisInWheel);
            return pulley.InverseTransformDirection(world).normalized;
        }

        // From the wheel centre to the clamp centre. When the clamp sits on the table side of the wheel,
        // this tells the pulley which way round to stand. Zero when the clamp is not found.
        private static Vector3 DirectionToClamp(Transform pulley, MeshFilter wheel)
        {
            MeshFilter clamp = FindPart(pulley, ClampName);
            if (clamp == null) return Vector3.zero;

            Renderer wheelRenderer = wheel.GetComponent<Renderer>();
            Renderer clampRenderer = clamp.GetComponent<Renderer>();
            if (wheelRenderer == null || clampRenderer == null) return Vector3.zero;

            Vector3 world = clampRenderer.bounds.center - wheelRenderer.bounds.center;
            return pulley.InverseTransformDirection(world).normalized;
        }

        private static MeshFilter FindPart(Transform root, string partName)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name == partName) return filter;
            }
            return null;
        }

        private static void SetVector(SerializedObject serialized, string field, Vector3 value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.vector3Value = value;
        }

        private static string Format(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
#endif