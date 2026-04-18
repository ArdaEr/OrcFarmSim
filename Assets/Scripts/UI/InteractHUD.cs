using System.Collections;
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

        [Tooltip("Optional. Shows a brief readout when a head is harvested from a HeadFarmTile.")]
        [SerializeField] private TextMeshProUGUI _harvestResultText;

        [Tooltip("Seconds before the harvest readout is cleared. Matches assembly result timing.")]
        [SerializeField] private float _harvestResultClearDelay = 4f;

        [Tooltip("Five TMP text elements for the hotbar display, assigned left to right (slots 1–5). " +
                 "The selected slot is highlighted gold. Leave unassigned to skip hotbar display.")]
        [SerializeField] private TextMeshProUGUI[] _hotbarSlotTexts;

        [Tooltip("Optional. Shows brief warning messages (e.g. 'Inventory is full').")]
        [SerializeField] private TextMeshProUGUI _warningText;

        [Tooltip("Seconds the warning stays visible before auto-clearing.")]
        [Min(0.1f)]
        [SerializeField] private float _warningClearDelay = 2f;

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
        private HeadTileState _lastTileState       = (HeadTileState)(-1);
        private bool          _lastCanSecondary;                        // tracks Q prompt visibility
        private bool          _loggedMissingInjection;

        // ── Harvest readout ────────────────────────────────────────────────────

        private WaitForSeconds _harvestClearWait;
        private Coroutine      _harvestClearCoroutine;

        // ── Warning display ────────────────────────────────────────────────────

        private WaitForSeconds _warningClearWait;
        private Coroutine      _warningClearCoroutine;

        // ── Hotbar display ─────────────────────────────────────────────────────

        private const int HotbarDisplaySize = 5;

        // -1 forces a full rebuild on the first frame after injection.
        private int        _lastHotbarSelected  = -1;
        private ItemType[] _cachedHotbarTypes   = new ItemType[HotbarDisplaySize];
        private int[]      _cachedHotbarCounts  = new int[HotbarDisplaySize];

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

            if (_harvestResultText != null)
                _harvestResultText.text = string.Empty;

            _harvestClearWait  = new WaitForSeconds(_harvestResultClearDelay);
            _warningClearWait  = new WaitForSeconds(_warningClearDelay);

            if (_warningText != null)
                _warningText.text = string.Empty;

            if (_hotbarSlotTexts != null && _hotbarSlotTexts.Length == HotbarDisplaySize)
            {
                for (int i = 0; i < HotbarDisplaySize; i++)
                {
                    if (_hotbarSlotTexts[i] != null)
                        _hotbarSlotTexts[i].text = string.Empty;
                    else
                        Debug.LogError(
                            "[InteractHUD] _hotbarSlotTexts[" + i + "] is null. " +
                            "Assign all 5 hotbar TMP elements in the Inspector.", this);
                }
            }
            else if (_hotbarSlotTexts != null)
            {
                Debug.LogError(
                    "[InteractHUD] _hotbarSlotTexts must contain exactly " + HotbarDisplaySize +
                    " elements. Found " + _hotbarSlotTexts.Length + ".", this);
            }
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
            UpdateHotbar();
        }

        // ── Prompt ─────────────────────────────────────────────────────────────

        private void UpdatePrompt()
        {
            IInteractable target          = _interactionService.CurrentTarget;
            bool          plotChanged     = target is FarmPlot fp && fp.State != _lastPlotState;
            bool          pondChanged     = target is LegPond  lp && lp.State != _lastPondState;
            // Growing tiles rebuild every frame so the F/W/C scores stay live.
            bool          tileChanged     = target is HeadFarmTile ht &&
                                            (ht.State != _lastTileState || ht.State == HeadTileState.Growing);
            bool          canSecondary    = target is ISecondaryInteractable s && s.CanSecondaryInteract;
            bool          secondaryChanged = canSecondary != _lastCanSecondary;

            // Harvest detection: same tile was the last target, was ReadyToHarvest, now is not.
            if (target == _lastTarget &&
                target is HeadFarmTile htHarvest &&
                _lastTileState == HeadTileState.ReadyToHarvest &&
                htHarvest.State != HeadTileState.ReadyToHarvest)
            {
                ShowHarvestReadout("Harvested head — " + htHarvest.LastHarvestQualityLabel + " quality");
            }

            if (target == _lastTarget && !plotChanged && !pondChanged && !tileChanged && !secondaryChanged)
                return;

            _lastTarget       = target;
            _lastCanSecondary = canSecondary;
            if (target is FarmPlot fp2)      _lastPlotState  = fp2.State;
            if (target is LegPond  lp2)      _lastPondState  = lp2.State;
            if (target is HeadFarmTile ht2)  _lastTileState  = ht2.State;

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

            if (target is OrcFarm.Carry.DroppedHotbarItem dropped)
                return "E:  Pick up " + GetItemDisplayName(dropped.ItemType);

            if (target is HeadStorageContainer storage)
                return _carry.IsCarrying
                    ? "E:  Store head"
                    : "E:  Retrieve head  (" + storage.StoredCount + ")";

            if (target is HeadFarmTile tile)
                return "E:  " + tile.InteractPrompt;

            if (target is KeepInteractable ki)
            {
                string prompt = "E:  Keep orc";
                if (ki is ISecondaryInteractable sec && sec.CanSecondaryInteract)
                    prompt += "\nQ:  Store for sale";
                return prompt;
            }

            return "E:  Interact";
        }

        // ── Harvest readout ────────────────────────────────────────────────────

        private void ShowHarvestReadout(string message)
        {
            if (_harvestResultText == null)
                return;

            _harvestResultText.text = message;

            if (_harvestClearCoroutine != null)
                StopCoroutine(_harvestClearCoroutine);

            _harvestClearCoroutine = StartCoroutine(ClearHarvestReadoutAfterDelay());
        }

        private IEnumerator ClearHarvestReadoutAfterDelay()
        {
            yield return _harvestClearWait;
            if (_harvestResultText != null)
                _harvestResultText.text = string.Empty;
            _harvestClearCoroutine = null;
        }

        // ── Warning display ────────────────────────────────────────────────────

        /// <summary>
        /// Shows a brief warning message. Auto-clears after <c>_warningClearDelay</c> seconds.
        /// Called by RootLifetimeScope's inventory-full callback wired into HotbarItemPresenter.
        /// </summary>
        public void ShowInventoryFullWarning()
        {
            if (_warningText == null)
                return;

            _warningText.text = "Inventory is full";

            if (_warningClearCoroutine != null)
                StopCoroutine(_warningClearCoroutine);

            _warningClearCoroutine = StartCoroutine(ClearWarningAfterDelay());
        }

        private IEnumerator ClearWarningAfterDelay()
        {
            yield return _warningClearWait;
            if (_warningText != null)
                _warningText.text = string.Empty;
            _warningClearCoroutine = null;
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

        private static string GetItemDisplayName(OrcFarm.Inventory.ItemType type) => type switch
        {
            OrcFarm.Inventory.ItemType.HeadSeed   => "Head Seed",
            OrcFarm.Inventory.ItemType.Fertilizer => "Fertilizer",
            OrcFarm.Inventory.ItemType.FeedItem   => "Feed",
            OrcFarm.Inventory.ItemType.LegFry     => "Leg Fry",
            OrcFarm.Inventory.ItemType.WaterItem  => "Water",
            _                                     => type.ToString(),
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

        // ── Hotbar display ─────────────────────────────────────────────────────

        private void UpdateHotbar()
        {
            if (_hotbarSlotTexts == null || _hotbarSlotTexts.Length < HotbarDisplaySize)
                return;
            if (_inventory == null)
                return;

            int  selected = _inventory.SelectedSlotIndex;
            bool changed  = selected != _lastHotbarSelected;

            // Check each slot for content changes; exit early on first mismatch.
            if (!changed)
            {
                for (int i = 0; i < HotbarDisplaySize; i++)
                {
                    HotbarSlot s = _inventory.GetHotbarSlot(i);
                    if (s.SlotItemType != _cachedHotbarTypes[i] || s.Count != _cachedHotbarCounts[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return;

            _lastHotbarSelected = selected;
            for (int i = 0; i < HotbarDisplaySize; i++)
            {
                if (_hotbarSlotTexts[i] == null)
                    continue;

                HotbarSlot s = _inventory.GetHotbarSlot(i);
                _cachedHotbarTypes[i]  = s.SlotItemType;
                _cachedHotbarCounts[i] = s.Count;

                // String concat here is intentional — only runs on state change, not per frame (§3.3).
                string content = BuildHotbarSlotLabel(i + 1, s);
                _hotbarSlotTexts[i].text = (i == selected)
                    ? "<color=#FFD700>" + content + "</color>"
                    : content;
            }
        }

        private static string BuildHotbarSlotLabel(int slotNumber, HotbarSlot slot)
        {
            return slot.IsEmpty
                ? slotNumber + "\nEmpty"
                : slotNumber + "\n" + slot.SlotItemType + "\n\u00d7" + slot.Count;
        }
    }
}
