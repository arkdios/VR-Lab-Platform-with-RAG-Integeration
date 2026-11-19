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
        public float angle_deg; // degrees, counter-clockwise like the pulleys: the direction of the string from the ring to the scale
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

    /// <summary>
    /// One check of the current step that does not pass yet. Only failed checks are sent.
    /// actual is what the lab shows now. limit is the number it is compared with: the most the value may
    /// differ from the case value (angle, mass, ring), the most the scale may read (scale-zero), or the least
    /// it must read (scale-pulling). For yes/no checks, actual 1 means yes and the limit is 1.
    /// The case value itself is never sent, because the backend finds it in the lab manual.
    /// </summary>
    [Serializable]
    public class StepCheckState
    {
        public string id;     // for example pulley-1-angle, ring-centred, scale-pulling
        public float actual;
        public float limit;
        public string unit;   // deg, kg, N, mm, rows or yes/no
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
        public string step_stage;         // setup, configure-masses, connect-scale, equilibrium or record
        public string case_id;            // A to D, empty during setup
        public StepCheckState[] failed_checks; // checks of the current step that do not pass yet, empty when the step is done
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