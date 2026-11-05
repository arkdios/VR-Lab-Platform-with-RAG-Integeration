using System;
using PhysicsLab.Core.State;
using UnityEngine;

namespace PhysicsLab.Lab
{
    // All classes here are data holders. Field names are the JSON keys.
    // Plus, Unity's JsonUtility cannot write dictionaries or null objects.

    /// <summary>
    /// What every lab object reports about itself. The other state classes add their own numbers on top.
    /// </summary>
    [Serializable]
    public class LabObjectState
    {
        public string id;
        public Vector3 object_position;          // table space, metres
        public bool held_by_user;                // a hand or the mouse holds it right now
        public string[] attached_to = new string[0]; // ids of what it is tied or clamped to, empty when free
        public string status = string.Empty;     // one word, see LabStatusReader
    }

    [Serializable]
    public class RingState : LabObjectState
    {
    }

    [Serializable]
    public class PulleyState : LabObjectState
    {
        public float angle_deg;
    }

    [Serializable]
    public class MassState : LabObjectState
    {
        public float mass_value; // kilograms
    }

    [Serializable]
    public class ScaleState : LabObjectState
    {
        public float reading_n; // newtons
    }

    /// <summary>
    /// One arm of the string: ring to (pulley) to its end.
    /// </summary>
    [Serializable]
    public class StringState
    {
        public string id;
        public string from;   // always the ring
        public string via;    // the pulley the string runs over, or empty
        public string to;     // the mass or scale on the other end, or free-end
        public bool taut;
        public float tension_n; // newtons
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
        public bool step_completed;       // the rules marked the current step as done
        public int step_index;            // 0 for the first step
        public int step_count;
        public float step_elapsed_s;      // seconds since this step began
        public EventRecord previous_action = new EventRecord();
        public string held_object;
        public string selected_object;
        public Vector3 ring_position;     // kept for the old backend. The same number is in ring.object_position
        public RingState ring = new RingState();
        public PulleyState[] pulleys;
        public MassState[] masses;
        public ScaleState spring_scale;
        public StringState[] strings;
        public EventRecord[] recent_events;
        public PerfState perf = new PerfState();
    }
}