using System;

namespace SAT
{
    [Serializable]
    public class SATQuestionDataset
    {
        public int version;
        public int seed;
        public string generatedAtUtc;
        public string sourceVocabularyFile;
        public int totalQuestions;
        public SATQuestionEntry[] questions;
    }

    [Serializable]
    public class SATQuestionEntry
    {
        public string id;
        public string answerWord;
        public int answerIndex;
        public string grouping;
        public string difficulty;
        public string prompt;
        public string[] options;
        public int correctOptionIndex;
        public string correctOptionLabel;
        public string sourceDefinition;

        public bool TryGetDifficulty(out SATQuestionDifficulty parsedDifficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty))
            {
                parsedDifficulty = SATQuestionDifficulty.Easy;
                return false;
            }

            return Enum.TryParse(difficulty, true, out parsedDifficulty);
        }
    }
}
