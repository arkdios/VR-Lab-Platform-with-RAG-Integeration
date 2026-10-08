using System;
using PhysicsLab.Core.State;
using UnityEngine;

namespace PhysicsLab.Lab
{
    // All classes here are data holders. Field names are the JSON keys.
    // Plus, Unity's JsonUtility cannot write dictionaries or null objects.

    [Serializable]
    public class PulleyState
    {
        public string id;
        public Vector3 object_position;
        public float angle_deg;
    }

    [Serializable]
    public class MassState
    {
        public string id;
        public Vector3 object_position;
        public float mass_value; // kilograms
    }

    [Serializable]
    public class ScaleState
    {
        public string id;
        public Vector3 object_position;
        public float reading_n; // newtons
    }

    [Serializable]
    public class PerfState
    {
        public float fps;
        public float frame_ms_avg;
        public float frame_ms_max;
    }

    /// <summary>
    /// Everything the headset side sends with one question.
    /// </summary>
    [Serializable]
    public class LabStatePayload
    {
        // Request info (filled by LabAssistant, Task 8)
        public string session_id;
        public int request_id;
        public string condition;
        public string question_type;
        public string question;

        // Lab state (filled by LabStateTracker)
        public long t_ms;
        public string current_step;
        public string previous_step;
        public EventRecord previous_action = new EventRecord();
        public string held_object;
        public string selected_object;
        public Vector3 ring_position;
        public PulleyState[] pulleys;
        public MassState[] masses;
        public ScaleState spring_scale;
        public EventRecord[] recent_events;
        public PerfState perf = new PerfState();
    }
}