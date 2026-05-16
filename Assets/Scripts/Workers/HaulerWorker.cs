using OrcFarm.Carry;
using UnityEngine;
using UnityEngine.AI;

namespace OrcFarm.Workers
{
    /// <summary>
    /// An assembled orc that can be Kept as a hauler worker or Stored in the holding pen.
    ///
    /// Keep flow:   Keep() → WalkingToWaitPoint → Idle → (head search loop)
    /// Store flow:  StoreInPen() → WalkingToPen → StoredInPen → (player Keeps from pen)
    ///
    /// Hauler loop: Idle → MovingToHead → Carrying → Returning → Idle
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
    [RequireComponent(typeof(NavMeshAgent))]
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

        [SerializeField] private NavMeshAgent _navMeshAgent;

        [Tooltip("Normal movement speed in m/s.")]
        [SerializeField] private float _moveSpeed = 3f;

        [Tooltip("Movement speed during an instability slowdown, in m/s.")]
        [SerializeField] private float _slowedSpeed = 0.8f;

        [Tooltip("Distance at which the hauler considers itself arrived at a target.")]
        [SerializeField] private float _arrivalDistance = 0.5f;

        [Header("Animation")]
        [Tooltip("Animator with a bool parameter named Walking.")]
        [SerializeField] private Animator _animator;

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

        private enum HaulerState
        {
            Idle,
            MovingToHead,
            Carrying,
            Returning,
            WalkingToWaitPoint, // after Keep: orc walks to wait point before searching
            WalkingToPen,       // after Store: orc walks to assigned pen standing spot
            StoredInPen,        // orc stands in pen; player can Keep it from here
        }

        private HaulerState   _state;
        private HarvestedHead _targetHead;
        private Rigidbody     _targetRb;   // cached at grab time, not at search time
        private Collider      _targetCol;  // cached at search time; used to detect player steal
        private bool          _animatorWalking;
        private bool          _hasMovementDestination;
        private Vector3       _movementDestination;

        private const string WalkingParameterName = "Walking";
        private static readonly int WalkingHash = Animator.StringToHash(WalkingParameterName);

        // ── Timers ─────────────────────────────────────────────────────────────

        private float _searchTimer;
        private float _instabilityTimer;
        private float _slowdownTimer;
        private bool  _isSlowed;

        // Pre-allocated to avoid per-search allocation (§5.3).
        // Size 64 ensures the buffer is never filled by scene geometry when the search
        // radius covers the full farm area. If OverlapSphereNonAlloc ever returns exactly
        // 64, increase this — silent truncation is the only failure mode.
        private readonly Collider[] _overlapBuffer = new Collider[64];

        // Cached to avoid sqrt in arrival checks every frame (§5.4).
        private float _sqArrival;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>True once the player has chosen Keep for this worker.</summary>
        public bool IsKept { get; private set; }

        /// <summary>True while the orc is walking to or standing in the holding pen.</summary>
        public bool IsStored { get; private set; }

        /// <summary>True specifically when the orc has arrived and is standing in the pen.</summary>
        public bool IsInPen => IsStored && _state == HaulerState.StoredInPen;

        /// <summary>The standing spot assigned by <see cref="OrcHoldingPen"/>.</summary>
        public Transform PenSpot { get; private set; }

        /// <summary>
        /// Called by <see cref="KeepInteractable"/> when the player chooses Keep.
        /// Orc walks to the wait point first, then begins the hauler idle loop.
        /// </summary>
        public void Keep()
        {
            IsKept   = true;
            IsStored = false;
            PenSpot  = null;
            gameObject.SetActive(true);
            EnterState(HaulerState.WalkingToWaitPoint);
        }

        /// <summary>
        /// Called by <see cref="OrcHoldingPen"/> when the player chooses Store.
        /// Orc walks to <paramref name="spot"/> and stands there until Kept.
        /// </summary>
        public void StoreInPen(Transform spot)
        {
            IsStored = true;
            PenSpot  = spot;
            gameObject.SetActive(true);
            EnterState(HaulerState.WalkingToPen);
        }

