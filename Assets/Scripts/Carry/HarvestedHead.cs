using OrcFarm.Core;
using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Carry
{
    /// <summary>
    /// A harvested head in the world. Implements <see cref="IInteractable"/> so the player
    /// can pick it up through the existing interaction flow, and <see cref="IPoolable"/>
    /// so the pool can cleanly reset it between uses (§3.5).
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
    /// The <see cref="ICarryController"/> reference is set via <see cref="Initialize"/> after
    /// the pool retrieves this instance — not via the inspector.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestedHead : MonoBehaviour, IInteractable, IPoolable
    {
        private Rigidbody        _rb;
        private Collider         _col;
        private bool             _isCarried;
        private ICarryController _carry;

        // ── Quality ────────────────────────────────────────────────────────────

        /// <summary>Quality assigned when this head was harvested. Reset to Low on pool return.</summary>
        public OrcQuality Quality { get; private set; }

        /// <summary>Sets the quality. Called by <see cref="HeadFarmTile"/> immediately after pool retrieval.</summary>
        public void SetQuality(OrcQuality quality) => Quality = quality;

        // ── Trait candidate ────────────────────────────────────────────────────

        /// <summary>Influence flags accumulated during this head's growth cycle.</summary>
        public TraitInfluenceFlags InfluenceFlags { get; private set; }

        /// <summary>Trait candidate evaluated at harvest time.</summary>
        public OrcTrait TraitCandidate { get; private set; }

        /// <summary>Stores the influence flags and trait candidate evaluated at harvest.</summary>
        public void SetTrait(TraitInfluenceFlags flags, OrcTrait trait)
        {
            InfluenceFlags = flags;
            TraitCandidate = trait;
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>False while carried or while this component is disabled.</remarks>
        public bool CanInteract => enabled && !_isCarried;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_carry == null)
            {
                Debug.LogWarning(
                    $"[HarvestedHead] Cannot pick up '{gameObject.name}' — ICarryController is null.", this);
                return;
            }

            _carry.PickUp(this);
        }

        // ── IPoolable ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void OnGetFromPool() => ResetState();

        /// <inheritdoc/>
        public void OnReturnToPool() { } // deactivation and re-parenting handled by the pool

        /// <inheritdoc/>
        /// <remarks>
        /// Clears the carry reference, resets physics to the dropped (non-kinematic) state,
        /// and re-enables the collider so the head is interactable when retrieved.
        /// </remarks>
        public void ResetState()
        {
            _isCarried     = false;
            _carry         = null;
            Quality        = OrcQuality.Low;
            InfluenceFlags = TraitInfluenceFlags.None;
            TraitCandidate = OrcTrait.None;

            transform.SetParent(null);

            _rb.isKinematic = false;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _col.enabled = true;
        }

        // ── Initialization ─────────────────────────────────────────────────────

        /// <summary>
        /// Sets the carry controller after the pool retrieves this instance.
        /// Must be called before the player can interact with this head.
        /// </summary>
        public void Initialize(ICarryController carryController)
        {
            _carry = carryController;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
        }

        // ── Called by CarryController only ─────────────────────────────────────

        /// <summary>
        /// Disables physics and collision, then parents this head to the carry anchor.
        /// Do not call this directly — use <see cref="ICarryController.PickUp"/> instead.
        /// </summary>
        internal void AttachToAnchor(Transform anchor)
        {
            _isCarried = true;

            // Disable the collider first.
            // Unity fires OnTriggerExit on the InteractionDetector's sphere when a
            // non-trigger collider is disabled, removing this head from its candidate list.
            _col.enabled = false;

            // Stop any in-progress motion before making kinematic.
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
        /// current disabled-physics state.
        /// Do not call this directly — use <see cref="ICarryController.TryStore"/> instead.
        /// </summary>
        internal void StoreInto(Transform storageRoot)
        {
            transform.SetParent(storageRoot);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Detaches from the carry anchor, re-enables physics, and applies a drop impulse.
        /// Do not call this directly — use <see cref="ICarryController.PhysicalDrop"/> instead.
        /// </summary>
        internal void DetachToWorld(Vector3 position, Vector3 impulse)
        {
            _isCarried = false;

            transform.SetParent(null);
            transform.position = position;

            _rb.isKinematic = false;

            // Re-enable collider at the drop position.
            _col.enabled = true;

            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
