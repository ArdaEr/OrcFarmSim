using OrcFarm.Carry;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Pre-warmed object pool for <see cref="HarvestedLeg"/> instances (§3.4, §3.6, §3.7).
    ///
    /// All instances are created during <see cref="Awake"/> — no <c>Instantiate</c> or
    /// <c>Destroy</c> is called during active gameplay.
    ///
    /// Assign the HarvestedLeg prefab and capacity in the Inspector, then assign this
    /// component directly to the <see cref="LegPond"/> that owns it.
    ///
    /// If the pool is exhausted at runtime a warning is logged in the Editor and
    /// <see cref="Get"/> returns <c>null</c>; no runtime allocation occurs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestedLegPool : MonoBehaviour
    {
        [Tooltip("HarvestedLeg prefab. Must have a HarvestedLeg component attached.")]
        [SerializeField] private GameObject _legPrefab;

        [Tooltip("Number of HarvestedLeg instances pre-instantiated during scene load. " +
                 "One per active pond is usually sufficient.")]
        [Min(1)]
        [SerializeField] private int _initialCapacity = 5;

        private GameObject[] _pool;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_legPrefab == null)
                throw new System.InvalidOperationException(
                    $"[HarvestedLegPool '{gameObject.name}'] _legPrefab is not assigned.");

            if (_legPrefab.GetComponent<HarvestedLeg>() == null)
                throw new System.InvalidOperationException(
                    $"[HarvestedLegPool '{gameObject.name}'] _legPrefab '{_legPrefab.name}' " +
                    "has no HarvestedLeg component.");

            _pool = new GameObject[_initialCapacity];

            for (int i = 0; i < _initialCapacity; i++)
            {
                GameObject instance = Instantiate(_legPrefab, transform);
                instance.SetActive(false);
                _pool[i] = instance;
            }
        }

        // ── Pool API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves an inactive pooled leg, positions it, activates it, and calls
        /// <see cref="OrcFarm.Core.IPoolable.OnGetFromPool"/>.
        /// Returns <c>null</c> and logs a warning in the editor if the pool is exhausted.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] == null || _pool[i].activeSelf)
                    continue;

                _pool[i].transform.SetParent(null);
                _pool[i].transform.SetPositionAndRotation(position, rotation);
                _pool[i].SetActive(true);

                if (_pool[i].TryGetComponent(out HarvestedLeg leg))
                    leg.OnGetFromPool();

                return _pool[i];
            }

            LogPoolExhausted();
            return null;
        }

        /// <summary>
        /// Returns <paramref name="go"/> to the pool: resets its state, re-parents it
        /// under the pool root, and deactivates it. Safe to call with <c>null</c>.
        /// </summary>
        public void Return(GameObject go)
        {
            if (go == null)
                return;

            if (go.TryGetComponent(out HarvestedLeg leg))
            {
                leg.OnReturnToPool();
                leg.ResetState();
            }

            go.transform.SetParent(transform);
            go.SetActive(false);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogPoolExhausted()
        {
            Debug.LogWarning(
                $"[HarvestedLegPool '{gameObject.name}'] Pool exhausted (capacity={_initialCapacity}). " +
                "Increase _initialCapacity to avoid lost harvests.");
        }
    }
}
