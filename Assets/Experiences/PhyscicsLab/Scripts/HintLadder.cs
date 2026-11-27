using System;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// How the hint level of the assistant is chosen.
    /// Auto goes up with each help question on the same step. The other three keep one level for every question,
    /// which is what an experiment needs when it compares the levels with each other.
    /// </summary>
    public enum HintPolicy { Auto, FixedTable, FixedStep, FixedAnswer }

    /// <summary>
    /// Tiered hints. The lab picks the level and sends it as hint_level. The backend writes the answer for that level.
    ///   Level 1 (table):  point to the lab manual table. No object is named and no number is given.
    ///   Level 2 (step):   name the step or the part that is wrong. No number is given.
    ///   Level 3 (answer): give the exact change.
    /// What the payload holds grows with the level (LabAssistant.ApplyHintLevel), and PayloadGuard checks it,
    /// so a low level cannot leak the answer even if the backend prompt is wrong.
    /// The class works on plain values, so it needs no scene.
    /// </summary>
    public sealed class HintLadder
    {
        public const int TableLevel = 1;
        public const int StepLevel = 2;
        public const int AnswerLevel = 3;

        private string _stepId = string.Empty;
        private int _helpQuestions;   // help questions asked on the current step

        /// <summary>
        /// The level for this question. In Auto, a help question raises the level by one on the same step, up to 3.
        /// A new step starts again at 1. A question that is not a help question (what is this, how does it affect the
        /// equation) is answered at the level reached so far and does not raise it.
        /// </summary>
        public int LevelFor(HintPolicy policy, string stepId, bool isHelpQuestion)
        {
            if (policy != HintPolicy.Auto) return FixedLevel(policy);

            if (stepId != _stepId)
            {
                _stepId = stepId ?? string.Empty;
                _helpQuestions = 0;
            }
            if (isHelpQuestion) _helpQuestions++;
            return Math.Max(TableLevel, Math.Min(_helpQuestions, AnswerLevel));
        }

        /// <summary>
        /// Writes hint_level and removes what this level may not hold: no diagnosis list below level 2,
        /// no case values below level 3. LabAssistant and the test set export both call it, so a test payload
        /// has the same content as a live one.
        /// </summary>
        public static void Limit(LabStatePayload payload, int level)
        {
            payload.hint_level = level;
            if (level < StepLevel) payload.check_diagnosis = new CheckDiagnosisState[0];
            if (level < AnswerLevel) payload.check_targets = new CheckTargetState[0];
        }

        /// <summary>Forgets the questions asked so far. For example when progress is loaded.</summary>
        public void Reset()
        {
            _stepId = string.Empty;
            _helpQuestions = 0;
        }

        /// <summary>The questions that ask the lab what to do or why. These raise the level.</summary>
        public static bool IsHelpQuestion(QuestionType question) =>
            question == QuestionType.WhatNext || question == QuestionType.WhyHappening;

        private static int FixedLevel(HintPolicy policy)
        {
            switch (policy)
            {
                case HintPolicy.FixedTable: return TableLevel;
                case HintPolicy.FixedStep: return StepLevel;
                case HintPolicy.FixedAnswer: return AnswerLevel;
                default: throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
            }
        }
    }
}
