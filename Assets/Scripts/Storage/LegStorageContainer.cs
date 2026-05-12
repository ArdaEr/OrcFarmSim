using OrcFarm.Carry;
using OrcFarm.Effects;
using OrcFarm.Interaction;
using UnityEngine;
using VContainer;

namespace OrcFarm.Storage
{
    /// <summary>
    /// LIFO storage container for <see cref="HarvestedLeg"/> objects.
    ///
    /// Mirrors <see cref="HeadStorageContainer"/> in structure and behavior; accepts only legs.
    ///
    /// Deposit: player must be carrying a <see cref="HarvestedLeg"/>.
    /// Retrieve: player must be carrying nothing and <see cref="StoredCount"/> must be > 0.
    /// Carrying a <see cref="HarvestedHead"/> while interacting produces no effect.
    ///
    /// <see cref="ICarryController"/> is injected via VContainer (§1.3).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LegStorageContainer : MonoBehaviour, IInteractable
    {
        private const float DefaultStoreShakeIntensity = 1.0f;
        private const float DefaultRetrieveShakeIntensity = 1.15f;
        private const float MinShakeDirectionLengthSquared = 0.0001f;

        [Tooltip("Storage slot roots. Each root can hold one harvested leg.")]
        [SerializeField] private Transform[] _contentsRoots;

        [SerializeField, HideInInspector] private Transform _contentsRoot;

        [Header("Jelly Shake")]
        [Tooltip("Optional child jelly controller played when a leg enters or leaves storage.")]
        [SerializeField] private JellyShakeController _jellyShakeController;

        [Tooltip("Origin used to calculate the direction of the leg entering or leaving the jelly.")]
        [SerializeField] private Transform _jellyShakeOrigin;

        [Tooltip("Shake intensity when a carried leg is put into storage.")]
        [Min(0.0f)]
        [SerializeField] private float _storeShakeIntensity = DefaultStoreShakeIntensity;

        [Tooltip("Shake intensity when a leg is removed from storage.")]
        [Min(0.0f)]
        [SerializeField] private float _retrieveShakeIntensity = DefaultRetrieveShakeIntensity;

        private ICarryController _carry;
        private bool             _loggedMissingInjection;

        [Inject]
        private void Construct(ICarryController carry) => _carry = carry;

        /// <summary>Number of legs currently in storage.</summary>
        public int StoredCount => CountStoredLegs();

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// True when the player is carrying a leg (deposit available) or when legs are
        /// stored and the player carries nothing (retrieval available).
        /// False when carrying a head — heads are not accepted here.
        /// </remarks>
        public bool CanInteract
        {
            get
            {
                if (!enabled || !HasContentsRoot())
                    return false;

                if (_carry == null)
                {
                    if (!_loggedMissingInjection)
                    {
                        _loggedMissingInjection = true;
                        Debug.LogError(
                            $"[LegStorageContainer '{gameObject.name}'] Missing injected ICarryController. " +
                            "Register this component in RootLifetimeScope.",
                            this);
                    }

                    return false;
                }

                return (_carry.IsCarryingLeg && TryGetAvailableRoot(out _))
                    || (!_carry.IsCarrying && TryGetRetrievalRoot(out _));
            }
        }

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_carry == null)
                return;

            if (_carry.IsCarryingLeg && TryGetAvailableRoot(out Transform storageRoot))
            {
                if (_carry.TryStoreLeg(storageRoot))
                {
                    PlayJellyShakeFromRoot(storageRoot, _storeShakeIntensity);
                    LogStored();
                }
            }
            else if (!_carry.IsCarrying && TryGetRetrievalRoot(out Transform retrievalRoot))
            {
                Retrieve(retrievalRoot);
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (!HasContentsRoot())
                throw new System.InvalidOperationException(
                    $"[LegStorageContainer] ContentsRoots not assigned on '{gameObject.name}'.");
        }

        // ── Assembly consumption ───────────────────────────────────────────────

        /// <summary>
        /// Removes the top-most stored leg, captures its quality, and deactivates it.
        /// Called by <see cref="OrcFarm.Assembly.AssemblyStation"/> on successful assembly;
        /// do not call from other gameplay code.
        /// </summary>
        /// <param name="quality">Quality of the consumed leg, or Low if none available.</param>
        /// <returns>True if a leg was consumed; false if storage is empty.</returns>
        public bool TryConsumeTop(out OrcFarm.Core.OrcQuality quality)
        {
            quality = OrcFarm.Core.OrcQuality.Low;

            if (!TryGetRetrievalRoot(out Transform retrievalRoot))
                return false;

            Transform last = retrievalRoot.GetChild(retrievalRoot.childCount - 1);

            if (!last.TryGetComponent(out HarvestedLeg leg))
            {
                Debug.LogWarning(
                    $"[LegStorageContainer '{gameObject.name}'] Top child has no HarvestedLeg component. " +
                    "Consume aborted.", this);
                return false;
            }

            quality = leg.Quality;
            PlayJellyShake(GetShakeDirection(last), _retrieveShakeIntensity);
            last.SetParent(null);
            last.gameObject.SetActive(false);
            return true;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private int CountStoredLegs()
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

            if (!last.TryGetComponent(out HarvestedLeg leg))
            {
                Debug.LogWarning(
                    $"[LegStorageContainer '{gameObject.name}'] Last child of ContentsRoot " +
                    "has no HarvestedLeg component. Retrieval aborted; leg left in storage.", this);
                return;
            }

            Vector3 shakeDirection = GetShakeDirection(last);
            _carry.PickUpLeg(leg);
            PlayJellyShake(shakeDirection, _retrieveShakeIntensity);
            LogRetrieved();
        }

        private void PlayJellyShakeFromRoot(Transform storageRoot, float intensity)
        {
            if (storageRoot == null || storageRoot.childCount == 0)
            {
                return;
            }

            Transform last = storageRoot.GetChild(storageRoot.childCount - 1);
            PlayJellyShake(GetShakeDirection(last), intensity);
        }

        private Vector3 GetShakeDirection(Transform legTransform)
        {
            Transform origin = _jellyShakeOrigin;

            if (origin == null && _jellyShakeController != null)
            {
                origin = _jellyShakeController.transform;
            }

            if (origin == null)
            {
                origin = transform;
            }

            Vector3 direction = legTransform.position - origin.position;

            if (direction.sqrMagnitude < MinShakeDirectionLengthSquared)
            {
                direction = transform.forward;
            }

            return direction;
        }

        private void PlayJellyShake(Vector3 direction, float intensity)
        {
            if (_jellyShakeController == null || intensity <= 0.0f)
            {
                return;
            }

            _jellyShakeController.PlayGrabShake(direction, intensity);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogStored()
        {
            Debug.Log(
                $"[LegStorageContainer '{gameObject.name}'] Stored leg. Total: {StoredCount}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogRetrieved()
        {
            Debug.Log(
                $"[LegStorageContainer '{gameObject.name}'] Retrieved leg. Remaining: {StoredCount}", this);
        }
    }
}
