#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PhysicsLab.Core.State;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>How the spring scale string is in a test setup.</summary>
    public enum TestScaleMode
    {
        Slack,      // tied on, loose, reads 0 N
        Weak,       // tied on, barely taut, reads below the pulling limit
        Balanced    // pulls against the masses, as it would at equilibrium
    }

    /// <summary>
    /// One lab setup for the assistant test set: a step, the pulleys, the scale and the ring,
    /// plus the checks that must fail and the plain-words fix. The fix text never goes into a payload.
    /// </summary>
    public sealed class AssistantTestConfig
    {
        public string Id;
        public string Description;
        public int CaseIndex;                 // -1 for the setup step
        public Lab5Stage Stage;
        public PulleyReading[] Pulleys;       // always three, like the lab
        public TestScaleMode Scale;
        public float RingOffsetM;             // how far the ring is from the post
        public float RingSpreadM;             // how far the ring moved during the last second
        public bool MeasurementReady;
        public string[] ExpectedFailedChecks; // the ids the judge must fail, in the judge's order
        public string[] KnownFix;             // what a correct answer tells the student to do
    }

    /// <summary>The numbers the exporter writes for one configuration, plus the checks the judge really failed.</summary>
    public sealed class AssistantTestRun
    {
        public Lab5Step Step;
        public int StepIndex;
        public int StepCount;
        public LabReading Reading;
        public CheckResult[] Results;
        public int ResultCount;
    }

    /// <summary>
    /// Makes the assistant test set. Sam asked for a small set of setups across the cases, each with one known
    /// correct fix, run several times. Each setup here is judged by the real Lab5Judge, so its failed checks and
    /// its payload are the ones the lab would send. It reads no scene, so it can run without Play Mode.
    /// The answers (the known fixes) are kept apart from the payloads, so a payload never holds the answer.
    /// </summary>
    public static class AssistantTestSetBuilder
    {
        public const int PulleyCount = Lab5Procedure.MaxPulleys;
        private const float GravityMetresPerSecondSquared = 9.81f;
        private const float TableRadiusMetres = 0.4f;
        private const float WeakPullN = 0.05f;
        private const float ScaleStringMetres = 0.45f;
        private const float HangDropMetres = 0.12f;
        private const float StepSeconds = 90f;
        private static readonly string[] PulleyIds = { "pulley-1", "pulley-2", "pulley-3" };

        private static PulleyReading Pulley(float angleDeg, float massKg, bool clamped = true) =>
            new PulleyReading { AngleDeg = angleDeg, MassKg = massKg, Clamped = clamped };

        // The third pulley is unused in Cases A to C. It sits clamped with no mass.
        private static PulleyReading[] Two(PulleyReading first, PulleyReading second) =>
            new[] { first, second, Pulley(200f, 0f) };

        /// <summary>The setups. Case numbers follow Table 1 of the lab slides (see Lab5Procedure).</summary>
        public static List<AssistantTestConfig> Configs() => new List<AssistantTestConfig>
        {
            new AssistantTestConfig
            {
                Id = "A-01", CaseIndex = 0, Stage = Lab5Stage.ConnectScale,
                Description = "Case A, connect scale. Pulley 1 at 120 deg with 200 g (right). Pulley 2 at 35 deg with 200 g (should be 30). The scale is tied on but slack. This is the setup of the 11-20 weekly report.",
                Pulleys = Two(Pulley(120f, 0.2f), Pulley(35f, 0.2f)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-angle", "scale-pulling" },
                KnownFix = new[] { "Move pulley 2 from 35 to 30 degrees.", "Pull the scale away from the ring until it reads at least 0.1 N (its string is tied on but slack)." }
            },
            new AssistantTestConfig
            {
                Id = "A-02", CaseIndex = 0, Stage = Lab5Stage.ConnectScale,
                Description = "Case A, connect scale. Pulleys and masses are right. The scale is tied on but slack.",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(120f, 0.2f)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "scale-pulling" },
                KnownFix = new[] { "Pull the scale away from the ring until it reads at least 0.1 N. It is already tied on." }
            },
            new AssistantTestConfig
            {
                Id = "A-03", CaseIndex = 0, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case A, configure masses. Pulley 2 carries 100 g (should be 200 g).",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(120f, 0.1f)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-mass" },
                KnownFix = new[] { "Hang 200 g on pulley 2 instead of 100 g." }
            },
            new AssistantTestConfig
            {
                Id = "A-04", CaseIndex = 0, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case A, configure masses. Pulley 2 carries no mass (should be 200 g).",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(120f, 0f)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-mass" },
                KnownFix = new[] { "Hang 200 g on pulley 2." }
            },
            new AssistantTestConfig
            {
                Id = "A-05", CaseIndex = 0, Stage = Lab5Stage.Equilibrium,
                Description = "Case A, equilibrium. Everything is right and the scale pulls, but the ring is 12 mm off the post.",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(120f, 0.2f)), Scale = TestScaleMode.Balanced, RingOffsetM = 0.012f,
                ExpectedFailedChecks = new[] { "ring-centred" },
                KnownFix = new[] { "Move the scale so the ring sits on the centre post (within 3 mm)." }
            },
            new AssistantTestConfig
            {
                Id = "A-06", CaseIndex = 0, Stage = Lab5Stage.Record,
                Description = "Case A, record. A good equilibrium measurement is held but not saved yet.",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(120f, 0.2f)), Scale = TestScaleMode.Balanced, MeasurementReady = true,
                ExpectedFailedChecks = new[] { "measurement-recorded" },
                KnownFix = new[] { "Press Record to save the measurement." }
            },
            new AssistantTestConfig
            {
                Id = "B-01", CaseIndex = 1, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case B, configure masses. Pulley 2 at 85 deg (should be 80).",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(85f, 0.15f)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-angle" },
                KnownFix = new[] { "Move pulley 2 from 85 to 80 degrees." }
            },
            new AssistantTestConfig
            {
                Id = "B-02", CaseIndex = 1, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case B, configure masses. Pulley 2 is at 80 deg with 150 g but is not clamped to a table spot.",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(80f, 0.15f, clamped: false)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-clamped" },
                KnownFix = new[] { "Clamp pulley 2 onto its table spot at 80 degrees." }
            },
            new AssistantTestConfig
            {
                Id = "B-03", CaseIndex = 1, Stage = Lab5Stage.ConnectScale,
                Description = "Case B, connect scale. Pulleys and masses are right. The scale string is barely taut and reads 0.05 N.",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(80f, 0.15f)), Scale = TestScaleMode.Weak,
                ExpectedFailedChecks = new[] { "scale-pulling" },
                KnownFix = new[] { "Pull the scale harder until it reads at least 0.1 N." }
            },
            new AssistantTestConfig
            {
                Id = "B-04", CaseIndex = 1, Stage = Lab5Stage.Equilibrium,
                Description = "Case B, equilibrium. Everything is right and the ring is on the post, but the ring is still moving.",
                Pulleys = Two(Pulley(30f, 0.2f), Pulley(80f, 0.15f)), Scale = TestScaleMode.Balanced, RingSpreadM = 0.01f,
                ExpectedFailedChecks = new[] { "ring-stable" },
                KnownFix = new[] { "Hold the scale still and wait until the ring stops moving (under 2 mm for 1 second)." }
            },
            new AssistantTestConfig
            {
                Id = "C-01", CaseIndex = 2, Stage = Lab5Stage.ConnectScale,
                Description = "Case C, connect scale. Pulleys and masses are right. The scale is tied on but slack.",
                Pulleys = Two(Pulley(5f, 0.2f), Pulley(95f, 0.15f)), Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "scale-pulling" },
                KnownFix = new[] { "Pull the scale away from the ring until it reads at least 0.1 N. It is already tied on." }
            },
            new AssistantTestConfig
            {
                Id = "C-02", CaseIndex = 2, Stage = Lab5Stage.ConnectScale,
                Description = "Case C, connect scale. Pulley 1 at 10 deg (should be 5). The scale already pulls.",
                Pulleys = Two(Pulley(10f, 0.2f), Pulley(95f, 0.15f)), Scale = TestScaleMode.Balanced,
                ExpectedFailedChecks = new[] { "pulley-1-angle" },
                KnownFix = new[] { "Move pulley 1 from 10 to 5 degrees." }
            },
            new AssistantTestConfig
            {
                Id = "D-01", CaseIndex = 3, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case D, configure masses. Pulley 3 at 230 deg (should be 235).",
                Pulleys = new[] { Pulley(40f, 0.1f), Pulley(100f, 0.2f), Pulley(230f, 0.3f) }, Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-3-angle" },
                KnownFix = new[] { "Move pulley 3 from 230 to 235 degrees." }
            },
            new AssistantTestConfig
            {
                Id = "D-02", CaseIndex = 3, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case D, configure masses. Pulley 3 carries 200 g (should be 300 g).",
                Pulleys = new[] { Pulley(40f, 0.1f), Pulley(100f, 0.2f), Pulley(235f, 0.2f) }, Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-3-mass" },
                KnownFix = new[] { "Hang 300 g on pulley 3 instead of 200 g." }
            },
            new AssistantTestConfig
            {
                Id = "D-03", CaseIndex = 3, Stage = Lab5Stage.ConfigureMasses,
                Description = "Case D, configure masses. Two faults: pulley 2 is not clamped, and pulley 3 carries no mass (should be 300 g).",
                Pulleys = new[] { Pulley(40f, 0.1f), Pulley(100f, 0.2f, clamped: false), Pulley(235f, 0f) }, Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-clamped", "pulley-3-mass" },
                KnownFix = new[] { "Clamp pulley 2 onto its table spot at 100 degrees.", "Hang 300 g on pulley 3." }
            },
            new AssistantTestConfig
            {
                Id = "S-01", CaseIndex = -1, Stage = Lab5Stage.Setup,
                Description = "Setup. Pulley 2 is not clamped. The scale reads 0 N.",
                Pulleys = new[] { Pulley(40f, 0f), Pulley(100f, 0f, clamped: false), Pulley(235f, 0f) }, Scale = TestScaleMode.Slack,
                ExpectedFailedChecks = new[] { "pulley-2-clamped" },
                KnownFix = new[] { "Clamp pulley 2 onto a table spot." }
            }
        };

        /// <summary>Judges one setup with the real Lab5Judge and keeps every check it made.</summary>
        public static AssistantTestRun Judge(AssistantTestConfig config)
        {
            Lab5Step[] steps = Lab5Procedure.BuildSteps(Lab5Procedure.CaseCountFor(PulleyCount));
            int stepIndex = FindStepIndex(steps, config);
            Lab5Step step = steps[stepIndex];
            LabReading reading = ReadingOf(config);

            var checks = new StepCheckSet();
            new Lab5Judge(new Lab5Tolerances(), PulleyIds).Evaluate(step, reading, checks);

            var results = new CheckResult[checks.Count];
            for (int i = 0; i < checks.Count; i++) results[i] = checks[i];
            return new AssistantTestRun
            {
                Step = step, StepIndex = stepIndex, StepCount = steps.Length,
                Reading = reading, Results = results, ResultCount = checks.Count
            };
        }

        /// <summary>The ids of the checks that failed, in the judge's order.</summary>
        public static string[] FailedIds(AssistantTestRun run)
        {
            var ids = new List<string>();
            for (int i = 0; i < run.ResultCount; i++)
            {
                if (!run.Results[i].Passed) ids.Add(run.Results[i].Id);
            }
            return ids.ToArray();
        }

        /// <summary>The payload the lab would send for this setup and hint level, as an object ready for JsonUtility.</summary>
        public static LabStatePayload BuildPayload(AssistantTestConfig config, AssistantTestRun run, int hintLevel, int requestId)
        {
            bool done = FailedIds(run).Length == 0;
            var payload = new LabStatePayload
            {
                session_id = "test-set",
                request_id = requestId,
                condition = WireNames.Of(ExperimentCondition.CurrentState),
                question_type = WireNames.Of(QuestionType.WhatNext),
                question = WireNames.QuestionText(QuestionType.WhatNext),
                current_step = run.Step.Id,
                step_stage = Lab5Procedure.StageName(run.Step.Stage),
                case_id = run.Step.CaseId,
                step_completed = done,
                step_index = run.StepIndex,
                step_count = run.StepCount,
                step_elapsed_s = StepSeconds,
                failed_checks = CheckPayloadBuilder.FailedChecks(run.Results, run.ResultCount, done),
                passed_checks = CheckPayloadBuilder.PassedIds(run.Results, run.ResultCount),
                check_diagnosis = CheckPayloadBuilder.Diagnosis(run.Results, run.ResultCount, done),
                check_targets = CheckPayloadBuilder.Targets(run.Results, run.ResultCount, done),
                recent_events = new EventRecord[0],
                ring_position = new Vector3(0f, 0f, 0f),
                ring = RingOf(config),
                pulleys = PulleysOf(run.Reading),
                masses = MassesOf(run.Reading),
                spring_scale = ScaleOf(config, run.Reading),
                strings = StringsOf(run.Reading),
                hint_policy = WireNames.Of(HintPolicyFor(hintLevel))
            };
            HintLadder.Limit(payload, hintLevel);
            return payload;
        }

        // ---------- the reading ----------

        private static int FindStepIndex(Lab5Step[] steps, AssistantTestConfig config)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].CaseIndex == config.CaseIndex && steps[i].Stage == config.Stage) return i;
            }
            throw new InvalidOperationException($"No step for test setup {config.Id}.");
        }

        private static LabReading ReadingOf(AssistantTestConfig config)
        {
            var reading = new LabReading
            {
                PulleyCount = PulleyCount,
                StringsConnected = true,
                ScaleAttached = true,
                ScaleStringLengthM = ScaleStringMetres,
                RingOffsetM = config.RingOffsetM,
                RingSpreadM = config.RingSpreadM,
                WindowFull = true,
                MeasurementReady = config.MeasurementReady
            };
            for (int i = 0; i < PulleyCount; i++) reading.Pulleys[i] = config.Pulleys[i];

            reading.ScaleReadingN = ScaleReadingOf(config);
            reading.ScaleAngleDeg = ScaleAngleOf(config);
            return reading;
        }

        private static float ScaleReadingOf(AssistantTestConfig config)
        {
            switch (config.Scale)
            {
                case TestScaleMode.Slack: return 0f;
                case TestScaleMode.Weak: return WeakPullN;
                default: return Mathf.Round(PullOfMasses(config, out _) * 100f) / 100f;
            }
        }

        // A slack or weak scale lies on the side opposite the first load, which is where it would rest.
        private static float ScaleAngleOf(AssistantTestConfig config)
        {
            if (config.Scale == TestScaleMode.Balanced)
            {
                PullOfMasses(config, out float angle);
                return Mathf.Round(angle * 10f) / 10f;
            }
            return Mathf.Repeat(config.Pulleys[0].AngleDeg + 180f, 360f);
        }

        // At equilibrium the scale balances the sum of the pulls of the masses. It pulls the other way.
        private static float PullOfMasses(AssistantTestConfig config, out float scaleAngleDeg)
        {
            float x = 0f;
            float z = 0f;
            foreach (PulleyReading pulley in config.Pulleys)
            {
                float pull = pulley.MassKg * GravityMetresPerSecondSquared;
                x += pull * Mathf.Cos(pulley.AngleDeg * Mathf.Deg2Rad);
                z += pull * Mathf.Sin(pulley.AngleDeg * Mathf.Deg2Rad);
            }
            scaleAngleDeg = Mathf.Repeat(Mathf.Atan2(z, x) * Mathf.Rad2Deg + 180f, 360f);
            return Mathf.Sqrt(x * x + z * z);
        }

        // ---------- the objects of the payload ----------

        private static HintPolicy HintPolicyFor(int level) =>
            level == HintLadder.TableLevel ? HintPolicy.FixedTable
            : level == HintLadder.StepLevel ? HintPolicy.FixedStep
            : HintPolicy.FixedAnswer;

        private static RingState RingOf(AssistantTestConfig config) => new RingState
        {
            id = "ring",
            object_position = new Vector3(config.RingOffsetM, 0f, 0f),
            status = LabStatusReader.Free,
            attached_to = new[] { "string-pulley1", "string-pulley2", "string-pulley3", "string-scale" }
        };

        private static PulleyState[] PulleysOf(LabReading reading)
        {
            var states = new PulleyState[PulleyCount];
            for (int i = 0; i < PulleyCount; i++)
            {
                PulleyReading pulley = reading.Pulleys[i];
                states[i] = new PulleyState
                {
                    id = PulleyIds[i],
                    object_position = OnRim(pulley.AngleDeg, 0f),
                    angle_deg = pulley.AngleDeg,
                    status = pulley.Clamped ? LabStatusReader.Clamped : LabStatusReader.Loose,
                    attached_to = pulley.Clamped ? new[] { "table", StringId(i) } : new[] { StringId(i) }
                };
            }
            return states;
        }

        private static MassState[] MassesOf(LabReading reading)
        {
            var states = new MassState[PulleyCount];
            for (int i = 0; i < PulleyCount; i++)
            {
                PulleyReading pulley = reading.Pulleys[i];
                states[i] = new MassState
                {
                    id = "mass-" + (i + 1),
                    object_position = OnRim(pulley.AngleDeg, -HangDropMetres),
                    mass_value = pulley.MassKg,
                    status = pulley.MassKg > 0f ? LabStatusReader.Hanging : LabStatusReader.Slack,
                    attached_to = new[] { StringId(i) }
                };
            }
            return states;
        }

        private static ScaleState ScaleOf(AssistantTestConfig config, LabReading reading)
        {
            bool taut = config.Scale != TestScaleMode.Slack;
            return new ScaleState
            {
                id = "spring-scale",
                object_position = OnRim(reading.ScaleAngleDeg, 0f),
                reading_n = reading.ScaleReadingN,
                angle_deg = reading.ScaleAngleDeg,
                status = taut ? LabStatusReader.Connected : LabStatusReader.Slack,
                attached_to = new[] { "string-scale" },
                attached = true,
                connection = taut ? LabStatusReader.Taut : LabStatusReader.Slack
            };
        }

        private static StringState[] StringsOf(LabReading reading)
        {
            var states = new StringState[PulleyCount + 1];
            for (int i = 0; i < PulleyCount; i++)
            {
                float pull = reading.Pulleys[i].MassKg * GravityMetresPerSecondSquared;
                states[i] = new StringState
                {
                    id = StringId(i), from = "ring", via = PulleyIds[i], to = "mass-" + (i + 1),
                    taut = pull > 0f, tension_n = Mathf.Round(pull * 100f) / 100f
                };
            }
            states[PulleyCount] = new StringState
            {
                id = "string-scale", from = "ring", via = string.Empty, to = "spring-scale",
                taut = reading.ScaleReadingN > 0f, tension_n = reading.ScaleReadingN
            };
            return states;
        }

        // The lab names a string after its arm: "Arm_Pulley1" becomes "string-pulley1".
        private static string StringId(int pulleyIndex) => "string-pulley" + (pulleyIndex + 1);

        // A point at a given angle on the rim of the table, in table space (x, y, z) in metres.
        private static Vector3 OnRim(float angleDeg, float height) => new Vector3(
            Mathf.Round(TableRadiusMetres * Mathf.Cos(angleDeg * Mathf.Deg2Rad) * 1000f) / 1000f,
            height,
            Mathf.Round(TableRadiusMetres * Mathf.Sin(angleDeg * Mathf.Deg2Rad) * 1000f) / 1000f);
    }
}
#endif
