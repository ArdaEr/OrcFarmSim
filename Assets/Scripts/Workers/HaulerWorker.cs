using OrcFarm.Carry;
using UnityEngine;

namespace OrcFarm.Workers
{
    /// <summary>
    /// An assembled orc kept as a hauler worker.
    ///
    /// Loop: scan for an available harvested head → walk to it → carry it to the storage
    /// drop-off point → walk back to the wait point → repeat.
    ///
    /// Instability: every <see cref="_instabilityCheckInterval"/> seconds the hauler rolls
    /// a chance to slow down for a random duration, producing a visible movement stutter
    /// that the player can read as instability.
    ///
    /// The hauler manages its own head carry state independently of the player's
    /// <see cref="CarryController"/>. It disables the head's Collider and Rigidbody on
    /// pickup (matching what <c>AttachToAnchor</c> does) and parents the head to the
    /// storage delivery root on arrival (matching what <c>StoreInto</c> does), so the
    /// head is immediately retrievable by the player through the normal storage UI.
    ///
    /// MonoBehaviour justification: per-frame movement loop, Unity physics queries,
    /// and scene-side serialized references all require MonoBehaviour lifecycle.
    /// </summary>
    public sealed class HaulerWorker : MonoBehaviour
    {
        // ── Route ──────────────────────────────────────────────────────────────

        [Tooltip("World position the hauler returns to when idle.")]
        [SerializeField] private Transform _waitPoint;

        [Tooltip("Point the hauler walks to before making a delivery. " +
                 "Position this outside the storage mesh — e.g. in front of the door.")]
        [SerializeField] private Transform _storageWalkTarget;

        [Tooltip("Transform under which delivered heads are parented. " +
                 "Must be HeadStorageContainer's ContentsRoot child for the " +
                 "storage retrieval UI to count heads correctly.")]
        [SerializeField] private Transform _storageDeliveryRoot;

        [Tooltip("Child transform on this object where the head is anchored during transit.")]
        [SerializeField] private Transform _carryAnchor;

        // ── Movement ───────────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Normal movement speed in m/s.")]
        [SerializeField] private float _moveSpeed = 3f;

        [Tooltip("Movement speed during an instability slowdown, in m/s.")]
        [SerializeField] private float _slowedSpeed = 0.8f;

        [Tooltip("Distance at which the hauler considers itself arrived at a target.")]
        [SerializeField] private float _arrivalDistance = 0.5f;

        // ── Search ─────────────────────────────────────────────────────────────

        [Header("Search")]
        [Tooltip("Radius around the hauler to scan for harvested heads.")]
        [SerializeField] private float _searchRadius = 25f;

        [Tooltip("Seconds between head scans while idle.")]
        [SerializeField] private float _searchInterval = 1.5f;

        // ── Instability ────────────────────────────────────────────────────────

        [Header("Instability")]
        [Tooltip("Seconds between instability rolls.")]
        [SerializeField] private float _instabilityCheckInterval = 10f;

        [Tooltip("Probability (0–1) of a slowdown on each instability roll.")]
        [Range(0f, 1f)]
        [SerializeField] private float _slowdownChance = 0.4f;

        [Tooltip("Minimum seconds a slowdown lasts.")]
        [SerializeField] private float _slowdownMinDuration = 2f;

        [Tooltip("Maximum seconds a slowdown lasts.")]
        [SerializeField] private float _slowdownMaxDuration = 5f;

        // ── State ──────────────────────────────────────────────────────────────

        private enum HaulerState { Idle, MovingToHead, Carrying, Returning }

        private HaulerState   _state;
        private HarvestedHead _targetHead;
        private Rigidbody     _targetRb;     // cached at grab time, not at search time
        private Collider      _targetCol;    // cached at search time; used to detect player steal

        // ── Timers ─────────────────────────────────────────────────────────────

        private float _searchTimer;
        private float _instabilityTimer;
        private float _slowdownTimer;
        private bool  _isSlowed;

        // Pre-allocated to avoid per-search allocation (§5.3).
        private readonly Collider[] _overlapBuffer = new Collider[16];

        // Cached to avoid sqrt in arrival checks every frame (§5.4).
        private float _sqArrival;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>True once the player has chosen Keep for this worker.</summary>
        public bool IsKept { get; private set; }

