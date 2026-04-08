using UnityEngine;

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
        public float GrowthDuration = 60f;

        [Tooltip("Fraction through GrowthDuration at which the care checkpoint opens (0.1–0.9).")]
        [Range(0.1f, 0.9f)]
        public float CareCheckpointFraction = 0.5f;

        [Tooltip("Seconds the player has to respond before the crop fails.")]
        [Min(1f)]
        public float CareWindowDuration = 15f;

        /// <summary>Absolute seconds-after-planting when the care window opens.</summary>
        public float CareCheckpointTime => GrowthDuration * CareCheckpointFraction;

        private void OnValidate()
        {
            GrowthDuration         = Mathf.Max(1f, GrowthDuration);
            CareCheckpointFraction = Mathf.Clamp(CareCheckpointFraction, 0.1f, 0.9f);
            CareWindowDuration     = Mathf.Max(1f, CareWindowDuration);

            // Guard: enough time must remain after care to complete growth
            float remainingAfterCare = GrowthDuration * (1f - CareCheckpointFraction);
            if (CareWindowDuration >= remainingAfterCare)
            {
                Debug.LogError(
                    $"[HeadSeedConfig '{name}'] CareWindowDuration ({CareWindowDuration}s) is " +
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
            if (GrowthDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadSeedConfig '{name}'] GrowthDuration must be > 0.");

            if (CareWindowDuration <= 0f)
                throw new System.InvalidOperationException(
                    $"[HeadSeedConfig '{name}'] CareWindowDuration must be > 0.");

            if (CareCheckpointFraction < 0.1f || CareCheckpointFraction > 0.9f)
                throw new System.InvalidOperationException(
                    $"[HeadSeedConfig '{name}'] CareCheckpointFraction must be between 0.1 and 0.9.");
        }
    }
}
