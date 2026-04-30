namespace OrcFarm.Farming
{
    /// <summary>
    /// Per-fish runtime state for a single fish inside a <see cref="LegPond"/>.
    /// Populated when the pond is stocked; modified during Growing and NeedsCare.
    /// </summary>
    internal sealed class LegFishData
    {
        internal float FeedScore        = 1f;
        internal float CareScore        = 1f;
        internal bool  IsAlive          = true;

        // Trait influence tracking — accumulated by LegPond.DecayFishScores each frame.
        internal float TimeLowFeed       = 0f;
        internal float TimeLowCare       = 0f;
        internal bool  FeedEverBelowHalf = false;
    }
}
