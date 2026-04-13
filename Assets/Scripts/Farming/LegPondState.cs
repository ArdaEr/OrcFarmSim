namespace OrcFarm.Farming
{
    /// <summary>Lifecycle states for a <see cref="LegPond"/>.</summary>
    public enum LegPondState
    {
        /// <summary>Pond is empty. Player can stock it with a LegFry.</summary>
        Empty = 0,

        /// <summary>Stocked and settling. Transitions to Growing after the stocked delay.</summary>
        Stocked = 1,

        /// <summary>Legs are actively growing. No player interaction available.</summary>
        Growing = 2,

        /// <summary>Care checkpoint reached. Player must feed within the starvation window.</summary>
        NeedsCare = 3,

        /// <summary>Legs are ready. Player can harvest to receive a HarvestedLeg.</summary>
        ReadyToHarvest = 4,

        /// <summary>Failure state — legs have eaten each other. Player clears to reset.</summary>
        Starved = 5,
    }
}
