using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Tracks the single <see cref="LegFryItem"/> the player is currently carrying.
    ///
    /// Attach to the Player root GameObject. Assign this component as a serialized
    /// field on <see cref="LegPond"/> so the pond can query and consume the carried item.
    ///
    /// For Play Mode testing, drag a scene <see cref="LegFryItem"/> into the
    /// <c>_carried</c> inspector slot. In a later task, <see cref="Hold"/> will be
    /// called by the LegFryItem pick-up interaction.
    /// </summary>
    public sealed class LegFryCarrySlot : MonoBehaviour
    {
        [Tooltip("Currently held LegFryItem. Assign a scene LegFryItem for Play Mode testing.")]
        [SerializeField] private LegFryItem _carried;

        /// <summary>The currently carried fry item, or null when the slot is empty.</summary>
        public LegFryItem CarriedItem => _carried;

        /// <summary>True while any <see cref="LegFryItem"/> occupies the carry slot.</summary>
        public bool IsCarrying => _carried != null;

        /// <summary>
        /// Places <paramref name="item"/> into the carry slot.
        /// If a different item is already held, the previous one is deactivated first.
        /// </summary>
        public void Hold(LegFryItem item)
        {
            if (item == null)
                throw new System.ArgumentNullException(nameof(item));

            if (_carried != null && _carried != item)
                _carried.gameObject.SetActive(false);

            _carried = item;
        }

        /// <summary>
        /// Deactivates the carried item and clears the slot.
        /// Called by <see cref="LegPond"/> on successful stocking.
        /// No-op if the slot is already empty.
        /// </summary>
        public void Consume()
        {
            if (_carried == null)
                return;

            _carried.gameObject.SetActive(false);
            _carried = null;
        }
    }
}
