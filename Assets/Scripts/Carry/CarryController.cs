using OrcFarm.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.Carry
{
    /// <summary>
    /// Manages carrying exactly one world item at a time — either a
    /// <see cref="HarvestedHead"/> or a <see cref="HarvestedLeg"/>.
    ///
    /// Each item type has its own carry anchor (<see cref="_headCarryAnchor"/>,
    /// <see cref="_legCarryAnchor"/>), positioned freely as children of the player
    /// in the editor. Carried items are parented to their anchor and zeroed locally,
    /// so repositioning is done by moving the anchor Transform — no code changes needed.
    ///
    /// Press Q (keyboard) or Left Shoulder (gamepad) to drop the currently carried item.
    /// Picking up a second item while already carrying one drops the first automatically.
    ///
    /// Assembly-station consumption uses <see cref="SilentReturn"/> to return the head
    /// to the pool without any physics drop.
    ///
    /// The <see cref="IHarvestedHeadPool"/> reference is set by the composition root
    /// via <see cref="SetPool"/> after the VContainer scope is built.
    ///
    /// MonoBehaviour justification: needs Unity lifecycle to manage the InputAction
    /// and to respond to per-frame drop input.
    /// </summary>
    public sealed class CarryController : MonoBehaviour, ICarryController
    {
        [Tooltip("Child Transform positioned where a carried head should appear in first-person view.")]
        [SerializeField] private Transform _headCarryAnchor;

        [Tooltip("Child Transform positioned where a carried leg should appear in first-person view.")]
        [SerializeField] private Transform _legCarryAnchor;

        [Tooltip("Metres in front of the player where a dropped item is placed.")]
        [SerializeField] private float _dropForwardOffset = 1.2f;

        [Tooltip("Metres above the player origin added to the drop position.")]
        [SerializeField] private float _dropHeightOffset = 0.5f;

        [Tooltip("Impulse magnitude applied to a dropped item (m/s).")]
        [SerializeField] private float _dropImpulseStrength = 2f;

        private readonly InputAction _dropAction =
            new InputAction("Drop", InputActionType.Button);

        private HarvestedHead      _carried;
        private HarvestedLeg       _carriedLeg;
        private IHarvestedHeadPool  _pool;
        private HotbarItemPresenter _hotbarPresenter;

        /// <inheritdoc/>
        public bool IsCarrying    => _carried != null || _carriedLeg != null;

        /// <inheritdoc/>
        public bool IsCarryingLeg => _carriedLeg != null;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _dropAction.AddBinding("<Keyboard>/q");
            _dropAction.AddBinding("<Gamepad>/leftShoulder");

            if (_headCarryAnchor == null)
                Debug.LogError(
                    $"[CarryController] _headCarryAnchor not assigned on '{gameObject.name}'.", this);

            if (_legCarryAnchor == null)
                Debug.LogError(
                    $"[CarryController] _legCarryAnchor not assigned on '{gameObject.name}'.", this);

            _hotbarPresenter = GetComponent<HotbarItemPresenter>();
        }

        private void OnEnable()  => _dropAction.Enable();
        private void OnDisable() => _dropAction.Disable();
        private void OnDestroy() => _dropAction.Dispose();

        // Zero allocations: bool check only (§3.1).
        private void Update()
        {
            if (IsCarrying && _dropAction.WasPressedThisFrame())
                PhysicalDrop();
        }

        // ── Initialization ─────────────────────────────────────────────────────

        /// <summary>
        /// Provides the object pool used for <see cref="SilentReturn"/>.
        /// Called by <see cref="OrcFarm.App.RootLifetimeScope"/> after the VContainer
        /// scope is built, before any gameplay Update runs.
        /// </summary>
        public void SetPool(IHarvestedHeadPool pool)
        {
            _pool = pool ?? throw new System.ArgumentNullException(nameof(pool));
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Picks up <paramref name="head"/>, parenting it to <see cref="_headCarryAnchor"/>.
        /// If anything is already being carried it is physically dropped first.
        /// Called by <see cref="HarvestedHead.OnInteract"/>; safe to call directly.
        /// </summary>
        public void PickUp(HarvestedHead head)
        {
            if (IsCarrying)
                PhysicalDrop();

            _carried = head;
            _carried.AttachToAnchor(_headCarryAnchor);
            _hotbarPresenter?.HideVisual();
        }

        /// <summary>
        /// Picks up <paramref name="leg"/>, parenting it to <see cref="_legCarryAnchor"/>.
        /// If anything is already being carried it is physically dropped first.
        /// Called by <see cref="HarvestedLeg.OnInteract"/> and by LegPond on harvest.
        /// </summary>
        public void PickUpLeg(HarvestedLeg leg)
        {
            if (IsCarrying)
                PhysicalDrop();

            _carriedLeg = leg;
            _carriedLeg.AttachToAnchor(_legCarryAnchor);
            _hotbarPresenter?.HideVisual();
        }

        /// <summary>
        /// Transfers the currently carried head into storage by parenting it under
        /// <paramref name="storageRoot"/> with physics and collision already disabled.
        /// Returns true if a head was transferred.
        /// Returns false if nothing is being carried, or if a leg is being carried.
        /// Called by storage containers; do not call from other gameplay code.
        /// </summary>
        public bool TryStore(Transform storageRoot)
        {
            if (_carried == null)
                return false;

            HarvestedHead head = _carried;
            _carried = null;          // clear before StoreInto in case of re-entrancy
            head.StoreInto(storageRoot);
            _hotbarPresenter?.RestoreAfterDrop();
            return true;
        }

        /// <summary>
        /// Transfers the currently carried leg into storage by parenting it under
        /// <paramref name="storageRoot"/> with physics and collision already disabled.
        /// Returns true if a leg was transferred. Returns false if carrying a head or nothing.
        /// Called by storage containers; do not call from other gameplay code.
        /// </summary>
        public bool TryStoreLeg(Transform storageRoot)
        {
            if (_carriedLeg == null)
                return false;

            HarvestedLeg leg = _carriedLeg;
            _carriedLeg = null;     // clear before StoreInto in case of re-entrancy
            leg.StoreInto(storageRoot);
            _hotbarPresenter?.RestoreAfterDrop();
            return true;
        }

        /// <summary>
        /// Detaches the carried item from its anchor, re-enables physics and collision,
        /// places it slightly in front of the player, and applies a small random horizontal
        /// impulse so it rolls naturally. The item stays in the world — it is NOT returned
        /// to any pool.
        ///
        /// No-op if nothing is being carried.
        /// </summary>
        public void PhysicalDrop()
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

            if (_carried != null)
            {
                HarvestedHead head = _carried;
                _carried = null;
                head.DetachToWorld(dropPosition, impulse);
            }
            else if (_carriedLeg != null)
            {
                HarvestedLeg leg = _carriedLeg;
                _carriedLeg = null;
                leg.DetachToWorld(dropPosition, impulse);
            }

            _hotbarPresenter?.RestoreAfterDrop();
        }

        /// <summary>
        /// Immediately returns the carried head to the object pool, deactivating it with
        /// no physics drop or visual effect.
        /// If a leg is carried instead, falls back to <see cref="PhysicalDrop"/> because
        /// no leg pool exists in this assembly yet.
        ///
        /// Call this for assembly-station consumption only.
        /// For the player's manual Q-key drop, use <see cref="PhysicalDrop"/> instead.
        ///
        /// No-op if nothing is being carried.
        /// </summary>
        public void SilentReturn()
        {
            if (_carried != null)
            {
                GameObject go = _carried.gameObject;
                _carried = null;
                _pool.Return(go);
                _hotbarPresenter?.RestoreAfterDrop();
            }
            else if (_carriedLeg != null)
            {
                // No leg pool in this assembly — drop it physically so it stays live.
                PhysicalDrop();
            }
        }
    }
}
