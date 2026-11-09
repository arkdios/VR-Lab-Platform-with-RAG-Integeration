#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Scale hand parts.
    /// Makes the parts a hand picks up easier to grab in the headset: the spring scale, both hanging masses and the
    /// spare hooked masses on the bench. The pulleys keep their real size (PulleyScale), because the clamp has to
    /// fit the table, and the wheel has to stay clear of the table legs. The table, ring, chair and bench stay as they are.
    /// It SETS the scale (it does not multiply), so running it twice gives the same result.
    /// Run it BEFORE Lab > 0. Lab > 0 works out colliders, hang spots and string lengths from the new size.
    /// </summary>
    public static class LabHandPartScale
    {
        private const float HandPartScale = 1.5f;   // spring scale and masses
        private const float PulleyScale = 1.0f;     // the clamp grips the table, so the pulleys keep their real size
        private const string EquipmentRootName = "LabEquipments";
        private const string PropsRootName = "LabProps (Temp)";
        private const string SpareMassPrefix = "HookedMass";   // the spare masses on the bench

        private static readonly string[] PulleyNames = { "Pulley 1", "Pulley 2" };
        private static readonly string[] MassNames = { "Mass 1", "Mass 2" };

        [MenuItem("Lab/Scale hand parts")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabHandPartScale]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentRootName);
            if (equipment == null)
            {
                log.AppendLine($"  ERROR: no object named {EquipmentRootName}.");
                return false;
            }

            ScaleEquipmentParts(equipment, PulleyNames, PulleyScale, log);
            ScaleEquipmentParts(equipment, MassNames, HandPartScale, log);
            ScaleSpringScale(log);
            ScaleSpareMasses(log);
            log.AppendLine("  Done. Now run Lab > 0 Build working lab, then Ctrl+S.");
            return true;
        }

        private static void ScaleEquipmentParts(Transform equipment, string[] partNames, float scale, StringBuilder log)
        {
            foreach (string partName in partNames)
            {
                Transform part = equipment.Find(partName);
                if (part == null) log.AppendLine($"  Warning: {partName} was not found, so it was not scaled.");
                else ScaleAroundPivot(part, scale, log);
            }
        }

        private static void ScaleSpringScale(StringBuilder log)
        {
            SpringScale springScale = Object.FindAnyObjectByType<SpringScale>(FindObjectsInactive.Include);
            if (springScale == null) log.AppendLine("  Warning: no SpringScale in the scene, so it was not scaled.");
            else ScaleAroundPivot(springScale.transform, HandPartScale, log);
        }

        private static void ScaleSpareMasses(StringBuilder log)
        {
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (props == null)
            {
                log.AppendLine($"  Warning: no object named {PropsRootName}, so the spare masses were not scaled.");
                return;
            }

            foreach (Transform prop in props)   // direct children only: each one is a prop
            {
                if (prop.name.StartsWith(SpareMassPrefix)) ScaleOnSurface(prop, HandPartScale, log);
            }
        }

        // Grows the part around its own pivot. The pivot stays where it is.
        private static void ScaleAroundPivot(Transform part, float scale, StringBuilder log)
        {
            Vector3 before = part.localScale;
            SetScale(part, scale);
            log.AppendLine($"  {part.name}: scale {Format(before)} set to {scale:0.00}.");
        }

        // A part that rests on a surface also grows downward, so it would sink into the surface.
        // This lifts it by exactly the amount its bottom moved down.
        private static void ScaleOnSurface(Transform part, float scale, StringBuilder log)
        {
            Vector3 before = part.localScale;
            float bottomBefore = LowestPoint(part);
            SetScale(part, scale);
            float rise = bottomBefore - LowestPoint(part);

            Undo.RecordObject(part, "Lift scaled part");
            part.position += Vector3.up * rise;
            log.AppendLine($"  {part.name}: scale {Format(before)} set to {scale:0.00}, lifted {rise:0.000} m so it does not sink.");
        }

        private static void SetScale(Transform part, float scale)
        {
            Undo.RecordObject(part, "Scale hand part");
            part.localScale = Vector3.one * scale;
        }

        // World height of the lowest point of everything the part draws. A part with no renderer gives its own height.
        private static float LowestPoint(Transform part)
        {
            float lowest = part.position.y;
            bool found = false;
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
            {
                lowest = found ? Mathf.Min(lowest, renderer.bounds.min.y) : renderer.bounds.min.y;
                found = true;
            }
            return lowest;
        }

        private static string Format(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
#endif