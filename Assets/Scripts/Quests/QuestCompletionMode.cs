namespace OrcFarm.Quests
{
    /// <summary>
    /// Determines what happens when all objectives for a quest are complete.
    /// </summary>
    public enum QuestCompletionMode
    {
        /// <summary>The quest completes immediately when all objectives are complete.</summary>
        AutoComplete = 0,

        /// <summary>The quest waits for an explicit completion call after objectives finish.</summary>
        ManualTurnIn = 1,
    }
}
