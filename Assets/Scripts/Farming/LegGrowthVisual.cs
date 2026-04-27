using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Scales a leg visual inside a fish prefab as the pond growth progresses.
    /// </summary>
    public sealed class LegGrowthVisual : MonoBehaviour
    {
        [Tooltip("Leg object inside this fish prefab that should scale while growing. If empty, this transform is used.")]
        [SerializeField] private Transform _growthVisual;

        [Tooltip("Scale used when the leg is first spawned.")]
        [Min(0f)]
        [SerializeField] private float _startScale = 0.2f;

        [Tooltip("Scale used when the pond reaches ReadyToHarvest.")]
        [Min(0f)]
        [SerializeField] private float _harvestScale = 1f;

        private void Awake()
        {
            if (_growthVisual == null)
                _growthVisual = transform;

            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            if (_growthVisual == null)
                return;

            float scale = Mathf.Lerp(_startScale, _harvestScale, Mathf.Clamp01(progress));
            _growthVisual.localScale = Vector3.one * scale;
        }

        private void OnValidate()
        {
            _startScale = Mathf.Max(0f, _startScale);
            _harvestScale = Mathf.Max(_startScale, _harvestScale);
        }
    }
}
