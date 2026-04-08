namespace OrcFarm.Farming
{
    /// <summary>All lifecycle states a <see cref="FarmPlot"/> can occupy.</summary>
    public enum PlotState
    {
        /// <summary>Soil is bare. Plot accepts a Prepare interaction.</summary>
        Empty,

        /// <summary>Soil has been tilled. Awaiting fertilizer.</summary>
        Prepared,

        /// <summary>Fertilizer applied. Awaiting a seed.</summary>
        Fertilized,

        /// <summary>
        /// Seed just placed. Transitions automatically to <see cref="Growing"/>
        /// on the next Update tick — no player interaction required.
        /// </summary>
        Planted,

        /// <summary>Growth timer is running. Plot is not interactable.</summary>
        Growing,

        /// <summary>
        /// Care checkpoint reached. Player must interact before
        /// <see cref="HeadSeedConfig.CareWindowDuration"/> expires or the crop fails.
        /// </summary>
        NeedsCare,

        /// <summary>Growth completed successfully. Awaiting harvest.</summary>
        ReadyToHarvest,

        /// <summary>Care window was missed. Plot must be cleared before replanting.</summary>
        FailedCrop,
    }
}
