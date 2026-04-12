using System.Collections;
using OrcFarm.Carry;
using OrcFarm.Interaction;
using OrcFarm.Workers;
using TMPro;
using UnityEngine;

namespace OrcFarm.Assembly
{
    /// <summary>
    /// Assembly station in the Home area. Accepts a carried harvested head from the
    /// player, combines it with fixed placeholder body-part data, and produces one
    /// assembled orc result per interaction.
    ///
    /// Interaction is gated on the player actively carrying a head; the station is
    /// idle and non-interactable otherwise.
    ///
    /// Head consumption: <see cref="CarryController.TryStore"/> parents the carried
    /// head into <c>_consumedRoot</c>, an inactive child GameObject created in Awake.
    /// Unity's inactive-hierarchy rule makes the head invisible and non-interactable
    /// immediately, without destroying the pooled object or calling SetActive per-head.
    ///
    /// MonoBehaviour justification: owns Unity lifecycle (Awake validation, Collider
    /// for interaction detection) and holds scene-side serialized references. No logic
    /// that belongs in a pure-C# class.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AssemblyStation : MonoBehaviour, IInteractable
    {
        // ── References ─────────────────────────────────────────────────────────

        [Tooltip("The player's CarryController. Assign in the Inspector.")]
        [SerializeField] private CarryController _carry;

        [Tooltip("Optional. World position where the assembled orc result appears. " +
                 "Defaults to 1.5 m in front of the station if unassigned.")]
        [SerializeField] private Transform _orcSpawnPoint;

        [Header("Orc worker routes  —  injected into each spawned HaulerWorker")]
        [Tooltip("The wait point Transform the hauler returns to when idle. " +
                 "Assign the same WaitPoint object used by your existing scene orcs.")]
        [SerializeField] private Transform _orcWaitPoint;

        [Tooltip("Walk target in front of the storage building. " +
                 "Assign the same StorageWalkTarget object used by your existing scene orcs.")]
        [SerializeField] private Transform _orcStorageWalkTarget;

        [Tooltip("ContentsRoot child of HeadStorageContainer where delivered heads are parented. " +
                 "Assign the same StorageDeliveryRoot object used by your existing scene orcs.")]
        [SerializeField] private Transform _orcStorageDeliveryRoot;

        [Tooltip("The orc holding pen. Injected into each spawned orc's KeepInteractable " +
                 "so the player can Store assembled orcs. Assign the OrcHoldingPen scene object.")]
        [SerializeField] private OrcHoldingPen _orcPen;

        [Tooltip("AssembledOrc prefab to instantiate on each successful assembly. " +
                 "Assign the AssembledOrc prefab from the Project window.")]
        [SerializeField] private AssembledOrc _orcPrefab;

        [Tooltip("Optional. TMP text that shows the assembly result summary. " +
                 "Can be on a world-space Canvas child or a screen-space HUD Canvas.")]
        [SerializeField] private TextMeshProUGUI _resultText;

        // ── Placeholder body-part data ─────────────────────────────────────────

        [Header("Placeholder body parts  —  no gameplay effect yet")]
        [Tooltip("Label for the torso slot (placeholder only).")]
        [SerializeField] private string _torsoLabel = "Basic Torso";

        [Tooltip("Label for the arms slot (placeholder only).")]
        [SerializeField] private string _armsLabel  = "Basic Arms";

        [Tooltip("Label for the legs slot (placeholder only).")]
        [SerializeField] private string _legsLabel  = "Basic Legs";

        // ── Result labels ──────────────────────────────────────────────────────

        [Header("Result labels")]
        [Tooltip("Quality label shown in the assembly result readout.")]
        [SerializeField] private string _qualityLabel  = "Normal";

        [Tooltip("Tendency label shown in the assembly result readout.")]
        [SerializeField] private string _tendencyLabel = "Unknown";

        [Tooltip("Seconds the result readout stays visible before auto-clearing.")]
        [Min(0.1f)]
        [SerializeField] private float _readoutClearDelay = 4f;

        // Inactive child that receives consumed heads.
        // Any head parented here becomes inactive-in-hierarchy (renderer hidden,
        // collider already disabled from pickup) without a per-head SetActive call.
        private Transform _consumedRoot;

        // Cached to avoid a new allocation each time the readout is shown (§3.9).
        private WaitForSeconds _clearWait;

        // Tracks the active clear coroutine so it can be cancelled on rapid re-assembly.
        private Coroutine _clearCoroutine;

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>Only available while the player is carrying a harvested head.</remarks>
        public bool CanInteract => enabled && _carry != null && _carry.IsCarrying;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (!CanInteract)
                return;

            // Consume: TryStore parents the carried head into the inactive _consumedRoot.
            // The head inherits the inactive-hierarchy state and disappears from the scene.
            if (!_carry.TryStore(_consumedRoot))
                return;

            ShowOrcResult();
            ShowResultText();
            LogAssembly();
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_carry == null)
                throw new System.InvalidOperationException(
                    $"[AssemblyStation '{gameObject.name}'] CarryController not assigned.");

            // Create the inactive consumed-heads root.
            // SetActive(false) here means every head parented to it later is
            // automatically hidden without additional per-head calls.
            var consumeGo = new GameObject("[ConsumedHeads]");
            consumeGo.transform.SetParent(transform, worldPositionStays: false);
            consumeGo.SetActive(false);
            _consumedRoot = consumeGo.transform;

            _clearWait = new WaitForSeconds(_readoutClearDelay);

            if (_resultText != null)
                _resultText.text = string.Empty;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void ShowOrcResult()
        {
            if (_orcPrefab == null)
                return;

            Vector3 spawnPos = _orcSpawnPoint != null
                ? _orcSpawnPoint.position
                : transform.position + transform.forward * 1.5f;

            AssembledOrc orc = Instantiate(_orcPrefab, spawnPos, _orcPrefab.transform.rotation);

            // Inject scene-side references that cannot be pre-assigned on a prefab.
            if (orc.TryGetComponent(out HaulerWorker hauler))
                hauler.Initialize(_orcWaitPoint, _orcStorageWalkTarget, _orcStorageDeliveryRoot);

            if (orc.TryGetComponent(out KeepInteractable keep))
                keep.Initialize(_orcPen);

            orc.gameObject.SetActive(true);
        }

        private void ShowResultText()
        {
            if (_resultText == null)
                return;

            // String concatenation is acceptable here: OnInteract is event-driven,
            // not a per-frame hot path (§3.3 applies to Update/FixedUpdate only).
            _resultText.text =
                "Orc assembled\n" +
                "Quality:    " + _qualityLabel  + "\n" +
                "Tendency:   " + _tendencyLabel;

            // Cancel any pending clear from a previous assembly before starting a new one.
            if (_clearCoroutine != null)
                StopCoroutine(_clearCoroutine);

            _clearCoroutine = StartCoroutine(ClearResultAfterDelay());
        }

        private IEnumerator ClearResultAfterDelay()
        {
            yield return _clearWait;
            if (_resultText != null)
                _resultText.text = string.Empty;
            _clearCoroutine = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogAssembly()
        {
            Debug.Log(
                $"[AssemblyStation '{gameObject.name}'] Assembly complete. " +
                $"Head + {_torsoLabel} + {_armsLabel} + {_legsLabel}. " +
                $"Quality: {_qualityLabel}, Tendency: {_tendencyLabel}.", this);
        }
    }
}