        // ── Setup ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Provides the three scene-side references that cannot be pre-assigned on a
        /// prefab. Must be called by the spawner (e.g. AssemblyStation) immediately
        /// after Instantiate, before Keep() or StoreInPen() are ever called.
        /// </summary>
        public void Initialize(
            Transform waitPoint,
            Transform storageWalkTarget,
            Transform storageDeliveryRoot)
        {
            _waitPoint          = waitPoint;
            _storageWalkTarget  = storageWalkTarget;
            _storageDeliveryRoot = storageDeliveryRoot;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_carryAnchor == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _carryAnchor not assigned.");

            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_navMeshAgent == null)
            {
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _navMeshAgent not assigned.");
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _sqArrival = _arrivalDistance * _arrivalDistance;
            _navMeshAgent.speed = CurrentSpeed;
            _navMeshAgent.stoppingDistance = _arrivalDistance;
        }

        private void Start()
        {
            // Scene-side refs are injected via Initialize() when spawned from a prefab.
            // Validate here so the error surfaces before the first Keep/Store call.
            if (_waitPoint == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _waitPoint not assigned. " +
                    "Call Initialize() after Instantiate().");
            if (_storageWalkTarget == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _storageWalkTarget not assigned. " +
                    "Call Initialize() after Instantiate().");
            if (_storageDeliveryRoot == null)
                throw new System.InvalidOperationException(
                    $"[HaulerWorker '{gameObject.name}'] _storageDeliveryRoot not assigned. " +
                    "Call Initialize() after Instantiate().");
        }

        // Zero heap allocations: all arithmetic uses value types; OverlapSphereNonAlloc
        // uses a pre-allocated buffer; Random returns floats (§3.1).
        private void Update()
        {
            // Do nothing until the player has chosen Keep or Store.
            if (!IsKept && !IsStored)
            {
                StopMovement();
                SetWalking(false);
                return;
            }

            if (IsKept) TickInstability(); // instability only applies to active haulers

            SyncAgentSpeed();
            TickState();
            SetWalking(IsMoving());
        }

        private void OnDisable()
        {
            StopMovement();
            SetWalking(false);
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
                case HaulerState.Idle:               TickIdle();               break;
                case HaulerState.MovingToHead:       TickMovingToHead();       break;
                case HaulerState.Carrying:           TickCarrying();           break;
                case HaulerState.Returning:          TickReturning();          break;
                case HaulerState.WalkingToWaitPoint: TickWalkingToWaitPoint(); break;
                case HaulerState.WalkingToPen:       TickWalkingToPen();       break;
                case HaulerState.StoredInPen:                                  break;
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
                EnterState(HaulerState.Returning);
                return;
            }

            // Head was picked up by the player — AttachToAnchor disables its collider.
            if (_targetCol != null && !_targetCol.enabled)
            {
                ClearTarget();
                EnterState(HaulerState.Returning);
                return;
            }

            if (!HasReachedDestination())
            {
                return;
            }

            if (SqDistTo(_targetHead.transform.position) <= _sqArrival)
            {
                GrabHead();
                return;
            }

