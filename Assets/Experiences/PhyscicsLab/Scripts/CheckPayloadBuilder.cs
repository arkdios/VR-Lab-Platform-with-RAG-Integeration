namespace PhysicsLab.Lab
{
    /// <summary>
    /// Turns the check results of one step into the lists that go into the payload.
    /// It works on plain data, so it needs no scene. LabStepTracker and the test set builder both use it,
    /// so a test payload is made the same way as a live one.
    /// A step that is done reports no failed checks, even when a check of an earlier moment failed
    /// (the equilibrium step stays done once a measurement was held).
    /// </summary>
    public static class CheckPayloadBuilder
    {
        /// <summary>One entry for each failed check, as failed_checks holds them. Empty when the step is done.</summary>
        public static StepCheckState[] FailedChecks(CheckResult[] results, int count, bool stepDone)
        {
            if (stepDone) return new StepCheckState[0];

            var list = new StepCheckState[CountWhere(results, count, passed: false)];
            int next = 0;
            for (int i = 0; i < count; i++)
            {
                if (results[i].Passed) continue;

                list[next++] = new StepCheckState
                {
                    id = results[i].Id,
                    actual = results[i].Actual,
                    limit = results[i].Limit,
                    unit = results[i].Unit
                };
            }
            return list;
        }

        /// <summary>The ids of the checks that pass.</summary>
        public static string[] PassedIds(CheckResult[] results, int count)
        {
            var ids = new string[CountWhere(results, count, passed: true)];
            int next = 0;
            for (int i = 0; i < count; i++)
            {
                if (results[i].Passed) ids[next++] = results[i].Id;
            }
            return ids;
        }

        /// <summary>One entry for each failed check: the id, the object and what is wrong. No numbers.</summary>
        public static CheckDiagnosisState[] Diagnosis(CheckResult[] results, int count, bool stepDone)
        {
            if (stepDone) return new CheckDiagnosisState[0];

            var list = new CheckDiagnosisState[CountWhere(results, count, passed: false)];
            int next = 0;
            for (int i = 0; i < count; i++)
            {
                if (results[i].Passed) continue;

                list[next++] = new CheckDiagnosisState
                {
                    id = results[i].Id,
                    object_id = results[i].Subject ?? string.Empty,
                    diagnosis = results[i].Diagnosis ?? string.Empty
                };
            }
            return list;
        }

        /// <summary>One entry for each failed check that has a case value: the value and the signed error.</summary>
        public static CheckTargetState[] Targets(CheckResult[] results, int count, bool stepDone)
        {
            if (stepDone) return new CheckTargetState[0];

            int wanted = 0;
            for (int i = 0; i < count; i++)
            {
                if (!results[i].Passed && results[i].HasTarget) wanted++;
            }

            var list = new CheckTargetState[wanted];
            int next = 0;
            for (int i = 0; i < count; i++)
            {
                if (results[i].Passed || !results[i].HasTarget) continue;

                list[next++] = new CheckTargetState
                {
                    id = results[i].Id,
                    case_value = results[i].Target,
                    error = results[i].Error,
                    unit = results[i].Unit
                };
            }
            return list;
        }

        private static int CountWhere(CheckResult[] results, int count, bool passed)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (results[i].Passed == passed) total++;
            }
            return total;
        }
    }
}
