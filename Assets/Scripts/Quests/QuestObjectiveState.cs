using System;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Runtime progress state for one quest objective.
    /// </summary>
    public sealed class QuestObjectiveState
    {
        /// <summary>Stable objective id unique within its quest.</summary>
        public string ObjectiveId { get; }

        /// <summary>Text label suitable for HUD display.</summary>
        public string DisplayName { get; }

        /// <summary>Current completed count.</summary>
        public int CurrentCount { get; private set; }

        /// <summary>Required count.</summary>
        public int TargetCount { get; }

        /// <summary>True when progress has reached the target count.</summary>
        public bool IsComplete => CurrentCount >= TargetCount;

        internal QuestObjectiveState(string objectiveId, string displayName, int targetCount)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                throw new ArgumentException("Objective id must not be empty.", nameof(objectiveId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name must not be empty.", nameof(displayName));
            }

            if (targetCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetCount), "Target count must be positive.");
            }

            ObjectiveId = objectiveId;
            DisplayName = displayName;
            TargetCount = targetCount;
        }

        internal bool AddProgress(int amount)
        {
            if (amount <= 0 || IsComplete)
            {
                return false;
            }

            int nextCount = CurrentCount + amount;
            CurrentCount = nextCount > TargetCount ? TargetCount : nextCount;
            return true;
        }

        internal QuestObjectiveSnapshot CreateSnapshot()
        {
            return new QuestObjectiveSnapshot(
                ObjectiveId,
                DisplayName,
                CurrentCount,
                TargetCount,
                IsComplete);
        }
    }
}
