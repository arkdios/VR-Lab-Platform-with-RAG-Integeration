using System;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The limits the rules use. They show in the Inspector of PrototypeStepRules, so they can be tuned
    /// during testing without touching code. Metres, degrees, newtons and kilograms.
    /// </summary>
    [Serializable]
    public sealed class Lab5Tolerances
    {
        // A pulley may be this far from the case angle. Clamp spots are 5 degrees apart, so the next spot is always outside.
        public float pulleyAngleDeg = 2f;
        public float massKg = 0.005f;

        // The scale must read at most this much with no load (setup).
        public float scaleZeroN = 0.05f;

        // The scale must read at least this much to count as pulling.
        public float scalePullingN = 0.10f;

        // The ring is on the post when it is at most this far from the table centre.
        public float ringCentredMetres = 0.003f;

        // The ring is steady when it moved at most this much during the last stableSeconds.
        public float ringStableMetres = 0.002f;
        public float stableSeconds = 1f;

        // The smallest step the student can read. The table is marked every 0.5 degrees (slide 18).
        // The scale step is an assumption: the slides show only 1 N marks.
        public float scaleResolutionN = 0.05f;
        public float angleResolutionDeg = 0.5f;
    }

    /// <summary>One pulley as the rules see it.</summary>
    public struct PulleyReading
    {
        public float AngleDeg;
        public float MassKg;
        public bool Clamped;
    }

    /// <summary>
    /// Everything the checks need, as plain numbers. PrototypeStepRules fills one of these every tick
    /// and reuses it, so no garbage is made.
    /// </summary>
    public sealed class LabReading
    {
        public readonly PulleyReading[] Pulleys = new PulleyReading[Lab5Procedure.MaxPulleys];
        public int PulleyCount;

        public bool StringsConnected;
        public bool ScaleAttached;
        public float ScaleReadingN;
        public float ScaleAngleDeg;        // counter-clockwise, like the pulley angles
        public float ScaleStringLengthM;

        public float RingOffsetM;          // distance of the ring from the post
        public float RingSpreadM;          // how far the ring moved during the window
        public float ScaleSpreadN;         // biggest minus smallest scale reading during the window
        public bool WindowFull;            // the window holds a full stableSeconds of samples

        public int ValidRecordsForCase;    // saved measurements of this case that were taken at equilibrium
        public bool MeasurementReady;      // an equilibrium measurement is held for this case
    }

    /// <summary>
    /// The numbers taken at the moment of equilibrium, with the uncertainty the student would choose.
    /// A default value means there is no measurement. It is a plain data holder, so it is filled
    /// with an object initializer instead of a constructor with six arguments.
    /// </summary>
    public struct StepMeasurement
    {
        public bool IsValid { get; set; }
        public string CaseId { get; set; }
        public float ForceN { get; set; }
        public float ForceErrorN { get; set; }
        public float AngleDeg { get; set; }
        public float AngleErrorDeg { get; set; }
        public float RingOffsetMm { get; set; }

        public static StepMeasurement None => default;
    }

    /// <summary>
    /// One check: what it measured and the limit it was compared with.
    /// The simulation also knows, for certain, which object the check is about and what is wrong with it.
    /// Subject, Diagnosis and the target fields carry that, so the assistant explains the problem and does not guess it.
    /// </summary>
    public struct CheckResult
    {
        public string Id { get; set; }
        public bool Passed { get; set; }
        public float Actual { get; set; }
        public float Limit { get; set; }
        public string Unit { get; set; }

        /// <summary>The lab object the check is about, for example pulley-2, spring-scale, ring. Empty when not set.</summary>
        public string Subject { get; set; }

        /// <summary>What is wrong, as a code, for example angle-too-large. Empty when the check passes.</summary>
        public string Diagnosis { get; set; }

        /// <summary>True when the case asks for one number (an angle, a mass, a force).</summary>
        public bool HasTarget { get; set; }

        /// <summary>The number the case asks for, in the unit of the check.</summary>
        public float Target { get; set; }

        /// <summary>Actual minus Target, signed, in the unit of the check. A negative number means too small.</summary>
        public float Error { get; set; }

        /// <summary>Makes a plain check without the extra facts. WithSubject and WithTarget add them.</summary>
        public static CheckResult Make(string id, string unit, bool passed, float actual, float limit) =>
            new CheckResult { Id = id, Unit = unit, Passed = passed, Actual = actual, Limit = limit };

        /// <summary>A copy that names the object and what is wrong. The diagnosis stays empty when the check passes.</summary>
        public CheckResult WithSubject(string subject, string diagnosisWhenFailed)
        {
            CheckResult copy = this;
            copy.Subject = subject;
            copy.Diagnosis = Passed ? string.Empty : diagnosisWhenFailed;
            return copy;
        }

        /// <summary>A copy that also holds the number the case asks for and how far off the lab is.</summary>
        public CheckResult WithTarget(float target, float error)
        {
            CheckResult copy = this;
            copy.HasTarget = true;
            copy.Target = target;
            copy.Error = error;
            return copy;
        }
    }

    /// <summary>
    /// The checks of one tick. A fixed array that is cleared and refilled, so it makes no garbage.
    /// CheckResult.Make, WithSubject and WithTarget build a check, so no call needs more than five arguments.
    /// </summary>
    public sealed class StepCheckSet
    {
        public const int Capacity = 16;

        private readonly CheckResult[] _items = new CheckResult[Capacity];

        public int Count { get; private set; }
        public int FailedCount { get; private set; }

        public CheckResult this[int index] => _items[index];

        public void Clear()
        {
            Count = 0;
            FailedCount = 0;
        }

        /// <summary>Adds a finished check, for the ones that carry a subject, a diagnosis or a target.</summary>
        public void Add(CheckResult check) => Store(check);

        private void Store(CheckResult check)
        {
            if (Count == Capacity) throw new InvalidOperationException("StepCheckSet is full.");

            _items[Count++] = check;
            if (!check.Passed) FailedCount++;
        }
    }

    /// <summary>
    /// Keeps the last few samples of the ring position and the scale reading, so the rules can tell
    /// if they are steady. The newest sample replaces the oldest once the window is full.
    /// </summary>
    public sealed class ReadingWindow
    {
        private readonly float[] _ringX;
        private readonly float[] _ringZ;
        private readonly float[] _scaleN;
        private int _count;
        private int _next;

        public ReadingWindow(int capacity)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity), "A window needs 2 samples or more.");

            _ringX = new float[capacity];
            _ringZ = new float[capacity];
            _scaleN = new float[capacity];
        }

        public bool IsFull => _count == _ringX.Length;

        public void Push(float ringX, float ringZ, float scaleN)
        {
            _ringX[_next] = ringX;
            _ringZ[_next] = ringZ;
            _scaleN[_next] = scaleN;
            _next = (_next + 1) % _ringX.Length;
            if (_count < _ringX.Length) _count++;
        }

        /// <summary>The diagonal of the smallest box around the ring samples, in metres.</summary>
        public float RingSpread
        {
            get
            {
                if (_count < 2) return 0f;
                float width = Range(_ringX);
                float depth = Range(_ringZ);
                return MathF.Sqrt(width * width + depth * depth);
            }
        }

        /// <summary>The biggest scale reading minus the smallest one, in newtons.</summary>
        public float ScaleSpread => _count < 2 ? 0f : Range(_scaleN);

        // Samples fill the array from index 0, so the first _count entries are always the live ones.
        private float Range(float[] samples)
        {
            float lowest = samples[0];
            float highest = samples[0];
            for (int i = 1; i < _count; i++)
            {
                if (samples[i] < lowest) lowest = samples[i];
                if (samples[i] > highest) highest = samples[i];
            }
            return highest - lowest;
        }
    }
}