using System;

namespace OrcFarm.Inventory
{
    /// <summary>
    /// In-memory inventory model with a hotbar and a main section.
    ///
    /// Pure C# — no UnityEngine dependency (§1.2). Construct with <c>new Inventory()</c>
    /// and inject via <see cref="IPlayerInputProvider"/> or a future VContainer binding.
    ///
    /// Add behaviour:
    ///   Hotbar is preferred over main inventory.
    ///   Within each section, an existing stack of the same type is topped up first;
    ///   only if no existing stack exists is an empty slot occupied.
    ///
    /// Remove behaviour:
    ///   Atomic — the full requested quantity must be available or nothing is removed.
    ///   Drains hotbar before main inventory.
    /// </summary>
    public sealed class Inventory
    {
        /// <summary>Number of hotbar slots.</summary>
        public const int HotbarSize = 5;

        /// <summary>Number of main inventory slots.</summary>
        public const int MainSize = 10;

        private readonly ItemStack[] _hotbar = new ItemStack[HotbarSize];
        private readonly ItemStack[] _main   = new ItemStack[MainSize];
        private int _activeHotbarSlot;

        // ── Read ──────────────────────────────────────────────────────────────

        /// <summary>Index of the currently selected hotbar slot (0 – HotbarSize-1).</summary>
        public int ActiveHotbarSlot => _activeHotbarSlot;

        /// <summary>Returns the stack at the given hotbar index.</summary>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public ItemStack GetHotbarSlot(int index)
        {
            GuardHotbarIndex(index);
            return _hotbar[index];
        }

        /// <summary>Returns the stack at the given main-inventory index.</summary>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public ItemStack GetMainSlot(int index)
        {
            GuardMainIndex(index);
            return _main[index];
        }

        /// <summary>Shorthand for the stack in the currently selected hotbar slot.</summary>
        public ItemStack GetActiveItem() => _hotbar[_activeHotbarSlot];

        /// <summary>Total count of <paramref name="type"/> across every slot.</summary>
        public int GetCount(ItemType type)
        {
            if (type == ItemType.None)
                return 0;

            int total = 0;
            for (int i = 0; i < _hotbar.Length; i++)
                if (_hotbar[i].Type == type) total += _hotbar[i].Count;
            for (int i = 0; i < _main.Length; i++)
                if (_main[i].Type == type) total += _main[i].Count;
            return total;
        }

        // ── Write ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds <paramref name="count"/> items of <paramref name="type"/>.
        /// Returns <c>false</c> if there is no space in either section.
        /// </summary>
        public bool TryAdd(ItemType type, int count = 1)
        {
            if (type == ItemType.None || count <= 0)
                return false;

            return TryAddToArray(_hotbar, type, count)
                || TryAddToArray(_main,   type, count);
        }

        /// <summary>
        /// Removes <paramref name="count"/> items of <paramref name="type"/>.
        /// Atomic: returns <c>false</c> without modifying anything if the total
        /// available quantity is below <paramref name="count"/>.
        /// </summary>
        public bool TryRemove(ItemType type, int count = 1)
        {
            if (type == ItemType.None || count <= 0)
                return false;

            if (GetCount(type) < count)
                return false;

            int remaining = count;
            remaining = DrainFromArray(_hotbar, type, remaining);
            DrainFromArray(_main, type, remaining);
            return true;
        }

        /// <summary>
        /// Selects the active hotbar slot. Index must be 0 – HotbarSize-1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public void SetActiveHotbarSlot(int index)
        {
            GuardHotbarIndex(index);
            _activeHotbarSlot = index;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <returns>True if the item was placed.</returns>
        private static bool TryAddToArray(ItemStack[] slots, ItemType type, int count)
        {
            // Pass 1: stack onto the first existing slot of the same type.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Type == type)
                {
                    slots[i] = slots[i].WithCount(slots[i].Count + count);
                    return true;
                }
            }

            // Pass 2: occupy the first empty slot.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = new ItemStack(type, count);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes up to <paramref name="toRemove"/> of <paramref name="type"/> from
        /// <paramref name="slots"/>. Clears a slot when its count reaches zero.
        /// </summary>
        /// <returns>Remainder that could not be removed from this array.</returns>
        private static int DrainFromArray(ItemStack[] slots, ItemType type, int toRemove)
        {
            for (int i = 0; i < slots.Length && toRemove > 0; i++)
            {
                if (slots[i].Type != type)
                    continue;

                int take     = toRemove < slots[i].Count ? toRemove : slots[i].Count;
                int newCount = slots[i].Count - take;
                slots[i]     = newCount > 0 ? slots[i].WithCount(newCount) : ItemStack.Empty;
                toRemove    -= take;
            }

            return toRemove;
        }

        private static void GuardHotbarIndex(int index)
        {
            // Cast to uint collapses the negative-or-too-large check into one branch.
            if ((uint)index >= HotbarSize)
                throw new ArgumentOutOfRangeException(
                    nameof(index), $"Hotbar index must be 0–{HotbarSize - 1}, got {index}.");
        }

        private static void GuardMainIndex(int index)
        {
            if ((uint)index >= MainSize)
                throw new ArgumentOutOfRangeException(
                    nameof(index), $"Main inventory index must be 0–{MainSize - 1}, got {index}.");
        }
    }
}
