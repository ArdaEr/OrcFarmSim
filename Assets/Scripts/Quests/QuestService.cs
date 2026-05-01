using System;
using System.Collections.Generic;

namespace OrcFarm.Quests
{
    /// <summary>
    /// In-memory quest runtime service for one play session.
    /// </summary>
    public sealed class QuestService : IQuestService
    {
        private readonly Dictionary<string, QuestDefinition> _definitions =
            new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<string, QuestState> _states =
            new Dictionary<string, QuestState>(StringComparer.Ordinal);

        /// <summary>
        /// Creates an empty quest service.
        /// </summary>
        public QuestService()
        {
        }

        /// <summary>
        /// Creates a quest service and registers the supplied definitions.
        /// </summary>
        public QuestService(IEnumerable<QuestDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            foreach (QuestDefinition definition in definitions)
            {
                RegisterQuestDefinition(definition);
            }
        }

        /// <inheritdoc/>
        public void RegisterQuestDefinition(QuestDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.Validate();

            if (_definitions.ContainsKey(definition.QuestId))
            {
                throw new InvalidOperationException(
                    $"Duplicate quest id '{definition.QuestId}' registered.");
            }

            _definitions.Add(definition.QuestId, definition);
        }

        /// <inheritdoc/>
        public bool TryStartQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return false;
            }

            if (_states.TryGetValue(questId, out QuestState existingState))
            {
                if (existingState.Status == QuestStatus.Active ||
                    existingState.Status == QuestStatus.ReadyToComplete ||
                    existingState.Status == QuestStatus.Failed)
                {
                    return false;
                }

                if (existingState.Status == QuestStatus.Completed && !definition.CanRepeat)
                {
                    return false;
                }
            }

            _states[questId] = definition.CreateState();
            return true;
        }

        /// <inheritdoc/>
        public bool TryRecordProgress(string progressKey, int amount)
        {
            if (string.IsNullOrWhiteSpace(progressKey) || amount <= 0)
            {
                return false;
            }

            bool changed = false;

            foreach (KeyValuePair<string, QuestState> pair in _states)
            {
                QuestState state = pair.Value;
                if (state.Status != QuestStatus.Active)
                {
                    continue;
                }

                QuestDefinition definition = _definitions[pair.Key];
                QuestObjectiveState[] objectiveStates = state.ObjectiveStates;
                IReadOnlyList<QuestObjectiveDefinition> objectiveDefinitions =
                    definition.Objectives;

                for (int i = 0; i < objectiveStates.Length; i++)
                {
                    if (objectiveStates[i].IsComplete)
                    {
                        continue;
                    }

                    if (!objectiveDefinitions[i].MatchesProgress(progressKey))
                    {
                        continue;
                    }

                    if (objectiveStates[i].AddProgress(amount))
                    {
                        changed = true;
                    }
                }

                if (state.AreAllObjectivesComplete)
                {
                    MoveCompletedState(definition, state);
                }
            }

            return changed;
        }

        /// <inheritdoc/>
        public bool TryCompleteQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            if (!_definitions.ContainsKey(questId))
            {
                return false;
            }

            if (!_states.TryGetValue(questId, out QuestState state))
            {
                return false;
            }

            if (state.Status == QuestStatus.Completed || state.Status == QuestStatus.Failed)
            {
                return false;
            }

            if (!state.AreAllObjectivesComplete)
            {
                return false;
            }

            state.Status = QuestStatus.Completed;
            return true;
        }

        /// <inheritdoc/>
        public QuestStatus GetQuestStatus(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return QuestStatus.Unavailable;
            }

            if (_states.TryGetValue(questId, out QuestState state))
            {
                return state.Status;
            }

            return _definitions.ContainsKey(questId)
                ? QuestStatus.Available
                : QuestStatus.Unavailable;
        }

        /// <inheritdoc/>
        public bool TryGetQuestSnapshot(string questId, out QuestSnapshot snapshot)
        {
            snapshot = default;

            if (string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return false;
            }

            if (!_states.TryGetValue(questId, out QuestState state))
            {
                return false;
            }

            snapshot = state.CreateSnapshot(definition);
            return true;
        }

        /// <inheritdoc/>
        public IReadOnlyList<QuestSnapshot> GetActiveQuestSnapshots()
        {
            List<QuestSnapshot> snapshots = new List<QuestSnapshot>();

            foreach (KeyValuePair<string, QuestState> pair in _states)
            {
                QuestState state = pair.Value;
                if (state.Status != QuestStatus.Active &&
                    state.Status != QuestStatus.ReadyToComplete)
                {
                    continue;
                }

                snapshots.Add(state.CreateSnapshot(_definitions[pair.Key]));
            }

            return snapshots;
        }

        /// <inheritdoc/>
        public IReadOnlyList<QuestSnapshot> GetAllQuestSnapshots()
        {
            List<QuestSnapshot> snapshots = new List<QuestSnapshot>(_states.Count);

            foreach (KeyValuePair<string, QuestState> pair in _states)
            {
                snapshots.Add(pair.Value.CreateSnapshot(_definitions[pair.Key]));
            }

            return snapshots;
        }

        private static void MoveCompletedState(QuestDefinition definition, QuestState state)
        {
            state.Status = definition.CompletionMode == QuestCompletionMode.AutoComplete
                ? QuestStatus.Completed
                : QuestStatus.ReadyToComplete;
        }
    }
}
