using OrcFarm.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OrcFarm.UI
{
    /// <summary>
    /// Screen-center dot crosshair.
    ///
    /// Polls <see cref="FarmFocusDetector.CurrentTarget"/> each frame and switches the
    /// dot color when a farm tile is focused. An optional outline Image sits behind the
    /// dot and stays white at all times.
    ///
    /// Setup:
    ///   • Place this MonoBehaviour on a persistent UI GameObject on the main Canvas
    ///     (must NOT live inside any GameObject that is ever deactivated at runtime).
    ///   • Create two overlapping Images anchored to the Canvas center:
    ///       – Outline Image  (white ring, drawn behind)  → assign to <c>_outlineImage</c>
    ///       – Dot Image      (filled circle, drawn front) → assign to <c>_dotImage</c>
    ///     Use a circle sprite on both for a round appearance.
    ///   • Assign <c>_focusDetector</c> to the FarmFocusDetector on the player.
    ///   • Tune <c>_dotSize</c>, <c>_outlineSize</c>, and <c>_focusColor</c> in the Inspector.
    /// </summary>
    public sealed class FarmCrosshair : MonoBehaviour
    {
        [Tooltip("Filled circle Image. Color changes to _focusColor when a farm tile is focused.")]
        [SerializeField] private Image _dotImage;

        [Tooltip("Optional white ring Image drawn behind the dot. Stays white at all times. " +
                 "Leave unassigned to use a single-Image crosshair.")]
        [SerializeField] private Image _outlineImage;

        [Tooltip("Diameter of the dot in pixels.")]
        [Min(1f)]
        [SerializeField] private float _dotSize = 8f;

        [Tooltip("Extra radius added to the outline Image on each side beyond the dot edge. " +
                 "Has no effect when _outlineImage is unassigned.")]
        [Min(0f)]
        [SerializeField] private float _outlineSize = 2f;

        [Tooltip("Dot color shown when FarmFocusDetector has an active target.")]
        [SerializeField] private Color _focusColor = new Color(1f, 0.92f, 0f, 1f);

        [Tooltip("FarmFocusDetector on the player. Polled each frame to detect active focus.")]
        [SerializeField] private FarmFocusDetector _focusDetector;

        // ── Constants ──────────────────────────────────────────────────────────

        private static readonly Color DefaultDotColor = Color.black;

        // ── Runtime state ──────────────────────────────────────────────────────

        private bool _focused;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_dotImage == null)
            {
                Debug.LogError("[FarmCrosshair] _dotImage is not assigned.", this);
                enabled = false;
                return;
            }

            if (_focusDetector == null)
            {
                Debug.LogError("[FarmCrosshair] _focusDetector is not assigned.", this);
                enabled = false;
                return;
            }

            _dotImage.rectTransform.sizeDelta = Vector2.one * _dotSize;

            if (_outlineImage != null)
            {
                _outlineImage.color                   = Color.white;
                _outlineImage.rectTransform.sizeDelta = Vector2.one * (_dotSize + _outlineSize * 2f);
            }

            ApplyColor(false);
        }

        private void Update()
        {
            bool nowFocused = _focusDetector.CurrentTarget != null;
            if (nowFocused == _focused)
                return;

            _focused = nowFocused;
            ApplyColor(_focused);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplyColor(bool focused)
        {
            _dotImage.color = focused ? _focusColor : DefaultDotColor;
        }
    }
}
