using OrcFarm.Carry;
using OrcFarm.Interaction;
using UnityEngine;
using VContainer;

namespace OrcFarm.Storage
{
    [RequireComponent(typeof(Collider))]
    public sealed class HeadStorageContainer : MonoBehaviour, IInteractable
    {
        [Tooltip("Storage slot roots. Each root can hold one harvested head.")]
        [SerializeField] private Transform[] _contentsRoots;

        [SerializeField, HideInInspector] private Transform _contentsRoot;

        private ICarryController _carry;
        private bool _loggedMissingInjection;

        [Inject]
        private void Construct(ICarryController carry) => _carry = carry;

        public int StoredCount => CountStoredHeads();

        public bool CanInteract
        {
            get
            {
                if (!enabled || !HasContentsRoot())
                {
                    return false;
                }

                if (_carry == null)
                {
                    if (!_loggedMissingInjection)
                    {
                        _loggedMissingInjection = true;
                        Debug.LogError(
                            $"[HeadStorageContainer '{gameObject.name}'] Missing injected ICarryController. " +
                            "Register this component in RootLifetimeScope.",
                            this);
                    }

                    return false;
                }

                return (!_carry.IsCarryingLeg && _carry.IsCarrying && TryGetAvailableRoot(out _))
                    || (!_carry.IsCarrying && TryGetRetrievalRoot(out _));
            }
        }

        public void OnInteract()
        {
            if (_carry == null)
            {
                return;
            }

            if (!_carry.IsCarryingLeg && _carry.IsCarrying && TryGetAvailableRoot(out Transform storageRoot))
            {
                if (_carry.TryStore(storageRoot))
                {
                    LogStored();
                }
            }
            else if (!_carry.IsCarrying && TryGetRetrievalRoot(out Transform retrievalRoot))
            {
                Retrieve(retrievalRoot);
            }
        }

        private void Awake()
        {
            if (!HasContentsRoot())
            {
                throw new System.InvalidOperationException(
                    $"[HeadStorageContainer] ContentsRoots not assigned on '{gameObject.name}'.");
            }
        }

        private int CountStoredHeads()
        {
            int storedCount = 0;

            if (HasContentsRootArray())
            {
                for (int i = 0; i < _contentsRoots.Length; i++)
                {
                    Transform root = _contentsRoots[i];
                    if (root != null && root.childCount > 0)
                    {
                        storedCount++;
                    }
                }

                return storedCount;
            }

            return _contentsRoot != null && _contentsRoot.childCount > 0 ? 1 : 0;
        }

        private bool HasContentsRoot()
        {
            return HasContentsRootArray() || _contentsRoot != null;
        }

        private bool HasContentsRootArray()
        {
            if (_contentsRoots == null)
            {
                return false;
            }

            for (int i = 0; i < _contentsRoots.Length; i++)
            {
                if (_contentsRoots[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetAvailableRoot(out Transform storageRoot)
        {
            storageRoot = null;

            if (HasContentsRootArray())
            {
                for (int i = 0; i < _contentsRoots.Length; i++)
                {
                    Transform root = _contentsRoots[i];
                    if (root != null && root.childCount == 0)
                    {
                        storageRoot = root;
                        return true;
                    }
                }

                return false;
            }

            if (_contentsRoot != null && _contentsRoot.childCount == 0)
            {
                storageRoot = _contentsRoot;
                return true;
            }

            return false;
        }

        private bool TryGetRetrievalRoot(out Transform retrievalRoot)
        {
            retrievalRoot = null;

            if (HasContentsRootArray())
            {
                for (int i = _contentsRoots.Length - 1; i >= 0; i--)
                {
                    Transform root = _contentsRoots[i];
                    if (root != null && root.childCount > 0)
                    {
                        retrievalRoot = root;
                        return true;
                    }
                }

                return false;
            }

            if (_contentsRoot != null && _contentsRoot.childCount > 0)
            {
                retrievalRoot = _contentsRoot;
                return true;
            }

            return false;
        }

        private void Retrieve(Transform retrievalRoot)
        {
            Transform last = retrievalRoot.GetChild(retrievalRoot.childCount - 1);

            if (!last.TryGetComponent(out HarvestedHead head))
            {
                Debug.LogWarning(
                    $"[HeadStorageContainer '{gameObject.name}'] Last child of ContentsRoot " +
                    "has no HarvestedHead component. Retrieval aborted; head left in storage.", this);
                return;
            }

            _carry.PickUp(head);
            LogRetrieved();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogStored()
        {
            Debug.Log(
                $"[HeadStorageContainer '{gameObject.name}'] Stored head. Total: {StoredCount}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogRetrieved()
        {
            Debug.Log(
                $"[HeadStorageContainer '{gameObject.name}'] Retrieved head. Remaining: {StoredCount}", this);
        }
    }
}
