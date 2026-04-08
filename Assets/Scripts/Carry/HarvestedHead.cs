using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Carry
{
    /// <summary>
    /// A harvested head in the world. Implements <see cref="IInteractable"/> so the player
    /// can pick it up through the existing interaction flow.
    ///
    /// While carried the Collider is disabled (preventing physics conflicts with the player
    /// capsule) and the Rigidbody is made kinematic (stopping physics simulation).
    /// Both are restored when the head is dropped.
    ///
    /// Disabling the Collider while inside the <see cref="InteractionDetector"/> sphere causes
    /// Unity's physics engine to fire <c>OnTriggerExit</c>, cleanly removing this object from
    /// the candidate list. Re-enabling it at the drop position fires <c>OnTriggerEnter</c>
    /// if still in range, making the head targetable again.
    ///
    /// Setup: assign the player's <see cref="CarryController"/> in the inspector.
    /// The GameObject also needs a Rigidbody and at least one Collider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestedHead : MonoBehaviour, IInteractable
    {
        [SerializeField] private CarryController _carryController;

        private Rigidbody _rb;
        private Collider  _col;
        private bool      _isCarried;

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>False while carried or while this component is disabled.</remarks>
        public bool CanInteract => enabled && !_isCarried;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_carryController == null)
            {
                Debug.LogWarning(
                    $"[HarvestedHead] Cannot pick up '{gameObject.name}' — CarryController is null.", this);
                return;
            }

            _carryController.PickUp(this);
        }

        // ── Called by FarmPlot only ────────────────────────────────────────────

        /// <summary>
        /// Assigns the CarryController after Instantiate. Must be called before the
        /// player can interact with this head. FarmPlot calls this immediately after
        /// spawning the prefab so the reference is set before Start() runs.
        /// </summary>
        public void Initialize(CarryController carryController)
        {
            _carryController = carryController;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
        }

        private void Start()
        {
            if (_carryController == null)
            {
                Debug.LogWarning(
                    $"[HarvestedHead] CarryController not assigned on '{gameObject.name}'. " +
                    "Call Initialize() or assign in the inspector for pickup to work.", this);
            }
        }

        // ── Called by CarryController only ─────────────────────────────────────

        /// <summary>
        /// Disables physics and collision, then parents this head to the carry anchor.
        /// Do not call this directly — use <see cref="CarryController.PickUp"/> instead.
        /// </summary>
        internal void AttachToAnchor(Transform anchor)
        {
            _isCarried = true;

            // Disable the collider first.
            // Unity fires OnTriggerExit on the InteractionDetector's sphere when a
            // non-trigger collider is disabled, removing this head from its candidate list.
            if (_col != null) _col.enabled = false;

            // Stop any in-progress motion before making kinematic.
            // Guard: setting velocity on an already-kinematic body (e.g. a stored head
            // re-entering carry) is unsupported and produces a Unity warning.
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity  = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = true;

            transform.SetParent(anchor);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Re-parents this head under <paramref name="storageRoot"/> while keeping it in its
        /// current disabled-physics state. <c>_isCarried</c> remains true so
        /// <see cref="CanInteract"/> stays false and the head is invisible to the detector.
        /// Do not call this directly — use <see cref="CarryController.TryStore"/> instead.
        /// </summary>
        internal void StoreInto(Transform storageRoot)
        {
            // Physics already kinematic, collider already disabled from being carried —
            // no state changes needed beyond re-parenting.
            transform.SetParent(storageRoot);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Detaches from the carry anchor, re-enables physics, and applies a drop impulse.
        /// Do not call this directly — use <see cref="CarryController.Drop"/> instead.
        /// </summary>
        internal void DetachToWorld(Vector3 position, Vector3 impulse)
        {
            _isCarried = false;

            transform.SetParent(null);
            transform.position = position;

            // Restore physics before re-enabling the collider so the Rigidbody
            // is ready to respond to the impulse when the collider comes back.
            _rb.isKinematic = false;

            // Re-enable collider at the drop position.
            // If within the InteractionDetector sphere, OnTriggerEnter fires and
            // adds this head back to the candidate list.
            if (_col != null) _col.enabled = true;

            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
