using System.Collections;
using OrcFarm.Assembly;
using OrcFarm.Interaction;
using OrcFarm.Workers;
using TMPro;
using UnityEngine;

namespace OrcFarm.Economy
{
    /// <summary>
    /// Sell point in the HOME area. The player interacts with it to sell the first
    /// orc stored in <see cref="OrcHoldingPen"/>. Sale price is determined by the
    /// orc's <see cref="OrcQuality"/> and the serialized price fields.
    ///
    /// Interaction is unavailable when the pen is empty — no prompt is shown.
    ///
    /// MonoBehaviour justification: owns Collider for interaction detection, coroutine
    /// for readout auto-clear, and scene-side serialized references.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BazaarPoint : MonoBehaviour, IInteractable
    {
        // ── References ─────────────────────────────────────────────────────────

        [Tooltip("The orc holding pen to sell from. Assign in the Inspector.")]
        [SerializeField] private OrcHoldingPen _pen;

        [Tooltip("The player wallet that receives bronze on sale. Assign in the Inspector.")]
        [SerializeField] private PlayerWallet _wallet;

        // ── Prices ─────────────────────────────────────────────────────────────

        [Header("Sale prices")]
        [Tooltip("Bronze received for a Low quality orc.")]
        [Min(0)]
        [SerializeField] private int _lowQualityPrice    = 10;

        [Tooltip("Bronze received for a Normal quality orc.")]
        [Min(0)]
        [SerializeField] private int _normalQualityPrice = 25;

        [Tooltip("Bronze received for a High quality orc.")]
        [Min(0)]
        [SerializeField] private int _highQualityPrice   = 50;

        // ── Readout ────────────────────────────────────────────────────────────

        [Header("Sale readout")]
        [Tooltip("Optional. TMP text that shows the sale result summary.")]
        [SerializeField] private TextMeshProUGUI _saleReadoutText;

        [Tooltip("Seconds the sale readout stays visible before auto-clearing.")]
        [Min(0.1f)]
        [SerializeField] private float _readoutClearDelay = 4f;

        // Cached to avoid a new WaitForSeconds allocation on every sale (§3.9).
        private WaitForSeconds _clearWait;

        // Tracks the active clear coroutine so it is cancelled on rapid re-sale.
        private Coroutine _clearCoroutine;

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>Only available when at least one orc is waiting in the pen.</remarks>
        public bool CanInteract => enabled && _pen != null && _pen.StoredCount > 0;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (!CanInteract)
                return;

            HaulerWorker sold = _pen.TrySellOne();
            if (sold == null)
                return;

            // Read quality from AssembledOrc on the same GameObject.
            AssembledOrc orc     = sold.GetComponent<AssembledOrc>();
            OrcQuality   quality = orc != null ? orc.Quality : OrcQuality.Normal;
            int          price   = GetPrice(quality);

            _wallet.Add(price);

            // Deactivate the sold orc — removes it from the scene visually.
            sold.gameObject.SetActive(false);

            ShowReadout(quality, price);
            LogSale(quality, price);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_pen == null)
                Debug.LogWarning(
                    $"[BazaarPoint '{gameObject.name}'] _pen not assigned. " +
                    "Assign the OrcHoldingPen in the Inspector.", this);

            if (_wallet == null)
                Debug.LogWarning(
                    $"[BazaarPoint '{gameObject.name}'] _wallet not assigned. " +
                    "Assign the PlayerWallet in the Inspector.", this);

            _clearWait = new WaitForSeconds(_readoutClearDelay);

            if (_saleReadoutText != null)
                _saleReadoutText.text = string.Empty;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private int GetPrice(OrcQuality quality) => quality switch
        {
            OrcQuality.Low    => _lowQualityPrice,
            OrcQuality.Normal => _normalQualityPrice,
            OrcQuality.High   => _highQualityPrice,
            _                 => _normalQualityPrice,
        };

        private void ShowReadout(OrcQuality quality, int price)
        {
            if (_saleReadoutText == null)
                return;

            _saleReadoutText.text =
                "Sold " + quality + " orc for " + price + " Bronze";

            if (_clearCoroutine != null)
                StopCoroutine(_clearCoroutine);

            _clearCoroutine = StartCoroutine(ClearReadoutAfterDelay());
        }

        private IEnumerator ClearReadoutAfterDelay()
        {
            yield return _clearWait;
            if (_saleReadoutText != null)
                _saleReadoutText.text = string.Empty;
            _clearCoroutine = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogSale(OrcQuality quality, int price)
        {
            Debug.Log(
                $"[BazaarPoint '{gameObject.name}'] Sold {quality} orc for {price} Bronze. " +
                $"New balance: {(_wallet != null ? _wallet.Balance : -1)}.", this);
        }
    }
}
