#if UNITY_EDITOR
using OrcFarm.Farming;
using OrcFarm.Player;
using TMPro;
using UnityEngine;

namespace OrcFarm.UI
{
    /// <summary>
    /// Editor-only HUD panel showing per-fish debug state when the player looks at a LegPond.
    /// Not compiled into builds. Place on the existing Canvas; assign all references in the Inspector.
    ///
    /// Setup:
    ///   • Assign <c>_focusDetector</c> (FarmFocusDetector on the player).
    ///   • Assign <c>_panelRoot</c> (a child GameObject of the existing Canvas — top-left corner).
    ///   • Assign <c>_debugText</c> (TMP text inside <c>_panelRoot</c>).
    /// </summary>
    public sealed class LegPondDebugPanel : MonoBehaviour
    {
        [Tooltip("FarmFocusDetector on the player. CurrentTarget is read each frame.")]
        [SerializeField] private FarmFocusDetector _focusDetector;

        [Tooltip("Root of the debug panel. Shown when a LegPond is focused; hidden otherwise.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("TMP text element that displays per-fish debug state.")]
        [SerializeField] private TextMeshProUGUI _debugText;

        private void Awake()
        {
            if (_focusDetector == null)
            {
                Debug.LogError("[LegPondDebugPanel] _focusDetector is not assigned.", this);
                enabled = false;
                return;
            }

            if (_panelRoot == null)
            {
                Debug.LogError("[LegPondDebugPanel] _panelRoot is not assigned.", this);
                enabled = false;
                return;
            }

            if (_debugText == null)
            {
                Debug.LogError("[LegPondDebugPanel] _debugText is not assigned.", this);
                enabled = false;
                return;
            }

            _panelRoot.SetActive(false);
        }

        private void Update()
        {
            LegPond pond = _focusDetector.CurrentTarget as LegPond;
            bool    show = pond != null;

            _panelRoot.SetActive(show);

            if (show)
                _debugText.text = pond.GetDebugInfo();
        }
    }
}
#endif
