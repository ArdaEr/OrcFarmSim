using OrcFarm.Carry;
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
        [SerializeField] private Transform _contentsRoot;

        private ICarryController _carry;
        private bool             _loggedMissingInjection;

        [Inject]
        private void Construct(ICarryController carry) => _carry = carry;

        /// <summary>Number of legs currently in storage.</summary>
        public int StoredCount => _contentsRoot != null ? _contentsRoot.childCount : 0;

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
                if (!enabled || _contentsRoot == null)
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

                return _carry.IsCarryingLeg
                    || (!_carry.IsCarrying && _contentsRoot.childCount > 0);
            }
        }

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_carry == null || _contentsRoot == null)
                return;

            if (_carry.IsCarryingLeg)
            {
                if (_carry.TryStoreLeg(_contentsRoot))
                    LogStored();
            }
            else if (!_carry.IsCarrying && _contentsRoot.childCount > 0)
            {
                Retrieve();
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_contentsRoot == null)
                throw new System.InvalidOperationException(
                    $"[LegStorageContainer] ContentsRoot not assigned on '{gameObject.name}'.");
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

            if (_contentsRoot == null || _contentsRoot.childCount == 0)
                return false;

            Transform last = _contentsRoot.GetChild(_contentsRoot.childCount - 1);

            if (!last.TryGetComponent(out HarvestedLeg leg))
            {
                Debug.LogWarning(
                    $"[LegStorageContainer '{gameObject.name}'] Top child has no HarvestedLeg component. " +
                    "Consume aborted.", this);
                return false;
            }

            quality = leg.Quality;
            last.SetParent(null);
            last.gameObject.SetActive(false);
            return true;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void Retrieve()
        {
            Transform last = _contentsRoot.GetChild(_contentsRoot.childCount - 1);

            if (!last.TryGetComponent(out HarvestedLeg leg))
            {
                Debug.LogWarning(
                    $"[LegStorageContainer '{gameObject.name}'] Last child of ContentsRoot " +
                    "has no HarvestedLeg component. Retrieval aborted; leg left in storage.", this);
                return;
            }

            _carry.PickUpLeg(leg);
            LogRetrieved();
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
