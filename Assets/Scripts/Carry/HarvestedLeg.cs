using OrcFarm.Core;
using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Carry
{
    /// <summary>
    /// A harvested leg in the world. Implements <see cref="IInteractable"/> so the player
    /// can re-pick it up after a drop, and <see cref="IPoolable"/> so the pool can cleanly
    /// reset it between uses (§3.5).
    ///
    /// On harvest, <see cref="LegPond"/> immediately calls <see cref="ICarryController.PickUpLeg"/>
    /// so the leg enters the carry slot without requiring a manual pickup. If the player
    /// then drops it (Q key), it becomes a live world object that can be picked up via
    /// <see cref="OnInteract"/>.
    ///
    /// While carried the Collider is disabled and the Rigidbody is kinematic — same
    /// contract as <see cref="HarvestedHead"/>. Both are restored on drop.
    ///
    /// <see cref="OrcQuality"/> is set by <see cref="LegPond"/> immediately after the
    /// pool returns this instance, before the leg enters carry.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestedLeg : MonoBehaviour, IInteractable, IPoolable
    {
        private Rigidbody        _rb;
        private Collider         _col;
        private bool             _isCarried;
        private ICarryController _carry;

        /// <summary>Quality tier set by the pond at harvest time.</summary>
        public OrcQuality Quality { get; private set; }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool CanInteract => enabled && !_isCarried;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_carry == null)
            {
                Debug.LogWarning(
                    $"[HarvestedLeg] Cannot pick up '{gameObject.name}' — ICarryController is null.", this);
                return;
            }

            _carry.PickUpLeg(this);
        }

        // ── IPoolable ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void OnGetFromPool() => ResetState();

        /// <inheritdoc/>
        public void OnReturnToPool() { }

        /// <inheritdoc/>
        public void ResetState()
        {
            _isCarried = false;
            _carry     = null;
            Quality    = OrcQuality.Low;

            transform.SetParent(null);

            _rb.isKinematic     = false;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _col.enabled = true;
        }

        // ── Initialization ─────────────────────────────────────────────────────

        /// <summary>
        /// Sets the carry controller. Must be called by the pool caller before entering carry.
        /// </summary>
        public void Initialize(ICarryController carryController)
        {
            _carry = carryController;
        }

        /// <summary>
        /// Sets the quality tier. Called by <see cref="OrcFarm.Farming.LegPond"/> after
        /// retrieving this instance from the pool.
        /// </summary>
        public void SetQuality(OrcQuality quality)
        {
            Quality = quality;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
        }

        // ── Called by CarryController only ─────────────────────────────────────

        /// <summary>
        /// Disables physics and collision, then parents this leg to the carry anchor.
        /// Do not call directly — use <see cref="ICarryController.PickUpLeg"/> instead.
        /// </summary>
        internal void AttachToAnchor(Transform anchor)
        {
            _isCarried = true;

            _col.enabled = false;

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
        /// Re-parents this leg under <paramref name="storageRoot"/> while keeping it in its
        /// current disabled-physics state.
        /// Do not call this directly — use <see cref="ICarryController.TryStoreLeg"/> instead.
        /// </summary>
        internal void StoreInto(Transform storageRoot)
        {
            transform.SetParent(storageRoot);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Detaches from the carry anchor, re-enables physics, and applies a drop impulse.
        /// Do not call directly — use <see cref="ICarryController.PhysicalDrop"/> instead.
        /// </summary>
        internal void DetachToWorld(Vector3 position, Vector3 impulse)
        {
            _isCarried = false;

            transform.SetParent(null);
            transform.position = position;

            _rb.isKinematic = false;
            _col.enabled    = true;

            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
