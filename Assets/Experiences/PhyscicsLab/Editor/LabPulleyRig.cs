#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// The one place that knows how the pulleys of the force table are named: "Pulley 1", "Pulley 2" and,
    /// for Case D, "Pulley 3". The builders ask it for the pulleys that exist, so they work with two or three.
    /// </summary>
    internal static class LabPulleyRig
    {
        public static string PulleyName(int number) => "Pulley " + number;

        public static string MassName(int number) => "Mass " + number;

        /// <summary>
        /// The pulleys that exist now under LabEquipments, in number order (Pulley 1 first).
        /// A missing pulley is skipped, so the result can be shorter than Lab5Procedure.MaxPulleys.
        /// </summary>
        public static Pulley[] FindAll(Transform equipment)
        {
            var found = new List<Pulley>(Lab5Procedure.MaxPulleys);
            for (int number = 1; number <= Lab5Procedure.MaxPulleys; number++)
            {
                Transform part = equipment.Find(PulleyName(number));
                Pulley pulley = part != null ? part.GetComponent<Pulley>() : null;
                if (pulley != null) found.Add(pulley);
            }
            return found.ToArray();
        }
    }
}
#endif
