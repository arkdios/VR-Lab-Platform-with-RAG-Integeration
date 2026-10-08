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