namespace OrcFarm.Core
{
    /// <summary>
    /// Immutable per-frame snapshot of which farm actions are available for a focused tile.
    /// Returned by <see cref="IFarmActionTarget.GetActionContext"/> every frame — must be a
    /// readonly struct so <see cref="FarmFocusDetector"/> reads it with zero heap allocation (§3.1).
    /// </summary>
    public readonly struct FarmActionContext
    {
        /// <summary>True when the player can apply Feed (F key). Requires correct item in hotbar.</summary>
        public bool ShowFeed  { get; }

        /// <summary>True when the player can apply Water (W key). Requires correct item in hotbar.</summary>
        public bool ShowWater { get; }

        /// <summary>True when the player can apply Care (C key). No item required.</summary>
        public bool ShowCare  { get; }

        /// <summary>True when at least one action is currently available.</summary>
        public bool HasAny => ShowFeed || ShowWater || ShowCare;

        /// <summary>Empty context — all actions hidden. Returned when no tile is focused.</summary>
        public static readonly FarmActionContext None = default;

        /// <param name="showFeed">Show the Feed action button.</param>
        /// <param name="showWater">Show the Water action button.</param>
        /// <param name="showCare">Show the Care action button.</param>
        public FarmActionContext(bool showFeed, bool showWater, bool showCare)
        {
            ShowFeed  = showFeed;
            ShowWater = showWater;
            ShowCare  = showCare;
        }
    }
}
