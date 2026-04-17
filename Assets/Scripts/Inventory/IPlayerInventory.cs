namespace OrcFarm.Inventory
{
    /// <summary>
    /// Contract for the scene-side owner of the player's inventory.
    /// Consumed by systems that need to read or modify item counts.
    /// </summary>
    public interface IPlayerInventory
    {
        /// <summary>Total count of <paramref name="type"/> across all inventory slots.</summary>
        int GetCount(ItemType type);

        /// <summary>True if at least <paramref name="count"/> of <paramref name="type"/> are held.</summary>
        bool Has(ItemType type, int count = 1);

        /// <summary>
        /// Removes <paramref name="count"/> of <paramref name="type"/> atomically.
        /// Returns false without modifying inventory if the quantity is insufficient.
        /// </summary>
        bool TryConsume(ItemType type, int count = 1);

        /// <summary>Adds <paramref name="count"/> of <paramref name="type"/>. Returns false if no space.</summary>
        bool TryAdd(ItemType type, int count = 1);

        // ── Hotbar ────────────────────────────────────────────────────────────────

        /// <summary>Index of the currently selected hotbar slot (0 – hotbar size minus 1).</summary>
        int SelectedSlotIndex { get; }

        /// <summary>
        /// Returns a snapshot of hotbar slot at <paramref name="index"/>.
        /// No heap allocation — <see cref="HotbarSlot"/> is a readonly struct (§3.1).
        /// </summary>
        HotbarSlot GetHotbarSlot(int index);

        /// <summary>Returns a snapshot of the currently selected hotbar slot.</summary>
        HotbarSlot GetSelectedSlot();

        /// <summary>
        /// Sets the selected hotbar slot. Index is clamped to the valid range.
        /// </summary>
        void SetSelectedSlot(int index);

        /// <summary>
        /// Attempts to consume <paramref name="amount"/> items from the currently
        /// selected hotbar slot. Returns false if the slot is empty or the count is
        /// insufficient. Does not fall through to other slots.
        /// </summary>
        bool TryConsumeFromSelectedSlot(int amount);
    }
}
