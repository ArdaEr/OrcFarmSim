namespace OrcFarm.Quests
{
    /// <summary>
    /// Immutable read model for one runtime objective.
    /// </summary>
    public readonly struct QuestObjectiveSnapshot
    {
        /// <summary>Stable objective id unique within its quest.</summary>
        public string ObjectiveId { get; }

        /// <summary>Text label suitable for HUD display.</summary>
        public string DisplayName { get; }

        /// <summary>Current completed count.</summary>
        public int CurrentCount { get; }

        /// <summary>Required count.</summary>
        public int TargetCount { get; }

        /// <summary>True when <see cref="CurrentCount"/> has reached <see cref="TargetCount"/>.</summary>
        public bool IsComplete { get; }

        /// <summary>
        /// Creates an immutable objective read model.
        /// </summary>
        public QuestObjectiveSnapshot(
            string objectiveId,
            string displayName,
            int currentCount,
            int targetCount,
            bool isComplete)
        {
            ObjectiveId = objectiveId;
            DisplayName = displayName;
            CurrentCount = currentCount;
            TargetCount = targetCount;
            IsComplete = isComplete;
        }
    }
}
