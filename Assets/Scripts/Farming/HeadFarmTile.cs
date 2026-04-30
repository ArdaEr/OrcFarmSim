using OrcFarm.Carry;
using OrcFarm.Core;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// A single interactable tile inside a <see cref="HeadFarmPlot"/> 3×3 grid.
    ///
    /// Owns its own <see cref="HeadTileStateMachine"/> and implements
    /// <see cref="IHeadTileStateContext"/> so state classes can call back without
    /// depending on the concrete tile type (§1.9, §7.1).
    ///
    /// Row and column indices are assigned by the owning <see cref="HeadFarmPlot"/>
    /// during its Awake. Until assigned, <see cref="CanInteract"/> returns false.
    ///
    /// Setup: assign <c>_data</c>, <c>_headPool</c>, and <c>_inventory</c> in the
    /// Inspector. All three are mandatory — Awake logs and disables on any missing ref.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class HeadFarmTile : MonoBehaviour, IInteractable, IHeadTileStateContext, IFarmActionTarget, IFarmFocusTarget
    {
        [Tooltip("BoxCollider that defines this tile's interactable footprint. " +
                 "Adjust size and center here — do not resize via Transform scale.")]
        [SerializeField] private BoxCollider _tileCollider;

        [Tooltip("Shared ScriptableObject with cover delay, grow duration, and spawn offset ranges.")]
        [SerializeField] private HeadFarmTileData _data;

        [Tooltip("Scene HarvestedHeadPool that hands out pooled head GameObjects on harvest.")]
        [SerializeField] private HarvestedHeadPool _headPool;

        [Tooltip("Scene PlayerInventory used to check and consume HeadSeeds on planting.")]
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("Scene CarryController used to initialize harvested heads so the player can pick them up.")]
        [SerializeField] private CarryController _carryController;

        [Tooltip("Optional OutlineGlowTarget component enabled only while this tile is focused.")]
        [SerializeField] private MonoBehaviour _outlineGlowTarget;

        [Header("Growth Visual")]
        [Tooltip("Object that scales while this tile is Growing. Optional; leave empty for no crop growth visual.")]
        [SerializeField] private Transform _growthVisual;

        [Tooltip("Scale used when Growing begins.")]
        [Min(0f)]
        [SerializeField] private float _growthVisualStartScale = 0.2f;

        [Tooltip("Scale used once the tile reaches ReadyToHarvest.")]
        [Min(0f)]
        [SerializeField] private float _growthVisualHarvestScale = 1f;

#if UNITY_EDITOR
        [Header("Debug  —  Play Mode only")]
        [Tooltip("Forces a Growing tile to ReadyToHarvest immediately. Auto-resets after firing.")]
        [SerializeField] private bool _debugForceReadyToHarvest;

        [Tooltip("Forces a Growing tile to Dead immediately. Auto-resets after firing.")]
        [SerializeField] private bool _debugForceDead;
