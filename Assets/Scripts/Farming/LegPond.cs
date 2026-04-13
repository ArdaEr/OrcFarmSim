using Cysharp.Threading.Tasks;
using OrcFarm.Carry;
using OrcFarm.Core;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using System.Threading;
using TMPro;
using UnityEngine;
using VContainer;

namespace OrcFarm.Farming
{
    /// <summary>
    /// A leg pond farming interactable. Acts as the state-machine context (§1.9) and
    /// exposes <see cref="IInteractable"/> so the interaction system can drive it.
    ///
    /// State flow:
    ///   Empty → Stocked → Growing → NeedsCare → ReadyToHarvest
    ///                                          ↘ Starved
    ///
    /// All per-state logic lives in the individual state classes; this class only
    /// orchestrates the machine and owns the context data states read/write (§7.5).
    ///
    /// On harvest, spawns a <see cref="HarvestedLeg"/> from <see cref="HarvestedLegPool"/>
    /// and immediately places it in the player's carry slot via
    /// <see cref="ICarryController.PickUpLeg"/>. If the player was already carrying
    /// something it is physically dropped first.
    ///
    /// Requires a Collider on the same GameObject so
    /// <see cref="InteractionDetector"/> can detect it inside its trigger sphere.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LegPond : MonoBehaviour, IInteractable, ILegPondStateContext
    {
        [SerializeField] private LegPondConfig    _config;
        [SerializeField] private HarvestedLegPool _legPool;

        [Tooltip("Optional. TMP text element that shows the harvest quality readout. " +
                 "Assign the same result-readout TMP used by AssemblyStation.")]
        [SerializeField] private TextMeshProUGUI _resultText;

        [Tooltip("Seconds the harvest quality readout stays visible before auto-clearing. " +
                 "Match the AssemblyStation _readoutClearDelay (default 4).")]
        [Min(0.1f)]
        [SerializeField] private float _readoutClearDelay = 4f;

        private IPlayerInventory  _inventory;
        private ICarryController  _carry;
        private LegPondStateMachine _stateMachine;
        private LegPondState      _pondState = LegPondState.Empty;

        private OrcQuality             _quality;
        private float                  _growthTimer;
        private float                  _stockedTimer;
        private float                  _starvationTimer;
        private bool                   _careGiven;
        private CancellationTokenSource _readoutCts;

#if UNITY_EDITOR
        // ── Debug hooks (Play Mode only) ───────────────────────────────────────
        // One-shot bools: tick in the Inspector during Play Mode; auto-reset after
        // one frame. Same pattern as FarmPlot and PlayerInventory debug hooks.

        [Header("Debug  —  Play Mode only")]
        [Tooltip("Advances a Growing pond to NeedsCare immediately. Auto-resets after firing.")]
        [SerializeField] private bool _debugForceNeedsCare;

        [Tooltip("Advances a Growing or NeedsCare pond to ReadyToHarvest immediately. Auto-resets after firing.")]
        [SerializeField] private bool _debugForceReadyToHarvest;
#endif

        // ── VContainer injection ───────────────────────────────────────────────

        /// <summary>Receives services from VContainer (§1.3).</summary>
        [Inject]
        private void Construct(IPlayerInventory inventory, ICarryController carry)
        {
            _inventory = inventory;
            _carry     = carry;
        }

        // ── ILegPondStateContext ───────────────────────────────────────────────

        /// <inheritdoc/>
        public LegPondConfig Config => _config;

        /// <inheritdoc/>
        public OrcQuality CurrentQuality => _quality;

        /// <inheritdoc/>
        public float GrowthTimer => _growthTimer;

        /// <inheritdoc/>
        public float StockedTimer => _stockedTimer;

        /// <inheritdoc/>
        public float StarvationTimer => _starvationTimer;

        /// <inheritdoc/>
        public bool CareGiven => _careGiven;

        /// <inheritdoc/>
        public void IncrementGrowthTimer(float delta)     => _growthTimer     += delta;

        /// <inheritdoc/>
        public void IncrementStockedTimer(float delta)    => _stockedTimer    += delta;

        /// <inheritdoc/>
        public void IncrementStarvationTimer(float delta) => _starvationTimer += delta;

        /// <inheritdoc/>
        public void SetCareGiven() => _careGiven = true;

        /// <inheritdoc/>
        public void ResetStarvationTimer() => _starvationTimer = 0f;

        /// <inheritdoc/>
        public void UpgradeQuality()
        {
            if      (_quality == OrcQuality.Low)    _quality = OrcQuality.Normal;
            else if (_quality == OrcQuality.Normal) _quality = OrcQuality.High;
            // High is the ceiling — no-op
        }

        /// <inheritdoc/>
        public bool TryConsumeItem(ItemType type)
        {
            if (_inventory == null)
            {
                LogInventoryNotInjected();
                return false;
            }

            if (!_inventory.TryConsume(type))
            {
                LogInventoryBlocked(type.ToString());
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public void SpawnAndCarryLeg()
        {
            Vector2 circle   = Random.insideUnitCircle.normalized;
            Vector3 offset   = new Vector3(circle.x, 0f, circle.y) * _config.HarvestSpawnRadius;
            Vector3 spawnPos = transform.position + offset + Vector3.up * _config.HarvestSpawnHeight;

            GameObject go = _legPool.Get(spawnPos, Quaternion.identity);
            if (go == null)
            {
                LogPoolExhausted();
                return;
            }

            if (!go.TryGetComponent(out HarvestedLeg leg))
            {
                LogMissingComponent(go.name);
                return;
            }

            OrcQuality finalQuality = _quality;
            if (_quality == OrcQuality.Normal && Random.value < _config.HighQualityChance)
                finalQuality = OrcQuality.High;

            leg.SetQuality(finalQuality);
            leg.Initialize(_carry);
            _carry.PickUpLeg(leg);

            ShowHarvestReadout(finalQuality);
        }

        /// <inheritdoc/>
        public void TransitionTo(LegPondState next)
        {
            // Reset tracking data for states that begin a new phase.
            if (next == LegPondState.Empty)
            {
                _growthTimer     = 0f;
                _stockedTimer    = 0f;
                _starvationTimer = 0f;
                _careGiven       = false;
                _quality         = OrcQuality.Low;
            }
            else if (next == LegPondState.NeedsCare)
            {
                _starvationTimer = 0f;
            }

            LogTransition(_pondState, next);
            _pondState = next;
            _stateMachine.ChangeState(CreateState(next));
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool CanInteract => enabled && _stateMachine.CanInteract;

        /// <inheritdoc/>
        public void OnInteract() => _stateMachine.OnInteract();

        // ── Public state read ──────────────────────────────────────────────────

        /// <summary>Current lifecycle state. Exposed for UI and debug inspection.</summary>
        public LegPondState State => _pondState;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_config == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] LegPondConfig is not assigned.");

            if (_legPool == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] HarvestedLegPool is not assigned.");

            _config.Validate();

            if (_resultText != null)
                _resultText.text = string.Empty;

#if UNITY_EDITOR
            _debugForceNeedsCare      = false; // discard any stale tick left from Edit Mode
            _debugForceReadyToHarvest = false;
#endif
            _stateMachine = new LegPondStateMachine();
            TransitionTo(LegPondState.Empty);
        }

        private void OnDestroy()
        {
            _readoutCts?.Cancel();
            _readoutCts?.Dispose();
        }

        private void Update()
        {
#if UNITY_EDITOR
            TickDebugHooks();
#endif
            _stateMachine.Update();
        }

#if UNITY_EDITOR
        // Runs before the state machine so the forced state is active for the
        // remainder of this frame's tick. Both fields are cleared before the
        // transition fires so a single inspector tick never fires twice.
        private void TickDebugHooks()
        {
            if (_debugForceNeedsCare)
            {
                _debugForceNeedsCare = false;
                if (_pondState == LegPondState.Growing)
                    TransitionTo(LegPondState.NeedsCare);
            }

            if (_debugForceReadyToHarvest)
            {
                _debugForceReadyToHarvest = false;
                if (_pondState == LegPondState.Growing || _pondState == LegPondState.NeedsCare)
                    TransitionTo(LegPondState.ReadyToHarvest);
            }
        }
#endif

        // ── Readout ────────────────────────────────────────────────────────────

        private void ShowHarvestReadout(OrcQuality quality)
        {
            if (_resultText == null)
                return;

            // String concat is acceptable here — OnInteract is event-driven, not per-frame (§3.3).
            _resultText.text = "Harvested leg  —  " + quality + " quality";

            _readoutCts?.Cancel();
            _readoutCts?.Dispose();
            _readoutCts = new CancellationTokenSource();
            ClearReadoutAsync(_readoutCts.Token).Forget(e =>
            {
                if (e is not System.OperationCanceledException)
                    Debug.LogException(e, this);
            });
        }

        private async UniTask ClearReadoutAsync(CancellationToken ct)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_readoutClearDelay),
                cancellationToken: ct);

