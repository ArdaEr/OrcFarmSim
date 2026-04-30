using System.Collections;
using OrcFarm.Core;
using OrcFarm.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.UI
{
    /// <summary>
    /// Reads <see cref="FarmFocusDetector.CurrentContext"/> each frame and updates the
    /// Feed / Water / Care buttons.
    ///
    /// Each button has three visual states:
    ///   Hidden   — <c>SetActive(false)</c> when <c>Visible</c> is false.
    ///   Active   — white text, full opacity.
    ///   Inactive — grey text at reduced opacity (serialized on this component).
    ///
    /// State is cached; visual calls fire only when a button's <c>Visible</c> or
    /// <c>Active</c> flag changes (§3.1).
    ///
    /// Setup:
    ///   • Assign <c>_focusDetector</c> (FarmFocusDetector on the player).
    ///   • Assign <c>_panelRoot</c> (parent of the three button GameObjects).
    ///   • Assign each button's root GameObject and its TMP label.
    ///   • This MonoBehaviour must stay enabled even while the panel is hidden —
    ///     place it on a persistent UI GameObject, not inside <c>_panelRoot</c>.
    /// </summary>
    public sealed class FarmActionPanel : MonoBehaviour
    {
        [Tooltip("FarmFocusDetector on the player. Its CurrentContext is read every frame.")]
        [SerializeField] private FarmFocusDetector _focusDetector;

        [Tooltip("Root GameObject of the whole action panel. Shown when at least one button is visible.")]
        [SerializeField] private GameObject _panelRoot;

        [Header("Feed Button")]
        [Tooltip("Feed button root GameObject (shown/hidden based on FeedVisible).")]
        [SerializeField] private GameObject _feedButton;

        [Tooltip("TMP label on the Feed button. Color reflects active/inactive state.")]
        [SerializeField] private TextMeshProUGUI _feedText;

        [Header("Water Button")]
        [Tooltip("Water button root GameObject (shown/hidden based on WaterVisible).")]
        [SerializeField] private GameObject _waterButton;

        [Tooltip("TMP label on the Water button.")]
        [SerializeField] private TextMeshProUGUI _waterText;

        [Header("Care Button")]
        [Tooltip("Care button root GameObject (shown/hidden based on CareVisible).")]
        [SerializeField] private GameObject _careButton;

        [Tooltip("TMP label on the Care button.")]
        [SerializeField] private TextMeshProUGUI _careText;

        [Header("Inactive Appearance")]
        [Tooltip("Text color when a button is visible but the action is unavailable. Alpha channel ignored — use _inactiveAlpha.")]
        [SerializeField] private Color _inactiveTextColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        [Tooltip("Text alpha when a button is inactive.")]
        [Range(0f, 1f)]
        [SerializeField] private float _inactiveAlpha = 0.4f;

        [Header("Reason Text")]
        [Tooltip("Optional. TMP element that shows why a greyed action is unavailable when the player presses its key.")]
        [SerializeField] private TextMeshProUGUI _reasonText;

        [Tooltip("Seconds before the reason message auto-clears.")]
        [Min(0.1f)]
        [SerializeField] private float _reasonClearDelay = 2f;

        // ── Constants ──────────────────────────────────────────────────────────

        private static readonly Color ActiveTextColor = Color.white;

        // ── Cached inactive color — built once in Awake, no per-frame alloc ───

        private Color _inactiveColor;

        // ── Reason text ────────────────────────────────────────────────────────

        private WaitForSeconds _reasonClearWait;
        private Coroutine      _reasonClearCoroutine;

        // ── Cached button state ────────────────────────────────────────────────

        private bool _lastAny;
        private bool _lastFeedVisible;
        private bool _lastFeedActive;
        private bool _lastWaterVisible;
        private bool _lastWaterActive;
        private bool _lastCareVisible;
        private bool _lastCareActive;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_focusDetector == null)
            {
                Debug.LogError("[FarmActionPanel] _focusDetector is not assigned.", this);
                enabled = false;
                return;
            }

            if (_feedText == null || _waterText == null || _careText == null)
            {
                Debug.LogError("[FarmActionPanel] One or more button text references (_feedText, " +
                               "_waterText, _careText) are not assigned.", this);
                enabled = false;
                return;
            }

            _inactiveColor = new Color(
                _inactiveTextColor.r,
                _inactiveTextColor.g,
                _inactiveTextColor.b,
                _inactiveAlpha);

            _reasonClearWait = new WaitForSeconds(_reasonClearDelay);

            if (_reasonText != null)
                _reasonText.text = string.Empty;
            else
                Debug.LogWarning("[FarmActionPanel] _reasonText is not assigned — reason messages will not show.", this);

            SetButtonState(_feedButton,  _feedText,  false, false);
            SetButtonState(_waterButton, _waterText, false, false);
            SetButtonState(_careButton,  _careText,  false, false);
            ApplyPanel(false);
        }

        private void Update()
        {
            FarmActionContext ctx = _focusDetector.CurrentContext;

            if (ctx.HasAny != _lastAny)
            {
                ApplyPanel(ctx.HasAny);
                _lastAny = ctx.HasAny;
            }

            if (ctx.FeedVisible != _lastFeedVisible || ctx.FeedActive != _lastFeedActive)
            {
                SetButtonState(_feedButton, _feedText, ctx.FeedVisible, ctx.FeedActive);
                _lastFeedVisible = ctx.FeedVisible;
                _lastFeedActive  = ctx.FeedActive;
            }

            if (ctx.WaterVisible != _lastWaterVisible || ctx.WaterActive != _lastWaterActive)
            {
                SetButtonState(_waterButton, _waterText, ctx.WaterVisible, ctx.WaterActive);
                _lastWaterVisible = ctx.WaterVisible;
                _lastWaterActive  = ctx.WaterActive;
            }

            if (ctx.CareVisible != _lastCareVisible || ctx.CareActive != _lastCareActive)
            {
                SetButtonState(_careButton, _careText, ctx.CareVisible, ctx.CareActive);
                _lastCareVisible = ctx.CareVisible;
                _lastCareActive  = ctx.CareActive;
            }

            HandleInactiveKeyPress(ctx);
        }

        // ── Reason text ────────────────────────────────────────────────────────

        private void HandleInactiveKeyPress(FarmActionContext ctx)
        {
            if (_reasonText == null)
                return;

            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            if (ctx.FeedVisible && !ctx.FeedActive && kb.fKey.wasPressedThisFrame)
            {
                ShowReason("No feed item selected");
                return;
            }

            if (ctx.CareVisible && !ctx.CareActive && kb.cKey.wasPressedThisFrame)
                ShowReason("Empty hands needed");
        }

        private void ShowReason(string message)
        {
            _reasonText.text = message;

            if (_reasonClearCoroutine != null)
                StopCoroutine(_reasonClearCoroutine);

            _reasonClearCoroutine = StartCoroutine(ClearReasonAfterDelay());
        }

        private IEnumerator ClearReasonAfterDelay()
        {
            yield return _reasonClearWait;
            if (_reasonText != null)
                _reasonText.text = string.Empty;
            _reasonClearCoroutine = null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplyPanel(bool show)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(show);
        }

        private void SetButtonState(GameObject button, TextMeshProUGUI text, bool visible, bool active)
        {
            if (button != null)
                button.SetActive(visible);

            text.color = (visible && !active) ? _inactiveColor : ActiveTextColor;
        }
    }
}
