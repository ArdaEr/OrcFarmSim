using OrcFarm.Carry;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// A single farming plot driven by an explicit state machine.
    /// Implements <see cref="IInteractable"/> so the player can advance the plot
    /// through preparation, planting, care, and harvest.
    ///
    /// Requires a Collider on the same GameObject so
    /// <see cref="InteractionDetector"/> can detect it inside its trigger sphere.
    /// Any Collider shape is acceptable (Box, Mesh, etc.).
    ///
    /// Harvest setup:
    ///   - Assign a <see cref="HarvestedHead"/> prefab to <c>_harvestedHeadPrefab</c>.
    ///   - Assign the scene's <see cref="CarryController"/> to <c>_carryController</c>.
    ///     If omitted the head still spawns but cannot be picked up automatically.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FarmPlot : MonoBehaviour, IInteractable
    {
        [SerializeField] private HeadSeedConfig  _config;
        [SerializeField] private HarvestedHead   _harvestedHeadPrefab;
        [SerializeField] private CarryController _carryController;

        [Tooltip("Metres above the plot origin where the harvested head spawns.")]
        [SerializeField] private float _harvestSpawnHeight = 0.8f;

        [Tooltip("Player inventory used to check and consume seeds and fertilizer. " +
                 "If unassigned, fertilizer and seed gates are skipped (useful for quick tests).")]
        [SerializeField] private PlayerInventory _playerInventory;

        private PlotState _state            = PlotState.Empty;
        private float     _growthTimer;       // seconds since planting; advances only in Growing
        private float     _careWindowTimer;   // seconds since NeedsCare entered
        private bool      _careGiven;         // true once the player has cared this cycle

        /// <summary>Current lifecycle state. Exposed for UI and debug inspection.</summary>
        public PlotState State => _state;

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>False while Growing or Planted — those states need no player input.</remarks>
        public bool CanInteract => enabled && IsInteractableState(_state);

        /// <inheritdoc/>
        public void OnInteract()
        {
            switch (_state)
            {
                case PlotState.Empty:          Transition(PlotState.Prepared);   break;
                case PlotState.Prepared:       TryFertilize();                   break;
                case PlotState.Fertilized:     TryPlant();                       break;
                case PlotState.NeedsCare:      GiveCare();                       break;
                case PlotState.ReadyToHarvest: Harvest();                        break;
                case PlotState.FailedCrop:     Transition(PlotState.Empty);      break;
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError(
                    $"[FarmPlot] HeadSeedConfig is not assigned on '{gameObject.name}'. Plot disabled.", this);
                enabled = false;
                return;
            }

            try
            {
                _config.Validate();
            }
            catch (System.InvalidOperationException e)
            {
                Debug.LogError(e.Message, this);
                enabled = false;
                return;
            }

            if (_harvestedHeadPrefab == null)
                Debug.LogWarning(
                    $"[FarmPlot] HarvestedHeadPrefab not assigned on '{gameObject.name}'. Harvest will not produce output.", this);

            if (_carryController == null)
                Debug.LogWarning(
                    $"[FarmPlot] CarryController not assigned on '{gameObject.name}'. Harvested head will not be auto-carried.", this);

            if (_playerInventory == null)
                Debug.LogWarning(
                    $"[FarmPlot] PlayerInventory not assigned on '{gameObject.name}'. " +
                    "Fertilizer and seed gates are disabled — plot will advance without consuming items.", this);
        }

        // Zero per-frame allocations: switch on value type, float arithmetic only (§3.1).
        private void Update()
        {
            switch (_state)
            {
                case PlotState.Planted:   BeginGrowing();  break;
                case PlotState.Growing:   TickGrowing();   break;
                case PlotState.NeedsCare: TickNeedsCare(); break;
            }
        }

        // ── State handlers ─────────────────────────────────────────────────────

        /// <summary>
        /// Called once on the first Update tick after planting.
        /// Resets all growth tracking before the timer starts.
        /// </summary>
        private void BeginGrowing()
        {
            _growthTimer = 0f;
            _careGiven   = false;
            Transition(PlotState.Growing);
        }

        /// <summary>
        /// Advances the growth timer. Opens the care window at the configured
        /// checkpoint and completes growth when the full duration is reached.
        /// The timer is paused while in NeedsCare.
        /// </summary>
        private void TickGrowing()
        {
            _growthTimer += Time.deltaTime;

            if (!_careGiven && _growthTimer >= _config.CareCheckpointTime)
            {
                _careWindowTimer = 0f;
                Transition(PlotState.NeedsCare);
                return; // don't check ReadyToHarvest in the same tick
            }

            if (_growthTimer >= _config.GrowthDuration)
            {
                Transition(PlotState.ReadyToHarvest);
            }
        }

        /// <summary>
        /// Counts down the care window. Fails the crop if the player does not
        /// interact before <see cref="HeadSeedConfig.CareWindowDuration"/> expires.
        /// </summary>
        private void TickNeedsCare()
        {
            _careWindowTimer += Time.deltaTime;

            if (_careWindowTimer >= _config.CareWindowDuration)
            {
                Transition(PlotState.FailedCrop);
            }
        }

        private void GiveCare()
        {
            _careGiven = true;
            Transition(PlotState.Growing);
        }

        private void TryFertilize()
        {
            if (_playerInventory == null || !_playerInventory.TryConsume(ItemType.Fertilizer))
            {
                LogInventoryBlocked("Fertilizer", PlotState.Fertilized);
                return;
            }
            Transition(PlotState.Fertilized);
        }

        private void TryPlant()
        {
            if (_playerInventory == null || !_playerInventory.TryConsume(ItemType.HeadSeed))
            {
                LogInventoryBlocked("HeadSeed", PlotState.Planted);
                return;
            }
            Transition(PlotState.Planted);
        }

        private void Harvest()
        {
            // Both references are required to produce a usable harvest result.
            // Missing either one leaves the plot in ReadyToHarvest so the designer
            // can fix the inspector assignment and try again.
            if (_harvestedHeadPrefab == null || _carryController == null)
                return;

            Vector3 spawnPosition = transform.position + Vector3.up * _harvestSpawnHeight;
            HarvestedHead head = Instantiate(_harvestedHeadPrefab, spawnPosition, Quaternion.identity);
            head.Initialize(_carryController);

            // Hand off to carry immediately if possible.
            // If the player is already carrying, PickUp auto-drops the current head first
            // (existing CarryController behaviour) — so PickUp always succeeds here.
            _carryController.PickUp(head);

            Transition(PlotState.Empty);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void Transition(PlotState next)
        {
            LogTransition(_state, next);
            _state = next;
        }

        private static bool IsInteractableState(PlotState state) => state switch
        {
            PlotState.Empty          => true,
            PlotState.Prepared       => true,
            PlotState.Fertilized     => true,
            PlotState.NeedsCare      => true,
            PlotState.ReadyToHarvest => true,
            PlotState.FailedCrop     => true,
            _                        => false,
        };

        // Stripped entirely in release builds — no string allocation on the hot path (§5.7).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogTransition(PlotState from, PlotState to)
        {
            Debug.Log($"[FarmPlot '{gameObject.name}'] {from} → {to}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogInventoryBlocked(string itemName, PlotState blocked)
        {
            Debug.Log($"[FarmPlot '{gameObject.name}'] Cannot advance to {blocked} — no {itemName} in inventory.", this);
        }
    }
}
