namespace OrcFarm.Core
{
    /// <summary>
    /// Stateless helpers for <see cref="OrcTrait"/> and trait-adjacent data.
    /// All methods are allocation-free — switch expressions return compile-time string literals.
    /// </summary>
    public static class OrcTraitUtility
    {
        /// <summary>
        /// Returns the player-facing display name for <paramref name="trait"/>.
        /// No dictionary, no heap allocation.
        /// </summary>
        public static string GetDisplayName(OrcTrait trait) => trait switch
        {
            OrcTrait.None      => "None",
            OrcTrait.Brutish   => "Brutish",
            OrcTrait.Resilient => "Resilient",
            OrcTrait.Diligent  => "Diligent",
            OrcTrait.BoneIdle  => "Bone-Idle",
            OrcTrait.Clumsy    => "Clumsy",
            OrcTrait.Twitchy   => "Twitchy",
            _                  => "Unknown",
        };

        /// <summary>
        /// Returns the integer weight for <paramref name="quality"/> used during trait selection.
        /// Higher-quality parts bias toward stronger traits.
        /// Low = 1, Normal = 2, High = 3.
        /// </summary>
        public static int GetQualityWeight(OrcQuality quality) => quality switch
        {
            OrcQuality.Low    => 1,
            OrcQuality.Normal => 2,
            OrcQuality.High   => 3,
            _                 => 1,
        };
    }
}
