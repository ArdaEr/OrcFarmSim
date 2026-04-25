using OrcFarm.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.Player
{
    /// <summary>
    /// Sits on the player and sends Feed / Water / Care actions to the nearest
    /// <see cref="IFarmActionTarget"/> in front of the camera.
    ///
    /// Each Update:
    ///   1. Fires a raycast on <see cref="_farmTileLayerMask"/> up to <see cref="_maxRange"/>.
    ///   2. Resolves the IFarmActionTarget on the hit collider (cached per collider to
    ///      avoid per-frame GetComponent calls — §5.2).
    ///   3. Calls <see cref="IFarmActionTarget.GetActionContext"/> to learn which buttons
    ///      are live this frame (zero allocation — FarmActionContext is a readonly struct).
    ///   4. Routes F / W / C key presses to the corresponding action method.
    ///
    /// <see cref="CurrentContext"/> is read each frame by <see cref="FarmActionPanel"/>
    /// to drive button visibility.
    ///
    /// Setup: assign <c>_camera</c> (player camera) and set <c>_farmTileLayerMask</c>
    /// to the FarmTile layer in the Inspector.
    /// </summary>
    public sealed class FarmFocusDetector : MonoBehaviour
    {
        [Tooltip("Player camera used as the raycast origin and direction.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Layer mask that limits the raycast to farm tile colliders. " +
                 "Set to the FarmTile layer (12) in the Inspector.")]
        [SerializeField] private LayerMask _farmTileLayerMask;

        [Tooltip("Maximum reach of the farm-action raycast in world units.")]
        [Min(0.1f)]
        [SerializeField] private float _maxRange = 3f;

        [Tooltip("Base color tint applied to the focused tile's Renderer via MaterialPropertyBlock (§5.5).")]
        [SerializeField] private Color _highlightColor = new Color(0f, 1f, 0.2f, 1f);

        [Tooltip("Emission color applied when the tile material has _EMISSION enabled.")]
        [SerializeField] private Color _emissionColor = new Color(0f, 0.6f, 0f, 1f);

        // ── Cached shader property IDs (URP Lit: _BaseColor / _EmissionColor) ──

        private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ── Runtime state ──────────────────────────────────────────────────────

        private IFarmActionTarget _currentTarget;

        // Cached so TryGetComponent is called only when the hit collider changes (§5.2).
        private Collider _lastHitCollider;

        // Highlight — one renderer tinted at a time; cleared before new one is set.
        private Renderer              _highlightedRenderer;
        private MaterialPropertyBlock _mpb;

        // Original material values cached when a tile is first focused (requirement §6).
        private Color _cachedBaseColor;
        private Color _cachedEmissionColor;
        private bool  _supportsEmission;

        // ── Public read — consumed by FarmActionPanel every frame ──────────────

        /// <summary>The resolved farm-action target under the player's crosshair, or null.</summary>
        public IFarmActionTarget CurrentTarget => _currentTarget;

        /// <summary>
        /// Context snapshot for the current frame. FarmActionPanel reads this to toggle buttons.
        /// Returns <see cref="FarmActionContext.None"/> when no target is focused.
        /// </summary>
        public FarmActionContext CurrentContext { get; private set; }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_camera == null)
            {
                Debug.LogError("[FarmFocusDetector] _camera is not assigned.", this);
                enabled = false;
                return;
            }

            _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            UpdateFocus();

            if (_currentTarget != null)
            {
                CurrentContext = _currentTarget.GetActionContext();
                HandleActionInput();
            }
            else
            {
                CurrentContext = FarmActionContext.None;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void UpdateFocus()
        {
            Transform camTransform = _camera.transform;
            Ray       ray          = new Ray(camTransform.position, camTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRange, _farmTileLayerMask))
            {
                Collider hitCollider = hit.collider;

                // Only call TryGetComponent and update highlight when the collider changes.
                if (hitCollider != _lastHitCollider)
                {
                    ClearHighlight();
                    _lastHitCollider = hitCollider;
                    hitCollider.TryGetComponent(out _currentTarget);

                    if (_currentTarget != null)
                        ApplyHighlight(hitCollider.gameObject);
                }
            }
            else
            {
                if (_lastHitCollider != null)
                    ClearHighlight();

                _lastHitCollider = null;
                _currentTarget   = null;
            }
        }

        private void ApplyHighlight(GameObject target)
        {
            if (!target.TryGetComponent(out _highlightedRenderer))
                return;

            // Cache originals from the shared material before overriding.
            Material mat     = _highlightedRenderer.sharedMaterial;
            _cachedBaseColor = mat.GetColor(BaseColorId);

            // Emission is only applied when the material already has _EMISSION enabled.
            // MaterialPropertyBlock cannot enable shader keywords — that requires modifying
            // the material itself, which would affect all instances (§5.5).
            _supportsEmission    = mat.HasProperty(EmissionColorId) && mat.IsKeywordEnabled("_EMISSION");
            _cachedEmissionColor = _supportsEmission ? mat.GetColor(EmissionColorId) : Color.black;

            _mpb.Clear();
            _mpb.SetColor(BaseColorId, _highlightColor);

            if (_supportsEmission)
                _mpb.SetColor(EmissionColorId, _emissionColor);
            else
                LogNoEmission(_highlightedRenderer.gameObject.name);

            _highlightedRenderer.SetPropertyBlock(_mpb);
        }

        private void ClearHighlight()
        {
            if (_highlightedRenderer == null)
                return;

            _mpb.Clear();
            _mpb.SetColor(BaseColorId, _cachedBaseColor);

            if (_supportsEmission)
                _mpb.SetColor(EmissionColorId, _cachedEmissionColor);

            _highlightedRenderer.SetPropertyBlock(_mpb);
            _highlightedRenderer = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogNoEmission(string rendererName)
        {
            Debug.LogWarning(
                "[FarmFocusDetector] Renderer on '" + rendererName + "' does not support emission " +
                "(_EMISSION keyword not enabled on its material). Emission boost skipped.", this);
        }

        private void HandleActionInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            FarmActionContext ctx = CurrentContext;

            if (ctx.ShowFeed  && kb.fKey.wasPressedThisFrame) _currentTarget.OnFeedAction();
            if (ctx.ShowWater && kb.wKey.wasPressedThisFrame) _currentTarget.OnWaterAction();
            if (ctx.ShowCare  && kb.cKey.wasPressedThisFrame) _currentTarget.OnCareAction();
        }
    }
}
