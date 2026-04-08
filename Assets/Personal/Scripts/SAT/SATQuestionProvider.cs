using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

namespace SAT
{
    public class SATQuestionProvider : MonoBehaviour
    {
        [SerializeField, Title("SAT Question Data Files")]
        private TextAsset easyQuestionsJson;

        [SerializeField, Title("SAT Question Data Files")]
        private TextAsset mediumQuestionsJson;

        [SerializeField, Title("SAT Question Data Files")]
        private TextAsset hardQuestionsJson;

        [SerializeField, Title("SAT Question Data Files")]
        private TextAsset expertQuestionsJson;

        [SerializeField, Title("Provider Settings")]
        private bool loadOnAwake = true;

        private readonly Dictionary<SATQuestionDifficulty, DifficultyCache> _difficultyCaches =
            new Dictionary<SATQuestionDifficulty, DifficultyCache>();

        private bool _isLoaded;

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadQuestions();
            }
        }

        public bool LoadQuestions()
        {
            // Build references only; question files are lazily loaded when queried.
            ClearIndexes();
            InitializeDifficultyCaches();

            int configuredFiles = 0;
            configuredFiles += AssignDifficultyAsset(SATQuestionDifficulty.Easy, easyQuestionsJson) ? 1 : 0;
            configuredFiles += AssignDifficultyAsset(SATQuestionDifficulty.Medium, mediumQuestionsJson) ? 1 : 0;
            configuredFiles += AssignDifficultyAsset(SATQuestionDifficulty.Hard, hardQuestionsJson) ? 1 : 0;
            configuredFiles += AssignDifficultyAsset(SATQuestionDifficulty.Expert, expertQuestionsJson) ? 1 : 0;

            if (configuredFiles == 0)
            {
                print("[Error] SATQuestionProvider: No difficulty question files are assigned.");
                _isLoaded = false;
                ClearIndexes();
                return false;
            }

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

            // Difficulty lookups load and index exactly one file when a filter is specified.
            if (difficultyFilter.HasValue)
            {
                if (!TryEnsureDifficultyLoaded(difficultyFilter.Value, out DifficultyCache cache))
                {
                    return new List<SATQuestionEntry>();
                }

                if (!cache.QuestionsByAnswerWord.TryGetValue(key, out List<SATQuestionEntry> difficultyMatches))
                {
                    return new List<SATQuestionEntry>();
                }

                return new List<SATQuestionEntry>(difficultyMatches);
            }

            // Unfiltered lookups aggregate from whichever difficulty files are assigned.
            List<SATQuestionEntry> allMatches = new List<SATQuestionEntry>();
            foreach (SATQuestionDifficulty difficulty in Enum.GetValues(typeof(SATQuestionDifficulty)))
            {
                if (!TryEnsureDifficultyLoaded(difficulty, out DifficultyCache cache))
                {
                    continue;
                }

                if (cache.QuestionsByAnswerWord.TryGetValue(key, out List<SATQuestionEntry> matches))
                {
                    allMatches.AddRange(matches);
                }
            }

            return allMatches;
        }

        public List<SATQuestionEntry> GetQuestionsByDifficulty(SATQuestionDifficulty difficulty)
        {
            if (!EnsureLoaded())
            {
                return new List<SATQuestionEntry>();
            }

            if (!TryEnsureDifficultyLoaded(difficulty, out DifficultyCache cache))
            {
                return new List<SATQuestionEntry>();
            }

            return new List<SATQuestionEntry>(cache.Questions);
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

        private bool TryEnsureDifficultyLoaded(SATQuestionDifficulty difficulty, out DifficultyCache cache)
        {
            if (!_difficultyCaches.TryGetValue(difficulty, out cache))
            {
                return false;
            }

            if (cache.IsLoaded)
            {
                return true;
            }

            if (cache.SourceAsset == null || string.IsNullOrWhiteSpace(cache.SourceAsset.text))
            {
                return false;
            }

            SATQuestionDataset parsedDataset = JsonUtility.FromJson<SATQuestionDataset>(cache.SourceAsset.text);
            if (parsedDataset == null || parsedDataset.questions == null)
            {
                return false;
            }

            BuildCacheIndexes(cache, parsedDataset.questions, difficulty);
            cache.IsLoaded = true;
            return true;
        }

        private void BuildCacheIndexes(DifficultyCache cache, SATQuestionEntry[] questions, SATQuestionDifficulty expectedDifficulty)
        {
            cache.Questions.Clear();
            cache.QuestionsByAnswerWord.Clear();

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

                if (parsedDifficulty != expectedDifficulty)
                {
                    continue;
                }

                string key = question.answerWord.Trim();
                if (!cache.QuestionsByAnswerWord.TryGetValue(key, out List<SATQuestionEntry> list))
                {
                    list = new List<SATQuestionEntry>();
                    cache.QuestionsByAnswerWord.Add(key, list);
                }

                list.Add(question);
                cache.Questions.Add(question);
            }
        }

        private void InitializeDifficultyCaches()
        {
            foreach (SATQuestionDifficulty difficulty in Enum.GetValues(typeof(SATQuestionDifficulty)))
            {
                _difficultyCaches[difficulty] = new DifficultyCache
                {
                    Questions = new List<SATQuestionEntry>(),
                    QuestionsByAnswerWord = new Dictionary<string, List<SATQuestionEntry>>(StringComparer.OrdinalIgnoreCase)
                };
            }
        }

        private void ClearIndexes()
        {
            _difficultyCaches.Clear();
        }

        private bool AssignDifficultyAsset(SATQuestionDifficulty difficulty, TextAsset source)
        {
            if (!_difficultyCaches.TryGetValue(difficulty, out DifficultyCache cache))
            {
                return false;
            }

            cache.SourceAsset = source;
            cache.IsLoaded = false;
            return source != null;
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

        private sealed class DifficultyCache
        {
            public TextAsset SourceAsset;
            public bool IsLoaded;
            public List<SATQuestionEntry> Questions;
            public Dictionary<string, List<SATQuestionEntry>> QuestionsByAnswerWord;
        }
    }
}
