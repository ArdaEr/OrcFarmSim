using UnityEngine;

namespace OrcFarm.Effects
{
    /// <summary>
    /// Drives the OrcFarm jelly shader with a directional grab impulse.
    /// </summary>
    public sealed class JellyShakeController : MonoBehaviour
    {
        private const float DefaultDurationSeconds = 0.55f;
        private const float DefaultAmplitude = 0.16f;
        private const float DefaultStretchAmplitude = 0.22f;
        private const float DefaultFrequency = 9.0f;
        private const float DefaultDamping = 4.25f;
        private const float MinDurationSeconds = 0.01f;
        private const float MinDirectionLengthSquared = 0.0001f;
        private const float MinBoundsHeight = 0.01f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const float DefaultIntensity = 1.0f;

        private static readonly int BaseYId = Shader.PropertyToID("_BaseY");
        private static readonly int JellyHeightId = Shader.PropertyToID("_JellyHeight");
        private static readonly int JellyCenterWorldId = Shader.PropertyToID("_JellyCenterWorld");
        private static readonly int JellyRadiusWorldId = Shader.PropertyToID("_JellyRadiusWorld");
        private static readonly int UseRendererBoundsId = Shader.PropertyToID("_UseRendererBounds");
        private static readonly int GrabShakeAmountId = Shader.PropertyToID("_GrabShakeAmount");
        private static readonly int GrabStretchAmountId = Shader.PropertyToID("_GrabStretchAmount");
        private static readonly int ShakeDirectionId = Shader.PropertyToID("_ShakeDirection");

        [Tooltip("Renderer using the OrcFarm/Effects/Jelly Shake material.")]
        [SerializeField] private Renderer _targetRenderer;

        [Tooltip("Base duration of the grab shake impulse.")]
        [Min(MinDurationSeconds)]
        [SerializeField] private float _durationSeconds = DefaultDurationSeconds;

        [Tooltip("Maximum directional displacement sent to the jelly shader.")]
        [Min(0.0f)]
        [SerializeField] private float _amplitude = DefaultAmplitude;

        [Tooltip("Maximum one-sided stretch sent to the jelly shader when something is grabbed from inside.")]
        [Min(0.0f)]
        [SerializeField] private float _stretchAmplitude = DefaultStretchAmplitude;

        [Tooltip("Shake oscillations per second.")]
        [Min(0.0f)]
        [SerializeField] private float _frequency = DefaultFrequency;

        [Tooltip("How quickly the grab impulse settles back to rest.")]
        [Min(0.0f)]
        [SerializeField] private float _damping = DefaultDamping;

        private MaterialPropertyBlock _materialProperties;
        private Vector3 _shakeDirection = Vector3.right;
        private float _shakeTimer;
        private float _currentDurationSeconds;
        private float _currentAmplitude;
        private float _currentStretchAmplitude;
        private bool _isShaking;

        /// <summary>
        /// Gets whether a grab shake impulse is currently active.
        /// </summary>
        public bool IsShaking => _isShaking;

        /// <summary>
        /// Starts a directional shake as if something was grabbed from inside the jelly.
        /// </summary>
        /// <param name="grabDirectionWorld">
        /// World-space direction of the grab or pull. The first wobble moves toward this direction,
        /// then rebounds and damps back to rest.
        /// </param>
        public void PlayGrabShake(Vector3 grabDirectionWorld)
        {
            PlayGrabShake(grabDirectionWorld, DefaultIntensity);
        }

        /// <summary>
        /// Starts a directional shake as if something was grabbed from inside the jelly.
        /// </summary>
        /// <param name="grabDirectionWorld">
        /// World-space direction of the grab or pull. The first wobble moves toward this direction,
        /// then rebounds and damps back to rest.
        /// </param>
        /// <param name="intensity">Multiplier for the configured shake amplitude and duration.</param>
        public void PlayGrabShake(Vector3 grabDirectionWorld, float intensity)
        {
            if (_targetRenderer == null)
            {
                return;
            }

            _shakeDirection = ResolveWorldShakeDirection(grabDirectionWorld);
            _shakeTimer = 0.0f;
            _currentDurationSeconds = Mathf.Max(MinDurationSeconds, _durationSeconds * Mathf.Max(intensity, MinDurationSeconds));
            _currentAmplitude = _amplitude * Mathf.Max(0.0f, intensity);
            _currentStretchAmplitude = _stretchAmplitude * Mathf.Max(0.0f, intensity);
            _isShaking = true;

            EnsureMaterialProperties();
            ApplyShake(_currentAmplitude, _currentStretchAmplitude);
        }

        /// <summary>
        /// Immediately clears the active grab shake impulse.
        /// </summary>
        public void StopShake()
        {
            if (_targetRenderer == null)
            {
                return;
            }

            _shakeTimer = 0.0f;
            _isShaking = false;
            EnsureMaterialProperties();
            ApplyShake(0.0f, 0.0f);
        }

        private void Reset()
        {
            _targetRenderer = GetComponentInChildren<Renderer>();
        }

        private void Awake()
        {
            if (_targetRenderer == null)
            {
                Debug.LogError("[JellyShakeController] Target renderer is not assigned.", this);
                enabled = false;
                return;
            }

            EnsureMaterialProperties();
            ApplyShake(0.0f, 0.0f);
        }

        private void Update()
        {
            if (!_isShaking)
            {
                return;
            }

            _shakeTimer += Time.deltaTime;

            if (_shakeTimer >= _currentDurationSeconds)
            {
                StopShake();
                return;
            }

            float normalizedTime = _shakeTimer / _currentDurationSeconds;
            float envelope = Mathf.Exp(-_damping * normalizedTime);
            float phase = _shakeTimer * _frequency * TwoPi;
            float amount = Mathf.Cos(phase) * envelope * _currentAmplitude;
            float stretchAmount = Mathf.Cos(phase * 0.75f) * envelope * _currentStretchAmplitude;

            ApplyShake(amount, stretchAmount);
        }

        private Vector3 ResolveWorldShakeDirection(Vector3 grabDirectionWorld)
        {
            Vector3 shakeDirection = grabDirectionWorld;
            shakeDirection.y = 0.0f;

            if (shakeDirection.sqrMagnitude < MinDirectionLengthSquared)
            {
                shakeDirection = transform.forward;
                shakeDirection.y = 0.0f;
            }

            if (shakeDirection.sqrMagnitude < MinDirectionLengthSquared)
            {
                shakeDirection = Vector3.forward;
            }

            return shakeDirection.normalized;
        }

        private void ApplyShake(float amount, float stretchAmount)
        {
            Bounds bounds = _targetRenderer.bounds;
            float radius = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), MinBoundsHeight);

            _targetRenderer.GetPropertyBlock(_materialProperties);
            _materialProperties.SetFloat(BaseYId, bounds.min.y);
            _materialProperties.SetFloat(JellyHeightId, Mathf.Max(bounds.size.y, MinBoundsHeight));
            _materialProperties.SetVector(JellyCenterWorldId, bounds.center);
            _materialProperties.SetFloat(JellyRadiusWorldId, radius);
            _materialProperties.SetFloat(UseRendererBoundsId, 1.0f);
            _materialProperties.SetFloat(GrabShakeAmountId, amount);
            _materialProperties.SetFloat(GrabStretchAmountId, stretchAmount);
            _materialProperties.SetVector(ShakeDirectionId, _shakeDirection);
            _targetRenderer.SetPropertyBlock(_materialProperties);
        }

        private void EnsureMaterialProperties()
        {
            if (_materialProperties != null)
            {
                return;
            }

            _materialProperties = new MaterialPropertyBlock();
        }
    }
}