#endif

        // ── Private state ──────────────────────────────────────────────────────

        [HideInInspector]
        [SerializeField] private int _row;

        [HideInInspector]
        [SerializeField] private int _column;

        [HideInInspector]
        [SerializeField] private bool _indexAssigned;

        private bool                 _isFarmFocused;
        private float                _timer;
        private HeadTileState        _tileState = HeadTileState.Empty;
        private HeadTileStateMachine _stateMachine;

        // Condition scores — valid only during Growing state; reset on Growing enter.
        private float _feedScore;
        private float _waterScore;
        private float _careScore;

        // Influence tracking — accumulated during Growing; read at harvest; reset on next Growing enter.
        private float        _growthTotalTime;
        private float        _timeLowFeed;
        private float        _timeLowWater;
        private float        _timeLowCare;
        private HeadFarmPlot _plot;

        // Height above tile origin where the harvested head spawns before dropping.
        private const float HarvestSpawnHeight = 0.8f;

        // Thresholds for influence flag evaluation.
        private const float LowScoreThreshold  = 0.3f;  // score below this counts as "low" per frame
        private const float LowRatioThreshold  = 0.35f; // fraction of grow time at low score → bad flag
        private const float GoodRatioThreshold = 0.15f; // fraction of grow time at low score → good flag
        private const int   IsolationMax       = 1;     // active tile count ≤ this → Isolation
        private const int   CrowdedMin         = 6;     // active tile count ≥ this → Crowded

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// False until <see cref="AssignIndex"/> is called, the active state allows it,
        /// and the player is looking directly at this tile via FarmFocusDetector.
        /// </remarks>
        public bool CanInteract =>
            enabled && _indexAssigned && _stateMachine.CanInteract &&
            _isFarmFocused;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (_tileState == HeadTileState.Growing)
                return;
            _stateMachine.OnInteract();
        }

        // ── IHeadTileStateContext ──────────────────────────────────────────────

        /// <inheritdoc/>
        public HeadFarmTileData Data => _data;

        /// <inheritdoc/>
        public float Timer => _timer;

        /// <inheritdoc/>
        public void IncrementTimer(float delta) => _timer += delta;

        /// <inheritdoc/>
        public void ResetTimer() => _timer = 0f;

        /// <inheritdoc/>
        public void ResetGrowthVisual() => SetGrowthVisualProgress(0f);

        /// <inheritdoc/>
        public void SetGrowthVisualProgress(float progress)
        {
            if (_growthVisual == null)
                return;

            float scale = Mathf.Lerp(
                _growthVisualStartScale,
                _growthVisualHarvestScale,
                Mathf.Clamp01(progress));

            _growthVisual.localScale = Vector3.one * scale;
        }

        /// <inheritdoc/>
        public bool HasItem(ItemType type) =>
            _inventory != null && _inventory.Has(type);

        /// <inheritdoc/>
        public bool TryConsumeItem(ItemType type)
        {
            if (_inventory == null)
            {
                LogInventoryNotAssigned();
                return false;
            }
            return _inventory.TryConsume(type);
        }

        /// <summary>Label from the last successful harvest. Empty before any harvest.</summary>
        public string LastHarvestQualityLabel { get; private set; } = string.Empty;

        /// <inheritdoc/>
        public void SpawnHarvestedHead(OrcQuality quality)
        {
            LastHarvestQualityLabel = quality.ToString();

            Vector2 dir  = Random.insideUnitCircle.normalized;
            float   dist = Random.Range(_data.SpawnOffsetMin, _data.SpawnOffsetMax);
            Vector3 pos  = transform.position
                         + new Vector3(dir.x, 0f, dir.y) * dist
                         + Vector3.up * HarvestSpawnHeight;

            GameObject go = _headPool.Get(pos, Quaternion.identity);

            if (go == null)
                return; // pool exhausted — HarvestedHeadPool already logged a warning

            if (go.TryGetComponent(out HarvestedHead head))
            {
                head.Initialize(_carryController);
                head.SetQuality(quality);
                TraitInfluenceFlags flags = EvaluateHeadInfluenceFlags();
                head.SetTrait(flags, SelectHeadTrait(flags, quality));
            }
        }

        /// <inheritdoc/>
        public void TransitionTo(HeadTileState next)
        {
            if (next == HeadTileState.Growing)
                ResetInfluenceTracking();

            LogTransition(_tileState, next);
            _tileState = next;
            _stateMachine.ChangeState(CreateState(next));
        }

        // ── IHeadTileStateContext — condition scores ───────────────────────────

        /// <inheritdoc/>
        public float FeedScore  => _feedScore;

        /// <inheritdoc/>
        public float WaterScore => _waterScore;

        /// <inheritdoc/>
        public float CareScore  => _careScore;

        /// <inheritdoc/>
        public void SetFeedScore(float value)
        {
            _feedScore = Mathf.Clamp01(value);
            NotifyConditionChanged();
        }

        /// <inheritdoc/>
        public void SetWaterScore(float value)
        {
            _waterScore = Mathf.Clamp01(value);
            NotifyConditionChanged();
        }

        /// <inheritdoc/>
        public void SetCareScore(float value)
        {
            _careScore = Mathf.Clamp01(value);
            NotifyConditionChanged();
        }

        /// <inheritdoc/>
        public void ResetConditionScores()
        {
            _feedScore  = 1f;
            _waterScore = 1f;
            _careScore  = 1f;
            NotifyConditionChanged();
        }

        /// <inheritdoc/>
        public void TrackGrowthFrame(float dt)
        {
            _growthTotalTime += dt;
            if (_feedScore  < LowScoreThreshold) _timeLowFeed  += dt;
            if (_waterScore < LowScoreThreshold) _timeLowWater += dt;
            if (_careScore  < LowScoreThreshold) _timeLowCare  += dt;
        }

        /// <inheritdoc/>
        public bool IsPlayerCarrying => _carryController.IsCarrying;

        // ── Condition feedback stub ────────────────────────────────────────────

        /// <summary>
        /// Called whenever any condition score changes during Growing.
        /// Override in a subclass to drive visual and audio feedback.
        /// Currently a stub — no implementation required.
        /// </summary>
        protected virtual void OnConditionChanged(float feedScore, float waterScore, float careScore) { }

        private void NotifyConditionChanged() =>
            OnConditionChanged(_feedScore, _waterScore, _careScore);

        // ── IFarmActionTarget ──────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Called every frame by FarmFocusDetector — zero heap allocation.
        /// FarmActionContext is a readonly struct; HotbarSlot is a readonly struct.
        /// </remarks>
        public FarmActionContext GetActionContext()
        {
            if (_tileState != HeadTileState.Growing)
                return FarmActionContext.None;

            HotbarSlot selected = _inventory.GetSelectedSlot();
            return new FarmActionContext(
                feedVisible:  true,
                feedActive:   selected.SlotItemType == ItemType.Fertilizer && !selected.IsEmpty,
                waterVisible: true,
                waterActive:  selected.SlotItemType == ItemType.WaterItem  && !selected.IsEmpty,
                careVisible:  true,
                careActive:   HasEmptyHands());
        }

        /// <inheritdoc/>
        public void OnFeedAction()
        {
            if (_tileState != HeadTileState.Growing || _feedScore >= 1f)
                return;

            HotbarSlot slot = _inventory.GetSelectedSlot();
            if (slot.SlotItemType != ItemType.Fertilizer || slot.IsEmpty)
                return;

            if (_inventory.TryConsumeFromSelectedSlot(1))
                SetFeedScore(1f);
        }

        /// <inheritdoc/>
        public void OnWaterAction()
        {
            if (_tileState != HeadTileState.Growing || _waterScore >= 1f)
                return;

            HotbarSlot slot = _inventory.GetSelectedSlot();
            if (slot.SlotItemType != ItemType.WaterItem || slot.IsEmpty)
                return;

            if (_inventory.TryConsumeFromSelectedSlot(1))
                SetWaterScore(1f);
        }

        /// <inheritdoc/>
        public void OnCareAction()
        {
            if (_tileState != HeadTileState.Growing || !HasEmptyHands())
                return;

            SetCareScore(Mathf.Min(1f, _careScore + _data.CareRestoreAmount));
        }

        // ── IFarmFocusTarget ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public void SetFarmFocused(bool focused)
        {
            _isFarmFocused = focused;

            if (_outlineGlowTarget != null)
                _outlineGlowTarget.enabled = focused;
        }

        // ── Public read ────────────────────────────────────────────────────────

        /// <summary>Current lifecycle state. Exposed for HUD and debug inspection.</summary>
        public HeadTileState State => _tileState;

        /// <summary>
        /// Prompt string from the active state. Returned empty when the tile is not
        /// interactable. The HUD may use this once it adds a HeadFarmTile branch.
        /// </summary>
        public string InteractPrompt => _stateMachine != null ? _stateMachine.InteractPrompt : string.Empty;

        // ── Called by HeadFarmPlot ─────────────────────────────────────────────

        /// <summary>
        /// Sets the row and column index for this tile.
        /// Called exclusively by <see cref="HeadFarmPlot.Awake"/> after tile-count validation.
        /// </summary>
        public void AssignIndex(int row, int column)
        {
            _row           = row;
            _column        = column;
            _indexAssigned = true;
        }

        /// <summary>
        /// Assigns scene references supplied by the owning <see cref="HeadFarmPlot"/>
        /// when it generates or refreshes this tile in Edit Mode.
        /// </summary>
        public void AssignSceneReferences(
            HarvestedHeadPool headPool,
            PlayerInventory inventory,
            CarryController carryController)
        {
            if (headPool != null)
                _headPool = headPool;

            if (inventory != null)
                _inventory = inventory;

            if (carryController != null)
                _carryController = carryController;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_tileCollider == null)
            {
                Debug.LogError(
                    $"[HeadFarmTile '{gameObject.name}'] _tileCollider is not assigned.", this);
                enabled = false;
                return;
            }

            if (_data == null)
            {
                Debug.LogError(
                    $"[HeadFarmTile '{gameObject.name}'] _data (HeadFarmTileData) is not assigned.", this);
                enabled = false;
                return;
            }

            if (_headPool == null)
            {
                Debug.LogError(
                    $"[HeadFarmTile '{gameObject.name}'] _headPool (HarvestedHeadPool) is not assigned.", this);
                enabled = false;
                return;
            }

            if (_inventory == null)
            {
                Debug.LogError(
                    $"[HeadFarmTile '{gameObject.name}'] _inventory (PlayerInventory) is not assigned.", this);
                enabled = false;
                return;
            }

            if (_carryController == null)
            {
                Debug.LogError(
                    $"[HeadFarmTile '{gameObject.name}'] _carryController (CarryController) is not assigned.", this);
                enabled = false;
                return;
            }

            _growthVisualStartScale   = Mathf.Max(0f, _growthVisualStartScale);
            _growthVisualHarvestScale = Mathf.Max(_growthVisualStartScale, _growthVisualHarvestScale);

            _plot = GetComponentInParent<HeadFarmPlot>();
            _data.Validate();

            SetFarmFocused(false);

