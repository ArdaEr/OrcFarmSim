using System;

namespace OrcFarm.Inventory
{
    /// <summary>
    /// Immutable snapshot of a single hotbar slot's contents.
    ///
    /// Returned as a value type by <see cref="IPlayerInventory"/> hotbar accessors so
    /// callers can read slot data without heap allocation (§3.1).
    ///
    /// Mirrors the <see cref="ItemStack"/> pattern but carries only the fields needed
    /// by hotbar display and gameplay consumers.
    /// </summary>
    public readonly struct HotbarSlot : IEquatable<HotbarSlot>
    {
        /// <summary>Item type held in this slot. <see cref="ItemType.None"/> when empty.</summary>
        public ItemType SlotItemType { get; }

        /// <summary>Number of items stacked in this slot. Always &gt;= 0.</summary>
        public int Count { get; }

        /// <summary>True when this slot holds no items.</summary>
        public bool IsEmpty => Count <= 0;

        /// <summary>A default, empty hotbar slot.</summary>
        public static readonly HotbarSlot Empty = default;

        /// <param name="type">Item type. Pass <see cref="ItemType.None"/> for an empty slot.</param>
        /// <param name="count">Item count. Negative values are clamped to zero.</param>
        public HotbarSlot(ItemType type, int count)
        {
            SlotItemType = type;
            Count        = count > 0 ? count : 0;
        }

        /// <inheritdoc/>
        public bool Equals(HotbarSlot other) =>
            SlotItemType == other.SlotItemType && Count == other.Count;

        /// <inheritdoc/>
        public override bool Equals(object obj) =>
            obj is HotbarSlot other && Equals(other);

        private const int HashPrime = 397;

        /// <inheritdoc/>
        public override int GetHashCode() => ((int)SlotItemType * HashPrime) ^ Count;
    }
}
