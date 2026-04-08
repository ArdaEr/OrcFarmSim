using System;

namespace OrcFarm.Inventory
{
    /// <summary>
    /// An immutable pairing of an <see cref="ItemType"/> and a positive integer count.
    /// Represents the contents of a single inventory slot.
    ///
    /// Value type — copied by value; no additional allocation per copy.
    /// Use <see cref="Empty"/> or <c>default</c> for an unoccupied slot.
    /// </summary>
    public readonly struct ItemStack : IEquatable<ItemStack>
    {
        /// <summary>The type of item in this stack.</summary>
        public ItemType Type  { get; }

        /// <summary>How many items are in this stack. Always &gt;= 0.</summary>
        public int      Count { get; }

        /// <summary>An empty slot — <see cref="ItemType.None"/>, count zero.</summary>
        public static readonly ItemStack Empty = default;

        /// <summary>True when this slot carries no item.</summary>
        public bool IsEmpty => Type == ItemType.None || Count <= 0;

        /// <param name="type">Must not be <see cref="ItemType.None"/> for a non-empty stack.</param>
        /// <param name="count">Clamped to 0 if negative.</param>
        public ItemStack(ItemType type, int count)
        {
            Type  = type;
            Count = count > 0 ? count : 0;
        }

        /// <summary>Returns a new stack with the same type but a different count.</summary>
        public ItemStack WithCount(int newCount) => new ItemStack(Type, newCount);

        // ── Equality ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool Equals(ItemStack other) => Type == other.Type && Count == other.Count;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => ((int)Type * 397) ^ Count;

        /// <inheritdoc/>
        public override string ToString() => IsEmpty ? "Empty" : $"{Count}x {Type}";
    }
}
