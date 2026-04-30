namespace OrcFarm.Core
{
    /// <summary>
    /// Demo trait set for Trait Assignment V1.
    /// Exactly one trait is selected per assembled orc at the assembly station.
    /// <see cref="None"/> means no trait was assigned (default / uninitialized).
    /// </summary>
    public enum OrcTrait
    {
        None      = 0,
        Brutish   = 1,
        Resilient = 2,
        Diligent  = 3,
        BoneIdle  = 4,
        Clumsy    = 5,
        Twitchy   = 6,
    }
}
