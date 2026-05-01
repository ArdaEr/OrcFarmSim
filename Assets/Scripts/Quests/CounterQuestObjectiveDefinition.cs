using System;
using UnityEngine;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Objective that advances when a matching progress key is reported.
    /// </summary>
    [CreateAssetMenu(
        menuName = "OrcFarm/Quests/Counter Objective",
        fileName = "CounterQuestObjective")]
    public sealed class CounterQuestObjectiveDefinition : QuestObjectiveDefinition
    {
        [Tooltip("Stable key matched against QuestProgressSignal.ProgressKey.")]
        [SerializeField] private string _progressKey = "quest.progress";

        /// <summary>Stable progress key that advances this objective.</summary>
        public string ProgressKey => _progressKey;

        /// <inheritdoc/>
        public override bool MatchesProgress(string progressKey)
        {
            return string.Equals(_progressKey, progressKey, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrWhiteSpace(_progressKey))
            {
                throw new InvalidOperationException(
                    $"[{nameof(CounterQuestObjectiveDefinition)} '{name}'] ProgressKey must not be empty.");
            }
        }

        /// <inheritdoc/>
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_progressKey == null)
            {
                _progressKey = string.Empty;
            }
        }
    }
}
