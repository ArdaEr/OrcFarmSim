using TMPro;
using UnityEngine;

namespace OrcFarm.Economy
{
    /// <summary>
    /// Tracks the player's bronze currency balance.
    ///
    /// Place on a persistent scene object (e.g. the Player or a dedicated
    /// GameManager GameObject). Assign <see cref="_bronzeText"/> to a TMP element
    /// on the HUD canvas to show the live balance.
    ///
    /// No save/load for MVP — balance resets each Play session.
    ///
    /// MonoBehaviour justification: scene-side serialized references and Unity
    /// lifecycle needed to initialise the display on Start.
    /// </summary>
    public sealed class PlayerWallet : MonoBehaviour
    {
        [Tooltip("Starting bronze balance at the beginning of each Play session.")]
        [SerializeField] private int _startingBronze = 0;

        [Tooltip("Optional. TMP text element that shows 'Bronze: N'. " +
                 "Assign a text on the same HUD canvas as seed and fertilizer counts.")]
        [SerializeField] private TextMeshProUGUI _bronzeText;

        /// <summary>Current bronze balance.</summary>
        public int Balance { get; private set; }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Start()
        {
            Balance = _startingBronze;
            UpdateDisplay();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Adds <paramref name="amount"/> bronze and refreshes the HUD display.</summary>
        public void Add(int amount)
        {
            Balance += amount;
            UpdateDisplay();
        }

        // ── Private ────────────────────────────────────────────────────────────

        private void UpdateDisplay()
        {
            if (_bronzeText != null)
                _bronzeText.text = "Bronze:  " + Balance;
        }
    }
}
