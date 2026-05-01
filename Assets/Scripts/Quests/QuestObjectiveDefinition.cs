using System;
using UnityEngine;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Base ScriptableObject definition for a count-based quest objective.
    /// </summary>
    public abstract class QuestObjectiveDefinition : ScriptableObject
    {
        private const int MinTargetCount = 1;

        [Tooltip("Stable id unique within the containing quest.")]
        [SerializeField] private string _objectiveId = "objective.id";

        [Tooltip("Short objective text shown to the player.")]
        [SerializeField] private string _displayName = "Objective";

        [Tooltip("Required count before the objective is complete.")]
        [Min(MinTargetCount)]
        [SerializeField] private int _targetCount = MinTargetCount;

        /// <summary>Stable id unique within the containing quest.</summary>
        public string ObjectiveId => _objectiveId;

        /// <summary>Short objective text shown to the player.</summary>
        public string DisplayName => _displayName;

        /// <summary>Required count before the objective is complete.</summary>
        public int TargetCount => _targetCount;

        /// <summary>
        /// Returns true when a progress key should advance this objective.
        /// </summary>
        public abstract bool MatchesProgress(string progressKey);

        /// <summary>
        /// Throws when the objective data is not safe to use at runtime.
        /// </summary>
        public virtual void Validate()
        {
            if (string.IsNullOrWhiteSpace(_objectiveId))
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name} '{name}'] ObjectiveId must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name} '{name}'] DisplayName must not be empty.");
            }

            if (_targetCount < MinTargetCount)
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name} '{name}'] TargetCount must be at least {MinTargetCount}.");
            }
        }

        internal QuestObjectiveState CreateState()
        {
            Validate();
            return new QuestObjectiveState(_objectiveId, _displayName, _targetCount);
        }

        /// <summary>
        /// Clamps serialized objective data to safe editor values.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (_displayName == null)
            {
                _displayName = string.Empty;
            }

            _targetCount = Mathf.Max(MinTargetCount, _targetCount);
        }
    }
}
