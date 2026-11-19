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

    /// <summary>One check: what it measured and the limit it was compared with.</summary>
    public struct CheckResult
    {
        public string Id { get; set; }
        public bool Passed { get; set; }
        public float Actual { get; set; }
        public float Limit { get; set; }
        public string Unit { get; set; }
    }

    /// <summary>
    /// The checks of one tick. A fixed array that is cleared and refilled, so it makes no garbage.
    /// The Add methods are named after the unit, so each call has four arguments or fewer.
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

        /// <summary>A yes/no check. Actual 1 means yes, and the limit is 1.</summary>
        public void AddFlag(string id, bool value) =>
            Store(new CheckResult { Id = id, Passed = value, Actual = value ? 1f : 0f, Limit = 1f, Unit = "yes/no" });

        public void AddDegrees(string id, bool passed, float actual, float limit) =>
            Store(new CheckResult { Id = id, Passed = passed, Actual = actual, Limit = limit, Unit = "deg" });

        public void AddKilograms(string id, bool passed, float actual, float limit) =>
            Store(new CheckResult { Id = id, Passed = passed, Actual = actual, Limit = limit, Unit = "kg" });

        public void AddNewtons(string id, bool passed, float actual, float limit) =>
            Store(new CheckResult { Id = id, Passed = passed, Actual = actual, Limit = limit, Unit = "N" });

        public void AddMillimetres(string id, bool passed, float actual, float limit) =>
            Store(new CheckResult { Id = id, Passed = passed, Actual = actual, Limit = limit, Unit = "mm" });

        public void AddRows(string id, bool passed, float actual, float limit) =>
            Store(new CheckResult { Id = id, Passed = passed, Actual = actual, Limit = limit, Unit = "rows" });

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