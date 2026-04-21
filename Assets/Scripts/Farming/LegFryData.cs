using OrcFarm.Core;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Tunable data for the LegFry tier system.
    /// Defines fish count and base output quality for each <see cref="LegFryTier"/>.
    ///
    /// Create via: Assets > Create > OrcFarm > Leg Fry Data
    /// </summary>
    [CreateAssetMenu(menuName = "OrcFarm/Leg Fry Data", fileName = "LegFryData")]
    public sealed class LegFryData : ScriptableObject
    {
        [SerializeField] private LegFryTierEntry _small  = new LegFryTierEntry(2, OrcQuality.Low);
        [SerializeField] private LegFryTierEntry _normal = new LegFryTierEntry(4, OrcQuality.Normal);
        [SerializeField] private LegFryTierEntry _large  = new LegFryTierEntry(6, OrcQuality.High);

        /// <summary>Number of fish stocked for <paramref name="tier"/>.</summary>
        public int GetFishCount(LegFryTier tier) => EntryFor(tier).FishCount;

        /// <summary>Baseline output quality for legs harvested from a <paramref name="tier"/> pond.</summary>
        public OrcQuality GetBaseQuality(LegFryTier tier) => EntryFor(tier).BaseQuality;

        /// <summary>
        /// Called by <see cref="LegPond"/> during Awake. Throws
        /// <see cref="System.InvalidOperationException"/> if any fish count is invalid.
        /// </summary>
        public void Validate()
        {
            if (_small.FishCount <= 0)
                throw new System.InvalidOperationException(
                    $"[LegFryData '{name}'] Small FishCount must be > 0.");

            if (_normal.FishCount <= 0)
                throw new System.InvalidOperationException(
                    $"[LegFryData '{name}'] Normal FishCount must be > 0.");

            if (_large.FishCount <= 0)
                throw new System.InvalidOperationException(
                    $"[LegFryData '{name}'] Large FishCount must be > 0.");
        }

        private void OnValidate()
        {
            _small.FishCount  = Mathf.Max(1, _small.FishCount);
            _normal.FishCount = Mathf.Max(1, _normal.FishCount);
            _large.FishCount  = Mathf.Max(1, _large.FishCount);
        }

        private LegFryTierEntry EntryFor(LegFryTier tier) => tier switch
        {
            LegFryTier.Small  => _small,
            LegFryTier.Normal => _normal,
            LegFryTier.Large  => _large,
            _                 => throw new System.ArgumentOutOfRangeException(
                                     nameof(tier), $"Unhandled LegFryTier: {tier}"),
        };

        [System.Serializable]
        private struct LegFryTierEntry
        {
            [Tooltip("How many fish are stocked for this tier.")]
            [Min(1)]
            public int FishCount;

            [Tooltip("Baseline output quality for harvested legs of this tier.")]
            public OrcQuality BaseQuality;

            public LegFryTierEntry(int fishCount, OrcQuality quality)
            {
                FishCount   = fishCount;
                BaseQuality = quality;
            }
        }
    }
}
