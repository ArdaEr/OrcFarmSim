using OrcFarm.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.Carry
{
    /// <summary>
    /// Displays the item visual for the currently selected hotbar slot at
    /// <see cref="_hotbarItemAnchor"/> in first-person view.
    ///
    /// One visual prefab per item type is pre-instantiated in Awake, parented to
    /// the anchor with zeroed local transform, and toggled active/inactive when the
    /// selected slot or its count changes (§3.6).
    ///
    /// Carry conflict rules (Bug 1):
    ///   When <see cref="CarryController"/> picks up a world object it calls
    ///   <see cref="HideVisual"/>. A <c>_suppressVisual</c> flag prevents Update from
    ///   re-showing the hotbar visual while the world object is held. The flag clears on
    ///   <see cref="RestoreAfterDrop"/> so the visual reactivates automatically.
    ///
    /// Deselect support (Bug 2):
    ///   Pressing the already-selected number key triggers deselect
    ///   (<see cref="IPlayerInventory.Deselect"/>). When
    ///   <see cref="IPlayerInventory.SelectedSlotIndex"/> is -1 the slot is treated as
    ///   empty and the visual is hidden.
    ///
    /// Hotbar drop (Bug 3):
    ///   G key spawns the selected item as a physical world object
    ///   (<see cref="DroppedHotbarItem"/>) and decrements the slot count. Pressing E on
    ///   the world object adds it back to inventory.
    ///
    /// <see cref="IPlayerInventory"/> is wired by
    /// <c>OrcFarm.App.RootLifetimeScope</c> via <see cref="SetInventory"/> after the
    /// VContainer container is built — the same pattern used by
    /// <see cref="CarryController.SetPool"/>.
    /// </summary>
    public sealed class HotbarItemPresenter : MonoBehaviour
    {
        // ── Visual prefabs ─────────────────────────────────────────────────────

        [Tooltip("Child Transform of the player Camera where hotbar item visuals are parented.")]
        [SerializeField] private Transform _hotbarItemAnchor;

        [Header("Hotbar Item Visuals — assign one prefab per item type")]
        [SerializeField] private GameObject _headSeedVisualPrefab;
        [SerializeField] private GameObject _fertilizerVisualPrefab;
        [SerializeField] private GameObject _feedItemVisualPrefab;
        [SerializeField] private GameObject _legFryVisualPrefab;
        [SerializeField] private GameObject _waterItemVisualPrefab;

        // ── World drop prefabs ─────────────────────────────────────────────────

        [Header("Hotbar Item World Prefabs — assign one DroppedHotbarItem prefab per type")]
        [SerializeField] private GameObject _headSeedWorldPrefab;
        [SerializeField] private GameObject _fertilizerWorldPrefab;
        [SerializeField] private GameObject _feedItemWorldPrefab;
        [SerializeField] private GameObject _legFryWorldPrefab;
        [SerializeField] private GameObject _waterItemWorldPrefab;

        [Header("Drop settings")]
        [Tooltip("Pool slots pre-warmed per item type.")]
        [Min(1)]
        [SerializeField] private int _dropPoolSize = 5;

        [Tooltip("Metres in front of the player where the dropped item appears.")]
        [SerializeField] private float _dropForwardOffset = 1.5f;

        [Tooltip("Metres above the player origin added to the drop spawn position.")]
        [SerializeField] private float _dropHeightOffset = 0.5f;

        [Tooltip("Forward speed applied to the dropped item on launch (m/s).")]
        [SerializeField] private float _dropForwardSpeed = 3f;

        [Tooltip("Downward speed component applied on launch (m/s). Gives a slight arc.")]
        [SerializeField] private float _dropDownSpeed = 1f;

        // ── Runtime state ──────────────────────────────────────────────────────

        private IPlayerInventory _inventory;
        private CarryController  _carry;
        private System.Action    _inventoryFullCallback;

        // Visual instances (display-only, parented to anchor).
        private GameObject _headSeedInstance;
        private GameObject _fertilizerInstance;
        private GameObject _feedItemInstance;
        private GameObject _legFryInstance;
        private GameObject _waterItemInstance;
        private GameObject _activeVisual;

        // Drop pools — one array per item type.
        private DroppedHotbarItem[] _headSeedDropPool;
        private DroppedHotbarItem[] _fertilizerDropPool;
        private DroppedHotbarItem[] _feedItemDropPool;
        private DroppedHotbarItem[] _legFryDropPool;
        private DroppedHotbarItem[] _waterItemDropPool;

        // Cache — rebuilt only when slot content changes (§3.1 — no per-frame alloc).
        private int      _lastSelectedIndex = -1;
        private ItemType _lastItemType      = ItemType.None;
        private int      _lastCount;

        // Bug 1: true while a world object (HarvestedHead/Leg) is in the carry slot.
        // Prevents Update from re-showing the hotbar visual while carrying.
        private bool _suppressVisual;

#if UNITY_EDITOR
        private bool _loggedMissingInventory;
#endif

        // G-key InputAction — mirrors Q-key pattern in CarryController.
        private readonly InputAction _dropHotbarAction =
            new InputAction("DropHotbarItem", InputActionType.Button);

        // ── Initialization ─────────────────────────────────────────────────────

        /// <summary>
        /// Provides the inventory reference and warms the drop pools.
        /// Called by RootLifetimeScope after the VContainer container is built.
        /// </summary>
        public void SetInventory(IPlayerInventory inventory)
        {
            _inventory = inventory ?? throw new System.ArgumentNullException(nameof(inventory));
            WarmDropPools();
            if (_inventoryFullCallback != null)
                ApplyCallbackToPools();
        }

        /// <summary>
        /// Sets the callback invoked when a player tries to pick up a dropped item
        /// but the inventory has no space. Called by RootLifetimeScope after SetInventory.
        /// </summary>
        public void SetInventoryFullCallback(System.Action callback)
        {
            _inventoryFullCallback = callback;
            if (_headSeedDropPool != null)
                ApplyCallbackToPools();
        }

        private void WarmDropPools()
        {
            _headSeedDropPool   = CreateDropPool(_headSeedWorldPrefab,   ItemType.HeadSeed);
            _fertilizerDropPool = CreateDropPool(_fertilizerWorldPrefab, ItemType.Fertilizer);
            _feedItemDropPool   = CreateDropPool(_feedItemWorldPrefab,   ItemType.FeedItem);
            _legFryDropPool     = CreateDropPool(_legFryWorldPrefab,     ItemType.LegFry);
            _waterItemDropPool  = CreateDropPool(_waterItemWorldPrefab,  ItemType.WaterItem);
        }

        private DroppedHotbarItem[] CreateDropPool(GameObject prefab, ItemType type)
        {
            if (prefab == null)
                return null;

            if (prefab.GetComponent<DroppedHotbarItem>() == null)
            {
                Debug.LogWarning(
                    $"[HotbarItemPresenter] World prefab for {type} has no DroppedHotbarItem " +
                    "component. Drop pool skipped.", this);
                return null;
            }

            var pool = new DroppedHotbarItem[_dropPoolSize];
            for (int i = 0; i < _dropPoolSize; i++)
            {
                GameObject go   = Instantiate(prefab, transform);
                var        item = go.GetComponent<DroppedHotbarItem>();
                item.SetItemType(type);
                item.SetInventory(_inventory);
                go.SetActive(false);
                pool[i] = item;
            }
            return pool;
        }

        private void ApplyCallbackToPools()
        {
            ApplyCallbackToPool(_headSeedDropPool);
            ApplyCallbackToPool(_fertilizerDropPool);
            ApplyCallbackToPool(_feedItemDropPool);
            ApplyCallbackToPool(_legFryDropPool);
            ApplyCallbackToPool(_waterItemDropPool);
        }

        private void ApplyCallbackToPool(DroppedHotbarItem[] pool)
        {
            if (pool == null)
                return;
            for (int i = 0; i < pool.Length; i++)
                pool[i].SetInventoryFullCallback(_inventoryFullCallback);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (!ValidateMandatoryFields())
            {
                enabled = false;
                return;
            }

            _headSeedInstance   = SpawnVisual(_headSeedVisualPrefab);
            _fertilizerInstance = SpawnVisual(_fertilizerVisualPrefab);
            _feedItemInstance   = SpawnVisual(_feedItemVisualPrefab);
            _legFryInstance     = SpawnVisual(_legFryVisualPrefab);
            _waterItemInstance  = SpawnVisual(_waterItemVisualPrefab);

            _dropHotbarAction.AddBinding("<Keyboard>/g");
        }

        private void OnEnable()  => _dropHotbarAction.Enable();
        private void OnDisable() => _dropHotbarAction.Disable();
        private void OnDestroy() => _dropHotbarAction.Dispose();

        private void Update()
        {
            if (_inventory == null)
            {
#if UNITY_EDITOR
                if (!_loggedMissingInventory)
                {
                    _loggedMissingInventory = true;
                    Debug.LogError(
                        "[HotbarItemPresenter] IPlayerInventory not set. " +
                        "Call SetInventory() from RootLifetimeScope.", this);
                }
#endif
                return;
            }

            // G key drop — checked before visual refresh so the slot count change is
            // visible in the same frame the drop fires.
            if (_dropHotbarAction.WasPressedThisFrame())
                TryDropHotbarItem();

            int        selected = _inventory.SelectedSlotIndex;
            HotbarSlot slot     = _inventory.GetSelectedSlot();

            bool indexChanged =
                selected          != _lastSelectedIndex ||
                slot.SlotItemType != _lastItemType      ||
                slot.Count        != _lastCount;

            if (!indexChanged)
                return;

            bool slotIndexChanged = selected != _lastSelectedIndex;

            _lastSelectedIndex = selected;
            _lastItemType      = slot.SlotItemType;
            _lastCount         = slot.Count;

            if (_suppressVisual)
            {
                // Pressing a number key while carrying a harvested item drops it and
                // shows the selected hotbar slot. Count-only changes (e.g. consuming a
                // seed at a farm plot) leave the carried item untouched.
                if (slotIndexChanged)
                {
                    _carry.PhysicalDrop(); // → RestoreAfterDrop clears _suppressVisual
                    _lastSelectedIndex = selected;
                    _lastItemType      = slot.SlotItemType;
                    _lastCount         = slot.Count;
                }
                else
                {
                    return;
                }
            }

            RefreshVisual(slot);
        }

        // ── Carry conflict API — called by CarryController ─────────────────────

        /// <summary>
        /// Deactivates the active hotbar visual and sets the suppress flag so Update
        /// does not re-activate it while a world object is held.
        /// </summary>
        public void HideVisual()
        {
            _suppressVisual = true;

            if (_activeVisual != null)
            {
                _activeVisual.SetActive(false);
                _activeVisual = null;
            }

            // Deselect the hotbar so the slot is cleared while the harvested item is held.
            // This also syncs the cache to -1 so Update sees no mismatch and stays suppressed.
            if (_inventory != null)
            {
                _inventory.Deselect();
                _lastSelectedIndex = -1;
                _lastItemType      = ItemType.None;
                _lastCount         = 0;
            }
        }

        /// <summary>
        /// Clears the suppress flag and invalidates the cache so Update re-evaluates
        /// the selected slot on the next tick and reactivates the hotbar visual if non-empty.
        /// </summary>
        public void RestoreAfterDrop()
        {
            _suppressVisual    = false;
            _lastSelectedIndex = -1;
        }

        // ── Drop logic (G key) ─────────────────────────────────────────────────

        private void TryDropHotbarItem()
        {
            // Req 10: G does nothing while carrying a world object.
            if (_carry.IsCarrying)
                return;

            HotbarSlot slot = _inventory.GetSelectedSlot();

            // Req 11: G does nothing with empty hands or no selection.
            if (slot.IsEmpty)
                return;

            DroppedHotbarItem[] pool = GetDropPool(slot.SlotItemType);
            if (pool == null)
            {
                LogDropPoolMissing(slot.SlotItemType);
                return;
            }

            DroppedHotbarItem item = GetInactiveFromPool(pool);
            if (item == null)
            {
                LogDropPoolExhausted(slot.SlotItemType);
                return;
            }

            Vector3 forward  = transform.forward;
            Vector3 dropPos  = transform.position
                             + forward          * _dropForwardOffset
                             + Vector3.up       * _dropHeightOffset;
            Vector3 velocity = forward * _dropForwardSpeed + Vector3.down * _dropDownSpeed;

            item.Launch(dropPos, velocity);

            _inventory.TryConsumeFromSelectedSlot(1);

            // Req 9: deselect if the slot is now empty.
            if (_inventory.GetSelectedSlot().IsEmpty)
                _inventory.Deselect();
        }

        private DroppedHotbarItem[] GetDropPool(ItemType type)
        {
            if (type == ItemType.HeadSeed)   return _headSeedDropPool;
            if (type == ItemType.Fertilizer) return _fertilizerDropPool;
            if (type == ItemType.FeedItem)   return _feedItemDropPool;
            if (type == ItemType.LegFry)     return _legFryDropPool;
            if (type == ItemType.WaterItem)  return _waterItemDropPool;
            return null;
        }

        private static DroppedHotbarItem GetInactiveFromPool(DroppedHotbarItem[] pool)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].gameObject.activeSelf)
                    return pool[i];
            }
            return null;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private bool ValidateMandatoryFields()
        {
            bool ok = true;

            _carry = GetComponent<CarryController>();
            if (_carry == null)
            {
                Debug.LogError(
                    "[HotbarItemPresenter] No CarryController found on this GameObject. " +
                    "Both components must be on the same GameObject.", this);
                ok = false;
            }

            if (_hotbarItemAnchor == null)
            {
                Debug.LogError("[HotbarItemPresenter] _hotbarItemAnchor is not assigned.", this);
                ok = false;
            }
            if (_headSeedVisualPrefab == null)
            {
                Debug.LogError("[HotbarItemPresenter] _headSeedVisualPrefab is not assigned.", this);
                ok = false;
            }
            if (_fertilizerVisualPrefab == null)
            {
                Debug.LogError("[HotbarItemPresenter] _fertilizerVisualPrefab is not assigned.", this);
                ok = false;
            }
            if (_feedItemVisualPrefab == null)
            {
                Debug.LogError("[HotbarItemPresenter] _feedItemVisualPrefab is not assigned.", this);
                ok = false;
            }
            if (_legFryVisualPrefab == null)
            {
                Debug.LogError("[HotbarItemPresenter] _legFryVisualPrefab is not assigned.", this);
                ok = false;
            }
            if (_waterItemVisualPrefab == null)
            {
                Debug.LogError("[HotbarItemPresenter] _waterItemVisualPrefab is not assigned.", this);
                ok = false;
            }

            // World prefabs are optional — drop feature is silently disabled per type if unassigned.
            if (_headSeedWorldPrefab   == null) LogMissingWorldPrefab(ItemType.HeadSeed);
            if (_fertilizerWorldPrefab == null) LogMissingWorldPrefab(ItemType.Fertilizer);
            if (_feedItemWorldPrefab   == null) LogMissingWorldPrefab(ItemType.FeedItem);
            if (_legFryWorldPrefab     == null) LogMissingWorldPrefab(ItemType.LegFry);
            if (_waterItemWorldPrefab  == null) LogMissingWorldPrefab(ItemType.WaterItem);

            return ok;
        }

        private GameObject SpawnVisual(GameObject prefab)
        {
            GameObject instance = Instantiate(prefab, _hotbarItemAnchor);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(false);
            return instance;
        }

        private void RefreshVisual(HotbarSlot slot)
        {
            if (_activeVisual != null)
            {
                _activeVisual.SetActive(false);
                _activeVisual = null;
            }

            if (slot.IsEmpty)
                return;

            GameObject next = GetVisualInstance(slot.SlotItemType);
            if (next == null)
                return;

            next.SetActive(true);
            _activeVisual = next;
        }

        private GameObject GetVisualInstance(ItemType type)
        {
            if (type == ItemType.HeadSeed)   return _headSeedInstance;
            if (type == ItemType.Fertilizer) return _fertilizerInstance;
            if (type == ItemType.FeedItem)   return _feedItemInstance;
            if (type == ItemType.LegFry)     return _legFryInstance;
            if (type == ItemType.WaterItem)  return _waterItemInstance;
            return null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogMissingWorldPrefab(ItemType type)
        {
            Debug.LogWarning(
                $"[HotbarItemPresenter] World prefab for {type} is not assigned. " +
                "G-key drop is disabled for this item type.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogDropPoolMissing(ItemType type)
        {
            Debug.LogWarning(
                $"[HotbarItemPresenter] No drop pool for {type}. " +
                "Assign the world prefab in the Inspector.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogDropPoolExhausted(ItemType type)
        {
            Debug.LogWarning(
                $"[HotbarItemPresenter] Drop pool exhausted for {type}. " +
                "Increase _dropPoolSize in the Inspector.", this);
        }
    }
}
