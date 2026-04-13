using System.Collections.Generic;
using UnityEngine;

namespace OrcFarm.Interaction
{
    /// <summary>
    /// Maintains a live set of nearby <see cref="IInteractable"/> objects via a trigger sphere
    /// and exposes the nearest valid one each frame as <see cref="IInteractionService.CurrentTarget"/>.
    ///
    /// Uses OnTriggerEnter/Exit to populate a cached candidate list so that no
    /// GetComponent call is ever made inside Update (§5.2).
    ///
    /// Setup requirements:
    ///   - A <see cref="SphereCollider"/> is added automatically via RequireComponent.
    ///   - The player root needs a <b>kinematic Rigidbody</b> for Unity to fire trigger events.
    ///   - Interactable GameObjects need at least one non-trigger Collider.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class InteractionDetector : MonoBehaviour, IInteractionService
    {
        [SerializeField] private float _range = 2f;

        private SphereCollider _trigger;

        // The interface reference, Transform, and Collider are all cached on TriggerEnter.
        // Storing the Collider lets FindNearest use ClosestPoint for an accurate range check
        // that matches how the SphereCollider itself detects overlaps — by surface proximity,
        // not pivot-to-pivot distance (§5.2 — no GetComponent in Update).
        private const int InitialCandidateCapacity = 8;

        private readonly List<(IInteractable interactable, Transform transform, Collider collider)> _candidates
            = new List<(IInteractable, Transform, Collider)>(InitialCandidateCapacity);

        // Pre-allocated for the spawn-inside-sphere fallback scan (§5.3).
        private readonly Collider[] _scanBuffer = new Collider[InitialCandidateCapacity];

        /// <inheritdoc/>
        public IInteractable CurrentTarget { get; private set; }

        private void Awake()
        {
            _trigger        = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius    = _range;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var col = GetComponent<SphereCollider>();
            if (col != null) col.radius = _range;
        }
#endif

        private void Update()
        {
            // Remove stale candidates every frame before selection.
            // Handles the case where a head is silently returned to the pool
            // (SetActive(false)) without OnTriggerExit firing reliably — the
            // candidate Transform is not destroyed so the t == null guard in
            // FindNearest does not catch it, but activeInHierarchy becomes false.
            // Zero allocation: backwards index loop, no new collections (§3.2).
            RemoveStaleCandidates();

            // Fallback: Unity does not fire OnTriggerEnter when a collider is activated
            // or spawned inside an existing trigger volume (e.g. a harvested head that
            // spawns at the plot while the player is standing next to it).
            // Scan once per frame only when the candidate list is empty so there is no
            // per-frame cost while at least one candidate is already tracked.
            if (_candidates.Count == 0)
                ScanForMissedEntries();

            CurrentTarget = FindNearest();
        }

        /// <inheritdoc/>
        public void TryInteract()
        {
            if (CurrentTarget != null && CurrentTarget.CanInteract)
                CurrentTarget.OnInteract();
        }

        // Removes candidates whose GameObject has been deactivated or whose Collider has
        // been disabled since the last frame. Necessary because SetActive(false) does not
        // reliably fire OnTriggerExit within the same physics step.
        // Backwards loop so RemoveAt(i) does not shift unvisited indices (§3.2).
        private void RemoveStaleCandidates()
        {
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                var (_, t, c) = _candidates[i];
                if (t == null || !t.gameObject.activeInHierarchy || !c.enabled)
                    _candidates.RemoveAt(i);
            }
        }

        // Populates candidates for colliders that enter the sphere normally.
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out IInteractable interactable))
                _candidates.Add((interactable, other.transform, other));
        }

        // Populates candidates for colliders that were already inside the sphere when
        // they were activated — OnTriggerEnter does not fire in that case.
        private void ScanForMissedEntries()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, _range, _scanBuffer);
            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i].TryGetComponent(out IInteractable interactable))
                    _candidates.Add((interactable, _scanBuffer[i].transform, _scanBuffer[i]));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                if (_candidates[i].collider == other)
                {
                    _candidates.RemoveAt(i);
                    return;
                }
            }
        }

        // Iterates the pre-allocated candidate list; removes destroyed entries in place.
        // ClosestPoint is called per live candidate to compute the actual surface distance
        // from the sphere centre — the same measurement the SphereCollider uses for overlap
        // detection, so a flat ground-level BoxCollider is never incorrectly rejected because
        // its pivot sits further than _range from the elevated sphere centre (§5.4).
        private IInteractable FindNearest()
        {
            IInteractable best       = null;
            float         bestSqDist = float.MaxValue;
            float         sqRadius   = _range * _range;
            Vector3       centre     = transform.position;

            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                var (interactable, t, c) = _candidates[i];

                if (t == null) // GameObject was destroyed without triggering OnTriggerExit
                {
                    _candidates.RemoveAt(i);
                    continue;
                }

                if (!interactable.CanInteract)
                    continue;

                // AABB proximity: c.bounds.ClosestPoint is pure geometric math on the
                // world-space bounding box — no physics API, works for every collider type
                // including non-convex MeshColliders and TerrainColliders (§5.4).
                // CanInteract being true implies the collider is enabled (HarvestedHead sets
                // _col.enabled = false while carried, which also sets CanInteract = false).
                float sqSurfaceDist = (c.bounds.ClosestPoint(centre) - centre).sqrMagnitude;

                if (sqSurfaceDist > sqRadius)
                    continue; // outside sphere — stale entry; OnTriggerExit will clean up

                // Rank by pivot distance so the closest object centre wins when two
                // interactables are both in range (no alloc: sqrMagnitude).
                float sqPivotDist = (t.position - centre).sqrMagnitude;
                if (sqPivotDist < bestSqDist)
                {
                    bestSqDist = sqPivotDist;
                    best       = interactable;
                }
            }

            return best;
        }
    }
}
