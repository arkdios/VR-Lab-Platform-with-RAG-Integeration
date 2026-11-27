using System;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Tracks which lab step the student is on, whether it is done, and which checks still fail.
    /// The step list is fixed in code (Lab5Procedure), so the scene cannot hold an old list.
    /// The backend receives the current and previous step ids, never the upcoming ones.
    /// PrototypeStepRules decides if a step is done and tells this class through Publish.
    /// </summary>
    public class LabStepTracker : MonoBehaviour
    {
        // The rig has two pulleys, so the list starts with Cases A to C. PrototypeStepRules.Configure
        // replaces it when the scene has more pulleys (Case D needs three).
        private const int DefaultPulleyCount = 2;

        private Lab5Step[] _steps = Lab5Procedure.BuildSteps(Lab5Procedure.CaseCountFor(DefaultPulleyCount));
        private readonly StepCheckState[] _failed = new StepCheckState[StepCheckSet.Capacity];
        private int _failedCount;
        private readonly CheckResult[] _results = new CheckResult[StepCheckSet.Capacity];   // every check of the last tick, passed or not
        private int _resultCount;
        private bool _resultsDone;     // the step was done when these results were published
        private int _index;
        private bool _completed;       // true while the rules say the current step is done
        private float _stepStartTime;  // Time.time when the current step began

        public Lab5Step CurrentStep => _steps[_index];
        public string CurrentStepId => CurrentStep.Id;
        public string CurrentStepTitle => CurrentStep.Title;
        public string StepStage => Lab5Procedure.StageName(CurrentStep.Stage);
        public string CaseId => CurrentStep.CaseId;
        public string PreviousStepId => _index == 0 ? string.Empty : _steps[_index - 1].Id;
        public bool IsLastStep => _index == _steps.Length - 1;
        public int CurrentIndex => _index;
        public int StepCount => _steps.Length;
        public bool IsCurrentStepComplete => _completed;
        public float StepElapsedSeconds => Time.time - _stepStartTime;

        /// <summary>How many checks of the current step fail. Zero when the step is done.</summary>
        public int FailedCheckCount => _failedCount;

        /// <summary>The measurement taken at equilibrium, or a default value when there is none.</summary>
        public StepMeasurement Measurement { get; private set; }

        /// <summary>
        /// The id of the step after this one. The app may show it. It must never go into the payload:
        /// PayloadGuard refuses the key next_step.
        /// </summary>
        public string NextStepId => IsLastStep ? string.Empty : _steps[_index + 1].Id;

        /// <summary>
        /// Raised after the step changes. Arguments: previous id, current id.
        /// </summary>
        public event Action<string, string> StepChanged;

        /// <summary>
        /// Raised each time the current step turns from not done to done. Argument: the step id.
        /// </summary>
        public event Action<string> StepCompleted;

        /// <summary>
        /// Builds the step list for this many cases. Keeps the current step when it still exists.
        /// </summary>
        public void Configure(int caseCount)
        {
            _steps = Lab5Procedure.BuildSteps(caseCount);
            _index = Mathf.Min(_index, _steps.Length - 1);
        }

        /// <summary>
        /// Jumps to a saved step when progress is loaded. Raises no StepChanged, because
        /// restoring is not something the student did. Returns false for an index out of range.
        /// </summary>
        public bool TrySetIndex(int index)
        {
            if (index < 0 || index >= _steps.Length) return false;

            _index = index;
            _completed = false; // a restored step starts as not done, until the rules check it again
            _failedCount = 0;
            _resultCount = 0;
            _stepStartTime = Time.time;
            return true;
        }

        /// <summary>
        /// The rules call this every tick with the result for the current step.
        /// </summary>
        public void Publish(bool complete, StepCheckSet checks, StepMeasurement measurement)
        {
            Measurement = measurement;
            StoreFailedChecks(complete, checks);
            SetCompleted(complete);
        }

        /// <summary>
        /// The failed checks of the current step as a new array, so the caller may change it freely.
        /// Empty when the step is done.
        /// </summary>
        public StepCheckState[] CopyFailedChecks()
        {
            var copy = new StepCheckState[_failedCount];
            for (int i = 0; i < _failedCount; i++) copy[i] = CloneCheck(_failed[i]);
            return copy;
        }

        /// <summary>The ids of the checks of the current step that pass now.</summary>
        public string[] CopyPassedCheckIds() => CheckPayloadBuilder.PassedIds(_results, _resultCount);

        /// <summary>For each failed check: the object and what is wrong, as a code. Hint level 2 and 3.</summary>
        public CheckDiagnosisState[] CopyDiagnosis() =>
            CheckPayloadBuilder.Diagnosis(_results, _resultCount, _resultsDone);

        /// <summary>For each failed check with a case value: the value and the signed error. Hint level 3 only.</summary>
        public CheckTargetState[] CopyTargets() =>
            CheckPayloadBuilder.Targets(_results, _resultCount, _resultsDone);

        /// <summary>
        /// Moves to the next step without any check. For test keys and tools only.
        /// </summary>
        public bool TryAdvance()
        {
            if (IsLastStep) return false;

            _index++;
            _completed = false;
            _failedCount = 0;
            _resultCount = 0;
            _stepStartTime = Time.time;
            StepChanged?.Invoke(PreviousStepId, CurrentStepId);
            return true;
        }

        /// <summary>
        /// Moves to the next step only if the current one is done. The Next step button uses this.
        /// </summary>
        public bool TryAdvanceWhenComplete() => _completed && TryAdvance();

        private void SetCompleted(bool complete)
        {
            if (complete == _completed) return;

            _completed = complete;
            if (complete) StepCompleted?.Invoke(CurrentStepId);
        }

        // Keeps a reusable copy of each failed check. Nothing is made after the first few ticks.
        private void StoreFailedChecks(bool complete, StepCheckSet checks)
        {
            KeepResults(complete, checks);
            _failedCount = 0;
            if (complete) return;

            for (int i = 0; i < checks.Count; i++)
            {
                CheckResult check = checks[i];
                if (check.Passed) continue;

                _failed[_failedCount] ??= new StepCheckState();
                Fill(_failed[_failedCount], check);
                _failedCount++;
            }
        }

        // Keeps a copy of every check of this tick (a struct copy, no garbage), for the diagnosis lists.
        private void KeepResults(bool complete, StepCheckSet checks)
        {
            _resultCount = checks.Count;
            _resultsDone = complete;
            for (int i = 0; i < _resultCount; i++) _results[i] = checks[i];
        }

        private static void Fill(StepCheckState target, CheckResult check)
        {
            target.id = check.Id;
            target.actual = check.Actual;
            target.limit = check.Limit;
            target.unit = check.Unit;
        }

        private static StepCheckState CloneCheck(StepCheckState source) => new StepCheckState
        {
            id = source.id,
            actual = source.actual,
            limit = source.limit,
            unit = source.unit
        };
    }
}