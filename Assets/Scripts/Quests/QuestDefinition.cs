using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrcFarm.Quests
{
    /// <summary>
    /// ScriptableObject definition for one quest.
    /// </summary>
    [CreateAssetMenu(menuName = "OrcFarm/Quests/Quest", fileName = "QuestDefinition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        private const int MinObjectiveCount = 1;

        [Tooltip("Stable id for save data, contracts, and quest givers.")]
        [SerializeField] private string _questId = "quest.id";

        [Tooltip("Short quest name shown to the player.")]
        [SerializeField] private string _displayName = "Quest";

        [Tooltip("Longer description for quest boards or journals.")]
        [TextArea(2, 4)]
        [SerializeField] private string _description = string.Empty;

        [Tooltip("High-level quest category.")]
        [SerializeField] private QuestCategory _category = QuestCategory.Contract;

        [Tooltip("How the quest completes after all objectives finish.")]
        [SerializeField] private QuestCompletionMode _completionMode =
            QuestCompletionMode.AutoComplete;

        [Tooltip("Allows this quest to be started again after completion.")]
        [SerializeField] private bool _canRepeat;

        [Tooltip("Objectives that must all complete before the quest completes.")]
        [SerializeField] private QuestObjectiveDefinition[] _objectives =
            Array.Empty<QuestObjectiveDefinition>();

        /// <summary>Stable id for save data, contracts, and quest givers.</summary>
        public string QuestId => _questId;

        /// <summary>Short quest name shown to the player.</summary>
        public string DisplayName => _displayName;

        /// <summary>Longer description for quest boards or journals.</summary>
        public string Description => _description;

        /// <summary>High-level quest category.</summary>
        public QuestCategory Category => _category;

        /// <summary>How the quest completes after all objectives finish.</summary>
        public QuestCompletionMode CompletionMode => _completionMode;

        /// <summary>True when the quest may be restarted after completion.</summary>
        public bool CanRepeat => _canRepeat;

        /// <summary>Objectives that must all complete before the quest completes.</summary>
        public IReadOnlyList<QuestObjectiveDefinition> Objectives => _objectives;

        /// <summary>
        /// Throws when the quest data is not safe to use at runtime.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(_questId))
            {
                throw new InvalidOperationException(
                    $"[{nameof(QuestDefinition)} '{name}'] QuestId must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                throw new InvalidOperationException(
                    $"[{nameof(QuestDefinition)} '{name}'] DisplayName must not be empty.");
            }

            if (_objectives == null || _objectives.Length < MinObjectiveCount)
            {
                throw new InvalidOperationException(
                    $"[{nameof(QuestDefinition)} '{name}'] At least one objective is required.");
            }

            HashSet<string> objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _objectives.Length; i++)
            {
                QuestObjectiveDefinition objective = _objectives[i];
                if (objective == null)
                {
                    throw new InvalidOperationException(
                        $"[{nameof(QuestDefinition)} '{name}'] Objective at index {i} is missing.");
                }

                objective.Validate();

                if (!objectiveIds.Add(objective.ObjectiveId))
                {
                    throw new InvalidOperationException(
                        $"[{nameof(QuestDefinition)} '{name}'] Duplicate objective id '{objective.ObjectiveId}'.");
                }
            }
        }

        internal QuestState CreateState()
        {
            Validate();

            QuestObjectiveState[] objectiveStates =
                new QuestObjectiveState[_objectives.Length];

            for (int i = 0; i < _objectives.Length; i++)
            {
                objectiveStates[i] = _objectives[i].CreateState();
            }

            return new QuestState(_questId, objectiveStates);
        }

        private void OnValidate()
        {
            if (_displayName == null)
            {
                _displayName = string.Empty;
            }

            if (_description == null)
            {
                _description = string.Empty;
            }

            if (_objectives == null)
            {
                _objectives = Array.Empty<QuestObjectiveDefinition>();
            }
        }
    }
}
