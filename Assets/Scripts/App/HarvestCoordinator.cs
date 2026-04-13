using System;
using MessagePipe;
using OrcFarm.Carry;
using OrcFarm.Core;
using OrcFarm.Farming;
using UnityEngine;
using VContainer.Unity;

namespace OrcFarm.App
{
    /// <summary>
    /// Bridges the <see cref="CropHarvestedSignal"/> to the carry system (§2.1).
    ///
    /// Subscribes to <see cref="CropHarvestedSignal"/>, retrieves a pre-warmed
    /// <see cref="HarvestedHead"/> from <see cref="IHarvestedHeadPool"/> (§3.4, §3.6),
    /// and initializes it with the carry controller so the player can pick it up
    /// manually via the interaction system.
    ///
    /// The head is not auto-carried. It spawns at the plot position and lands on
    /// the ground as a world object with its collider enabled, making it visible to
    /// both the player's InteractionDetector and the hauler's HaulerWorker search.
    ///
    /// Subscriptions are disposed when the scope is torn down (§2.4).
    /// </summary>
    public sealed class HarvestCoordinator : IStartable, IDisposable
    {
        private readonly ICarryController                 _carry;
        private readonly ISubscriber<CropHarvestedSignal> _subscriber;
        private readonly IHarvestedHeadPool               _pool;
        private          IDisposable                      _subscription;

        /// <summary/>
        public HarvestCoordinator(
            ICarryController                  carry,
            ISubscriber<CropHarvestedSignal>  subscriber,
            IHarvestedHeadPool                pool)
        {
            if (carry      == null) throw new ArgumentNullException(nameof(carry));
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            if (pool       == null) throw new ArgumentNullException(nameof(pool));

            _carry      = carry;
            _subscriber = subscriber;
            _pool       = pool;
        }

        /// <summary>Wires the subscription. Called by VContainer after scope build.</summary>
        public void Start()
        {
            _subscription = _subscriber.Subscribe(OnCropHarvested);
        }

        /// <summary>Disposes the subscription when the scope tears down (§2.4).</summary>
        public void Dispose() => _subscription?.Dispose();

        private void OnCropHarvested(CropHarvestedSignal signal)
        {
            GameObject go = _pool.Get(signal.SpawnPosition, Quaternion.identity);

            if (go == null)
            {
                // Pool exhausted — harvest result is lost this cycle.
                // Increase HarvestedHeadPool._initialCapacity in the Inspector.
                LogPoolExhausted();
                return;
            }

            if (!go.TryGetComponent(out HarvestedHead head))
            {
                LogMissingComponent(go.name);
                return;
            }

            head.Initialize(_carry);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogPoolExhausted()
        {
            Debug.LogWarning("[HarvestCoordinator] Could not retrieve head from pool. " +
                             "Pool may be exhausted — increase HarvestedHeadPool._initialCapacity.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogMissingComponent(string goName)
        {
            Debug.LogError($"[HarvestCoordinator] GameObject '{goName}' returned from pool " +
                           "has no HarvestedHead component. Check the HarvestedHeadPool prefab.");
        }
    }
}
