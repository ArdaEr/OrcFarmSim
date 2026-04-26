using Cysharp.Threading.Tasks;
using OrcFarm.Carry;
using OrcFarm.Core;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using System.Collections.Generic;
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
    public sealed class LegPond : MonoBehaviour, IInteractable, ILegPondStateContext, IFarmActionTarget
    {
        [SerializeField] private LegPondConfig    _config;
        [SerializeField] private LegFryData       _fryData;
        [SerializeField] private LegFryCarrySlot  _legFryCarrySlot;
        [SerializeField] private HarvestedLegPool _legPool;

        [Tooltip("Assign the FarmFocusDetector component from the player. " +
                 "Ensures E interaction only fires when the player is looking at this pond.")]
        [SerializeField] private MonoBehaviour _farmFocusBehaviour;

        [Tooltip("Optional. TMP text element that shows the harvest quality readout. " +
                 "Assign the same result-readout TMP used by AssemblyStation.")]
        [SerializeField] private TextMeshProUGUI _resultText;

        [Tooltip("Seconds the harvest quality readout stays visible before auto-clearing. " +
                 "Match the AssemblyStation _readoutClearDelay (default 4).")]
        [Min(0.1f)]
        [SerializeField] private float _readoutClearDelay = 4f;

        private IPlayerInventory    _inventory;
        private ICarryController    _carry;
        private IFarmFocusSource    _farmFocus;
        private LegPondStateMachine _stateMachine;
        private LegPondState        _pondState = LegPondState.Empty;

        private OrcQuality             _quality;
        private float                  _growthTimer;
        private float                  _stockedTimer;
        private float                  _starvationTimer;
        private bool                   _careGiven;
        private CancellationTokenSource _readoutCts;

        private readonly List<LegFishData> _fish = new List<LegFishData>(8);

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
        public LegFryItem CarriedLegFry => _legFryCarrySlot.CarriedItem;

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
        public void ConsumeCarriedLegFry() => _legFryCarrySlot.Consume();

        /// <inheritdoc/>
        public bool TryStockFromHotbar()
        {
            if (_inventory == null)
                return false;

            HotbarSlot slot = _inventory.GetSelectedSlot();
            if (slot.SlotItemType != ItemType.LegFry || slot.IsEmpty)
                return false;

            if (!_inventory.TryConsumeFromSelectedSlot(1))
                return false;

            InitializeFish(LegFryTier.Normal);
            return true;
        }

        /// <inheritdoc/>
        public void InitializeFish(LegFryTier tier)
        {
            _fish.Clear();
            int count = _fryData.GetFishCount(tier);
            for (int i = 0; i < count; i++)
                _fish.Add(new LegFishData());

            _quality = _fryData.GetBaseQuality(tier);
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
                _fish.Clear();
            }
            else if (next == LegPondState.NeedsCare)
            {
                _starvationTimer = 0f;
            }

            LogTransition(_pondState, next);
            _pondState = next;
            _stateMachine.ChangeState(CreateState(next));
        }

        // ── ILegPondStateContext — fish decay ──────────────────────────────────

        /// <inheritdoc/>
        public bool DecayFishScores(float feedDecay, float careDecay)
        {
            bool anyAlive = false;
            for (int i = 0; i < _fish.Count; i++)
            {
                LegFishData fish = _fish[i];
                if (!fish.IsAlive)
                    continue;

                fish.FeedScore = Mathf.Max(0f, fish.FeedScore - feedDecay);
                fish.CareScore = Mathf.Max(0f, fish.CareScore - careDecay);

                if (fish.FeedScore <= 0f)
                    fish.IsAlive = false;
                else
                    anyAlive = true;
            }

            return !anyAlive;
        }

        // ── ILegPondStateContext — per-fish harvest ────────────────────────────

        /// <inheritdoc/>
        public int AliveRemainingFishCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _fish.Count; i++)
                    if (_fish[i].IsAlive) count++;
                return count;
            }
        }

        /// <inheritdoc/>
        public void HarvestNextLeg()
        {
            for (int i = 0; i < _fish.Count; i++)
            {
                LegFishData fish = _fish[i];
                if (!fish.IsAlive)
                    continue;

                OrcQuality quality = CalculateFishQuality(fish);

                Vector2 circle   = Random.insideUnitCircle.normalized;
                float   dist     = Random.Range(_config.SpawnOffsetMin, _config.SpawnOffsetMax);
                Vector3 offset   = new Vector3(circle.x, 0f, circle.y) * dist;
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

                fish.IsAlive = false;
                leg.SetQuality(quality);
                leg.Initialize(_carry);
                _carry.PickUpLeg(leg);
                ShowHarvestReadout(quality);
                return;
            }
        }

        // ── IFarmActionTarget ──────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>Called every frame by FarmFocusDetector — zero heap allocation.</remarks>
        public FarmActionContext GetActionContext()
        {
            if (_pondState != LegPondState.Growing && _pondState != LegPondState.NeedsCare)
                return FarmActionContext.None;

            bool feedActive = false;
            if (_inventory != null)
            {
                HotbarSlot slot = _inventory.GetSelectedSlot();
                if (slot.SlotItemType == ItemType.FeedItem && !slot.IsEmpty)
                {
                    for (int i = 0; i < _fish.Count; i++)
                    {
                        if (_fish[i].IsAlive && _fish[i].FeedScore < 1f)
                        {
                            feedActive = true;
                            break;
                        }
                    }
                }
            }

            return new FarmActionContext(
                feedVisible:  true,  feedActive:  feedActive,
                waterVisible: false, waterActive: false,
                careVisible:  true,  careActive:  HasEmptyHands());
        }

        /// <inheritdoc/>
        public void OnFeedAction()
        {
            if (_pondState != LegPondState.Growing && _pondState != LegPondState.NeedsCare)
                return;

            int lowestIndex = -1;
            float lowestScore = 1f;
            for (int i = 0; i < _fish.Count; i++)
            {
                if (_fish[i].IsAlive && _fish[i].FeedScore < lowestScore)
                {
                    lowestScore  = _fish[i].FeedScore;
                    lowestIndex  = i;
                }
            }

            if (lowestIndex < 0)
                return; // All alive fish are already fully fed — do not consume

            if (_inventory == null)
                return;

            HotbarSlot slot = _inventory.GetSelectedSlot();
            if (slot.SlotItemType != ItemType.FeedItem || slot.IsEmpty)
                return;

            if (!_inventory.TryConsumeFromSelectedSlot(1))
                return;

            _fish[lowestIndex].FeedScore = 1f;
        }

        /// <inheritdoc/>
        public void OnWaterAction() { } // Pond never uses water

        /// <inheritdoc/>
        public void OnCareAction()
        {
            if ((_pondState != LegPondState.Growing && _pondState != LegPondState.NeedsCare) || !HasEmptyHands())
                return;

            for (int i = 0; i < _fish.Count; i++)
            {
                if (_fish[i].IsAlive)
                    _fish[i].CareScore = Mathf.Min(1f, _fish[i].CareScore + _config.CareRestoreAmount);
            }
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Requires the player to be looking at this pond via FarmFocusDetector —
        /// prevents E interaction from firing when the pond is only within proximity range.
        /// </remarks>
        public bool CanInteract =>
            enabled && _stateMachine.CanInteract &&
            _farmFocus?.CurrentTarget == (IFarmActionTarget)this;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_pondState == LegPondState.Growing || _pondState == LegPondState.NeedsCare)
                return;
            _stateMachine.OnInteract();
        }

        // ── Public state read ──────────────────────────────────────────────────

        /// <summary>Current lifecycle state. Exposed for UI and debug inspection.</summary>
        public LegPondState State => _pondState;

        /// <summary>
        /// Dynamic prompt shown by InteractHUD during ReadyToHarvest.
        /// Rebuilt only when alive fish count changes — string concat acceptable outside hot path.
        /// </summary>
        public string HarvestPrompt => "Harvest leg  (" + AliveRemainingFishCount + " remaining)";

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_config == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] LegPondConfig is not assigned.");

            if (_fryData == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] LegFryData is not assigned.");

            if (_legFryCarrySlot == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] LegFryCarrySlot is not assigned.");

            if (_legPool == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] HarvestedLegPool is not assigned.");

            if (_farmFocusBehaviour == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] _farmFocusBehaviour is not assigned. " +
                    "Drag FarmFocusDetector from the player.");

            _farmFocus = _farmFocusBehaviour as IFarmFocusSource;
            if (_farmFocus == null)
                throw new System.InvalidOperationException(
                    $"[LegPond '{gameObject.name}'] _farmFocusBehaviour does not implement " +
                    "IFarmFocusSource — assign FarmFocusDetector.");

            _config.Validate();
            _fryData.Validate();

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

        private OrcQuality CalculateFishQuality(LegFishData fish)
        {
            float avg     = (fish.FeedScore + fish.CareScore) * 0.5f;
            int   baseInt = (int)_quality;

            if (avg >= _config.HighQualityThreshold)
                return (OrcQuality)Mathf.Min(baseInt + 1, (int)OrcQuality.High);

            if (avg >= _config.NormalQualityThreshold)
                return _quality;

            return (OrcQuality)Mathf.Max(baseInt - 1, (int)OrcQuality.Low);
        }

        private bool HasEmptyHands()
        {
            if (_carry != null && _carry.IsCarrying)
                return false;

            if (_inventory != null)
            {
                HotbarSlot slot = _inventory.GetSelectedSlot();
                if (!slot.IsEmpty)
                    return false;
            }

            return true;
        }

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
