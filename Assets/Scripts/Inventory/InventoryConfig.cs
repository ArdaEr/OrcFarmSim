using UnityEngine;

namespace OrcFarm.Inventory
{
    /// <summary>
    /// Designer-tunable inventory layout. Controls how many hotbar and main-inventory
    /// slots the player has without requiring a code recompile (§6.5).
    ///
    /// Create via: Assets > Create > OrcFarm > Inventory Config
    /// </summary>
    [CreateAssetMenu(menuName = "OrcFarm/Inventory Config", fileName = "InventoryConfig")]
    public sealed class InventoryConfig : ScriptableObject
    {
        [Tooltip("Number of hotbar slots visible at the bottom of the screen.")]
        [Min(1)]
        [SerializeField] private int _hotbarSize = 5;

        [Tooltip("Number of main inventory slots available in the full inventory panel.")]
        [Min(1)]
        [SerializeField] private int _mainInventorySize = 10;

        /// <summary>Number of hotbar slots.</summary>
        public int HotbarSize => _hotbarSize;

        /// <summary>Number of main inventory slots.</summary>
        public int MainInventorySize => _mainInventorySize;

        private void OnValidate()
        {
            _hotbarSize        = Mathf.Max(1, _hotbarSize);
            _mainInventorySize = Mathf.Max(1, _mainInventorySize);
        }

        /// <summary>
        /// Called during game initialization. Throws <see cref="System.InvalidOperationException"/>
        /// if any value is invalid (§6.4).
        /// </summary>
        public void Validate()
        {
            if (_hotbarSize < 1)
                throw new System.InvalidOperationException(
                    $"[InventoryConfig '{name}'] HotbarSize must be >= 1.");

            if (_mainInventorySize < 1)
                throw new System.InvalidOperationException(
                    $"[InventoryConfig '{name}'] MainInventorySize must be >= 1.");
        }
    }
}
