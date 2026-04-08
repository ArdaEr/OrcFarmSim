using MessagePipe;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using UnityEngine;
using VContainer;

namespace OrcFarm.Farming
{
    /// <summary>
    /// A single farming plot. Acts as the state-machine context (§1.9) and exposes
    /// <see cref="IInteractable"/> so the interaction system can drive it.
    ///
    /// All per-state logic lives in the individual state classes; this class only
    /// orchestrates the machine and owns the context data states read/write (§7.5).
    ///
    /// On harvest, publishes <see cref="CropHarvestedSignal"/> via MessagePipe (§2.1).
    /// <see cref="OrcFarm.App.HarvestCoordinator"/> subscribes and handles head
    /// instantiation and pickup — FarmPlot has no direct dependency on the carry system.
    ///
    /// Requires a Collider on the same GameObject so
    /// <see cref="InteractionDetector"/> can detect it inside its trigger sphere.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FarmPlot : MonoBehaviour, IInteractable, IFarmPlotStateContext
    {
        [SerializeField] private HeadSeedConfig _config;

        [Tooltip("Metres above the plot origin where the harvested head spawns.")]
        [SerializeField] private float _harvestSpawnHeight = 0.8f;

        private IPlayerInventory               _inventory;
        private IPublisher<CropHarvestedSignal> _harvestPublisher;
        private FarmPlotStateMachine            _stateMachine;
        private PlotState                       _plotState = PlotState.Empty;

        // ── VContainer injection ───────────────────────────────────────────────

        /// <summary>Receives services from VContainer (§1.3).</summary>
        [Inject]
        private void Construct(
            IPlayerInventory               inventory,
            IPublisher<CropHarvestedSignal> harvestPublisher)
        {
            _inventory        = inventory;
            _harvestPublisher = harvestPublisher;
        }

        // ── IFarmPlotStateContext — growth tracking data ───────────────────────

        /// <inheritdoc/>
        public HeadSeedConfig Config => _config;

        /// <inheritdoc/>
        public float GrowthTimer { get; private set; }

        /// <inheritdoc/>
        public float CareWindowTimer { get; private set; }

        /// <inheritdoc/>
        public bool CareGiven { get; private set; }

        /// <inheritdoc/>
        public void IncrementGrowthTimer(float delta)     => GrowthTimer     += delta;

        /// <inheritdoc/>
        public void IncrementCareWindowTimer(float delta)  => CareWindowTimer += delta;

        /// <inheritdoc/>
        public void ResetGrowthTracking()
        {
            GrowthTimer     = 0f;
            CareWindowTimer = 0f;
            CareGiven       = false;
        }

        /// <inheritdoc/>
        public void SetCareGiven() => CareGiven = true;

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
        public void SpawnHarvestedHead()
        {
            Vector3 spawnPosition = transform.position + Vector3.up * _harvestSpawnHeight;
            _harvestPublisher.Publish(new CropHarvestedSignal(spawnPosition)); // (§2.1)
        }

        /// <inheritdoc/>
        public void TransitionTo(PlotState next)
        {
            LogTransition(_plotState, next);
            _plotState = next;
            _stateMachine.ChangeState(CreateState(next));
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool CanInteract => enabled && _stateMachine.CanInteract;

        /// <inheritdoc/>
        public void OnInteract() => _stateMachine.OnInteract();

        // ── Public state read ──────────────────────────────────────────────────

        /// <summary>Current lifecycle state. Exposed for UI and debug inspection.</summary>
        public PlotState State => _plotState;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_config == null)
                throw new System.InvalidOperationException(
                    $"[FarmPlot] HeadSeedConfig is not assigned on '{gameObject.name}'.");

            _config.Validate();

            _stateMachine = new FarmPlotStateMachine();
            TransitionTo(PlotState.Empty);
        }

        // Controllers call StateMachine.Update(); all branching lives inside state classes (§7.5).
        private void Update() => _stateMachine.Update();

        // ── Helpers ────────────────────────────────────────────────────────────

        private IFarmPlotState CreateState(PlotState state) => state switch
        {
            PlotState.Empty          => new EmptyState(this),
            PlotState.Prepared       => new PreparedState(this),
            PlotState.Fertilized     => new FertilizedState(this),
            PlotState.Planted        => new PlantedState(this),
            PlotState.Growing        => new GrowingState(this),
            PlotState.NeedsCare      => new NeedsCareState(this),
            PlotState.ReadyToHarvest => new ReadyToHarvestState(this),
            PlotState.FailedCrop     => new FailedCropState(this),
            _                        => throw new System.ArgumentOutOfRangeException(
                                            nameof(state), $"Unhandled PlotState: {state}")
        };

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogTransition(PlotState from, PlotState to)
        {
            Debug.Log($"[FarmPlot '{gameObject.name}'] {from} → {to}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogInventoryNotInjected()
        {
            Debug.LogError($"[FarmPlot '{gameObject.name}'] IPlayerInventory was not injected. Register this FarmPlot in RootLifetimeScope.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogInventoryBlocked(string itemName)
        {
            Debug.Log($"[FarmPlot '{gameObject.name}'] Cannot proceed — no {itemName} in inventory.", this);
        }
    }
}
