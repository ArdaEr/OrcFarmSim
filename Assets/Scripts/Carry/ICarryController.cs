using UnityEngine;

namespace OrcFarm.Carry
{
    /// <summary>
    /// Contract for the system that manages carrying exactly one world item at a time.
    /// The item is either a <see cref="HarvestedHead"/> or a <see cref="HarvestedLeg"/>;
    /// both use the same anchor and drop physics.
    /// </summary>
    public interface ICarryController
    {
        /// <summary>True while any item (head or leg) is attached to the carry anchor.</summary>
        bool IsCarrying { get; }

        /// <summary>True while a <see cref="HarvestedLeg"/> specifically is being carried.</summary>
        bool IsCarryingLeg { get; }

        /// <summary>
        /// Picks up <paramref name="head"/>, attaching it to the carry anchor.
        /// If anything is already being carried it is physically dropped first.
        /// </summary>
        void PickUp(HarvestedHead head);

        /// <summary>
        /// Picks up <paramref name="leg"/>, attaching it to the carry anchor.
        /// If anything is already being carried it is physically dropped first.
        /// </summary>
        void PickUpLeg(HarvestedLeg leg);

        /// <summary>
        /// Transfers the currently carried head into storage by parenting it under
        /// <paramref name="storageRoot"/>. Returns true if a head was transferred.
        /// Returns false if carrying a leg (legs have no storage yet).
        /// </summary>
        bool TryStore(Transform storageRoot);

        /// <summary>
        /// Detaches the carried item, re-enables physics, and launches it slightly in front
        /// of the player with a small random horizontal impulse. The item remains a live
        /// world object — it is NOT returned to any pool.
        ///
        /// Use this for the player Q-key drop and for automatic drop-on-pickup.
        /// </summary>
        void PhysicalDrop();

        /// <summary>
        /// Immediately returns the carried head to the object pool, deactivating it.
        /// If a leg is being carried, falls back to <see cref="PhysicalDrop"/> (no leg pool exists yet).
        /// No physics drop or visual fall occurs for heads.
        ///
        /// Use this only for assembly-station consumption and hauler delivery —
        /// never for the player's manual drop.
        /// </summary>
        void SilentReturn();
    }
}
