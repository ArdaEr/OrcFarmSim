namespace OrcFarm.Quests
{
    /// <summary>
    /// Published when gameplay has made progress that quest objectives may consume.
    /// </summary>
    public readonly struct QuestProgressSignal
    {
        /// <summary>Stable key identifying the progressed activity.</summary>
        public readonly string ProgressKey;

        /// <summary>Positive amount of progress to apply.</summary>
        public readonly int Amount;

        /// <summary>
        /// Creates a progress notification for one quest objective key.
        /// </summary>
        public QuestProgressSignal(string progressKey, int amount)
        {
            ProgressKey = progressKey;
            Amount = amount;
        }
    }
}
