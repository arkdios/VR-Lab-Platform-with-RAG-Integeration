using System;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Decides which checks a step needs and whether they pass. It works on plain numbers (LabReading),
    /// so it never touches the scene and can be tested without Unity.
    /// Every step shows the same kind of result: a list of checks, each with its measured value and its limit.
    /// </summary>
    public sealed class Lab5Judge
    {
        private const float MinStringLengthM = 0.05f;
        private const float RadiansToDegrees = 57.29578f;
        private const float AngleCostWeight = 0.01f / 180f;   // a tie-breaker, much smaller than one failed check
        private const float MassCostWeight = 0.01f / 0.5f;

        private readonly Lab5Tolerances _tolerance;
        private readonly string[] _clampedIds;
        private readonly string[] _angleIds;
        private readonly string[] _massIds;
        private readonly int[] _bestLoadOfPulley;
        private readonly int[] _trialLoadOfPulley;
        private readonly bool[] _loadTaken;
        private readonly StepCheckSet _scratch = new StepCheckSet();
        private float _bestCost;

        /// <param name="tolerances">The limits to use.</param>
        /// <param name="pulleyIds">The ids of the pulleys, in a fixed order, for example pulley-1, pulley-2.</param>
        public Lab5Judge(Lab5Tolerances tolerances, string[] pulleyIds)
        {
            if (pulleyIds.Length > Lab5Procedure.MaxPulleys)
            {
                throw new ArgumentException($"At most {Lab5Procedure.MaxPulleys} pulleys are supported.", nameof(pulleyIds));
            }

            _tolerance = tolerances;
            _clampedIds = SuffixAll(pulleyIds, "-clamped");
            _angleIds = SuffixAll(pulleyIds, "-angle");
            _massIds = SuffixAll(pulleyIds, "-mass");
            _bestLoadOfPulley = new int[pulleyIds.Length];
            _trialLoadOfPulley = new int[pulleyIds.Length];
            _loadTaken = new bool[pulleyIds.Length];
        }

        /// <summary>
        /// Fills the check list for a step. Later stages repeat the checks of the earlier ones,
        /// so a student who undoes earlier work sees it fail again.
        /// </summary>
        public void Evaluate(Lab5Step step, LabReading reading, StepCheckSet checks)
        {
            checks.Clear();
            switch (step.Stage)
            {
                case Lab5Stage.Setup:
                    AddSetupChecks(reading, checks);
                    break;
                case Lab5Stage.ConfigureMasses:
                    AddConfigChecks(step.CaseIndex, reading, checks);
                    break;
                case Lab5Stage.ConnectScale:
                    AddConfigChecks(step.CaseIndex, reading, checks);
                    AddScaleChecks(reading, checks);
                    break;
                case Lab5Stage.Equilibrium:
                    AddEquilibriumChecks(step.CaseIndex, reading, checks);
                    break;
                case Lab5Stage.Record:
                    AddRecordChecks(step.CaseIndex, reading, checks);
                    break;
            }
        }

        /// <summary>True when every pulley and mass matches the case.</summary>
        public bool ConfigPasses(int caseIndex, LabReading reading)
        {
            _scratch.Clear();
            AddConfigChecks(caseIndex, reading, _scratch);
            return _scratch.FailedCount == 0;
        }

        /// <summary>True when the scale pulls and the ring sits on the post and is steady.</summary>
        public bool EquilibriumHolds(LabReading reading)
        {
            _scratch.Clear();
            AddScaleChecks(reading, _scratch);
            AddRingChecks(reading, _scratch);
            return _scratch.FailedCount == 0;
        }

        /// <summary>
        /// The numbers to keep at equilibrium. The force error is the scale step or half of how much the reading
        /// moved, whichever is bigger. The angle error joins the reading step with the angle the ring offset
        /// can hide (slide 21: a ring off the post means a larger uncertainty).
        /// </summary>
        public StepMeasurement MeasureEquilibrium(LabReading reading, string caseId)
        {
            float forceError = MathF.Max(_tolerance.scaleResolutionN, 0.5f * reading.ScaleSpreadN);
            float stringLength = MathF.Max(reading.ScaleStringLengthM, MinStringLengthM);
            float offsetAngle = MathF.Atan2(reading.RingOffsetM, stringLength) * RadiansToDegrees;
            float angleError = MathF.Sqrt(_tolerance.angleResolutionDeg * _tolerance.angleResolutionDeg
                                          + offsetAngle * offsetAngle);

            return new StepMeasurement
            {
                IsValid = true,
                CaseId = caseId,
                ForceN = Round(reading.ScaleReadingN),
                ForceErrorN = Round(forceError),
                AngleDeg = Round(reading.ScaleAngleDeg),
                AngleErrorDeg = Round(angleError),
                RingOffsetMm = Round(reading.RingOffsetM * 1000f)
            };
        }

        // ---------- stage checks ----------

        private void AddSetupChecks(LabReading reading, StepCheckSet checks)
        {
            checks.AddFlag("strings-connected", reading.StringsConnected);
            for (int pulley = 0; pulley < reading.PulleyCount; pulley++)
            {
                checks.AddFlag(_clampedIds[pulley], reading.Pulleys[pulley].Clamped);
            }
            bool zero = MathF.Abs(reading.ScaleReadingN) <= _tolerance.scaleZeroN;
            checks.AddNewtons("scale-zero", zero, Round(reading.ScaleReadingN), _tolerance.scaleZeroN);
        }

        private void AddScaleChecks(LabReading reading, StepCheckSet checks)
        {
            checks.AddFlag("scale-attached", reading.ScaleAttached);
            bool pulling = reading.ScaleAttached && reading.ScaleReadingN >= _tolerance.scalePullingN;
            checks.AddNewtons("scale-pulling", pulling, Round(reading.ScaleReadingN), _tolerance.scalePullingN);
        }

        private void AddRingChecks(LabReading reading, StepCheckSet checks)
        {
            bool centred = reading.RingOffsetM <= _tolerance.ringCentredMetres;
            checks.AddMillimetres("ring-centred", centred, Round(reading.RingOffsetM * 1000f),
                Round(_tolerance.ringCentredMetres * 1000f));

            bool steady = reading.WindowFull && reading.RingSpreadM <= _tolerance.ringStableMetres;
            checks.AddMillimetres("ring-stable", steady, Round(reading.RingSpreadM * 1000f),
                Round(_tolerance.ringStableMetres * 1000f));
        }

        private void AddEquilibriumChecks(int caseIndex, LabReading reading, StepCheckSet checks)
        {
            AddConfigChecks(caseIndex, reading, checks);
            AddScaleChecks(reading, checks);
            AddRingChecks(reading, checks);
        }

        // Once a measurement is saved, nothing else is asked. Until then the student sees what blocks a good one.
        private void AddRecordChecks(int caseIndex, LabReading reading, StepCheckSet checks)
        {
            bool saved = reading.ValidRecordsForCase > 0;
            checks.AddRows("measurement-recorded", saved, reading.ValidRecordsForCase, 1f);
            if (saved || reading.MeasurementReady) return;

            AddEquilibriumChecks(caseIndex, reading, checks);
        }

        // ---------- pulleys and masses ----------

        private void AddConfigChecks(int caseIndex, LabReading reading, StepCheckSet checks)
        {
            Lab5Case lab5Case = Lab5Procedure.CaseAt(caseIndex);
            AssignLoadsToPulleys(lab5Case, reading);
            for (int pulley = 0; pulley < reading.PulleyCount; pulley++)
            {
                CaseLoad load = lab5Case.LoadAt(_bestLoadOfPulley[pulley]);
                AddPulleyChecks(pulley, load, reading.Pulleys[pulley], checks);
            }
        }

        private void AddPulleyChecks(int pulley, CaseLoad load, PulleyReading actual, StepCheckSet checks)
        {
            if (!load.IsEmpty)
            {
                checks.AddFlag(_clampedIds[pulley], actual.Clamped);
                bool angleOk = AngleDistance(actual.AngleDeg, load.AngleDeg) <= _tolerance.pulleyAngleDeg;
                checks.AddDegrees(_angleIds[pulley], angleOk, Round(actual.AngleDeg), _tolerance.pulleyAngleDeg);
            }
            bool massOk = MathF.Abs(actual.MassKg - load.MassKg) <= _tolerance.massKg;
            checks.AddKilograms(_massIds[pulley], massOk, Round(actual.MassKg), _tolerance.massKg);
        }

        // The pulleys are the same, so the student may put the 80 degree mass on either one.
        // This tries every way to hand the loads out and keeps the one with the fewest mistakes.
        private void AssignLoadsToPulleys(Lab5Case lab5Case, LabReading reading)
        {
            _bestCost = float.MaxValue;
            Array.Clear(_loadTaken, 0, _loadTaken.Length);
            SearchAssignments(lab5Case, reading, 0, 0f);
        }

        private void SearchAssignments(Lab5Case lab5Case, LabReading reading, int pulley, float costSoFar)
        {
            if (costSoFar >= _bestCost) return;

            if (pulley == reading.PulleyCount)
            {
                _bestCost = costSoFar;
                Array.Copy(_trialLoadOfPulley, _bestLoadOfPulley, reading.PulleyCount);
                return;
            }

            for (int load = 0; load < reading.PulleyCount; load++)
            {
                if (_loadTaken[load]) continue;

                _loadTaken[load] = true;
                _trialLoadOfPulley[pulley] = load;
                float cost = costSoFar + PairCost(lab5Case.LoadAt(load), reading.Pulleys[pulley]);
                SearchAssignments(lab5Case, reading, pulley + 1, cost);
                _loadTaken[load] = false;
            }
        }

        // One point for each failed check, plus a tiny part for how far off it is, so equal cases pick the closer pair.
        private float PairCost(CaseLoad load, PulleyReading actual)
        {
            float massError = MathF.Abs(actual.MassKg - load.MassKg);
            float cost = massError > _tolerance.massKg ? 1f : 0f;
            cost += massError * MassCostWeight;
            if (load.IsEmpty) return cost;

            float angleError = AngleDistance(actual.AngleDeg, load.AngleDeg);
            if (!actual.Clamped) cost += 1f;
            if (angleError > _tolerance.pulleyAngleDeg) cost += 1f;
            return cost + angleError * AngleCostWeight;
        }

        // ---------- small helpers ----------

        // The smallest turn between two angles, so 359 and 1 are 2 degrees apart.
        private static float AngleDistance(float a, float b)
        {
            float distance = MathF.Abs(a - b) % 360f;
            return distance > 180f ? 360f - distance : distance;
        }

        // Three decimals keep the JSON short and free of float noise like 0.0299999993.
        private static float Round(float value) => MathF.Round(value * 1000f) / 1000f;

        private static string[] SuffixAll(string[] ids, string suffix)
        {
            var result = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++) result[i] = ids[i] + suffix;
            return result;
        }
    }
}