        /// <summary>
        /// Called by <see cref="KeepInteractable"/> when the player chooses Keep.
        /// Activates this GameObject and starts the hauler loop.
        /// Safe to call on an inactive GameObject — Unity allows method calls on
        /// inactive components; SetActive then triggers the normal lifecycle.
        /// </summary>
        public void Keep()
        {
            IsKept = true;
            _state = HaulerState.Idle;
            gameObject.SetActive(true);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_waitPoint == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _waitPoint not assigned.");
            if (_storageWalkTarget == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _storageWalkTarget not assigned.");
            if (_storageDeliveryRoot == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _storageDeliveryRoot not assigned.");
            if (_carryAnchor == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _carryAnchor not assigned.");

            _sqArrival = _arrivalDistance * _arrivalDistance;
        }

        // Zero heap allocations: all arithmetic uses value types; OverlapSphereNonAlloc
        // uses a pre-allocated buffer; Random returns floats (§3.1).
        private void Update()
        {
            TickInstability();
            TickState();
        }

        // ── Instability ────────────────────────────────────────────────────────

        private void TickInstability()
        {
            if (_isSlowed)
            {
                _slowdownTimer -= Time.deltaTime;
                if (_slowdownTimer <= 0f)
                    _isSlowed = false;
                return;
            }

            _instabilityTimer += Time.deltaTime;
            if (_instabilityTimer < _instabilityCheckInterval)
                return;

            _instabilityTimer = 0f;
            if (Random.value < _slowdownChance)
            {
                _isSlowed      = true;
                _slowdownTimer = Random.Range(_slowdownMinDuration, _slowdownMaxDuration);
                LogSlowdown();
            }
        }

        private float CurrentSpeed => _isSlowed ? _slowedSpeed : _moveSpeed;

        // ── State machine ──────────────────────────────────────────────────────

        private void TickState()
        {
            switch (_state)
            {
                case HaulerState.Idle:          TickIdle();          break;
                case HaulerState.MovingToHead:  TickMovingToHead();  break;
                case HaulerState.Carrying:      TickCarrying();      break;
                case HaulerState.Returning:     TickReturning();     break;
            }
        }

        private void TickIdle()
        {
            _searchTimer += Time.deltaTime;
            if (_searchTimer < _searchInterval)
                return;

            _searchTimer = 0f;
            FindAndTargetHead();
        }

        private void TickMovingToHead()
        {
            // Head was destroyed or deactivated.
            if (_targetHead == null || !_targetHead.gameObject.activeInHierarchy)
            {
                ClearTarget();
                _state = HaulerState.Idle;
                return;
            }

            // Head was picked up by the player — AttachToAnchor disables its collider.
            if (_targetCol != null && !_targetCol.enabled)
            {
                ClearTarget();
                _state = HaulerState.Idle;
                return;
            }

            MoveToward(_targetHead.transform.position);

            if (SqDistTo(_targetHead.transform.position) <= _sqArrival)
                GrabHead();
        }

        private void TickCarrying()
        {
            MoveToward(_storageWalkTarget.position);

            if (SqDistTo(_storageWalkTarget.position) <= _sqArrival)
                DeliverHead();
        }

        private void TickReturning()
        {
            MoveToward(_waitPoint.position);

            if (SqDistTo(_waitPoint.position) <= _sqArrival)
                _state = HaulerState.Idle;
        }

        // ── Head interactions ──────────────────────────────────────────────────

        // OverlapSphereNonAlloc only returns enabled colliders, so stored heads
        // (collider disabled by StoreInto's preceding AttachToAnchor) and player-
        // carried heads (collider disabled by AttachToAnchor) are excluded
        // automatically — no extra filtering needed (§5.3).
        private void FindAndTargetHead()
        {
            Debug.Log("[HaulerWorker] FindAndTargetHead called");

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _searchRadius, _overlapBuffer);

            HarvestedHead best   = null;
            Collider      bestCol = null;
            float         bestSq  = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (!_overlapBuffer[i].TryGetComponent(out HarvestedHead head))
                    continue;

                float sq = SqDistTo(head.transform.position);   // sqrMagnitude (§5.4)
                if (sq < bestSq)
                {
                    bestSq  = sq;
                    best    = head;
                    bestCol = _overlapBuffer[i];
                }
            }

            if (best == null)
                return;

            _targetHead = best;
            _targetCol  = bestCol;
            _state      = HaulerState.MovingToHead;
        }

        private void GrabHead()
        {
            // Cache Rigidbody here (once at grab time) rather than at search time
            // to avoid GetComponent in the periodic search loop (§5.2).
            _targetRb = _targetHead.GetComponent<Rigidbody>();

            // Disable collider first — fires OnTriggerExit on InteractionDetector,
            // removing the head from its candidate list so the player can't interact
            // with a head the hauler is carrying.
            if (_targetCol != null)
                _targetCol.enabled = false;

            if (_targetRb != null)
            {
                _targetRb.linearVelocity  = Vector3.zero;
                _targetRb.angularVelocity = Vector3.zero;
                _targetRb.isKinematic     = true;
            }

            _targetHead.transform.SetParent(_carryAnchor);
            _targetHead.transform.localPosition = Vector3.zero;
            _targetHead.transform.localRotation = Quaternion.identity;

            _state = HaulerState.Carrying;
        }

        private void DeliverHead()
        {
            // Parent to delivery root — matches what HeadStorageContainer.StoreInto
            // does, so the existing retrieval UI counts this head correctly.
            // Rigidbody stays kinematic and Collider stays disabled (stored state).
            _targetHead.transform.SetParent(_storageDeliveryRoot);
            _targetHead.transform.localPosition = Vector3.zero;
            _targetHead.transform.localRotation = Quaternion.identity;

            LogDelivery();
            ClearTarget();
            _state = HaulerState.Returning;
        }

        private void ClearTarget()
        {
            _targetHead = null;
            _targetRb   = null;
            _targetCol  = null;
        }

        // ── Movement ───────────────────────────────────────────────────────────

        private void MoveToward(Vector3 target)
        {
            // Flatten to hauler's Y so it doesn't pitch up/down on uneven terrain.
            Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);

            transform.position = Vector3.MoveTowards(
                transform.position, flatTarget, CurrentSpeed * Time.deltaTime);

            Vector3 dir = flatTarget - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private float SqDistTo(Vector3 target) =>
            (transform.position - target).sqrMagnitude;

        // ── Logging ────────────────────────────────────────────────────────────

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogSlowdown()
        {
            Debug.Log(
                $"[HaulerWorker '{gameObject.name}'] Instability: slowed for " +
                $"{_slowdownTimer:F1}s.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogDelivery()
        {
            Debug.Log(
                $"[HaulerWorker '{gameObject.name}'] Head delivered to storage.", this);
        }
    }
}
