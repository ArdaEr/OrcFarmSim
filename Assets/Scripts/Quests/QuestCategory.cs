namespace OrcFarm.Quests
{
    /// <summary>
    /// High-level quest category used by UI filters and quest giver selection.
    /// </summary>
    public enum QuestCategory
    {
        /// <summary>A bazaar-style job with a bronze payout.</summary>
        Contract = 0,

        /// <summary>A lord request that can award prestige or unlocks later.</summary>
        Lord = 1,

        /// <summary>A short tutorial objective that teaches one interaction.</summary>
        Tutorial = 2,
    }
}
