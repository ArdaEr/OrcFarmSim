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

        /// <summary>The <see cref="LegFryItem"/> the player is currently carrying, or null.</summary>
        LegFryItem CarriedLegFry { get; }

        /// <summary>
        /// Tries to stock the pond using LegFry from the player's selected hotbar slot.
        /// Consumes min(slotCount, PondCapacity) items and initializes that many fish.
        /// Returns true if stocking succeeded.
        /// </summary>
        bool TryStockFromHotbar();

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
        /// Deactivates the carried <see cref="LegFryItem"/> and clears the carry slot.
        /// No-op if nothing is being carried.
        /// </summary>
        void ConsumeCarriedLegFry();

        /// <summary>
        /// Clears the existing fish list and populates it with <paramref name="count"/> new
        /// <see cref="LegFishData"/> instances. Sets the pond's base quality from
        /// <paramref name="tier"/>. Pass the result of <see cref="GetCappedFishCount"/>
        /// for the carry-stocking path so pond capacity is respected.
        /// </summary>
        void InitializeFish(LegFryTier tier, int count);

        /// <summary>
        /// Returns the fish count for <paramref name="tier"/> from LegFryData, capped at
        /// <see cref="LegPondConfig.PondCapacity"/>.
        /// </summary>
        int GetCappedFishCount(LegFryTier tier);

        /// <summary>
        /// Spawns a HarvestedLeg at a random offset near the pond and immediately places
        /// it in the player's carry slot via <see cref="OrcFarm.Carry.ICarryController.PickUpLeg"/>.
        /// </summary>
        void SpawnAndCarryLeg();

        /// <summary>
        /// Decays FeedScore and CareScore on all alive fish by the given per-frame delta amounts.
        /// Fish whose FeedScore reaches zero are marked dead (IsAlive = false).
        /// Returns true if every fish in the pond is dead after decay.
        /// </summary>
        bool DecayFishScores(float feedDecay, float careDecay);

        /// <summary>
        /// Count of alive fish that have not yet been harvested.
        /// Relevant during ReadyToHarvest; returns 0 when all fish are dead or after
        /// the last harvest before the pond transitions to Empty.
        /// </summary>
        int AliveRemainingFishCount { get; }

        /// <summary>
        /// Harvests the next alive fish: calculates per-fish quality from condition scores,
        /// spawns a HarvestedLeg at a random offset, immediately carries it, and shows
        /// the harvest readout. Does nothing if no alive fish remain or the pool is exhausted.
        /// </summary>
        void HarvestNextLeg();
    }
}
