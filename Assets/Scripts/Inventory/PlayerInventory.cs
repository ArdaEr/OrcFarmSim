using UnityEngine;

namespace OrcFarm.Inventory
{
    /// <summary>
    /// Scene-side owner of the player's <see cref="Inventory"/> domain model.
    ///
    /// Attach to a single scene GameObject (e.g. the Player root or a dedicated
    /// "GameSystems" object). Assign a reference to any <see cref="OrcFarm.Farming.FarmPlot"/>
    /// that should consume seeds and fertilizer.
    ///
    /// Starting item counts are configurable in the Inspector so playtests can begin
    /// with a known supply without writing any extra setup code.
    ///
    /// Debug refill (Play Mode only):
    ///   Set <c>_debugAddSeeds</c> / <c>_debugAddFertilizer</c> to the desired amounts,
    ///   then tick <c>_applyDebugRefill</c> in the Inspector. The refill is applied
    ///   immediately and the checkbox resets itself.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        // ── Startup ───────────────────────────────────────────────────────────

        [Tooltip("Number of HeadSeeds added to inventory at start.")]
        [SerializeField] private int _startingSeeds = 5;

        [Tooltip("Number of Fertilizer items added to inventory at start.")]
        [SerializeField] private int _startingFertilizer = 5;

        // ── Debug refill (Play Mode only) ─────────────────────────────────────

        [Header("Debug Refill  —  Play Mode only")]
        [Tooltip("HeadSeeds to add when the refill is triggered.")]
        [SerializeField] private int _debugAddSeeds = 5;

        [Tooltip("Fertilizer to add when the refill is triggered.")]
        [SerializeField] private int _debugAddFertilizer = 5;

        [Tooltip("Tick this during Play Mode to immediately add the amounts above to the " +
                 "runtime inventory. The checkbox resets itself after one use.")]
        [SerializeField] private bool _applyDebugRefill;

        private readonly Inventory _inventory = new Inventory();

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _applyDebugRefill = false; // discard any stale tick left from Edit Mode

            if (_startingSeeds > 0)
                _inventory.TryAdd(ItemType.HeadSeed, _startingSeeds);

            if (_startingFertilizer > 0)
                _inventory.TryAdd(ItemType.Fertilizer, _startingFertilizer);
        }

#if UNITY_EDITOR
        // OnValidate fires whenever a serialized field changes in the Inspector.
        // The Application.isPlaying guard restricts this to Play Mode only.
        private void OnValidate()
        {
            if (!Application.isPlaying || !_applyDebugRefill)
                return;

            _applyDebugRefill = false; // one-shot: reset immediately so repeated ticks are explicit

            if (_debugAddSeeds > 0)
                _inventory.TryAdd(ItemType.HeadSeed, _debugAddSeeds);
            if (_debugAddFertilizer > 0)
                _inventory.TryAdd(ItemType.Fertilizer, _debugAddFertilizer);

            Debug.Log(
                $"[PlayerInventory] Debug refill applied: +{_debugAddSeeds} HeadSeed, " +
                $"+{_debugAddFertilizer} Fertilizer.  " +
                $"Now: {_inventory.GetCount(ItemType.HeadSeed)} seeds, " +
                $"{_inventory.GetCount(ItemType.Fertilizer)} fertilizer.", this);
        }
#endif

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Total count of <paramref name="type"/> across all inventory slots.</summary>
        public int GetCount(ItemType type) => _inventory.GetCount(type);

        /// <summary>True if at least <paramref name="count"/> of <paramref name="type"/> are held.</summary>
        public bool Has(ItemType type, int count = 1) => _inventory.GetCount(type) >= count;

        /// <summary>
        /// Removes <paramref name="count"/> of <paramref name="type"/> atomically.
        /// Returns false without modifying inventory if the quantity is insufficient.
        /// </summary>
        public bool TryConsume(ItemType type, int count = 1) => _inventory.TryRemove(type, count);

        /// <summary>Adds <paramref name="count"/> of <paramref name="type"/>. Returns false if no space.</summary>
        public bool TryAdd(ItemType type, int count = 1) => _inventory.TryAdd(type, count);
    }
}
