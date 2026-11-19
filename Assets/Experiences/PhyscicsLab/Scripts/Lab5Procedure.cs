using System;
using System.Collections.Generic;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The stages of PHYS 124 Lab 5. Setup is done once. The other four repeat for every case (A to D).
    /// </summary>
    public enum Lab5Stage
    {
        Setup,
        ConfigureMasses,   // pulleys clamped at the right angles, the right masses hanging
        ConnectScale,      // the spring scale is tied on and pulling
        Equilibrium,       // the ring sits on the post and stays there
        Record             // the measurement is saved
    }

    /// <summary>
    /// One hanging mass of a case: the angle of its pulley and how heavy it is.
    /// </summary>
    public readonly struct CaseLoad
    {
        public CaseLoad(float angleDeg, float massKg)
        {
            AngleDeg = angleDeg;
            MassKg = massKg;
        }

        public float AngleDeg { get; }
        public float MassKg { get; }

        /// <summary>A pulley that carries nothing. It pads a case that needs fewer pulleys than the table has.</summary>
        public bool IsEmpty => MassKg <= 0f;

        public static CaseLoad Empty => new CaseLoad(0f, 0f);
    }

    /// <summary>
    /// One case of Table 1: an id (A to D) and the hanging masses the student has to set up.
    /// </summary>
    public sealed class Lab5Case
    {
        public Lab5Case(string id, params CaseLoad[] loads)
        {
            Id = id;
            Loads = loads;
        }

        public string Id { get; }
        public CaseLoad[] Loads { get; }

        /// <summary>The load at this position. Past the end it is an empty load.</summary>
        public CaseLoad LoadAt(int index) => index < Loads.Length ? Loads[index] : CaseLoad.Empty;
    }

    /// <summary>
    /// One step of the lab: a stage of one case, or the single setup step.
    /// </summary>
    public sealed class Lab5Step
    {
        public Lab5Step(Lab5Stage stage, int caseIndex, string caseId)
        {
            Stage = stage;
            CaseIndex = caseIndex;
            CaseId = caseId;
            bool isSetup = stage == Lab5Stage.Setup;
            Id = isSetup ? "setup" : $"case-{caseId.ToLowerInvariant()}-{Lab5Procedure.StageName(stage)}";
            Title = isSetup ? "Setup" : $"Case {caseId}: {Lab5Procedure.StageTitle(stage)}";
        }

        /// <summary>Sent to the backend as current_step, for example "case-a-equilibrium".</summary>
        public string Id { get; }
        public Lab5Stage Stage { get; }

        /// <summary>Position in the case list, or -1 for the setup step.</summary>
        public int CaseIndex { get; }

        /// <summary>"A" to "D", or an empty text for the setup step.</summary>
        public string CaseId { get; }

        /// <summary>Short text for the student's HUD.</summary>
        public string Title { get; }

        public bool HasCase => CaseIndex >= 0;
    }

    /// <summary>
    /// The fixed facts of Lab 5: the four cases and the list of steps. Nothing here reads the scene.
    /// </summary>
    public static class Lab5Procedure
    {
        public const int MaxPulleys = 3;

        // Table 1 of the lab slides (Phys124_144_Ex5_VectorForces_v3, slide 16).
        // The cases are listed with the fewest pulleys first. CaseCountFor relies on that order.
        private static readonly Lab5Case[] Cases =
        {
            new Lab5Case("A", new CaseLoad(30f, 0.200f), new CaseLoad(120f, 0.200f)),
            new Lab5Case("B", new CaseLoad(30f, 0.200f), new CaseLoad(80f, 0.150f)),
            new Lab5Case("C", new CaseLoad(5f, 0.200f), new CaseLoad(95f, 0.150f)),
            new Lab5Case("D", new CaseLoad(40f, 0.100f), new CaseLoad(100f, 0.200f), new CaseLoad(235f, 0.300f))
        };

        public static Lab5Case CaseAt(int index) => Cases[index];

        /// <summary>
        /// How many cases the table can run with this many pulleys. Two pulleys give A to C.
        /// Case D needs three.
        /// </summary>
        public static int CaseCountFor(int pulleyCount)
        {
            int count = 0;
            foreach (Lab5Case lab5Case in Cases)
            {
                if (lab5Case.Loads.Length <= pulleyCount) count++;
            }
            return count;
        }

        /// <summary>
        /// Setup, then four steps for each case. With 3 cases that is 13 steps.
        /// </summary>
        public static Lab5Step[] BuildSteps(int caseCount)
        {
            int cases = Math.Max(0, Math.Min(caseCount, Cases.Length));
            var steps = new List<Lab5Step> { new Lab5Step(Lab5Stage.Setup, -1, string.Empty) };
            for (int caseIndex = 0; caseIndex < cases; caseIndex++) AddCaseSteps(steps, caseIndex);
            return steps.ToArray();
        }

        /// <summary>
        /// The stage name that goes to the backend as step_stage.
        /// </summary>
        public static string StageName(Lab5Stage stage)
        {
            switch (stage)
            {
                case Lab5Stage.Setup: return "setup";
                case Lab5Stage.ConfigureMasses: return "configure-masses";
                case Lab5Stage.ConnectScale: return "connect-scale";
                case Lab5Stage.Equilibrium: return "equilibrium";
                case Lab5Stage.Record: return "record";
                default: throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        private static void AddCaseSteps(List<Lab5Step> steps, int caseIndex)
        {
            string caseId = Cases[caseIndex].Id;
            for (Lab5Stage stage = Lab5Stage.ConfigureMasses; stage <= Lab5Stage.Record; stage++)
            {
                steps.Add(new Lab5Step(stage, caseIndex, caseId));
            }
        }

        /// <summary>
        /// The stage name for the student's HUD.
        /// </summary>
        public static string StageTitle(Lab5Stage stage)
        {
            switch (stage)
            {
                case Lab5Stage.Setup: return "Setup";
                case Lab5Stage.ConfigureMasses: return "Configure masses";
                case Lab5Stage.ConnectScale: return "Connect scale";
                case Lab5Stage.Equilibrium: return "Equilibrium";
                case Lab5Stage.Record: return "Record";
                default: throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }
    }
}