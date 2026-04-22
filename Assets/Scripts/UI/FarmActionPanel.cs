using OrcFarm.Core;
using OrcFarm.Player;
using UnityEngine;

namespace OrcFarm.UI
{
    /// <summary>
    /// Reads <see cref="FarmFocusDetector.CurrentContext"/> each frame and shows or hides
    /// the Feed / Water / Care button GameObjects accordingly.
    ///
    /// The panel root and three button roots are toggled via SetActive — cached state
    /// prevents redundant calls on frames where visibility does not change (§3.1).
    ///
    /// Setup:
    ///   • Assign <c>_focusDetector</c> (FarmFocusDetector on the player).
    ///   • Assign <c>_panelRoot</c> (parent of the three button GameObjects).
    ///   • Assign <c>_feedButton</c>, <c>_waterButton</c>, <c>_careButton</c>
    ///     (the individual button GameObjects to show/hide).
    ///   • This MonoBehaviour must stay enabled even while the panel is hidden —
    ///     place it on a persistent UI GameObject, not inside <c>_panelRoot</c>.
    /// </summary>
    public sealed class FarmActionPanel : MonoBehaviour
    {
        [Tooltip("FarmFocusDetector on the player. Its CurrentContext is read every frame.")]
        [SerializeField] private FarmFocusDetector _focusDetector;

        [Tooltip("Root GameObject of the whole action panel. " +
                 "Shown when at least one action is available, hidden otherwise.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("Feed button GameObject (F key hint). Shown only when Fertilizer is in selected hotbar slot.")]
        [SerializeField] private GameObject _feedButton;

        [Tooltip("Water button GameObject (W key hint). Shown only when WaterItem is in selected hotbar slot.")]
        [SerializeField] private GameObject _waterButton;

        [Tooltip("Care button GameObject (C key hint). Shown whenever the tile is in Growing state.")]
        [SerializeField] private GameObject _careButton;

        // ── Cached visibility — SetActive called only on change ────────────────

        private bool _lastAny;
        private bool _lastFeed;
        private bool _lastWater;
        private bool _lastCare;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_focusDetector == null)
            {
                Debug.LogError("[FarmActionPanel] _focusDetector is not assigned.", this);
                enabled = false;
                return;
            }

            ApplyButton(_feedButton,  false);
            ApplyButton(_waterButton, false);
            ApplyButton(_careButton,  false);
            ApplyPanel(false);
        }

        private void Update()
        {
            FarmActionContext ctx = _focusDetector.CurrentContext;
            bool any = ctx.HasAny;

            if (any        != _lastAny)   { ApplyPanel(any);                       _lastAny   = any;        }
            if (ctx.ShowFeed  != _lastFeed)  { ApplyButton(_feedButton,  ctx.ShowFeed);  _lastFeed  = ctx.ShowFeed; }
            if (ctx.ShowWater != _lastWater) { ApplyButton(_waterButton, ctx.ShowWater); _lastWater = ctx.ShowWater; }
            if (ctx.ShowCare  != _lastCare)  { ApplyButton(_careButton,  ctx.ShowCare);  _lastCare  = ctx.ShowCare; }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplyPanel(bool active)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(active);
        }

        private static void ApplyButton(GameObject button, bool active)
        {
            if (button != null)
                button.SetActive(active);
        }
    }
}
