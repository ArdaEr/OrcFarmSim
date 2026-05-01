namespace OrcFarm.Quests
{
    /// <summary>
    /// Runtime lifecycle state for a quest.
    /// </summary>
    public enum QuestStatus
    {
        /// <summary>The quest is unknown or currently gated off.</summary>
        Unavailable = 0,

        /// <summary>The quest is registered and can be started.</summary>
        Available = 1,

        /// <summary>The quest is active and accepting objective progress.</summary>
        Active = 2,

        /// <summary>All objectives are complete and the quest is waiting for turn-in.</summary>
        ReadyToComplete = 3,

        /// <summary>The quest has been completed.</summary>
        Completed = 4,

        /// <summary>The quest can no longer be completed in the current run.</summary>
        Failed = 5,
    }
}
