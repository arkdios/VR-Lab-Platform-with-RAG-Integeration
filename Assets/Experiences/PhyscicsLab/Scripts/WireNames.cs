using System;

namespace PhysicsLab.Lab
{
    public enum QuestionType { WhatNext, WhatIsThis, WhyHappening, HowAffectsEquation }

    public enum ExperimentCondition { QuestionOnly, CurrentState, StateAndHistory }

    /// <summary>
    /// Converts enums to the exact strings of the question text shown to the user.
    /// </summary>
    public static class WireNames
    {
        public static string Of(QuestionType type)
        {
            switch (type)
            {
                case QuestionType.WhatNext: return "what_next";
                case QuestionType.WhatIsThis: return "what_is_this";
                case QuestionType.WhyHappening: return "why_happening";
                case QuestionType.HowAffectsEquation: return "how_affects_equation";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static string Of(ExperimentCondition condition)
        {
            switch (condition)
            {
                case ExperimentCondition.QuestionOnly: return "question_only";
                case ExperimentCondition.CurrentState: return "current_state";
                case ExperimentCondition.StateAndHistory: return "state_and_history";
                default: throw new ArgumentOutOfRangeException(nameof(condition), condition, null);
            }
        }

        public static string QuestionText(QuestionType type)
        {
            switch (type)
            {
                case QuestionType.WhatNext: return "What should I do next?";
                case QuestionType.WhatIsThis: return "What is this?";
                case QuestionType.WhyHappening: return "Why is this happening?";
                case QuestionType.HowAffectsEquation: return "How does this affect the equation?";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}