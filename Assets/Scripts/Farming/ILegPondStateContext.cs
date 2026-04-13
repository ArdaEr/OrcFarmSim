using OrcFarm.Core;
using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>
    /// The surface that LegPond state classes can call on their owning pond.
    /// States receive this interface — never the concrete <see cref="LegPond"/> type (§1.9).
    /// </summary>
    public interface ILegPondStateContext
    {
        /// <summary>Pond timing and spawn configuration.</summary>
        LegPondConfig Config { get; }

        /// <summary>Current output quality. Starts Low; raised one tier per successful feed.</summary>
        OrcQuality CurrentQuality { get; }

        /// <summary>Seconds elapsed since Growing began (paused while in NeedsCare).</summary>
        float GrowthTimer { get; }

        /// <summary>Seconds elapsed since the pond was stocked.</summary>
        float StockedTimer { get; }

        /// <summary>Seconds elapsed since NeedsCare was entered.</summary>
        float StarvationTimer { get; }

        /// <summary>True once the player has fed the pond this growth cycle.</summary>
        bool CareGiven { get; }

        /// <summary>Requests a transition to <paramref name="next"/>.</summary>
        void TransitionTo(LegPondState next);

        /// <summary>Advances the growing timer by <paramref name="delta"/> seconds.</summary>
        void IncrementGrowthTimer(float delta);

        /// <summary>Advances the stocked timer by <paramref name="delta"/> seconds.</summary>
        void IncrementStockedTimer(float delta);

        /// <summary>Advances the starvation timer by <paramref name="delta"/> seconds.</summary>
        void IncrementStarvationTimer(float delta);

        /// <summary>Marks that the player has fed the pond this cycle.</summary>
        void SetCareGiven();

        /// <summary>Resets the starvation/neglect timer to zero. Called on any player interaction during NeedsCare.</summary>
        void ResetStarvationTimer();

        /// <summary>Raises output quality by one tier (Low → Normal → High). No-op at High.</summary>
        void UpgradeQuality();

        /// <summary>
        /// Tries to remove one item of <paramref name="type"/> from the player's inventory.
        /// Returns false if the inventory is unassigned or lacks the item.
        /// </summary>
        bool TryConsumeItem(ItemType type);

        /// <summary>
        /// Spawns a HarvestedLeg at a random offset near the pond and immediately places
        /// it in the player's carry slot via <see cref="OrcFarm.Carry.ICarryController.PickUpLeg"/>.
        /// </summary>
        void SpawnAndCarryLeg();
    }
}
