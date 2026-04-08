using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.Carry
{
    /// <summary>
    /// Manages carrying exactly one <see cref="HarvestedHead"/> at a time.
    ///
    /// Attach to the player root alongside other player components.
    /// Requires a carry anchor Transform child (e.g. at chest/hand height) assigned
    /// in the inspector — the carried head is parented to this anchor.
    ///
    /// Press Q (keyboard) or Left Shoulder (gamepad) to drop the currently carried head.
    /// Picking up a second head while already carrying one drops the first automatically.
    ///
    /// MonoBehaviour justification: needs Unity lifecycle to manage the InputAction
    /// and to respond to per-frame drop input.
    /// </summary>
    public sealed class CarryController : MonoBehaviour
    {
        [SerializeField] private Transform _carryAnchor;

        [Tooltip("Metres in front of the player where a dropped head is placed.")]
        [SerializeField] private float _dropForwardOffset = 1.2f;

        [Tooltip("Metres above the player origin added to the drop position.")]
        [SerializeField] private float _dropHeightOffset = 0.5f;

        [Tooltip("Impulse magnitude applied to a dropped head (m/s).")]
        [SerializeField] private float _dropImpulseStrength = 2f;

        private readonly InputAction _dropAction =
            new InputAction("Drop", InputActionType.Button);

        private HarvestedHead _carried;

        /// <summary>True while a <see cref="HarvestedHead"/> is attached to the carry anchor.</summary>
        public bool IsCarrying => _carried != null;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _dropAction.AddBinding("<Keyboard>/q");
            _dropAction.AddBinding("<Gamepad>/leftShoulder");

            if (_carryAnchor == null)
            {
                Debug.LogError(
                    $"[CarryController] _carryAnchor not assigned on '{gameObject.name}'. Carry disabled.", this);
                enabled = false;
            }
        }

        private void OnEnable()  => _dropAction.Enable();
        private void OnDisable() => _dropAction.Disable();
        private void OnDestroy() => _dropAction.Dispose();

        // Zero allocations: bool check only (§3.1).
        private void Update()
        {
            if (IsCarrying && _dropAction.WasPressedThisFrame())
                Drop();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Picks up <paramref name="head"/>, attaching it to the carry anchor.
        /// If a head is already being carried it is dropped first.
        /// Called by <see cref="HarvestedHead.OnInteract"/>; safe to call directly.
        /// </summary>
        public void PickUp(HarvestedHead head)
        {
            if (IsCarrying)
                Drop();

            _carried = head;
            _carried.AttachToAnchor(_carryAnchor);
        }

        /// <summary>
        /// Transfers the currently carried head into storage by parenting it under
        /// <paramref name="storageRoot"/> with physics and collision already disabled.
        /// Clears the carry reference so <see cref="IsCarrying"/> becomes false.
        /// Returns true if a head was transferred, false if nothing was being carried.
        /// Called by storage containers; do not call from other gameplay code.
        /// </summary>
        public bool TryStore(Transform storageRoot)
        {
            if (!IsCarrying)
                return false;

            HarvestedHead head = _carried;
            _carried = null;          // clear before StoreInto in case of re-entrancy
            head.StoreInto(storageRoot);
            return true;
        }

        /// <summary>
        /// Drops the currently carried head slightly in front of the player with a small
        /// random horizontal impulse. No-op if nothing is being carried.
        /// </summary>
        public void Drop()
        {
            if (!IsCarrying)
                return;

            Vector3 dropPosition = transform.position
                + transform.forward * _dropForwardOffset
                + Vector3.up        * _dropHeightOffset;

            // Random direction on the XZ plane — always non-zero magnitude.
            float   angle   = Random.Range(0f, Mathf.PI * 2f);
            Vector3 impulse = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                              * _dropImpulseStrength;

            _carried.DetachToWorld(dropPosition, impulse);
            _carried = null;
        }
    }
}
