using System;
using UnityEngine;

namespace PhysicsLab.Lab
{
    // Data holders for the saved progress file. Field names are the JSON keys.
    // JsonUtility cannot write dictionaries, so everything is a plain class or array.

    /// <summary>
    /// Where one movable object was left, in table space (metres, relative to TableCenter).
    /// </summary>
    [Serializable]
    public class SavedBody
    {
        public string id;
        public Vector3 table_position;
    }

    /// <summary>
    /// The angle and the hanging mass of one pulley.
    /// </summary>
    [Serializable]
    public class SavedPulley
    {
        public string id;
        public float angle_deg;
        public float mass_kg;
    }

    /// <summary>
    /// Everything the lab needs to continue where the student stopped.
    /// </summary>
    [Serializable]
    public class LabProgressData
    {
        // Bump this number when the fields change, so an old file is ignored instead of misread.
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int step_index;
        public long saved_at_unix_ms;
        public SavedBody[] bodies;
        public SavedPulley[] pulleys;
    }
}
