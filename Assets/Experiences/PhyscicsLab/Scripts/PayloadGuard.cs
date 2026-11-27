namespace PhysicsLab.Lab
{
    /// <summary>
    /// Last check before a payload leaves the app. The next-step answer must never be sent,
    /// because the experiment would then test the answer, not the context. Unity's JsonUtility
    /// writes "key":value with no space, so a search for the quoted key plus a colon finds keys
    /// but not words inside the question text.
    /// </summary>
    public static class PayloadGuard
    {
        private static readonly string[] ForbiddenKeys =
        {
            "next_step", "next_action", "expected_value", "expected_answer", "answer", "solution"
        };

        /// <summary>
        /// True if the JSON holds more than its hint level allows. Level 1 may not hold check_diagnosis entries
        /// (they name the part and what is wrong). Level 1 and 2 may not hold check_targets entries (they hold the
        /// case value and the error, which is the fix). An empty list is written as [] and is allowed.
        /// </summary>
        public static bool TryFindHintLeak(string json, int hintLevel, out string foundKey)
        {
            if (hintLevel < HintLadder.StepLevel && HasEntries(json, "check_diagnosis"))
            {
                foundKey = "check_diagnosis";
                return true;
            }
            if (hintLevel < HintLadder.AnswerLevel && HasEntries(json, "check_targets"))
            {
                foundKey = "check_targets";
                return true;
            }

            foundKey = string.Empty;
            return false;
        }

        // JsonUtility writes a list with entries as "key":[{ and an empty list as "key":[].
        private static bool HasEntries(string json, string key) => json.Contains("\"" + key + "\":[{");

        /// <summary>
        /// True if the JSON contains a key that could leak the expected answer.
        /// </summary>
        public static bool TryFindForbiddenKey(string json, out string foundKey)
        {
            foreach (string key in ForbiddenKeys)
            {
                if (json.Contains("\"" + key + "\":"))
                {
                    foundKey = key;
                    return true;
                }
            }

            foundKey = string.Empty;
            return false;
        }
    }
}