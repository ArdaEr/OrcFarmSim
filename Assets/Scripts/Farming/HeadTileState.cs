namespace OrcFarm.Farming
{
    /// <summary>All lifecycle states a <see cref="HeadFarmTile"/> can occupy.</summary>
    public enum HeadTileState
    {
        /// <summary>Soil is bare. Accepts a Till interaction.</summary>
        Empty,

        /// <summary>Soil has been tilled. Awaiting a HeadSeed.</summary>
        Tilled,

        /// <summary>Seed is placed. Awaiting a Cover interaction.</summary>
        Seeded,

        /// <summary>Soil is covered. Auto-transitions to Growing after the cover delay.</summary>
        Covered,

        /// <summary>Growth timer is running. Not interactable.</summary>
        Growing,

        /// <summary>Growth completed. Awaiting a Harvest interaction.</summary>
        ReadyToHarvest,

        /// <summary>
        /// Crop died during growth. Requires a Clear interaction before replanting.
        /// Reachable from Growing only (via debug hook or condition tracking in a future task).
        /// </summary>
        Dead,
    }
}
