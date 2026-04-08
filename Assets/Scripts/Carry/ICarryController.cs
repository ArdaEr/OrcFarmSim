using UnityEngine;

namespace OrcFarm.Carry
{
    /// <summary>
    /// Contract for the system that manages carrying exactly one
    /// <see cref="HarvestedHead"/> at a time.
    /// </summary>
    public interface ICarryController
    {
        /// <summary>True while a <see cref="HarvestedHead"/> is attached to the carry anchor.</summary>
        bool IsCarrying { get; }

        /// <summary>
        /// Picks up <paramref name="head"/>, attaching it to the carry anchor.
        /// If a head is already being carried it is dropped first.
        /// </summary>
        void PickUp(HarvestedHead head);

        /// <summary>
        /// Transfers the currently carried head into storage by parenting it under
        /// <paramref name="storageRoot"/>. Returns true if a head was transferred.
        /// </summary>
        bool TryStore(Transform storageRoot);

        /// <summary>
        /// Drops the currently carried head in front of the player with a small impulse.
        /// No-op if nothing is being carried.
        /// </summary>
        void Drop();
    }
}
