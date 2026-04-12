using System.Collections.Generic;
using UnityEngine;

namespace OrcFarm.Workers
{
    /// <summary>
    /// A pen in the HOME area where assembled orcs wait after the player chooses Store.
    ///
    /// Each standing spot is a child Transform assigned in the Inspector.
    /// Stored orcs are visible in the scene standing at their assigned spot.
    /// The player can visit and Keep any stored orc, which removes it from the pen
    /// and sends it to the hauler wait point.
    ///
    /// <see cref="StoredCount"/> is readable by the bazaar sell system.
    /// </summary>
    public sealed class OrcHoldingPen : MonoBehaviour
    {
        [Tooltip("Standing spots inside the pen — one per orc capacity. " +
                 "Create empty child GameObjects and position them evenly inside the pen area.")]
        [SerializeField] private Transform[] _standingSpots;

        private readonly List<HaulerWorker> _storedOrcs = new List<HaulerWorker>();

        /// <summary>Number of orcs currently stored or walking to the pen.</summary>
        public int StoredCount => _storedOrcs.Count;

        /// <summary>
        /// Assigns the next free standing spot to <paramref name="orc"/> and tells it
        /// to walk there. Returns false if the pen is full.
        /// </summary>
        public bool TryStore(HaulerWorker orc)
        {
            Transform spot = FindFreeSpot();
            if (spot == null)
                return false;

            _storedOrcs.Add(orc);
            orc.StoreInPen(spot);
            return true;
        }

        /// <summary>
        /// Removes <paramref name="orc"/> from pen tracking after the player Keeps it.
        /// The spot it occupied becomes available for the next stored orc.
        /// </summary>
        public void Release(HaulerWorker orc)
        {
            _storedOrcs.Remove(orc);
        }

        /// <summary>
        /// Removes and returns the first stored orc so it can be sold.
        /// Its standing spot is freed immediately for future use.
        /// Returns <c>null</c> if no orcs are currently stored.
        /// </summary>
        public HaulerWorker TrySellOne()
        {
            if (_storedOrcs.Count == 0)
                return null;

            HaulerWorker orc = _storedOrcs[0];
            _storedOrcs.RemoveAt(0);
            return orc;
        }

        private Transform FindFreeSpot()
        {
            if (_standingSpots == null)
                return null;
            foreach (Transform spot in _standingSpots)
            {
                bool occupied = false;
                foreach (HaulerWorker orc in _storedOrcs)
                {
                    if (orc.PenSpot == spot)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (!occupied)
                    return spot;
            }
            return null;
        }

        private void Awake()
        {
            if (_standingSpots == null || _standingSpots.Length == 0)
                Debug.LogWarning(
                    $"[OrcHoldingPen '{gameObject.name}'] No standing spots assigned. " +
                    "Add child Transforms and assign them in the Inspector.", this);
        }
    }
}
