using System;
using System.Collections.Generic;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Immutable read model for one runtime quest.
    /// </summary>
    public readonly struct QuestSnapshot
    {
        private readonly QuestObjectiveSnapshot[] _objectives;

        /// <summary>Stable quest id.</summary>
        public string QuestId { get; }

        /// <summary>Text label suitable for HUD display.</summary>
        public string DisplayName { get; }

        /// <summary>Description suitable for journal or contract board display.</summary>
        public string Description { get; }

        /// <summary>Quest category used by UI and quest giver filters.</summary>
        public QuestCategory Category { get; }

        /// <summary>Current runtime status.</summary>
        public QuestStatus Status { get; }

        /// <summary>Read-only objective snapshots.</summary>
        public IReadOnlyList<QuestObjectiveSnapshot> Objectives =>
            _objectives ?? Array.Empty<QuestObjectiveSnapshot>();

        /// <summary>
        /// Creates an immutable quest read model.
        /// </summary>
        public QuestSnapshot(
            string questId,
            string displayName,
            string description,
            QuestCategory category,
            QuestStatus status,
            QuestObjectiveSnapshot[] objectives)
        {
            QuestId = questId;
            DisplayName = displayName;
            Description = description;
            Category = category;
            Status = status;
            _objectives = objectives ?? Array.Empty<QuestObjectiveSnapshot>();
        }
    }
}
