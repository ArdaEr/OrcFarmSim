using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// World-object component for a bag of leg fry.
    /// The <see cref="Tier"/> is set in the inspector on the prefab variant and
    /// determines fish count and base quality when the bag is used to stock a
    /// <see cref="LegPond"/>.
    ///
    /// Place this component on the LegFryWorld prefab. Assign the carrying player's
    /// <see cref="LegFryCarrySlot"/> to pick-up interactions in a later task.
    /// </summary>
    public sealed class LegFryItem : MonoBehaviour
    {
        [Tooltip("Size class of this fry batch. Set on each prefab variant.")]
        [SerializeField] private LegFryTier _tier;

        /// <summary>Size class of this fry batch; determines fish count and base quality.</summary>
        public LegFryTier Tier => _tier;
    }
}
