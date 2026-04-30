using System;

namespace OrcFarm.Core
{
    /// <summary>
    /// Bit flags that farming systems accumulate during a growth cycle.
    /// Assembly reads the combined flags from the deposited head and leg
    /// to bias final trait selection.
    ///
    /// Multiple flags can be active simultaneously — e.g. a pond can carry
    /// both <see cref="Overstocked"/> and <see cref="FishDeaths"/> from the same cycle.
    /// </summary>
    [Flags]
    public enum TraitInfluenceFlags
    {
        None              = 0,
        GoodCare          = 1 << 0,
        LowCare           = 1 << 1,
        ConsistentCare    = 1 << 2,
        LowFeed           = 1 << 3,
        HighFeed          = 1 << 4,
        Isolation         = 1 << 5,
        Crowded           = 1 << 6,
        NeglectedWater    = 1 << 7,
        GoodWater         = 1 << 8,
        Overstocked       = 1 << 9,
        Understocked      = 1 << 10,
        FishDeaths        = 1 << 11,
        ConsistentFeeding = 1 << 12,
    }
}
