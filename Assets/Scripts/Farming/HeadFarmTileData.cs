using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// All tunable values for the head-farm tile state machine.
    ///
    /// Create via: Assets > Create > OrcFarm > Head Farm Tile Data
    /// Assign the same asset to every <see cref="HeadFarmTile"/> in a plot.
    /// </summary>
    [CreateAssetMenu(menuName = "OrcFarm/Head Farm Tile Data", fileName = "HeadFarmTileData")]
    public sealed class HeadFarmTileData : ScriptableObject
    {
        [Tooltip("Seconds after covering the soil before growth begins.")]
        [Min(0.1f)]
        [SerializeField] private float _coverDelay = 2f;

        [Tooltip("Seconds from the start of growth to ReadyToHarvest.")]
        [Min(1f)]
        [SerializeField] private float _growDuration = 60f;

        [Tooltip("Minimum XZ distance from the tile centre where the harvested head spawns.")]
        [Min(0.1f)]
        [SerializeField] private float _spawnOffsetMin = 1.2f;

        [Tooltip("Maximum XZ distance from the tile centre where the harvested head spawns. " +
                 "Must be >= SpawnOffsetMin.")]
        [Min(0.1f)]
        [SerializeField] private float _spawnOffsetMax = 1.8f;

        [Header("Condition Decay — Growing state")]
        [Tooltip("FeedScore lost per second during Growing. " +
                 "Default 0.05 → full decay in ~20 s. Reaching 0 kills the crop.")]
        [Min(0.001f)]
        [SerializeField] private float _feedDecayRate = 0.05f;

        [Tooltip("WaterScore lost per second during Growing. Does not cause death — tracked for quality.")]
        [Min(0.001f)]
        [SerializeField] private float _waterDecayRate = 0.03f;

        [Tooltip("CareScore lost per second during Growing. Does not cause death — tracked for quality.")]
        [Min(0.001f)]
        [SerializeField] private float _careDecayRate = 0.02f;

        [Tooltip("Amount added to CareScore per player interaction during Growing (clamped to 1.0 max).")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _careRestoreAmount = 0.3f;

        [Header("Quality Thresholds — Growing state")]
        [Tooltip("Average score (F+W+C)/3 required for High quality harvest. Must be > NormalQualityThreshold.")]
        [Range(0f, 1f)]
        [SerializeField] private float _highQualityThreshold = 0.7f;

        [Tooltip("Average score (F+W+C)/3 required for Normal quality harvest. Must be < HighQualityThreshold.")]
        [Range(0f, 1f)]
        [SerializeField] private float _normalQualityThreshold = 0.4f;

        /// <summary>Seconds after covering the soil before growth begins.</summary>
        public float CoverDelay        => _coverDelay;

        /// <summary>Seconds from the start of growth to ReadyToHarvest.</summary>
        public float GrowDuration      => _growDuration;

        /// <summary>Minimum XZ spawn radius at harvest.</summary>
        public float SpawnOffsetMin    => _spawnOffsetMin;

        /// <summary>Maximum XZ spawn radius at harvest.</summary>
        public float SpawnOffsetMax    => _spawnOffsetMax;

        /// <summary>FeedScore lost per second during Growing. Reaching 0 kills the crop.</summary>
        public float FeedDecayRate     => _feedDecayRate;

        /// <summary>WaterScore lost per second during Growing.</summary>
        public float WaterDecayRate    => _waterDecayRate;

        /// <summary>CareScore lost per second during Growing.</summary>
        public float CareDecayRate     => _careDecayRate;

        /// <summary>Amount added to CareScore per player interaction during Growing.</summary>
        public float CareRestoreAmount => _careRestoreAmount;

        /// <summary>Average score threshold for High quality. Must be > NormalQualityThreshold.</summary>
        public float HighQualityThreshold   => _highQualityThreshold;

        /// <summary>Average score threshold for Normal quality. Must be &lt; HighQualityThreshold.</summary>
        public float NormalQualityThreshold => _normalQualityThreshold;

        private void OnValidate()
        {
            _coverDelay        = Mathf.Max(0.1f,   _coverDelay);
            _growDuration      = Mathf.Max(1f,     _growDuration);
            _spawnOffsetMin    = Mathf.Max(0.1f,   _spawnOffsetMin);
            _spawnOffsetMax    = Mathf.Max(_spawnOffsetMin, _spawnOffsetMax);
            _feedDecayRate     = Mathf.Max(0.001f, _feedDecayRate);
            _waterDecayRate    = Mathf.Max(0.001f, _waterDecayRate);
            _careDecayRate     = Mathf.Max(0.001f, _careDecayRate);
            _careRestoreAmount = Mathf.Clamp(_careRestoreAmount, 0.01f, 1f);

            // Ensure normal < high so the tiers are always distinct.
            _normalQualityThreshold = Mathf.Min(_normalQualityThreshold, _highQualityThreshold - 0.01f);
        }

        /// <summary>
        /// Called by <see cref="HeadFarmTile"/> during Awake.
        /// Throws <see cref="System.InvalidOperationException"/> if any value is invalid.
        /// </summary>
        public void Validate()
        {
            if (_coverDelay <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] CoverDelay must be > 0.");

            if (_growDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] GrowDuration must be > 0.");

            if (_spawnOffsetMin <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] SpawnOffsetMin must be > 0.");

            if (_spawnOffsetMax < _spawnOffsetMin)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] SpawnOffsetMax ({_spawnOffsetMax}) " +
                    $"must be >= SpawnOffsetMin ({_spawnOffsetMin}).");

            if (_feedDecayRate <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] FeedDecayRate must be > 0.");

            if (_waterDecayRate <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] WaterDecayRate must be > 0.");

            if (_careDecayRate <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadFarmTileData '{name}'] CareDecayRate must be > 0.");
        }
    }
}
