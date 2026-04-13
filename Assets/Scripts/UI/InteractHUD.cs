using OrcFarm.Carry;
using OrcFarm.Farming;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using OrcFarm.Storage;
using OrcFarm.Workers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace OrcFarm.UI
{
    /// <summary>
    /// Minimal playtest HUD. Polls the interaction system each frame and displays:
    ///
    ///   _promptText    — "E: [action]" when there is a valid interactable target;
    ///                    empty when there is none.
    ///
    ///   _statusText    — "Carrying: head" while the player holds a harvested head;
    ///                    empty otherwise.
    ///
    ///   _inventoryText  — live seed and fertilizer counts from PlayerInventory.
    ///
    ///   _plotStatusText — plain-text state for the assigned FarmPlot while it is
    ///                     in a non-interactable (growing/waiting) state.
    ///
    /// <see cref="IInteractionService"/>, <see cref="ICarryController"/>, and
    /// <see cref="IPlayerInventory"/> are injected via VContainer (§1.3).
    /// </summary>
    public sealed class InteractHUD : MonoBehaviour
    {
        [Tooltip("Shown at screen centre-bottom. Displays the current interact action.")]
        [SerializeField] private TextMeshProUGUI _promptText;

        [Tooltip("Shown at screen top-left. Displays carry state.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("Optional. Displays live seed and fertilizer counts.")]
        [SerializeField] private TextMeshProUGUI _inventoryText;

        [Tooltip("Optional. FarmPlot to show non-interactable state text for (Growing, etc.).")]
        [SerializeField] private FarmPlot _farmPlot;

        [Tooltip("Optional. Shows plot state when the plot cannot be interacted with.")]
        [SerializeField] private TextMeshProUGUI _plotStatusText;

        private IInteractionService _interactionService;
        private ICarryController    _carry;
        private IPlayerInventory    _inventory;

        // ── Cached display state — rebuilt only when values change ─────────────

        private IInteractable _lastTarget;
        private bool          _lastCarrying        = true;   // force first-frame status write
        private bool          _lastCarryingLeg;
        private int           _lastSeedCount       = -1;
        private int           _lastFertilizerCount = -1;
        private int           _lastFeedItemCount   = -1;
        private int           _lastLegFryCount     = -1;
        private PlotState     _lastPlotState       = (PlotState)(-1);  // used by UpdatePrompt
        private PlotState     _lastPlotStatusState = (PlotState)(-1);  // used by UpdatePlotStatus
        private LegPondState  _lastPondState       = (LegPondState)(-1);
        private bool          _lastCanSecondary;                        // tracks Q prompt visibility
        private bool          _loggedMissingInjection;

        // ── VContainer injection ───────────────────────────────────────────────

        /// <summary>Receives services from VContainer (§1.3).</summary>
        [Inject]
        private void Construct(
            IInteractionService interactionService,
            ICarryController    carry,
            IPlayerInventory    inventory)
        {
            _interactionService = interactionService;
            _carry              = carry;
            _inventory          = inventory;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_promptText == null || _statusText == null)
                throw new System.InvalidOperationException(
                    "[InteractHUD] _promptText and _statusText must be assigned.");
            _promptText.text = string.Empty;
            _statusText.text = string.Empty;

            if (_inventoryText != null)
            _inventoryText.text = string.Empty;

            if (_plotStatusText != null)
            _plotStatusText.text = string.Empty;
        }

        // Compares current state against cached values; rebuilds text only on change.
        private void Update()
        {
            if (_interactionService == null || _carry == null)
            {
                if (!_loggedMissingInjection)
                {
                    _loggedMissingInjection = true;
                    Debug.LogError(
                        "[InteractHUD] Missing injected dependencies. " +
                        "Register this component in RootLifetimeScope.", this);
                }
                return;
            }

            UpdatePrompt();
            UpdateSecondaryInput();
            UpdateStatus();
            UpdateInventory();
            UpdatePlotStatus();
        }

        // ── Prompt ─────────────────────────────────────────────────────────────

        private void UpdatePrompt()
        {
            IInteractable target         = _interactionService.CurrentTarget;
            bool          plotChanged    = target is FarmPlot fp  && fp.State  != _lastPlotState;
            bool          pondChanged    = target is LegPond  lp  && lp.State  != _lastPondState;
            bool          canSecondary   = target is ISecondaryInteractable s && s.CanSecondaryInteract;
            bool          secondaryChanged = canSecondary != _lastCanSecondary;

            if (target == _lastTarget && !plotChanged && !pondChanged && !secondaryChanged)
                return;

            _lastTarget       = target;
            _lastCanSecondary = canSecondary;
            if (target is FarmPlot fp2) _lastPlotState = fp2.State;
            if (target is LegPond  lp2) _lastPondState = lp2.State;

            if (target == null || !target.CanInteract)
            {
                _promptText.text = string.Empty;
                return;
            }

            _promptText.text = BuildPromptText(target);
        }

        // Routes Q key press to the current target's secondary action.
        private void UpdateSecondaryInput()
        {
            if (!(Keyboard.current?.qKey.wasPressedThisFrame ?? false))
                return;

            IInteractable target = _interactionService.CurrentTarget;
            if (target is ISecondaryInteractable sec && sec.CanSecondaryInteract)
                sec.OnSecondaryInteract();
        }

        private string BuildPromptText(IInteractable target)
        {
            if (target is FarmPlot plot)
                return "E:  " + GetFarmPlotAction(plot.State);

            if (target is LegPond pond)
                return "E:  " + GetLegPondAction(pond.State);

            if (target is HarvestedHead)
                return "E:  Pick up head";

            if (target is HarvestedLeg)
                return "E:  Pick up leg";

            if (target is HeadStorageContainer storage)
                return _carry.IsCarrying
                    ? "E:  Store head"
                    : "E:  Retrieve head  (" + storage.StoredCount + ")";

            if (target is KeepInteractable ki)
            {
                string prompt = "E:  Keep orc";
                if (ki is ISecondaryInteractable sec && sec.CanSecondaryInteract)
                    prompt += "\nQ:  Store for sale";
                return prompt;
            }

            return "E:  Interact";
        }

        private static string GetFarmPlotAction(PlotState state) => state switch
        {
            PlotState.Empty          => "Prepare plot",
            PlotState.Prepared       => "Fertilize plot",
            PlotState.Fertilized     => "Plant seed",
            PlotState.NeedsCare      => "Care for crop",
            PlotState.ReadyToHarvest => "Harvest head",
            PlotState.FailedCrop     => "Clear failed crop",
            _                        => string.Empty,
        };

        private static string GetLegPondAction(LegPondState state) => state switch
        {
            LegPondState.Empty          => "Stock pond",
            LegPondState.NeedsCare      => "Feed pond",
            LegPondState.ReadyToHarvest => "Harvest legs",
            LegPondState.Starved        => "Clear pond",
            _                           => string.Empty,
        };

        // ── Status ─────────────────────────────────────────────────────────────

        private void UpdateStatus()
        {
            bool carrying    = _carry.IsCarrying;
            bool carryingLeg = _carry.IsCarryingLeg;

            if (carrying == _lastCarrying && carryingLeg == _lastCarryingLeg)
                return;

            _lastCarrying    = carrying;
            _lastCarryingLeg = carryingLeg;
            _statusText.text = carrying
                ? (carryingLeg ? "Carrying:  leg" : "Carrying:  head")
                : string.Empty;
        }

        // ── Plot status (non-interactable states only) ─────────────────────────

        private void UpdatePlotStatus()
        {
            if (_plotStatusText == null || _farmPlot == null)
                return;

            PlotState state = _farmPlot.State;
            if (state == _lastPlotStatusState)
                return;

            _lastPlotStatusState = state;
            _plotStatusText.text = state switch
            {
                PlotState.Planted => "Plot:  Starting...",
                PlotState.Growing => "Plot:  Growing",
                _                 => string.Empty,
            };
        }

        // ── Inventory counts ───────────────────────────────────────────────────

        private void UpdateInventory()
        {
            if (_inventoryText == null || _inventory == null)
                return;

            int seeds      = _inventory.GetCount(ItemType.HeadSeed);
            int fertilizer = _inventory.GetCount(ItemType.Fertilizer);
            int feedItem   = _inventory.GetCount(ItemType.FeedItem);
            int legFry     = _inventory.GetCount(ItemType.LegFry);

            if (seeds      == _lastSeedCount
             && fertilizer == _lastFertilizerCount
             && feedItem   == _lastFeedItemCount
             && legFry     == _lastLegFryCount)
                return;

            _lastSeedCount       = seeds;
            _lastFertilizerCount = fertilizer;
            _lastFeedItemCount   = feedItem;
            _lastLegFryCount     = legFry;

            _inventoryText.text = "Seeds:       " + seeds
                                + "\nFertilizer:  " + fertilizer
                                + "\nFeed:        " + feedItem
                                + "\nLeg Fry:     " + legFry;
        }
    }
}
