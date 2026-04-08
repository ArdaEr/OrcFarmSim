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
    }
}
