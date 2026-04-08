using OrcFarm.Carry;
using OrcFarm.Interaction;
using UnityEngine;
using VContainer;

namespace OrcFarm.Storage
{
    [RequireComponent(typeof(Collider))]
    public sealed class HeadStorageContainer : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _contentsRoot;

        private ICarryController _carry;
        private bool _loggedMissingInjection;

        [Inject]
        private void Construct(ICarryController carry) => _carry = carry;

        public int StoredCount => _contentsRoot != null ? _contentsRoot.childCount : 0;

        public bool CanInteract
        {
            get
            {
                if (!enabled || _contentsRoot == null)
                    return false;

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

                return _carry.IsCarrying || _contentsRoot.childCount > 0;
            }
        }

        public void OnInteract()
        {
            if (_carry == null || _contentsRoot == null)
                return;

            if (_carry.IsCarrying)
            {
                if (_carry.TryStore(_contentsRoot))
                    LogStored();
            }
            else if (_contentsRoot.childCount > 0)
            {
                Retrieve();
            }
        }

        private void Awake()
        {
            if (_contentsRoot == null)
                throw new System.InvalidOperationException(
                    $"[HeadStorageContainer] ContentsRoot not assigned on '{gameObject.name}'.");
        }

        private void Retrieve()
        {
            Transform last = _contentsRoot.GetChild(_contentsRoot.childCount - 1);

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