#if UNITY_EDITOR
            _debugForceReadyToHarvest = false;
            _debugForceDead           = false;
#endif

            _stateMachine = new HeadTileStateMachine();
            TransitionTo(HeadTileState.Empty);
        }

        private void Update()
        {
#if UNITY_EDITOR
            TickDebugHooks();
#endif
            _stateMachine.Update();
        }

        private void OnDisable()
        {
            SetFarmFocused(false);
        }

        // ── Debug hooks ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        // Both hooks fire before the state machine update so the forced state is
        // active for the remainder of this frame's tick. Fields are cleared before
        // the transition so a single inspector tick never fires twice.
        private void TickDebugHooks()
        {
            if (_debugForceReadyToHarvest)
            {
                _debugForceReadyToHarvest = false;
                if (_tileState == HeadTileState.Growing)
                    TransitionTo(HeadTileState.ReadyToHarvest);
            }

            if (_debugForceDead)
            {
                _debugForceDead = false;
                if (_tileState == HeadTileState.Growing)
                    TransitionTo(HeadTileState.Dead);
            }
        }
#endif

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ResetInfluenceTracking()
        {
            _growthTotalTime = 0f;
            _timeLowFeed     = 0f;
            _timeLowWater    = 0f;
            _timeLowCare     = 0f;
        }

        private TraitInfluenceFlags EvaluateHeadInfluenceFlags()
        {
            TraitInfluenceFlags flags = TraitInfluenceFlags.None;

            if (_growthTotalTime > 0f)
            {
                float feedRatio  = _timeLowFeed  / _growthTotalTime;
                float waterRatio = _timeLowWater / _growthTotalTime;
                float careRatio  = _timeLowCare  / _growthTotalTime;

                if (feedRatio  > LowRatioThreshold)  flags |= TraitInfluenceFlags.LowFeed;
                if (waterRatio > LowRatioThreshold)  flags |= TraitInfluenceFlags.NeglectedWater;
                else if (waterRatio < GoodRatioThreshold) flags |= TraitInfluenceFlags.GoodWater;
                if (careRatio  > LowRatioThreshold)  flags |= TraitInfluenceFlags.LowCare;
                else if (careRatio < GoodRatioThreshold)  flags |= TraitInfluenceFlags.GoodCare;
            }

            if (_plot != null)
            {
                int activeTiles = _plot.GetActiveTileCount();
                if (activeTiles <= IsolationMax)    flags |= TraitInfluenceFlags.Isolation;
                else if (activeTiles >= CrowdedMin) flags |= TraitInfluenceFlags.Crowded;
            }

            return flags;
        }

        private static OrcTrait SelectHeadTrait(TraitInfluenceFlags flags, OrcQuality quality)
        {
            if ((flags & TraitInfluenceFlags.LowFeed)   != 0) return OrcTrait.BoneIdle;
            if ((flags & TraitInfluenceFlags.LowCare)   != 0) return OrcTrait.Clumsy;
            if ((flags & TraitInfluenceFlags.GoodCare)  != 0 &&
                (flags & TraitInfluenceFlags.GoodWater) != 0) return OrcTrait.Resilient;
            if ((flags & TraitInfluenceFlags.GoodWater) != 0) return OrcTrait.Diligent;
            if ((flags & TraitInfluenceFlags.Crowded)   != 0) return OrcTrait.Brutish;
            if ((flags & TraitInfluenceFlags.Isolation) != 0) return OrcTrait.Twitchy;
            return TraitFallback.Select(quality);
        }

        private bool HasEmptyHands()
        {
            if (_carryController != null && _carryController.IsCarrying)
                return false;

            HotbarSlot slot = _inventory.GetSelectedSlot();
            return slot.IsEmpty;
        }

        private IHeadTileState CreateState(HeadTileState state) => state switch
        {
            HeadTileState.Empty          => new HeadTileEmptyState(this),
            HeadTileState.Tilled         => new HeadTileTilledState(this),
            HeadTileState.Seeded         => new HeadTileSeededState(this),
            HeadTileState.Covered        => new HeadTileCoveredState(this),
            HeadTileState.Growing        => new HeadTileGrowingState(this),
            HeadTileState.ReadyToHarvest => new HeadTileReadyToHarvestState(this),
            HeadTileState.Dead           => new HeadTileDeadState(this),
            _                            => throw new System.ArgumentOutOfRangeException(
                                               nameof(state), $"Unhandled HeadTileState: {state}")
        };

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogTransition(HeadTileState from, HeadTileState to)
        {
            if (from == to) return;
            Debug.Log($"[HeadFarmTile '{gameObject.name}'] {from} → {to}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogInventoryNotAssigned()
        {
            Debug.LogError(
                $"[HeadFarmTile '{gameObject.name}'] _inventory is not assigned. Cannot consume items.", this);
        }
    }
}
