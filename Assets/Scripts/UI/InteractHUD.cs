using OrcFarm.Carry;
using OrcFarm.Farming;
using OrcFarm.Interaction;
using OrcFarm.Storage;
using TMPro;
using UnityEngine;

namespace OrcFarm.UI
{
    /// <summary>
    /// Minimal playtest HUD. Polls the interaction system each frame and displays:
    ///
    ///   _promptText — "E: [action]" when there is a valid interactable target;
    ///                 empty when there is none.
    ///
    ///   _statusText — "Carrying: head" while the player holds a harvested head;
    ///                 empty otherwise.
    ///
    /// Scene setup:
    ///   - Add a Screen Space – Overlay Canvas to the scene.
    ///   - Add two TextMeshProUGUI objects as children (prompt and status).
    ///   - Place this component on the Canvas (or any scene GameObject).
    ///   - Assign InteractionDetector, CarryController, and both Text references.
    /// </summary>
    public sealed class InteractHUD : MonoBehaviour
    {
        [SerializeField] private InteractionDetector _detector;
        [SerializeField] private CarryController     _carryController;

        [Tooltip("Shown at screen centre-bottom. Displays the current interact action.")]
        [SerializeField] private TextMeshProUGUI _promptText;

        [Tooltip("Shown at screen top-left. Displays carry state.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_detector == null || _carryController == null ||
                _promptText == null || _statusText == null)
            {
                Debug.LogError("[InteractHUD] One or more required references are not assigned. HUD disabled.", this);
                enabled = false;
            }
        }

        // Polls each frame — acceptable for a placeholder playtest HUD.
        private void Update()
        {
            UpdatePrompt();
            UpdateStatus();
        }

        // ── Prompt ─────────────────────────────────────────────────────────────

        private void UpdatePrompt()
        {
            IInteractable target = _detector.CurrentTarget;

            if (target == null || !target.CanInteract)
            {
                _promptText.text = string.Empty;
                return;
            }

            _promptText.text = "E:  " + GetActionText(target);
        }

        private string GetActionText(IInteractable target)
        {
            if (target is FarmPlot plot)
                return GetFarmPlotAction(plot.State);

            if (target is HarvestedHead)
                return "Pick up head";

            if (target is HeadStorageContainer storage)
                return _carryController.IsCarrying
                    ? "Store head"
                    : $"Retrieve head  ({storage.StoredCount})";

            return "Interact"; // fallback for any future IInteractable types
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

        // ── Status ─────────────────────────────────────────────────────────────

        private void UpdateStatus()
        {
            _statusText.text = _carryController.IsCarrying ? "Carrying:  head" : string.Empty;
        }
    }
}