            if (_resultText != null)
                _resultText.text = string.Empty;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private ILegPondState CreateState(LegPondState state) => state switch
        {
            LegPondState.Empty          => new LegPondEmptyState(this),
            LegPondState.Stocked        => new LegPondStockedState(this),
            LegPondState.Growing        => new LegPondGrowingState(this),
            LegPondState.NeedsCare      => new LegPondNeedsCareState(this),
            LegPondState.ReadyToHarvest => new LegPondReadyToHarvestState(this),
            LegPondState.Starved        => new LegPondStarvedState(this),
            _                           => throw new System.ArgumentOutOfRangeException(
                                               nameof(state), $"Unhandled LegPondState: {state}"),
        };

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogTransition(LegPondState from, LegPondState to)
        {
            if (from == to) return;
            Debug.Log($"[LegPond '{gameObject.name}'] {from} → {to}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogInventoryNotInjected()
        {
            Debug.LogError(
                $"[LegPond '{gameObject.name}'] IPlayerInventory was not injected. " +
                "Register this LegPond in RootLifetimeScope.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogInventoryBlocked(string itemName)
        {
            Debug.Log(
                $"[LegPond '{gameObject.name}'] Cannot proceed — no {itemName} in inventory.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogPoolExhausted()
        {
            Debug.LogWarning(
                $"[LegPond '{gameObject.name}'] HarvestedLegPool exhausted — harvest result lost. " +
                "Increase HarvestedLegPool._initialCapacity.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogMissingComponent(string goName)
        {
            Debug.LogError(
                $"[LegPond '{gameObject.name}'] Pool object '{goName}' has no HarvestedLeg component. " +
                "Check the HarvestedLegPool prefab.", this);
        }
    }
}
