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

        // ── Runtime state ──────────────────────────────────────────────────────

        private IFarmActionTarget _currentTarget;

        // Cached so TryGetComponent is called only when the hit collider changes (§5.2).
        private Collider _lastHitCollider;

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

                // Only call TryGetComponent when the collider changes.
                if (hitCollider != _lastHitCollider)
                {
                    _lastHitCollider = hitCollider;
                    hitCollider.TryGetComponent(out _currentTarget);
                }
                // If same collider as last frame, _currentTarget is still valid.
            }
            else
            {
                _lastHitCollider = null;
                _currentTarget   = null;
            }
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
