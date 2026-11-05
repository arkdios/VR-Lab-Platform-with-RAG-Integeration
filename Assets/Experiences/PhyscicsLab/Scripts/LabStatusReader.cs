using System.Collections.Generic;
using PhysicsLab.Lab.Connect;
using PhysicsLab.Lab.Rope;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Works out what each lab object is doing right now: is it held, what is it attached to, and one status word.
    /// LabStateTracker calls it when it builds a snapshot. It only reads. It never moves or changes anything.
    ///
    /// Status words (the backend can put this list in its prompt):
    ///   held       a hand or the mouse holds it
    ///   free       the ring, not held
    ///   clamped    a pulley fixed to a table spot
    ///   loose      a pulley not on a spot, or a part with no string
    ///   hanging    a mass whose string is tight
    ///   connected  the spring scale, its string is tight
    ///   slack      a mass or the scale whose string is loose
    /// </summary>
    public sealed class LabStatusReader
    {
        public const string Held = "held";
        public const string Free = "free";
        public const string Clamped = "clamped";
        public const string Loose = "loose";
        public const string Hanging = "hanging";
        public const string Connected = "connected";
        public const string Slack = "slack";

        private const string FreeEndId = "free-end";
        private const string TableId = "table";
        private const string DefaultRingId = "ring";

        private readonly LabObject _ringObject;
        private readonly LabObject _scaleObject;
        private RopeArm[] _arms; // found on first use, because the arms are built after this class is made

        public LabStatusReader(Rigidbody ring, SpringScale scale)
        {
            _ringObject = ring != null ? ring.GetComponent<LabObject>() : null;
            _scaleObject = scale != null ? scale.GetComponent<LabObject>() : null;
        }

        public string RingId => _ringObject != null ? _ringObject.ObjectId : DefaultRingId;

        public void FillRing(RingState state)
        {
            state.held_by_user = _ringObject != null && _ringObject.IsHeld;
            state.attached_to = IdsOfAllStrings();
            state.status = state.held_by_user ? Held : Free;
        }

        public void FillPulley(PulleyState state, Pulley pulley)
        {
            ConnectPart clamp = pulley.GetComponent<ConnectPart>();
            bool clamped = clamp != null && clamp.IsConnected;

            state.held_by_user = pulley.GetComponent<LabObject>().IsHeld;
            state.status = state.held_by_user ? Held : clamped ? Clamped : Loose;

            // A clamped pulley is attached to the table. Any string that runs over it is attached too.
            var attached = new List<string>();
            if (clamped) attached.Add(TableId);
            RopeArm arm = FindArmOverPulley(pulley);
            if (arm != null) attached.Add(StringIdOf(arm));
            state.attached_to = attached.ToArray();
        }

        public void FillMass(MassState state, Pulley pulley)
        {
            LabObject massObject = pulley.MassObject;
            RopeArm arm = FindArmEndingAt(massObject.gameObject);

            state.held_by_user = massObject.IsHeld;
            state.attached_to = arm != null ? new[] { StringIdOf(arm) } : new string[0];
            if (state.held_by_user) state.status = Held;
            else if (arm == null) state.status = Loose;
            else state.status = arm.IsTaut ? Hanging : Slack;
        }

        public void FillScale(ScaleState state)
        {
            RopeArm arm = _scaleObject != null ? FindArmEndingAt(_scaleObject.gameObject) : null;

            state.held_by_user = _scaleObject != null && _scaleObject.IsHeld;
            state.attached_to = arm != null ? new[] { StringIdOf(arm) } : new string[0];
            if (state.held_by_user) state.status = Held;
            else if (arm == null) state.status = Loose;
            else state.status = arm.IsTaut ? Connected : Slack;
        }

        public StringState[] ReadStrings()
        {
            RopeArm[] arms = Arms();
            var states = new List<StringState>(arms.Length);
            foreach (RopeArm arm in arms)
            {
                Pulley pulley = arm.Guide != null ? arm.Guide.GetComponentInParent<Pulley>() : null;
                LabObject end = arm.EndBody != null ? arm.EndBody.GetComponent<LabObject>() : null;
                states.Add(new StringState
                {
                    id = StringIdOf(arm),
                    from = RingId,
                    via = pulley != null ? pulley.ObjectId : string.Empty,
                    to = end != null ? end.ObjectId : FreeEndId,
                    taut = arm.IsTaut,
                    tension_n = Mathf.Round(arm.TensionNewtons * 100f) / 100f // 0.01 N
                });
            }
            return states.ToArray();
        }

        // "Arm_Pulley1" becomes "string-pulley1". The id follows the arm's name in the Hierarchy.
        private static string StringIdOf(RopeArm arm) =>
            "string-" + arm.name.Replace("Arm_", string.Empty).ToLowerInvariant();

        private string[] IdsOfAllStrings()
        {
            RopeArm[] arms = Arms();
            var ids = new string[arms.Length];
            for (int i = 0; i < arms.Length; i++) ids[i] = StringIdOf(arms[i]);
            return ids;
        }

        // The arm whose string runs over this pulley. Its guide point is a child of the pulley.
        private RopeArm FindArmOverPulley(Pulley pulley)
        {
            foreach (RopeArm arm in Arms())
            {
                if (arm.Guide != null && arm.Guide.IsChildOf(pulley.transform)) return arm;
            }
            return null;
        }

        // The arm whose loose end is this object (a mass or the spring scale).
        private RopeArm FindArmEndingAt(GameObject target)
        {
            foreach (RopeArm arm in Arms())
            {
                if (arm.EndBody != null && arm.EndBody.gameObject == target) return arm;
            }
            return null;
        }

        // Only arms that started well. RopeArm turns itself off when its setup is wrong.
        private RopeArm[] Arms()
        {
            if (_arms != null) return _arms;

            RopeArm[] found = Object.FindObjectsByType<RopeArm>();
            var valid = new List<RopeArm>(found.Length);
            foreach (RopeArm arm in found)
            {
                if (arm.Link != null) valid.Add(arm);
            }
            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // the same order in every snapshot
            _arms = valid.ToArray();
            return _arms;
        }
    }
}