using UnityEngine;
using OrcFarm.Inventory;

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
    /// Carry conflict rules:
    ///   <see cref="CarryController"/> calls <see cref="HideVisual"/> on every pickup so the
    ///   hotbar visual is suppressed while a world object is held. It calls
    ///   <see cref="RestoreAfterDrop"/> after every drop or store so the hotbar visual
    ///   reactivates on the next Update tick if the selected slot is non-empty.
    ///   When the selected slot changes while a world object is carried, Update calls
    ///   <see cref="CarryController.PhysicalDrop"/> to drop the world object, then
    ///   immediately shows the hotbar visual for the new slot.
    ///
    /// <see cref="IPlayerInventory"/> is wired by
    /// <c>OrcFarm.App.RootLifetimeScope</c> via <see cref="SetInventory"/> after the
    /// VContainer container is built — the same pattern used by
    /// <see cref="CarryController.SetPool"/>.
    /// </summary>
    public sealed class HotbarItemPresenter : MonoBehaviour
    {
        [Tooltip("Child Transform of the player Camera where hotbar item visuals are parented. " +
                 "Position in front of and slightly below centre in first-person view.")]
        [SerializeField] private Transform _hotbarItemAnchor;

        [Header("Hotbar Item Visuals — assign one prefab per item type")]
        [SerializeField] private GameObject _headSeedVisualPrefab;
        [SerializeField] private GameObject _fertilizerVisualPrefab;
        [SerializeField] private GameObject _feedItemVisualPrefab;
        [SerializeField] private GameObject _legFryVisualPrefab;
        [SerializeField] private GameObject _waterItemVisualPrefab;

        private IPlayerInventory _inventory;
        private CarryController  _carry;

        private GameObject _headSeedInstance;
        private GameObject _fertilizerInstance;
        private GameObject _feedItemInstance;
        private GameObject _legFryInstance;
        private GameObject _waterItemInstance;

        private GameObject _activeVisual;

        // -1 forces a full rebuild on the first frame after SetInventory is called.
        private int      _lastSelectedIndex = -1;
        private ItemType _lastItemType      = ItemType.None;
        private int      _lastCount;

#if UNITY_EDITOR
        private bool _loggedMissingInventory;
#endif

        // ── Initialization ─────────────────────────────────────────────────────

        /// <summary>
        /// Provides the inventory reference. Called by RootLifetimeScope after the
        /// VContainer container is built, before the first Update tick.
        /// </summary>
        public void SetInventory(IPlayerInventory inventory)
        {
            _inventory = inventory ?? throw new System.ArgumentNullException(nameof(inventory));
        }

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
        }

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

        // ── Unity lifecycle ────────────────────────────────────────────────────

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

            int        selected = _inventory.SelectedSlotIndex;
            HotbarSlot slot     = _inventory.GetSelectedSlot();

            if (selected          == _lastSelectedIndex &&
                slot.SlotItemType == _lastItemType      &&
                slot.Count        == _lastCount)
                return;

            _lastSelectedIndex = selected;
            _lastItemType      = slot.SlotItemType;
            _lastCount         = slot.Count;

            if (_carry.IsCarrying)
            {
                _carry.PhysicalDrop();
                // Re-sync after drop: PhysicalDrop → RestoreAfterDrop sets _lastSelectedIndex = -1,
                // which would cause a redundant second refresh on the next tick without this.
                _lastSelectedIndex = selected;
                _lastItemType      = slot.SlotItemType;
                _lastCount         = slot.Count;
            }

            RefreshVisual(slot);
        }

        // ── Carry conflict API — called by CarryController ─────────────────────

        /// <summary>
        /// Deactivates the active hotbar visual and syncs the internal cache so
        /// <see cref="Update"/> does not re-activate it while a world object is carried.
        /// </summary>
        public void HideVisual()
        {
            if (_activeVisual != null)
            {
                _activeVisual.SetActive(false);
                _activeVisual = null;
            }

            // Sync cache to current slot so Update sees no change and leaves the visual hidden.
            if (_inventory != null)
            {
                _lastSelectedIndex = _inventory.SelectedSlotIndex;
                HotbarSlot slot    = _inventory.GetSelectedSlot();
                _lastItemType      = slot.SlotItemType;
                _lastCount         = slot.Count;
            }
        }

        /// <summary>
        /// Invalidates the internal cache so <see cref="Update"/> re-evaluates the
        /// selected slot on the next tick and reactivates the hotbar visual if non-empty.
        /// </summary>
        public void RestoreAfterDrop()
        {
            _lastSelectedIndex = -1;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void RefreshVisual(HotbarSlot slot)
        {
            if (_activeVisual != null)
            {
                _activeVisual.SetActive(false);
                _activeVisual = null;
            }

            if (slot.IsEmpty)
                return;

            GameObject next = GetInstanceForType(slot.SlotItemType);
            if (next == null)
                return;

            next.SetActive(true);
            _activeVisual = next;
        }

        private GameObject GetInstanceForType(ItemType type)
        {
            if (type == ItemType.HeadSeed)   return _headSeedInstance;
            if (type == ItemType.Fertilizer) return _fertilizerInstance;
            if (type == ItemType.FeedItem)   return _feedItemInstance;
            if (type == ItemType.LegFry)     return _legFryInstance;
            if (type == ItemType.WaterItem)  return _waterItemInstance;
            return null;
        }
    }
}
