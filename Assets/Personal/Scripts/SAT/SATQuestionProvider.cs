using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

namespace SAT
{
    public class SATQuestionProvider : MonoBehaviour
    {
        [SerializeField, Title("SAT Question Data")]
        private TextAsset questionsJson;

        [SerializeField, Title("Provider Settings")]
        private bool loadOnAwake = true;

        private readonly Dictionary<string, List<SATQuestionEntry>> _questionsByAnswerWord =
            new Dictionary<string, List<SATQuestionEntry>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<SATQuestionDifficulty, List<SATQuestionEntry>> _questionsByDifficulty =
            new Dictionary<SATQuestionDifficulty, List<SATQuestionEntry>>();

        private readonly Dictionary<SATQuestionDifficulty, Dictionary<string, List<SATQuestionEntry>>> _questionsByDifficultyAndAnswerWord =
            new Dictionary<SATQuestionDifficulty, Dictionary<string, List<SATQuestionEntry>>>();

        private bool _isLoaded;

        public SATQuestionDataset LoadedDataset { get; private set; }

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadQuestions();
            }
        }

        public bool LoadQuestions()
        {
            if (questionsJson == null || string.IsNullOrWhiteSpace(questionsJson.text))
            {
                Debug.LogError("SATQuestionProvider: Questions JSON is not assigned or is empty.");
                _isLoaded = false;
                LoadedDataset = null;
                ClearIndexes();
                return false;
            }

            // Deserialize the generated dataset and build a fast lookup cache by answer word.
            SATQuestionDataset parsedDataset = JsonUtility.FromJson<SATQuestionDataset>(questionsJson.text);
            if (parsedDataset == null)
            {
                Debug.LogError("SATQuestionProvider: Failed to parse SAT questions JSON.");
                _isLoaded = false;
                LoadedDataset = null;
                ClearIndexes();
                return false;
            }

            LoadedDataset = parsedDataset;
            BuildAnswerWordIndex(parsedDataset.questions);
            _isLoaded = true;
            return true;
        }

        public List<SATQuestionEntry> GetQuestionsByAnswerWord(string answerWord, SATQuestionDifficulty? difficultyFilter = null)
        {
            if (!EnsureLoaded())
            {
                return new List<SATQuestionEntry>();
            }

            if (string.IsNullOrWhiteSpace(answerWord))
            {
                return new List<SATQuestionEntry>();
            }

            string key = answerWord.Trim();

            // Difficulty filtering uses pre-built indexes so lookups stay fast even with very large datasets.
            if (difficultyFilter.HasValue)
            {
                if (!_questionsByDifficultyAndAnswerWord.TryGetValue(difficultyFilter.Value, out Dictionary<string, List<SATQuestionEntry>> byWordForDifficulty))
                {
                    return new List<SATQuestionEntry>();
                }

                if (!byWordForDifficulty.TryGetValue(key, out List<SATQuestionEntry> difficultyMatches))
                {
                    return new List<SATQuestionEntry>();
                }

                return new List<SATQuestionEntry>(difficultyMatches);
            }

            if (!_questionsByAnswerWord.TryGetValue(key, out List<SATQuestionEntry> allMatches))
            {
                return new List<SATQuestionEntry>();
            }

            return new List<SATQuestionEntry>(allMatches);
        }

        public List<SATQuestionEntry> GetQuestionsByDifficulty(SATQuestionDifficulty difficulty)
        {
            if (!EnsureLoaded())
            {
                return new List<SATQuestionEntry>();
            }

            if (!_questionsByDifficulty.TryGetValue(difficulty, out List<SATQuestionEntry> matches))
            {
                return new List<SATQuestionEntry>();
            }

            return new List<SATQuestionEntry>(matches);
        }

        public List<SATQuestionEntry> GetQuestionsForLearnedWords(IEnumerable<string> learnedWords, SATQuestionDifficulty? difficultyFilter = null)
        {
            if (!EnsureLoaded())
            {
                return new List<SATQuestionEntry>();
            }

            if (learnedWords == null)
            {
                return new List<SATQuestionEntry>();
            }

            // Use a stable de-duplication key so repeated learned words never duplicate returned questions.
            HashSet<string> seenQuestionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<SATQuestionEntry> results = new List<SATQuestionEntry>();

            foreach (string learnedWord in learnedWords)
            {
                List<SATQuestionEntry> matches = GetQuestionsByAnswerWord(learnedWord, difficultyFilter);
                for (int i = 0; i < matches.Count; i++)
                {
                    SATQuestionEntry question = matches[i];
                    string dedupeKey = BuildQuestionDedupeKey(question);
                    if (seenQuestionIds.Add(dedupeKey))
                    {
                        results.Add(question);
                    }
                }
            }

            return results;
        }

        private bool EnsureLoaded()
        {
            return _isLoaded || LoadQuestions();
        }

        private void BuildAnswerWordIndex(SATQuestionEntry[] questions)
        {
            ClearIndexes();
            InitializeDifficultyIndexes();

            if (questions == null)
            {
                return;
            }

            for (int i = 0; i < questions.Length; i++)
            {
                SATQuestionEntry question = questions[i];
                if (question == null || string.IsNullOrWhiteSpace(question.answerWord))
                {
                    continue;
                }

                if (question.options == null || question.options.Length != 4)
                {
                    continue;
                }

                if (!question.TryGetDifficulty(out SATQuestionDifficulty parsedDifficulty))
                {
                    continue;
                }

                string key = question.answerWord.Trim();
                if (!_questionsByAnswerWord.TryGetValue(key, out List<SATQuestionEntry> list))
                {
                    list = new List<SATQuestionEntry>();
                    _questionsByAnswerWord.Add(key, list);
                }

                list.Add(question);

                _questionsByDifficulty[parsedDifficulty].Add(question);

                Dictionary<string, List<SATQuestionEntry>> byWordForDifficulty = _questionsByDifficultyAndAnswerWord[parsedDifficulty];
                if (!byWordForDifficulty.TryGetValue(key, out List<SATQuestionEntry> difficultyList))
                {
                    difficultyList = new List<SATQuestionEntry>();
                    byWordForDifficulty.Add(key, difficultyList);
                }

                difficultyList.Add(question);
            }
        }

        private void InitializeDifficultyIndexes()
        {
            foreach (SATQuestionDifficulty difficulty in Enum.GetValues(typeof(SATQuestionDifficulty)))
            {
                _questionsByDifficulty[difficulty] = new List<SATQuestionEntry>();
                _questionsByDifficultyAndAnswerWord[difficulty] = new Dictionary<string, List<SATQuestionEntry>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void ClearIndexes()
        {
            _questionsByAnswerWord.Clear();
            _questionsByDifficulty.Clear();
            _questionsByDifficultyAndAnswerWord.Clear();
        }

        private static string BuildQuestionDedupeKey(SATQuestionEntry question)
        {
            if (question == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(question.id))
            {
                return question.id.Trim();
            }

            return string.Format("{0}|{1}", question.answerWord ?? string.Empty, question.prompt ?? string.Empty);
        }
    }
}
