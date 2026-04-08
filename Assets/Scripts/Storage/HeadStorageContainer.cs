using OrcFarm.Carry;
using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Storage
{
    /// <summary>
    /// A world-placed storage container that stores and retrieves harvested heads
    /// through the existing interaction flow.
    ///
    /// Interaction behaviour:
    ///   - Player carrying a head  → stores it (adds to container).
    ///   - Player empty-handed, container has heads → retrieves one (last stored first).
    ///   - Player empty-handed, container empty → no-op; container not interactable.
    ///
    /// Stored heads are parked under <see cref="_contentsRoot"/> with physics and
    /// collision already disabled (they retain the carry state set by the carry system).
    /// <c>_contentsRoot.childCount</c> is the live stored count; no separate list needed.
    ///
    /// Retrieval order: last stored, first retrieved (LIFO). The most recently stored
    /// head is always the last child of <c>_contentsRoot</c> and is retrieved first.
    ///
    /// Setup:
    ///   - Assign the player's <see cref="CarryController"/> to <c>_carryController</c>.
    ///   - Assign a child Transform as <c>_contentsRoot</c>; position it inside or below
    ///     the container mesh so parked heads are not visible in the world.
    ///   - The GameObject needs a non-trigger Collider for the InteractionDetector to detect it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HeadStorageContainer : MonoBehaviour, IInteractable
    {
        [SerializeField] private CarryController _carryController;
        [SerializeField] private Transform       _contentsRoot;

        /// <summary>
        /// Number of harvested heads currently parked in this container.
        /// Derived directly from <c>_contentsRoot.childCount</c> so it stays accurate
        /// whenever a head is re-parented in or out by the carry system.
        /// </summary>
        public int StoredCount => _contentsRoot != null ? _contentsRoot.childCount : 0;

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// True while the player is carrying (can store) or while the container holds at
        /// least one head (can retrieve). False when empty and empty-handed — the container
        /// is then invisible to the <see cref="InteractionDetector"/>.
        /// Short-circuit on <c>enabled</c> stays safe when the component is disabled due to
        /// a missing inspector assignment (guards null _carryController / _contentsRoot).
        /// </remarks>
        public bool CanInteract => enabled &&
            (_carryController.IsCarrying || _contentsRoot.childCount > 0);

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_carryController.IsCarrying)
            {
                if (_carryController.TryStore(_contentsRoot))
                    LogStored();
            }
            else if (_contentsRoot.childCount > 0)
            {
                Retrieve();
            }
            // empty + empty-handed: do nothing
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_carryController == null)
            {
                Debug.LogError(
                    $"[HeadStorageContainer] CarryController not assigned on '{gameObject.name}'.", this);
                enabled = false;
                return;
            }

            if (_contentsRoot == null)
            {
                Debug.LogError(
                    $"[HeadStorageContainer] ContentsRoot not assigned on '{gameObject.name}'.", this);
                enabled = false;
            }
        }

        // ── Retrieval ──────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the most recently stored head (last child of ContentsRoot) and
        /// hands it directly to the carry system. The head is re-parented from
        /// ContentsRoot to the carry anchor by the existing AttachToAnchor path —
        /// no physics or collision state changes are needed beyond what PickUp already does.
        /// </summary>
        private void Retrieve()
        {
            Transform last = _contentsRoot.GetChild(_contentsRoot.childCount - 1);

            if (!last.TryGetComponent(out HarvestedHead head))
            {
                Debug.LogWarning(
                    $"[HeadStorageContainer '{gameObject.name}'] Last child of ContentsRoot " +
                    "has no HarvestedHead component. Retrieval aborted; head left in storage.", this);
                return;
            }

            _carryController.PickUp(head); // re-parents head to carry anchor; StoredCount decreases automatically
            LogRetrieved();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogStored()
        {
            Debug.Log(
                $"[HeadStorageContainer '{gameObject.name}'] Stored head. Total: {StoredCount}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogRetrieved()
        {
            Debug.Log(
                $"[HeadStorageContainer '{gameObject.name}'] Retrieved head. Remaining: {StoredCount}", this);
        }
    }
}
