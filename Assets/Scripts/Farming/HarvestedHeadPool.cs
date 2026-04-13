using OrcFarm.Carry;
using OrcFarm.Core;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Pre-warmed object pool for <see cref="HarvestedHead"/> instances (§3.4, §3.6, §3.7).
    ///
    /// All instances are created during <see cref="Awake"/> — no <c>Instantiate</c> or
    /// <c>Destroy</c> is called during active gameplay.
    ///
    /// Setup in the Inspector:
    ///   • Assign the HarvestedHead prefab to <c>_headPrefab</c>.
    ///   • Set <c>_initialCapacity</c> to the maximum simultaneous heads expected.
    ///   • Assign this component by serialized reference to any caller
    ///     (e.g. RootLifetimeScope) — no FindObjectOfType, no static singleton.
    ///
    /// If the pool is exhausted at runtime a warning is logged in the Editor and
    /// <see cref="Get"/> returns <c>null</c>; no runtime allocation occurs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestedHeadPool : MonoBehaviour, IHarvestedHeadPool
    {
        [Tooltip("HarvestedHead prefab. Must have a HarvestedHead component attached.")]
        [SerializeField] private GameObject _headPrefab;

        [Tooltip("Number of HarvestedHead instances pre-instantiated during scene load. " +
                 "Must be at least 1.")]
        [Min(1)]
        [SerializeField] private int _initialCapacity = 10;

#if UNITY_EDITOR
        [Header("Debug  —  Play Mode only")]
        [Tooltip("Immediately returns all active pool instances to the pool. " +
                 "Auto-resets after firing (one-shot inspector bool, consistent with " +
                 "FarmPlot and PlayerInventory debug hooks).")]
        [SerializeField] private bool _debugForceReturnAll;
#endif

        private GameObject[] _pool;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            ValidateConfig();

            _pool = new GameObject[_initialCapacity];

            for (int i = 0; i < _initialCapacity; i++)
            {
                GameObject instance = Instantiate(_headPrefab, transform);
                instance.SetActive(false);
                _pool[i] = instance;
            }
        }

#if UNITY_EDITOR
        // Zero allocations: bool check only in Update (§3.1).
        private void Update()
        {
            if (_debugForceReturnAll)
            {
                _debugForceReturnAll = false;
                ReturnAll();
            }
        }

        private void ReturnAll()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] != null && _pool[i].activeSelf)
                    Return(_pool[i]);
            }
        }
#endif

        // ── IHarvestedHeadPool ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] == null || _pool[i].activeSelf)
                    continue;

                _pool[i].transform.SetParent(null);
                _pool[i].transform.SetPositionAndRotation(position, rotation);
                _pool[i].SetActive(true);

                if (_pool[i].TryGetComponent(out HarvestedHead head))
                    head.OnGetFromPool();

                return _pool[i];
            }

            LogPoolExhausted();
            return null;
        }

        /// <inheritdoc/>
        public void Return(GameObject go)
        {
            if (go == null)
                return;

            if (go.TryGetComponent(out HarvestedHead head))
            {
                head.OnReturnToPool();
                head.ResetState();
            }

            go.transform.SetParent(transform);
            go.SetActive(false);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void ValidateConfig()
        {
            if (_headPrefab == null)
                throw new System.InvalidOperationException(
                    $"[HarvestedHeadPool '{gameObject.name}'] _headPrefab is not assigned.");

            if (_headPrefab.GetComponent<HarvestedHead>() == null)
                throw new System.InvalidOperationException(
                    $"[HarvestedHeadPool '{gameObject.name}'] _headPrefab '{_headPrefab.name}' " +
                    "has no HarvestedHead component.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogPoolExhausted()
        {
            Debug.LogWarning(
                $"[HarvestedHeadPool '{gameObject.name}'] Pool exhausted (capacity={_initialCapacity}). " +
                "Increase _initialCapacity to avoid lost harvests.");
        }
    }
}
