using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>
    /// The surface that state classes can call on their owning plot.
    /// States receive this interface — never the concrete <see cref="FarmPlot"/> type (§1.9).
    /// </summary>
    public interface IFarmPlotStateContext
    {
        /// <summary>Crop timing configuration.</summary>
        HeadSeedConfig Config { get; }

        /// <summary>Seconds elapsed since planting began.</summary>
        float GrowthTimer { get; }

        /// <summary>Seconds elapsed since the NeedsCare state was entered.</summary>
        float CareWindowTimer { get; }

        /// <summary>True once the player has cared for the crop this growth cycle.</summary>
        bool CareGiven { get; }

        /// <summary>Requests a transition to <paramref name="next"/>.</summary>
        void TransitionTo(PlotState next);

        /// <summary>Advances the growth timer by <paramref name="delta"/> seconds.</summary>
        void IncrementGrowthTimer(float delta);

        /// <summary>Advances the care-window timer by <paramref name="delta"/> seconds.</summary>
        void IncrementCareWindowTimer(float delta);

        /// <summary>Resets growth timer, care-window timer, and CareGiven to their initial values.</summary>
        void ResetGrowthTracking();

        /// <summary>Marks that the player has cared for the crop this cycle.</summary>
        void SetCareGiven();

        /// <summary>
        /// Tries to remove one item of <paramref name="type"/> from the player's inventory.
        /// Returns false if the inventory is unassigned or lacks sufficient quantity.
        /// </summary>
        bool TryConsumeItem(ItemType type);

        /// <summary>Instantiates a harvested head and hands it to the carry system.</summary>
        void SpawnHarvestedHead();
    }
}
