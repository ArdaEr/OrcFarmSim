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

        [Header("Fish Visuals")]
        [Tooltip("Fish placeholder prefab. One instance per pool slot is pre-instantiated in Awake.")]
        [SerializeField] private GameObject _fishPrefab;

        [Tooltip("Center of the fish placement area. Falls back to this pond transform if unassigned.")]
        [SerializeField] private Transform _fishSpawnPoint;

        [Tooltip("Radius around _fishSpawnPoint within which fish visuals are placed randomly.")]
        [Min(0f)]
        [SerializeField] private float _fishSpawnJitterRadius = 0.25f;

        [Tooltip("Total visual slots pre-instantiated in Awake. Stocking more fish than this shows only this many visuals.")]
        [Min(1)]
        [SerializeField] private int _maxFishVisuals = 8;

        [Tooltip("Minimum XZ separation between fish visuals. Placement tries up to 10 positions per fish.")]
        [Min(0f)]
        [SerializeField] private float _minFishSeparation = 0.3f;

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

        private OrcQuality              _quality;
        private float                   _growthTimer;
        private float                   _stockedTimer;
        private float                   _starvationTimer;
        private bool                    _careGiven;
        private int                     _fishDeathCount;
        private CancellationTokenSource _readoutCts;

        // Thresholds for influence flag evaluation.
        private const float LowFeedThreshold       = 0.3f;  // fish FeedScore below this counts as low per frame
        private const float LowCareThreshold       = 0.3f;  // fish CareScore below this counts as low per frame
        private const float HalfFeedThreshold      = 0.5f;  // fish FeedScore ever below this → FeedEverBelowHalf
        private const float LowFeedRatioThreshold  = 0.35f; // fraction of grow time at low feed → LowFeed flag
        private const float LowCareRatioThreshold  = 0.35f; // fraction of grow time at low care → LowCare flag
        private const float GoodFeedRatioThreshold = 0.15f; // fraction of grow time at low feed → HighFeed flag
        private const float GoodCareRatioThreshold = 0.15f; // fraction of grow time at low care → GoodCare flag

        private readonly List<LegFishData>    _fish             = new List<LegFishData>(8);
        private readonly List<Transform>      _fishVisualPool   = new List<Transform>(8);
        private readonly List<LegGrowthVisual> _legGrowthVisuals = new List<LegGrowthVisual>(8);

        // Index i = visual/growth-visual for fish i. Null means slot unassigned or returned to pool.
        private Transform[]     _fishVisualByIndex;
        private LegGrowthVisual[] _poolGrowthVisuals; // cached from each pool object in Awake, never changes
        private bool            _fishVisualsReady;

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

            int fishCount = Mathf.Min(slot.Count, _config.PondCapacity);

            if (!_inventory.TryConsumeFromSelectedSlot(fishCount))
                return false;

            // Hotbar slots track ItemType only — tier is not available; Normal tier used for quality.
            InitializeFish(LegFryTier.Normal, fishCount);
            return true;
        }

        /// <inheritdoc/>
        public void InitializeFish(LegFryTier tier, int count)
        {
            _fish.Clear();
            _fishDeathCount = 0;
            for (int i = 0; i < count; i++)
                _fish.Add(new LegFishData());

            _quality = _fryData.GetBaseQuality(tier);
            AssignFishVisuals(count);
        }

        /// <inheritdoc/>
        public int GetCappedFishCount(LegFryTier tier) =>
            Mathf.Min(_fryData.GetFishCount(tier), _config.PondCapacity);

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
            // Legacy path has no individual fish data — use quality-based fallback directly.
            leg.SetTrait(TraitInfluenceFlags.None, TraitFallback.Select(finalQuality));
            leg.Initialize(_carry);
            _carry.PickUpLeg(leg);
            ReturnAllFishVisuals();

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
                ReturnAllFishVisuals();
            }
            else if (next == LegPondState.Growing)
            {
                ActivateFishVisuals();
            }
            else if (next == LegPondState.NeedsCare)
            {
                _starvationTimer = 0f;
            }
            else if (next == LegPondState.ReadyToHarvest)
            {
                SetLegGrowthVisualProgress(1f);
            }

            LogTransition(_pondState, next);
            _pondState = next;
            _stateMachine.ChangeState(CreateState(next));
        }

        // ── ILegPondStateContext — fish decay ──────────────────────────────────

        /// <inheritdoc/>
        public bool DecayFishScores(float feedDecay, float careDecay)
        {
            // Recover dt from the pre-multiplied feedDecay so we can accumulate time per fish.
            float dt = _config.FeedDecayRate > 0f ? feedDecay / _config.FeedDecayRate : 0f;

            bool anyAlive = false;
            for (int i = 0; i < _fish.Count; i++)
            {
                LegFishData fish = _fish[i];
                if (!fish.IsAlive)
                    continue;

                fish.FeedScore = Mathf.Max(0f, fish.FeedScore - feedDecay);
                fish.CareScore = Mathf.Max(0f, fish.CareScore - careDecay);

                if (fish.FeedScore < LowFeedThreshold)  fish.TimeLowFeed += dt;
                if (fish.CareScore < LowCareThreshold)  fish.TimeLowCare += dt;
                if (fish.FeedScore < HalfFeedThreshold) fish.FeedEverBelowHalf = true;

                if (fish.FeedScore <= 0f)
                {
                    fish.IsAlive = false;
                    _fishDeathCount++;
                    DeactivateFishVisualAt(i);
                }
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

                TraitInfluenceFlags flags = EvaluateLegInfluenceFlags(fish);
                fish.IsAlive = false;
                DeactivateFishVisualAt(i);
                leg.SetQuality(quality);
                leg.SetTrait(flags, SelectLegTrait(flags, quality));
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
        public string HarvestPrompt => "Harvest leg (" + AliveRemainingFishCount + " remaining)";

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

            InitFishVisualPool();

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

        private void OnValidate()
        {
            _fishSpawnJitterRadius = Mathf.Max(0f, _fishSpawnJitterRadius);
            _maxFishVisuals        = Mathf.Max(1, _maxFishVisuals);
            _minFishSeparation     = Mathf.Max(0f, _minFishSeparation);
        }

        private void Update()
        {
#if UNITY_EDITOR
            TickDebugHooks();
#endif
            _stateMachine.Update();
            UpdateLegGrowthVisuals();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Returns a formatted string with current state and per-fish scores.
        /// Called by <see cref="OrcFarm.UI.LegPondDebugPanel"/> each frame.
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("State: ").AppendLine(_pondState.ToString());
            sb.Append("Fish: ").Append(AliveRemainingFishCount).Append('/').Append(_fish.Count).AppendLine(" alive");
            for (int i = 0; i < _fish.Count; i++)
            {
                LegFishData f = _fish[i];
                sb.Append("Fish ").Append(i + 1)
                  .Append(": Feed ").Append(Mathf.RoundToInt(f.FeedScore * 100)).Append('%')
                  .Append(" Care ").Append(Mathf.RoundToInt(f.CareScore * 100)).Append('%')
                  .Append(' ').AppendLine(f.IsAlive ? "Alive" : "Dead");
            }
            return sb.ToString();
        }

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
            _resultText.text = "Harvested leg — " + quality + " quality";

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

        private void InitFishVisualPool()
        {
            if (_fishPrefab == null)
            {
                LogMissingFishPrefab();
                return;
            }

            _fishVisualByIndex  = new Transform[_maxFishVisuals];
            _poolGrowthVisuals  = new LegGrowthVisual[_maxFishVisuals];

            for (int i = 0; i < _maxFishVisuals; i++)
            {
                Transform visual = Instantiate(_fishPrefab, transform).transform;

                // Disable physics on static display visuals so they never interfere with gameplay.
                if (visual.TryGetComponent(out Collider col)) col.enabled    = false;
                if (visual.TryGetComponent(out Rigidbody  rb)) rb.isKinematic = true;

                _poolGrowthVisuals[i] = visual.GetComponentInChildren<LegGrowthVisual>();
                visual.gameObject.SetActive(false);
                _fishVisualPool.Add(visual);
            }

            _fishVisualsReady = true;
        }

        private void AssignFishVisuals(int fishCount)
        {
            ReturnAllFishVisuals();

            if (!_fishVisualsReady)
                return;

            int count = Mathf.Min(fishCount, _maxFishVisuals);
            for (int i = 0; i < count; i++)
            {
                Transform visual = _fishVisualPool[i];
                _fishVisualByIndex[i] = visual;
                PlaceFishVisual(visual, i);

                LegGrowthVisual growthVisual = _poolGrowthVisuals[i];
                if (growthVisual != null)
                {
                    growthVisual.SetProgress(0f);
                    _legGrowthVisuals.Add(growthVisual);
                }
                // Kept inactive — activated when the pond enters Growing.
            }
        }

        private void PlaceFishVisual(Transform visual, int fishIndex)
        {
            Transform spawnCenter = _fishSpawnPoint != null ? _fishSpawnPoint : transform;
            Vector3   center      = spawnCenter.position;
            float     radius      = _fishSpawnJitterRadius;
            float     sepSqr      = _minFishSeparation * _minFishSeparation;

            Vector3 bestPos  = center;
            float   bestDist = -1f;

            const int MaxAttempts = 10;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Vector2 jitter    = Random.insideUnitCircle * radius;
                Vector3 candidate = new Vector3(center.x + jitter.x, center.y, center.z + jitter.y);

                float minSqr = float.MaxValue;
                for (int j = 0; j < fishIndex; j++)
                {
                    if (_fishVisualByIndex[j] == null)
                        continue;

                    Vector3 delta = candidate - _fishVisualByIndex[j].position;
                    float   dsqr  = delta.x * delta.x + delta.z * delta.z;
                    if (dsqr < minSqr)
                        minSqr = dsqr;
                }

                if (minSqr >= sepSqr)
                {
                    visual.position = candidate;
                    return;
                }

                if (minSqr > bestDist)
                {
                    bestDist = minSqr;
                    bestPos  = candidate;
                }
            }

            // No separation-valid position found — use the best available attempt.
            visual.position = bestPos;
        }

        private void ActivateFishVisuals()
        {
            if (!_fishVisualsReady)
                return;

            for (int i = 0; i < _fish.Count && i < _maxFishVisuals; i++)
            {
                if (_fishVisualByIndex[i] != null)
                    _fishVisualByIndex[i].gameObject.SetActive(true);
            }
        }

        private void DeactivateFishVisualAt(int fishIndex)
        {
            if (!_fishVisualsReady || fishIndex >= _maxFishVisuals)
                return;

            if (_fishVisualByIndex[fishIndex] != null)
            {
                _fishVisualByIndex[fishIndex].gameObject.SetActive(false);
                _fishVisualByIndex[fishIndex] = null;
            }
        }

        private void ReturnAllFishVisuals()
        {
            if (!_fishVisualsReady)
                return;

            for (int i = 0; i < _maxFishVisuals; i++)
            {
                if (_fishVisualByIndex[i] != null)
                {
                    _fishVisualByIndex[i].gameObject.SetActive(false);
                    _fishVisualByIndex[i] = null;
                }
            }

            _legGrowthVisuals.Clear();
        }

        private void UpdateLegGrowthVisuals()
        {
            if (_pondState == LegPondState.Growing || _pondState == LegPondState.NeedsCare)
            {
                float progress = _growthTimer / _config.GrowthDuration;
                SetLegGrowthVisualProgress(progress);
            }
            else if (_pondState == LegPondState.ReadyToHarvest)
            {
                SetLegGrowthVisualProgress(1f);
            }
        }

        private void SetLegGrowthVisualProgress(float progress)
        {
            for (int i = 0; i < _legGrowthVisuals.Count; i++)
            {
                if (_legGrowthVisuals[i] != null)
                    _legGrowthVisuals[i].SetProgress(progress);
            }
        }

        private TraitInfluenceFlags EvaluateLegInfluenceFlags(LegFishData fish)
        {
            TraitInfluenceFlags flags = TraitInfluenceFlags.None;

            float totalTime = _growthTimer;
            if (totalTime > 0f)
            {
                float feedRatio = fish.TimeLowFeed / totalTime;
                float careRatio = fish.TimeLowCare / totalTime;

                if (feedRatio > LowFeedRatioThreshold)
                    flags |= TraitInfluenceFlags.LowFeed;
                else if (feedRatio < GoodFeedRatioThreshold && !fish.FeedEverBelowHalf)
                    flags |= TraitInfluenceFlags.HighFeed;

                if (careRatio > LowCareRatioThreshold)
                    flags |= TraitInfluenceFlags.LowCare;
                else if (careRatio < GoodCareRatioThreshold)
                    flags |= TraitInfluenceFlags.GoodCare;
            }

            if (_fish.Count >= _config.PondCapacity) flags |= TraitInfluenceFlags.Overstocked;
            else if (_fish.Count == 1)               flags |= TraitInfluenceFlags.Understocked;

            if (_fishDeathCount > 0) flags |= TraitInfluenceFlags.FishDeaths;

            return flags;
        }

        private static OrcTrait SelectLegTrait(TraitInfluenceFlags flags, OrcQuality quality)
        {
            if ((flags & TraitInfluenceFlags.FishDeaths)   != 0) return OrcTrait.Twitchy;
            if ((flags & TraitInfluenceFlags.LowFeed)      != 0) return OrcTrait.BoneIdle;
            if ((flags & TraitInfluenceFlags.Overstocked)  != 0) return OrcTrait.Brutish;
            if ((flags & TraitInfluenceFlags.Understocked) != 0) return OrcTrait.Resilient;
            if ((flags & TraitInfluenceFlags.GoodCare)  != 0 &&
                (flags & TraitInfluenceFlags.HighFeed)  != 0) return OrcTrait.Diligent;
            if ((flags & TraitInfluenceFlags.LowCare)      != 0) return OrcTrait.Clumsy;
            return TraitFallback.Select(quality);
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogMissingFishPrefab()
        {
            Debug.LogWarning(
                $"[LegPond '{gameObject.name}'] _fishPrefab is not assigned — " +
                "fish visuals will not appear. Assign a prefab in the Inspector.", this);
        }
    }
}
