using OrcFarm.Core;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Quality-based fallback trait pools used when no priority-graph rule fires at harvest.
    /// Arrays are allocated once at class load — no per-call heap allocation (§3.1).
    /// </summary>
    internal static class TraitFallback
    {
        private static readonly OrcTrait[] HighPool   = { OrcTrait.Diligent, OrcTrait.Resilient };
        private static readonly OrcTrait[] NormalPool = { OrcTrait.Brutish,  OrcTrait.Diligent  };
        private static readonly OrcTrait[] LowPool    = { OrcTrait.BoneIdle, OrcTrait.Clumsy, OrcTrait.Twitchy };

        /// <summary>Returns a random fallback trait weighted by <paramref name="quality"/>.</summary>
        internal static OrcTrait Select(OrcQuality quality) => quality switch
        {
            OrcQuality.High   => HighPool  [Random.Range(0, HighPool.Length)],
            OrcQuality.Normal => NormalPool[Random.Range(0, NormalPool.Length)],
            _                 => LowPool   [Random.Range(0, LowPool.Length)],
        };
    }
}
