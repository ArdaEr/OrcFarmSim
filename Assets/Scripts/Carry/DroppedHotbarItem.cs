using OrcFarm.Interaction;
using OrcFarm.Inventory;
using UnityEngine;

namespace OrcFarm.Carry
{
    /// <summary>
    /// A hotbar item that has been physically dropped into the world.
    ///
    /// Implements <see cref="IInteractable"/> so the player can press E to pick it
    /// back up. Picking up adds one unit of <see cref="ItemType"/> to the player's
    /// inventory via <see cref="IPlayerInventory.TryAdd"/>. If inventory is full the
    /// item stays in the world and a warning callback fires.
    ///
    /// Lifecycle: inactive in pool → <see cref="Launch"/> activates it → player presses E
    /// → <see cref="OnInteract"/> → <see cref="ReturnToPool"/> deactivates it.
    ///
    /// Layer is forced to "Carriable" in Awake so the physics collision matrix
    /// applies correctly without requiring prefab edits (§5.9).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class DroppedHotbarItem : MonoBehaviour, IInteractable
    {
        private static readonly string CarriableLayerName = "Carriable";

        private Rigidbody        _rb;
        private Collider         _col;
        private IPlayerInventory _inventory;
        private System.Action    _onInventoryFull;
        private bool             _isLive; // true while visible in the world

        /// <summary>The item type this world object represents.</summary>
        public ItemType ItemType { get; private set; }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool CanInteract => enabled && _isLive;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (!_isLive || _inventory == null)
                return;

            if (!_inventory.TryAdd(ItemType))
            {
                _onInventoryFull?.Invoke();
                return;
            }

            ReturnToPool();
        }

        // ── Initialization ─────────────────────────────────────────────────────

        /// <summary>
        /// Sets the item type this instance represents. Called once after pool creation.
        /// </summary>
        public void SetItemType(ItemType type) => ItemType = type;

        /// <summary>Sets the inventory used for pickup. Called by HotbarItemPresenter.</summary>
        public void SetInventory(IPlayerInventory inventory) => _inventory = inventory;

        /// <summary>
        /// Sets the callback fired when the player tries to pick up but inventory is full.
        /// </summary>
        public void SetInventoryFullCallback(System.Action callback) => _onInventoryFull = callback;

        // ── Pool API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Activates this item, positions it, and applies a launch velocity.
        /// No-op if the item is already live.
        /// </summary>
        public void Launch(Vector3 position, Vector3 velocity)
        {
            if (_isLive)
                return;

            _isLive = true;

            transform.SetParent(null);
            transform.position = position;

            _rb.isKinematic     = false;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _col.enabled        = true;

            gameObject.SetActive(true);

            _rb.AddForce(velocity, ForceMode.VelocityChange);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();

            int carriableLayer = LayerMask.NameToLayer(CarriableLayerName);
            if (carriableLayer < 0)
            {
                Debug.LogError(
                    "[DroppedHotbarItem] Layer 'Carriable' not found in project layers. " +
                    "Add it under Edit > Project Settings > Tags and Layers.", this);
            }
            else
            {
                gameObject.layer = carriableLayer;
            }

            _isLive = false;
            gameObject.SetActive(false);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void ReturnToPool()
        {
            _isLive = false;

            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
            _col.enabled        = false;

            gameObject.SetActive(false);
        }
    }
}
