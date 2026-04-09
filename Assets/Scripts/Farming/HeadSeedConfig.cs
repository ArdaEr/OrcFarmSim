using UnityEngine;
using UnityEngine.Serialization;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Tunable data for the single MVP crop: the Head Seed.
    /// All timing values are real-time seconds.
    ///
    /// Create via: Assets > Create > OrcFarm > Head Seed Config
    /// </summary>
    [CreateAssetMenu(menuName = "OrcFarm/Head Seed Config", fileName = "HeadSeedConfig")]
    public sealed class HeadSeedConfig : ScriptableObject
    {
        [Tooltip("Total seconds for the crop to fully grow after planting.")]
        [Min(1f)]
        [FormerlySerializedAs("GrowthDuration")]
        [SerializeField] private float _growthDuration = 60f;

        [Tooltip("Fraction through GrowthDuration at which the care checkpoint opens (0.1–0.9).")]
        [Range(0.1f, 0.9f)]
        [FormerlySerializedAs("CareCheckpointFraction")]
        [SerializeField] private float _careCheckpointFraction = 0.5f;

        [Tooltip("Seconds the player has to respond before the crop fails.")]
        [Min(1f)]
        [FormerlySerializedAs("CareWindowDuration")]
        [SerializeField] private float _careWindowDuration = 15f;

        [Tooltip("Seconds the 'Planted' confirmation state is shown before growth begins.")]
        [Min(0.1f)]
        [SerializeField] private float _plantedConfirmationDuration = 1.5f;

        /// <summary>Total seconds for the crop to fully grow after planting.</summary>
        public float GrowthDuration => _growthDuration;

        /// <summary>Fraction through GrowthDuration at which the care checkpoint opens (0.1–0.9).</summary>
        public float CareCheckpointFraction => _careCheckpointFraction;

        /// <summary>Seconds the player has to respond before the crop fails.</summary>
        public float CareWindowDuration => _careWindowDuration;

        /// <summary>Seconds the 'Planted' confirmation state is shown before growth begins.</summary>
        public float PlantedConfirmationDuration => _plantedConfirmationDuration;

        /// <summary>Absolute seconds-after-planting when the care window opens.</summary>
        public float CareCheckpointTime => _growthDuration * _careCheckpointFraction;

        private void OnValidate()
        {
            _growthDuration                = Mathf.Max(1f, _growthDuration);
            _careCheckpointFraction        = Mathf.Clamp(_careCheckpointFraction, 0.1f, 0.9f);
            _careWindowDuration            = Mathf.Max(1f, _careWindowDuration);
            _plantedConfirmationDuration   = Mathf.Max(0.1f, _plantedConfirmationDuration);

            // Guard: enough time must remain after care to complete growth
            float remainingAfterCare = _growthDuration * (1f - _careCheckpointFraction);
            if (_careWindowDuration >= remainingAfterCare)
            {
                Debug.LogError(
                    $"[HeadSeedConfig '{name}'] CareWindowDuration ({_careWindowDuration}s) is " +
                    $">= the post-checkpoint growth window ({remainingAfterCare}s). " +
                    $"Reduce CareWindowDuration or increase GrowthDuration.", this);
            }
        }

        /// <summary>
        /// Called by <see cref="FarmPlot"/> during Awake. Throws
        /// <see cref="System.InvalidOperationException"/> if any value is out of range.
        /// </summary>
        public void Validate()
        {
            if (_growthDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadSeedConfig '{name}'] GrowthDuration must be > 0.");

            if (_careWindowDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadSeedConfig '{name}'] CareWindowDuration must be > 0.");

            if (_careCheckpointFraction < 0.1f || _careCheckpointFraction > 0.9f)
                throw new System.InvalidOperationException(
                    $"[HeadSeedConfig '{name}'] CareCheckpointFraction must be between 0.1 and 0.9.");
        }
    }
}
