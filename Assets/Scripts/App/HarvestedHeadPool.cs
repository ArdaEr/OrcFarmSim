using System.Threading;
using Cysharp.Threading.Tasks;
using OrcFarm.Carry;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

namespace OrcFarm.App
{
    /// <summary>
    /// Pre-warmed object pool for <see cref="HarvestedHead"/> instances.
    ///
    /// The prefab is loaded asynchronously from Addressables during <see cref="StartAsync"/>,
    /// then all pool instances are instantiated before gameplay begins.
    ///
    /// Setup in the Inspector:
    ///   - Mark the HarvestedHead prefab as Addressable in the Unity Editor.
    ///   - Assign its address to <c>_headRef</c> on <see cref="RootLifetimeScope"/>.
    ///   - Create an empty child GameObject for <c>_headPoolRoot</c>.
    ///   - Set <c>_headPoolSize</c> to the maximum simultaneous heads expected in a session.
    ///
    /// If the pool is exhausted at runtime a warning is logged.
    /// </summary>
    public sealed class HarvestedHeadPool : IAsyncStartable
    {
        private readonly AssetReferenceT<GameObject> _headRef;
        private readonly Transform _poolRoot;
        private readonly int _preWarmCount;
        private HarvestedHead[] _pool;

        public HarvestedHeadPool(
            AssetReferenceT<GameObject> headRef,
            Transform poolRoot,
            int preWarmCount)
        {
            _headRef = headRef;
            _poolRoot = poolRoot;
            _preWarmCount = preWarmCount;
        }

        /// <summary>
        /// Loads the prefab from Addressables then pre-warms all pool instances.
        /// Called by VContainer before any gameplay Update runs.
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            GameObject prefabGo;

            try
            {
                AsyncOperationHandle<GameObject> handle = _headRef.LoadAssetAsync<GameObject>();
                await handle.Task;

                cancellation.ThrowIfCancellationRequested();

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogError("[HarvestedHeadPool] Failed to load HarvestedHead prefab.");
                    return;
                }

                prefabGo = handle.Result;
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HarvestedHeadPool] Failed to load HarvestedHead prefab: {e.Message}");
                return;
            }

            _pool = new HarvestedHead[_preWarmCount];

            for (int i = 0; i < _preWarmCount; i++)
            {
                cancellation.ThrowIfCancellationRequested();

                GameObject go = Object.Instantiate(prefabGo, _poolRoot);
                HarvestedHead head = go.GetComponent<HarvestedHead>();

                if (head == null)
                {
                    Debug.LogError("[HarvestedHeadPool] Prefab is missing HarvestedHead component.");
                    Object.Destroy(go);
                    continue;
                }

                head.ResetState();
                go.SetActive(false);
                _pool[i] = head;
            }
        }

        /// <summary>
        /// Retrieves a pooled head, positions it, activates it, and calls OnGetFromPool.
        /// </summary>
        public HarvestedHead Get(Vector3 position, Quaternion rotation)
        {
            if (_pool != null)
            {
                for (int i = 0; i < _pool.Length; i++)
                {
                    if (_pool[i] == null)
                    {
                        continue;
                    }

                    if (!_pool[i].gameObject.activeInHierarchy)
                    {
                        HarvestedHead instance = _pool[i];
                        instance.transform.SetParent(null);
                        instance.transform.SetPositionAndRotation(position, rotation);
                        instance.gameObject.SetActive(true);
                        instance.OnGetFromPool();
                        return instance;
                    }
                }
            }

            Debug.LogWarning(
                $"[HarvestedHeadPool] Pool exhausted (size={_preWarmCount}). Increase pre-warm count to avoid mid-gameplay allocation.");
            return null;
        }

        /// <summary>
        /// Returns a head to the pool: resets state, deactivates, and re-parents under pool root.
        /// </summary>
        public void Return(HarvestedHead head)
        {
            if (head == null)
            {
                return;
            }

            head.OnReturnToPool();
            head.ResetState();
            head.transform.SetParent(_poolRoot);
            head.gameObject.SetActive(false);
        }
    }
}