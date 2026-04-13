namespace OrcFarm.Inventory
{
    /// <summary>
    /// All item types supported by the MVP inventory.
    /// Extend this enum when new item types are introduced.
    /// </summary>
    public enum ItemType
    {
        /// <summary>Represents the absence of an item (empty slot sentinel).</summary>
        None = 0,

        /// <summary>Planted in a prepared, fertilized plot to grow a Harvested Head.</summary>
        HeadSeed = 1,

        /// <summary>Applied to a prepared plot before planting.</summary>
        Fertilizer = 2,

        /// <summary>The result of a successful harvest from a farm plot.</summary>
        HarvestedHead = 3,

        /// <summary>Juvenile leg fry stocked into a LegPond to begin growth.</summary>
        LegFry = 4,

        /// <summary>Feed consumed at the leg pond care checkpoint.</summary>
        FeedItem = 5,

        /// <summary>The result of a successful leg pond harvest.</summary>
        HarvestedLeg = 6,
    }
}
