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
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Tooltip("Number of HeadSeeds added to inventory at start.")]
        [SerializeField] private int _startingSeeds = 5;

        [Tooltip("Number of Fertilizer items added to inventory at start.")]
        [SerializeField] private int _startingFertilizer = 5;

        private readonly Inventory _inventory = new Inventory();

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_startingSeeds > 0)
                _inventory.TryAdd(ItemType.HeadSeed, _startingSeeds);

            if (_startingFertilizer > 0)
                _inventory.TryAdd(ItemType.Fertilizer, _startingFertilizer);
        }

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
