using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Workers
{
    /// <summary>
    /// Placed on the AssembledOrc GameObject alongside <see cref="HaulerWorker"/>.
    ///
    /// Implements <see cref="IInteractable"/> (E key) and <see cref="ISecondaryInteractable"/>
    /// (Q key) to give the player two choices after assembly:
    ///
    ///   E — Keep as worker: orc walks to wait point and begins the hauler loop.
    ///   Q — Store for sale: orc walks to the holding pen and stands there.
    ///       The E prompt reappears when the player visits the pen, allowing a
    ///       stored orc to be Kept later.
    ///
    /// Scene setup: assign <see cref="OrcHoldingPen"/> in the Inspector.
    /// HaulerWorker is found automatically via GetComponent.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(HaulerWorker))]
    public sealed class KeepInteractable : MonoBehaviour, IInteractable, ISecondaryInteractable
    {
        [Tooltip("The orc holding pen. Required for the Store option (Q key).")]
        [SerializeField] private OrcHoldingPen _pen;

        private HaulerWorker _hauler;

        // ── IInteractable — E key ──────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// True for a freshly assembled orc and for an orc that has arrived in the pen.
        /// False while the orc is walking to the pen (not yet interactable in transit).
        /// False after Keep has been chosen.
        /// </remarks>
        public bool CanInteract =>
            enabled && _hauler != null && !_hauler.IsKept &&
            (!_hauler.IsStored || _hauler.IsInPen);

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (!CanInteract)
                return;

            // If keeping from the pen, release the spot so others can use it.
            if (_hauler.IsStored)
                _pen?.Release(_hauler);

            _hauler.Keep();
            enabled = false; // suppress all prompts — this orc is now a permanent worker
        }

        // ── ISecondaryInteractable — Q key ─────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>True only for a freshly assembled orc that has not yet been stored or kept.</remarks>
        public bool CanSecondaryInteract =>
            enabled && _hauler != null && !_hauler.IsKept && !_hauler.IsStored && _pen != null;

        /// <inheritdoc/>
        public void OnSecondaryInteract()
        {
            if (!CanSecondaryInteract)
                return;

            if (!_pen.TryStore(_hauler))
            {
                Debug.LogWarning(
                    $"[KeepInteractable '{gameObject.name}'] Pen is full — cannot store orc.", this);
                return;
            }

            // Do NOT disable: the orc will show E: Keep once it arrives in the pen.
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects the scene-side <see cref="OrcHoldingPen"/> reference.
        /// Must be called by the spawner immediately after Instantiate because
        /// the pen is a scene object and cannot be pre-assigned on a prefab.
        /// </summary>
        public void Initialize(OrcHoldingPen pen)
        {
            _pen = pen;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _hauler = GetComponent<HaulerWorker>();
            if (_hauler == null)
                throw new System.InvalidOperationException(
                    $"[KeepInteractable '{gameObject.name}'] HaulerWorker not found on this GameObject.");
        }
    }
}
