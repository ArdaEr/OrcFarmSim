using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Published by a <see cref="FarmPlot"/> when a crop completes harvesting.
    /// <see cref="HarvestCoordinator"/> subscribes and handles head instantiation and pickup.
    ///
    /// Naming convention: past-tense notification (§2.6).
    /// Immutable readonly struct — zero allocation, thread-safe (§2.2).
    /// </summary>
    public readonly struct CropHarvestedSignal
    {
        /// <summary>World-space position where the harvested head should appear.</summary>
        public readonly Vector3 SpawnPosition;

        /// <summary/>
        public CropHarvestedSignal(Vector3 spawnPosition) => SpawnPosition = spawnPosition;
    }
}
