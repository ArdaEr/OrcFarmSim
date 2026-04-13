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
        }
    }
}
