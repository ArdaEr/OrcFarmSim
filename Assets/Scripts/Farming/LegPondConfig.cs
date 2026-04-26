using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Tunable data for the LegPond farming system.
    /// All values are real-time seconds unless otherwise noted.
    ///
    /// Create via: Assets > Create > OrcFarm > Leg Pond Config
    /// </summary>
    [CreateAssetMenu(menuName = "OrcFarm/Leg Pond Config", fileName = "LegPondConfig")]
    public sealed class LegPondConfig : ScriptableObject
    {
        [Tooltip("Seconds after stocking before growth begins.")]
        [Min(0.1f)]
        [SerializeField] private float _stockedDelay = 5f;

        [Tooltip("Total seconds for the legs to fully grow after the stocked delay ends.")]
        [Min(1f)]
        [SerializeField] private float _growthDuration = 60f;

        [Tooltip("Fraction through GrowthDuration when the feed checkpoint opens (0.1–0.9).")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float _careCheckpointFraction = 0.5f;

        [Tooltip("Seconds the player has to feed the pond after the care checkpoint before the legs starve.")]
        [Min(1f)]
        [SerializeField] private float _starvationWindow = 15f;

        [Tooltip("Horizontal metres from the pond centre where the harvested leg spawns.")]
        [Min(0.1f)]
        [SerializeField] private float _harvestSpawnRadius = 1.2f;

        [Tooltip("Metres above the pond origin where the harvested leg spawns.")]
        [Min(0f)]
        [SerializeField] private float _harvestSpawnHeight = 0.8f;

        [Tooltip("Seconds after entering NeedsCare with zero interaction before the pond enters Starved. " +
                 "Any interaction resets this clock.")]
        [Min(1f)]
        [SerializeField] private float _neglectDeadline = 30f;

        [Tooltip("Chance (0–1) that a Normal-quality harvest upgrades to High. Rolled once per harvest.")]
        [Range(0f, 1f)]
        [SerializeField] private float _highQualityChance = 0.10f;

        [Tooltip("FeedScore units lost per second per fish while the pond is Growing.")]
        [Min(0.001f)]
        [SerializeField] private float _feedDecayRate = 0.05f;

        [Tooltip("CareScore units lost per second per fish while the pond is Growing.")]
        [Min(0.001f)]
        [SerializeField] private float _careDecayRate = 0.025f;

        [Tooltip("CareScore restored per OnCareAction call, applied to all alive fish. Clamped to 1.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _careRestoreAmount = 0.5f;

        [Tooltip("Seconds the NeedsCare window stays open before the pond automatically returns to Growing.")]
        [Min(1f)]
        [SerializeField] private float _needsCareDuration = 20f;

        [Tooltip("Minimum horizontal distance from the pond centre where a harvested leg spawns.")]
        [Min(0.1f)]
        [SerializeField] private float _spawnOffsetMin = 1.2f;

        [Tooltip("Maximum horizontal distance from the pond centre where a harvested leg spawns. " +
                 "Must be >= SpawnOffsetMin.")]
        [Min(0.1f)]
        [SerializeField] private float _spawnOffsetMax = 1.8f;

        [Tooltip("Average of FeedScore and CareScore at or above which the fish yields one tier " +
                 "above the pond's BaseQuality (capped at High).")]
        [Range(0f, 1f)]
        [SerializeField] private float _highQualityThreshold = 0.75f;

        [Tooltip("Average of FeedScore and CareScore at or above which the fish yields exactly " +
                 "BaseQuality. Below this value yields one tier lower (capped at Low).")]
        [Range(0f, 1f)]
        [SerializeField] private float _normalQualityThreshold = 0.4f;

        /// <summary>Seconds after stocking before growth begins.</summary>
        public float StockedDelay => _stockedDelay;

        /// <summary>Total seconds for the legs to fully grow after the stocked delay ends.</summary>
        public float GrowthDuration => _growthDuration;

        /// <summary>Fraction through GrowthDuration when the feed checkpoint opens (0.1–0.9).</summary>
        public float CareCheckpointFraction => _careCheckpointFraction;

        /// <summary>Seconds before starvation triggers after the care checkpoint opens.</summary>
        public float StarvationWindow => _starvationWindow;

        /// <summary>Horizontal metres from the pond centre where the harvested leg spawns.</summary>
        public float HarvestSpawnRadius => _harvestSpawnRadius;

        /// <summary>Metres above the pond origin where the harvested leg spawns.</summary>
        public float HarvestSpawnHeight => _harvestSpawnHeight;

        /// <summary>
        /// Seconds after entering NeedsCare with zero interaction before the pond starves.
        /// Any player interaction resets the clock.
        /// </summary>
        public float NeglectDeadline => _neglectDeadline;

        /// <summary>
        /// Chance (0–1) that a Normal-quality harvest upgrades to High for that cycle only.
        /// </summary>
        public float HighQualityChance => _highQualityChance;

        /// <summary>FeedScore units lost per second per fish while Growing.</summary>
        public float FeedDecayRate => _feedDecayRate;

        /// <summary>CareScore units lost per second per fish while Growing.</summary>
        public float CareDecayRate => _careDecayRate;

        /// <summary>CareScore restored per OnCareAction call applied to all alive fish.</summary>
        public float CareRestoreAmount => _careRestoreAmount;

        /// <summary>Seconds the NeedsCare window stays open before automatically returning to Growing.</summary>
        public float NeedsCareDuration => _needsCareDuration;

        /// <summary>Minimum horizontal spawn distance for a harvested leg.</summary>
        public float SpawnOffsetMin => _spawnOffsetMin;

        /// <summary>Maximum horizontal spawn distance for a harvested leg.</summary>
        public float SpawnOffsetMax => _spawnOffsetMax;

        /// <summary>
        /// Score average threshold (inclusive) for one-tier upgrade above BaseQuality.
        /// Must be greater than <see cref="NormalQualityThreshold"/>.
        /// </summary>
        public float HighQualityThreshold => _highQualityThreshold;

        /// <summary>
        /// Score average threshold (inclusive) for BaseQuality output.
        /// Below this value the fish yields one tier below BaseQuality.
        /// </summary>
        public float NormalQualityThreshold => _normalQualityThreshold;

        /// <summary>Absolute seconds-after-stocking when the care checkpoint opens.</summary>
        public float CareCheckpointTime => _growthDuration * _careCheckpointFraction;

        private void OnValidate()
        {
            _stockedDelay            = Mathf.Max(0.1f, _stockedDelay);
            _growthDuration          = Mathf.Max(1f,   _growthDuration);
            _careCheckpointFraction  = Mathf.Clamp(_careCheckpointFraction, 0.1f, 0.9f);
            _starvationWindow        = Mathf.Max(1f, _starvationWindow);
            _harvestSpawnRadius      = Mathf.Max(0.1f, _harvestSpawnRadius);
            _harvestSpawnHeight      = Mathf.Max(0f,   _harvestSpawnHeight);
            _neglectDeadline         = Mathf.Max(1f, _neglectDeadline);
            _highQualityChance       = Mathf.Clamp01(_highQualityChance);
            _feedDecayRate           = Mathf.Max(0.001f, _feedDecayRate);
            _careDecayRate           = Mathf.Max(0.001f, _careDecayRate);
            _careRestoreAmount       = Mathf.Clamp(_careRestoreAmount, 0.1f, 1f);
            _needsCareDuration        = Mathf.Max(1f, _needsCareDuration);
            _spawnOffsetMin           = Mathf.Max(0.1f, _spawnOffsetMin);
            _spawnOffsetMax           = Mathf.Max(_spawnOffsetMin, _spawnOffsetMax);
            _highQualityThreshold     = Mathf.Clamp01(_highQualityThreshold);
            _normalQualityThreshold   = Mathf.Clamp(_normalQualityThreshold, 0f, _highQualityThreshold);
        }

        /// <summary>
        /// Called by <see cref="LegPond"/> during Awake. Throws
        /// <see cref="System.InvalidOperationException"/> if any value is out of range.
        /// </summary>
        public void Validate()
        {
            if (_stockedDelay <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] StockedDelay must be > 0.");

            if (_growthDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] GrowthDuration must be > 0.");

            if (_starvationWindow <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] StarvationWindow must be > 0.");

            if (_careCheckpointFraction < 0.1f || _careCheckpointFraction > 0.9f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] CareCheckpointFraction must be between 0.1 and 0.9.");

            if (_neglectDeadline <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] NeglectDeadline must be > 0.");

            if (_feedDecayRate <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] FeedDecayRate must be > 0.");

            if (_careDecayRate <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] CareDecayRate must be > 0.");

            if (_needsCareDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] NeedsCareDuration must be > 0.");

            if (_spawnOffsetMin <= 0f)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] SpawnOffsetMin must be > 0.");

            if (_spawnOffsetMax < _spawnOffsetMin)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] SpawnOffsetMax must be >= SpawnOffsetMin.");

            if (_normalQualityThreshold >= _highQualityThreshold)
                throw new System.InvalidOperationException(
                    $"[LegPondConfig '{name}'] NormalQualityThreshold must be < HighQualityThreshold.");
        }
    }
}