            SetMovementDestination(_targetHead.transform.position);
        }

        private void TickCarrying()
        {
            if (HasReachedDestination())
            {
                DeliverHead();
            }
        }

        private void TickReturning()
        {
            if (HasReachedDestination())
            {
                EnterState(HaulerState.Idle);
            }
        }

        private void TickWalkingToWaitPoint()
        {
            if (HasReachedDestination())
            {
                EnterState(HaulerState.Idle);
            }
        }

        private void TickWalkingToPen()
        {
            if (HasReachedDestination())
            {
                EnterState(HaulerState.StoredInPen);
            }
        }

        // ── Head interactions ──────────────────────────────────────────────────

        // OverlapSphereNonAlloc only returns enabled colliders, so stored heads
        // (collider disabled by StoreInto's preceding AttachToAnchor) and player-
        // carried heads (collider disabled by AttachToAnchor) are excluded
        // automatically — no extra filtering needed (§5.3).
        private void FindAndTargetHead()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _searchRadius, _overlapBuffer);

            HarvestedHead best    = null;
            Collider      bestCol = null;
            float         bestSq  = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (!_overlapBuffer[i].TryGetComponent(out HarvestedHead head))
                    continue;

                float sq = SqDistTo(head.transform.position); // sqrMagnitude (§5.4)
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
            EnterState(HaulerState.MovingToHead);
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
            else
                Debug.LogWarning("[HaulerWorker] _targetCol is NULL in GrabHead()", this);

            if (_targetRb != null)
            {
                _targetRb.linearVelocity  = Vector3.zero;
                _targetRb.angularVelocity = Vector3.zero;
                _targetRb.isKinematic     = true;
            }
            else
            {
                Debug.LogWarning(
                    "[HaulerWorker] _targetRb is NULL in GrabHead() — no Rigidbody on head.", this);
            }

            _targetHead.transform.SetParent(_carryAnchor);
            _targetHead.transform.localPosition = Vector3.zero;
            _targetHead.transform.localRotation = Quaternion.identity;

            EnterState(HaulerState.Carrying);
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
            EnterState(HaulerState.Returning);
        }

        private void ClearTarget()
        {
            _targetHead = null;
            _targetRb   = null;
            _targetCol  = null;
        }

        // ── Movement ───────────────────────────────────────────────────────────

        private void EnterState(HaulerState nextState)
        {
            _state = nextState;

            switch (nextState)
            {
                case HaulerState.MovingToHead:
                    SetMovementDestination(_targetHead.transform.position);
                    break;
                case HaulerState.Carrying:
                    SetMovementDestination(_storageWalkTarget.position);
                    break;
                case HaulerState.Returning:
                case HaulerState.WalkingToWaitPoint:
                    SetMovementDestination(_waitPoint.position);
                    break;
                case HaulerState.WalkingToPen:
                    SetMovementDestination(PenSpot.position);
                    break;
                case HaulerState.Idle:
                case HaulerState.StoredInPen:
                    StopMovement();
                    break;
            }
        }

        private void SetMovementDestination(Vector3 destination)
        {
            if (_navMeshAgent == null || !_navMeshAgent.enabled)
            {
                return;
            }

            _movementDestination = destination;
            _hasMovementDestination = true;
            _navMeshAgent.isStopped = false;
            _navMeshAgent.stoppingDistance = _arrivalDistance;
            _navMeshAgent.speed = CurrentSpeed;

            if (!IsAgentReady())
            {
                LogMissingNavMesh();
                return;
            }

            if (!_navMeshAgent.SetDestination(destination))
            {
                LogDestinationFailed();
            }
        }

        private void StopMovement()
        {
            if (!_hasMovementDestination)
            {
                return;
            }

            _hasMovementDestination = false;

            if (!IsAgentReady())
            {
                return;
            }

            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();
        }

        private void SyncAgentSpeed()
        {
            if (_navMeshAgent == null || !_navMeshAgent.enabled)
            {
                return;
            }

            float speed = CurrentSpeed;
            if (!Mathf.Approximately(_navMeshAgent.speed, speed))
            {
                _navMeshAgent.speed = speed;
            }
        }

        private bool HasReachedDestination()
        {
            if (!_hasMovementDestination)
            {
                return true;
            }

            if (!IsAgentReady())
            {
                return SqDistTo(_movementDestination) <= _sqArrival;
            }

            if (_navMeshAgent.pathPending)
            {
                return false;
            }

            if (_navMeshAgent.hasPath)
            {
                return _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance;
            }

            return SqDistTo(_movementDestination) <= _sqArrival;
        }

        private bool IsMoving()
        {
            if (!IsAgentReady())
            {
                return false;
            }

            return _navMeshAgent.pathPending ||
                   (_navMeshAgent.hasPath &&
                    _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance);
        }

        private bool IsAgentReady()
        {
            if (_navMeshAgent == null ||
                !_navMeshAgent.enabled ||
                !_navMeshAgent.gameObject.activeInHierarchy)
            {
                return false;
            }

            return _navMeshAgent.isOnNavMesh;
        }

        private void SetWalking(bool isWalking)
        {
            if (_animator == null || _animatorWalking == isWalking)
            {
                return;
            }

            _animatorWalking = isWalking;
            _animator.SetBool(WalkingHash, isWalking);
        }

        // XZ-only to match old manual movement arrival behavior and to provide
        // a fallback when the NavMeshAgent cannot report path distance.
        private float SqDistTo(Vector3 target)
        {
            float dx = transform.position.x - target.x;
            float dz = transform.position.z - target.z;
            return dx * dx + dz * dz;
        }

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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogMissingNavMesh()
        {
            Debug.LogWarning(
                $"[HaulerWorker '{gameObject.name}'] NavMeshAgent is not on a NavMesh.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogDestinationFailed()
        {
            Debug.LogWarning(
                $"[HaulerWorker '{gameObject.name}'] NavMeshAgent rejected destination " +
                $"{_movementDestination}.", this);
        }
    }
}
