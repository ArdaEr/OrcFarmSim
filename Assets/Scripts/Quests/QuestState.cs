using System.Collections.Generic;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Runtime progress state for one quest.
    /// </summary>
    public sealed class QuestState
    {
        private readonly QuestObjectiveState[] _objectives;

        /// <summary>Stable quest id.</summary>
        public string QuestId { get; }

        /// <summary>Current runtime status.</summary>
        public QuestStatus Status { get; internal set; }

        /// <summary>Read-only runtime objective states.</summary>
        public IReadOnlyList<QuestObjectiveState> Objectives => _objectives;

        internal QuestObjectiveState[] ObjectiveStates => _objectives;

        internal QuestState(string questId, QuestObjectiveState[] objectives)
        {
            QuestId = questId;
            _objectives = objectives;
            Status = QuestStatus.Active;
        }

        internal bool AreAllObjectivesComplete
        {
            get
            {
                for (int i = 0; i < _objectives.Length; i++)
                {
                    if (!_objectives[i].IsComplete)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        internal QuestSnapshot CreateSnapshot(QuestDefinition definition)
        {
            QuestObjectiveSnapshot[] objectiveSnapshots =
                new QuestObjectiveSnapshot[_objectives.Length];

            for (int i = 0; i < _objectives.Length; i++)
            {
                objectiveSnapshots[i] = _objectives[i].CreateSnapshot();
            }

            return new QuestSnapshot(
                QuestId,
                definition.DisplayName,
                definition.Description,
                definition.Category,
                Status,
                objectiveSnapshots);
        }
    }
}
