namespace OrcFarm.Quests
{
    /// <summary>
    /// Published when gameplay performs an action that may advance quest objectives.
    /// </summary>
    public readonly struct QuestObjectiveActionSignal
    {
        /// <summary>Stable key identifying the gameplay action that happened.</summary>
        public readonly string ActionKey;

        /// <summary>Positive amount of action progress to apply.</summary>
        public readonly int Amount;

        /// <summary>
        /// Creates a quest objective action notification.
        /// </summary>
        public QuestObjectiveActionSignal(string actionKey, int amount)
        {
            ActionKey = actionKey;
            Amount = amount;
        }
    }
}
