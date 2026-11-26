using System.Collections.Generic;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Marks the practice bench. Everything under this object is practice gear: a copy of the lab that is
    /// never judged, never recorded and never sent to the assistant.
    /// Code that looks for "all pulleys" or "all strings" in the scene asks this class first, so the practice
    /// copy is never counted as the real lab.
    /// Put it on the PracticeBench root object. The Editor builder (LabPracticeBenchBuilder) does that.
    /// </summary>
    public class PracticeZone : MonoBehaviour
    {
        /// <summary>True when the part is the practice root or sits under it.</summary>
        public static bool Contains(Component part) =>
            part != null && part.GetComponentInParent<PracticeZone>(true) != null;

        /// <summary>
        /// The same list without the practice parts. Use it right after FindObjectsByType.
        /// It makes a new array and leaves the old one alone.
        /// </summary>
        public static T[] Without<T>(T[] found) where T : Component
        {
            var kept = new List<T>(found.Length);
            foreach (T part in found)
            {
                if (!Contains(part)) kept.Add(part);
            }
            return kept.ToArray();
        }
    }
}
