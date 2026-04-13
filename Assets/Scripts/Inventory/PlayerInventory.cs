using UnityEngine;

namespace OrcFarm.Inventory
{
    /// <summary>
    /// Scene-side owner of the player's <see cref="Inventory"/> domain model.
    ///
    /// Slot counts are sourced from <see cref="InventoryConfig"/> so designers can adjust
    /// layout without recompiling (§6.5). If <c>_config</c> is unassigned the inventory
    /// falls back to built-in defaults (5 hotbar / 10 main).
    ///
    /// Starting item counts are configurable in the Inspector so playtests can begin
    /// with a known supply without extra setup code.
    ///
    /// Debug refill (Play Mode only):
    ///   Set <c>_debugAddSeeds</c> / <c>_debugAddFertilizer</c> to the desired amounts,
    ///   then tick <c>_applyDebugRefill</c> in the Inspector. The refill is applied
    ///   immediately and the checkbox resets itself.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour, IPlayerInventory
    {
        // ── Config ────────────────────────────────────────────────────────────

        [Tooltip("Inventory slot layout. If unassigned, defaults (5 hotbar / 10 main) are used.")]
        [SerializeField] private InventoryConfig _config;

        // ── Startup ───────────────────────────────────────────────────────────

        [Tooltip("Number of HeadSeeds added to inventory at start.")]
        [SerializeField] private int _startingSeeds = 5;

        [Tooltip("Number of Fertilizer items added to inventory at start.")]
        [SerializeField] private int _startingFertilizer = 5;

        [Tooltip("Number of LegFry added to inventory at start.")]
        [SerializeField] private int _startingLegFry = 3;

        [Tooltip("Number of FeedItems added to inventory at start.")]
        [SerializeField] private int _startingFeedItem = 5;

        // ── Debug refill (Play Mode only) ─────────────────────────────────────

        [Header("Debug Refill  —  Play Mode only")]
        [Tooltip("HeadSeeds to add when the refill is triggered.")]
        [SerializeField] private int _debugAddSeeds = 5;

        [Tooltip("Fertilizer to add when the refill is triggered.")]
        [SerializeField] private int _debugAddFertilizer = 5;

        [Tooltip("LegFry to add when the refill is triggered.")]
        [SerializeField] private int _debugAddLegFry = 5;

        [Tooltip("FeedItems to add when the refill is triggered.")]
        [SerializeField] private int _debugAddFeedItem = 5;

        [Tooltip("Tick this during Play Mode to immediately add the amounts above to the " +
                 "runtime inventory. The checkbox resets itself after one use.")]
        [SerializeField] private bool _applyDebugRefill;

        private Inventory _inventory;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _applyDebugRefill = false; // discard any stale tick left from Edit Mode

            if (_config != null)
            {
                _config.Validate();
                _inventory = new Inventory(_config.HotbarSize, _config.MainInventorySize);
            }
            else
            {
                _inventory = new Inventory(); // default: 5 hotbar, 10 main
            }

            if (_startingSeeds > 0)
                _inventory.TryAdd(ItemType.HeadSeed, _startingSeeds);

            if (_startingFertilizer > 0)
                _inventory.TryAdd(ItemType.Fertilizer, _startingFertilizer);

            if (_startingLegFry > 0)
                _inventory.TryAdd(ItemType.LegFry, _startingLegFry);

            if (_startingFeedItem > 0)
                _inventory.TryAdd(ItemType.FeedItem, _startingFeedItem);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || !_applyDebugRefill)
                return;

            _applyDebugRefill = false;

            if (_debugAddSeeds > 0)
                _inventory.TryAdd(ItemType.HeadSeed, _debugAddSeeds);
            if (_debugAddFertilizer > 0)
                _inventory.TryAdd(ItemType.Fertilizer, _debugAddFertilizer);
            if (_debugAddLegFry > 0)
                _inventory.TryAdd(ItemType.LegFry, _debugAddLegFry);
            if (_debugAddFeedItem > 0)
                _inventory.TryAdd(ItemType.FeedItem, _debugAddFeedItem);

            Debug.Log(
                $"[PlayerInventory] Debug refill applied: " +
                $"+{_debugAddSeeds} HeadSeed, +{_debugAddFertilizer} Fertilizer, " +
                $"+{_debugAddLegFry} LegFry, +{_debugAddFeedItem} FeedItem.  " +
                $"Now: {_inventory.GetCount(ItemType.HeadSeed)} seeds, " +
                $"{_inventory.GetCount(ItemType.Fertilizer)} fertilizer, " +
                $"{_inventory.GetCount(ItemType.LegFry)} leg fry, " +
                $"{_inventory.GetCount(ItemType.FeedItem)} feed.", this);
        }
#endif

        // ── IPlayerInventory ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public int GetCount(ItemType type) => _inventory.GetCount(type);

        /// <inheritdoc/>
        public bool Has(ItemType type, int count = 1) => _inventory.GetCount(type) >= count;

        /// <inheritdoc/>
        public bool TryConsume(ItemType type, int count = 1) => _inventory.TryRemove(type, count);

        /// <inheritdoc/>
        public bool TryAdd(ItemType type, int count = 1) => _inventory.TryAdd(type, count);
    }
}
