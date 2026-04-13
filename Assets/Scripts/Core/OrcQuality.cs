namespace OrcFarm.Core
{
    /// <summary>
    /// Quality tier used by both orc assembly and leg-pond output.
    /// Placed in OrcFarm.Core so assemblies that cannot reference OrcFarm.Workers
    /// (e.g. OrcFarm.Carry, OrcFarm.Farming) can store and pass quality values
    /// without creating a circular dependency.
    ///
    /// <see cref="OrcFarm.Workers.OrcQuality"/> remains in Workers for the existing
    /// assembly/economy flow. The two types will be unified when leg assembly is built.
    /// </summary>
    public enum OrcQuality
    {
        Low    = 0,
        Normal = 1,
        High   = 2,
    }
}
