using System.Collections;
using MessagePipe;
using OrcFarm.Carry;
using OrcFarm.Interaction;
using OrcFarm.Quests;
using OrcFarm.Workers;
using TMPro;
using UnityEngine;
using CoreQuality = OrcFarm.Core.OrcQuality;
using CoreTrait   = OrcFarm.Core.OrcTrait;
using static OrcFarm.Core.OrcTraitUtility;

namespace OrcFarm.Assembly
{
    /// <summary>
    /// Assembly station in the Home area. Accepts one deposited <see cref="HarvestedHead"/>
    /// and one deposited <see cref="HarvestedLeg"/> before assembly is allowed.
    ///
    /// Deposit flow: player carries an item and interacts → item is parked in its slot
    /// (<see cref="_headSlot"/> or <see cref="_legSlot"/>) via the carry system's store
    /// methods. The player can take either part back as long as both slots are not filled
    /// simultaneously (which triggers assembly instead).
    ///
    /// Assembly: when both slots are filled the next interaction consumes both parts,
    /// spawns an <see cref="AssembledOrc"/> from the prefab, and shows the result readout.
    ///
    /// Quality of the assembled orc equals the deposited leg's quality tier.
    /// Both head and leg quality are read during assembly for trait weighting.
    ///
    /// MonoBehaviour justification: owns Unity lifecycle (Awake validation, Collider for
    /// interaction detection) and holds scene-side serialized references.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AssemblyStation : MonoBehaviour, IInteractable, IQuestObjectiveActionPublisherTarget
    {
        // ── References ─────────────────────────────────────────────────────────

        [Tooltip("The player's CarryController. Assign in the Inspector.")]
        [SerializeField] private CarryController _carry;

        [Tooltip("Hidden child Transform where a deposited HarvestedHead is parked. " +
                 "Create an empty child GameObject named 'HeadSlot' and assign it here.")]
        [SerializeField] private Transform _headSlot;

        [Tooltip("Hidden child Transform where a deposited HarvestedLeg is parked. " +
                 "Create an empty child GameObject named 'LegSlot' and assign it here.")]
        [SerializeField] private Transform _legSlot;

        [Tooltip("Optional. World position where the assembled orc appears. " +
                 "Defaults to 1.5 m in front of the station if unassigned.")]
        [SerializeField] private Transform _orcSpawnPoint;

        [Header("Orc worker routes  —  injected into each spawned HaulerWorker")]
        [Tooltip("The wait point Transform the hauler returns to when idle.")]
        [SerializeField] private Transform _orcWaitPoint;

        [Tooltip("Walk target in front of the storage building.")]
        [SerializeField] private Transform _orcStorageWalkTarget;

        [Tooltip("ContentsRoot child of HeadStorageContainer where delivered heads are parented.")]
        [SerializeField] private Transform _orcStorageDeliveryRoot;

        [Tooltip("The orc holding pen. Injected into each spawned orc's KeepInteractable.")]
        [SerializeField] private OrcHoldingPen _orcPen;

        [Tooltip("AssembledOrc prefab to instantiate on each successful assembly.")]
        [SerializeField] private AssembledOrc _orcPrefab;

        [Tooltip("Optional. TMP text that shows the assembly result or hint messages.")]
        [SerializeField] private TextMeshProUGUI _resultText;

        // ── Placeholder body-part data ─────────────────────────────────────────

        [Header("Placeholder body parts  —  no gameplay effect yet")]
        [Tooltip("Label for the torso slot (placeholder only).")]
        [SerializeField] private string _torsoLabel = "Basic Torso";

        [Tooltip("Label for the arms slot (placeholder only).")]
        [SerializeField] private string _armsLabel  = "Basic Arms";

        // ── Result labels ──────────────────────────────────────────────────────

        [Header("Result labels")]
        [Tooltip("Seconds the result readout stays visible before auto-clearing.")]
        [Min(0.1f)]
        [SerializeField] private float _readoutClearDelay = 4f;

        private static readonly string MissingPartsHint = "Need a head and a leg to assemble";

        // Cached to avoid a new allocation each time the readout is shown (§3.9).
        private WaitForSeconds _clearWait;

        // Tracks the active clear coroutine so it can be cancelled on rapid re-assembly.
        private Coroutine _clearCoroutine;

        private IPublisher<QuestObjectiveActionSignal> _questActionPublisher;

        // ── Slot state ─────────────────────────────────────────────────────────

        private HarvestedHead _depositedHead;
        private HarvestedLeg  _depositedLeg;

        /// <summary>True when a HarvestedHead has been deposited and is waiting in the slot.</summary>
        public bool HeadFilled => _depositedHead != null;

        /// <summary>True when a HarvestedLeg has been deposited and is waiting in the slot.</summary>
        public bool LegFilled  => _depositedLeg  != null;

        /// <summary>
        /// Sets the publisher used to dispatch quest objective actions.
        /// </summary>
        public void SetQuestActionPublisher(IPublisher<QuestObjectiveActionSignal> questActionPublisher)
        {
            if (questActionPublisher == null)
            {
                throw new System.ArgumentNullException(nameof(questActionPublisher));
            }

            _questActionPublisher = questActionPublisher;
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// False only when the player is carrying nothing and both slots are empty.
        /// All other states (depositing, taking back, assembling, partial hint) are true.
        /// </remarks>
        public bool CanInteract
        {
            get
            {
                if (!enabled || _carry == null)
                    return false;

                // No carry: prompt only if something can be taken back.
                if (!_carry.IsCarrying)
                    return _depositedHead != null || _depositedLeg != null;

                // Carrying something: always show a prompt.
                return true;
            }
        }

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (!CanInteract)
                return;

            bool headFilled = _depositedHead != null;
            bool legFilled  = _depositedLeg  != null;

            // Both slots filled: player must have empty hands to assemble.
            if (headFilled && legFilled)
            {
                if (_carry.IsCarrying)
                {
                    ShowHintText("Empty hands needed to assemble");
                    return;
                }
                Assemble();
                return;
            }

            // Carrying nothing → take back whichever slot is filled (req 6, 7).
            if (!_carry.IsCarrying)
            {
                if (headFilled)
                    TakeBackHead();
                else
                    TakeBackLeg();
                return;
            }

            bool carryingLeg = _carry.IsCarryingLeg;

            // Carrying a head (req 4: deposit; req 10: head slot already filled).
            if (!carryingLeg)
            {
                if (!headFilled)
                    DepositHead();
                else
                    ShowHintText(MissingPartsHint);
                return;
            }

            // Carrying a leg (req 5: deposit; req 10: leg slot already filled).
            if (!legFilled)
                DepositLeg();
            else
                ShowHintText(MissingPartsHint);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_carry == null)
                throw new System.InvalidOperationException(
                    $"[AssemblyStation '{gameObject.name}'] CarryController not assigned.");

            if (_headSlot == null)
                throw new System.InvalidOperationException(
                    $"[AssemblyStation '{gameObject.name}'] HeadSlot Transform not assigned.");

            if (_legSlot == null)
                throw new System.InvalidOperationException(
                    $"[AssemblyStation '{gameObject.name}'] LegSlot Transform not assigned.");

            _clearWait = new WaitForSeconds(_readoutClearDelay);

            if (_resultText != null)
                _resultText.text = string.Empty;
        }

        // ── Slot operations ────────────────────────────────────────────────────

        private void DepositHead()
        {
            if (!_carry.TryStore(_headSlot))
                return;

            _depositedHead = _headSlot.childCount > 0
                ? _headSlot.GetChild(0).GetComponent<HarvestedHead>()
                : null;

            LogSlotState("Deposited head");

            if (_depositedHead != null && _depositedLeg != null)
                Assemble();
        }

        private void DepositLeg()
        {
            if (!_carry.TryStoreLeg(_legSlot))
                return;

            _depositedLeg = _legSlot.childCount > 0
                ? _legSlot.GetChild(0).GetComponent<HarvestedLeg>()
                : null;

            LogSlotState("Deposited leg");

            if (_depositedHead != null && _depositedLeg != null)
                Assemble();
        }

        private void TakeBackHead()
        {
            _carry.PickUp(_depositedHead);
            _depositedHead = null;
            LogSlotState("Took back head");
        }

        private void TakeBackLeg()
        {
            _carry.PickUpLeg(_depositedLeg);
            _depositedLeg = null;
            LogSlotState("Took back leg");
        }

        private void Assemble()
        {
            CoreQuality headQuality = _depositedHead.Quality;
            CoreQuality legQuality  = _depositedLeg.Quality;
            CoreTrait   headTrait   = _depositedHead.TraitCandidate;
            CoreTrait   legTrait    = _depositedLeg.TraitCandidate;
            OrcQuality  quality     = (OrcQuality)(int)legQuality;
            CoreTrait   finalTrait  = SelectFinalTrait(headTrait, headQuality, legTrait, legQuality);

            LogTraitSelection(headTrait, headQuality, legTrait, legQuality, finalTrait);

            // Consume deposited head: deactivate in place.
            // Note: does not return to HarvestedHeadPool — acceptable for prototype demo length.
            _depositedHead.transform.SetParent(null);
            _depositedHead.gameObject.SetActive(false);
            _depositedHead = null;

            // Consume deposited leg: deactivate in place (no leg pool exists).
            _depositedLeg.transform.SetParent(null);
            _depositedLeg.gameObject.SetActive(false);
            _depositedLeg = null;

            ShowOrcResult(quality, finalTrait);
            ShowResultText(quality, finalTrait);
            PublishOrcCraftedAction();
            LogAssembly(quality, finalTrait);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void ShowOrcResult(OrcQuality quality, CoreTrait trait)
        {
            if (_orcPrefab == null)
                return;

            Vector3 spawnPos = _orcSpawnPoint != null
                ? _orcSpawnPoint.position
                : transform.position + transform.forward * 1.5f;

            AssembledOrc orc = Instantiate(_orcPrefab, spawnPos, _orcPrefab.transform.rotation);
            orc.SetQuality(quality);
            orc.SetTrait(trait);

            if (orc.TryGetComponent(out HaulerWorker hauler))
                hauler.Initialize(_orcWaitPoint, _orcStorageWalkTarget, _orcStorageDeliveryRoot);

            if (orc.TryGetComponent(out KeepInteractable keep))
                keep.Initialize(_orcPen);

            orc.gameObject.SetActive(true);
        }

        private void PublishOrcCraftedAction()
        {
            if (_questActionPublisher == null)
            {
                throw new System.InvalidOperationException(
                    $"[AssemblyStation '{gameObject.name}'] Quest action publisher was not injected.");
            }

            _questActionPublisher.Publish(
                new QuestObjectiveActionSignal(
                    QuestObjectiveActionKeys.OrcCraftedWithHeadAndLeg,
                    1));
        }

        private void ShowHintText(string hint)
        {
            if (_resultText == null)
                return;

            _resultText.text = hint;

            if (_clearCoroutine != null)
                StopCoroutine(_clearCoroutine);

            _clearCoroutine = StartCoroutine(ClearResultAfterDelay());
        }

        private void ShowResultText(OrcQuality quality, CoreTrait trait)
        {
            if (_resultText == null)
                return;

            // String concatenation is acceptable here — OnInteract is event-driven,
            // not a per-frame hot path (§3.3 applies to Update/FixedUpdate only).
            _resultText.text =
                "Orc assembled\n" +
                "Quality:  " + quality              + "\n" +
                "Trait:    " + GetDisplayName(trait);

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
        private void LogSlotState(string action)
        {
            Debug.Log(
                $"[AssemblyStation '{gameObject.name}'] {action}. " +
                $"Head slot: {(_depositedHead != null ? "filled" : "empty")}, " +
                $"Leg slot: {(_depositedLeg  != null ? "filled" : "empty")}.", this);
        }

        // ── Trait selection ────────────────────────────────────────────────────

        /// <summary>
        /// Selects one final trait from the two part candidates using quality weighting
        /// (Low = 1, Normal = 2, High = 3). Falls back when one or both candidates are None.
        /// </summary>
        private static CoreTrait SelectFinalTrait(
            CoreTrait   headCandidate, CoreQuality headQuality,
            CoreTrait   legCandidate,  CoreQuality legQuality)
        {
            bool headValid = headCandidate != CoreTrait.None;
            bool legValid  = legCandidate  != CoreTrait.None;

            if (!headValid && !legValid)
                return GetFallbackTrait();

            if (!headValid)
                return legCandidate;

            if (!legValid)
                return headCandidate;

            int headWeight = GetQualityWeight(headQuality);
            int legWeight  = GetQualityWeight(legQuality);
            int roll       = UnityEngine.Random.Range(0, headWeight + legWeight);

            return roll < headWeight ? headCandidate : legCandidate;
        }

        /// <summary>
        /// Returns a random trait from the Normal-quality fallback pool
        /// when both deposited parts have no valid candidate (None).
        /// Pool: Brutish, Diligent.
        /// </summary>
        private static CoreTrait GetFallbackTrait()
        {
            return UnityEngine.Random.Range(0, 2) == 0 ? CoreTrait.Brutish : CoreTrait.Diligent;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogTraitSelection(
            CoreTrait   headCandidate, CoreQuality headQuality,
            CoreTrait   legCandidate,  CoreQuality legQuality,
            CoreTrait   finalTrait)
        {
            Debug.Log(
                $"[AssemblyStation '{gameObject.name}'] Trait selection — " +
                $"Head: {GetDisplayName(headCandidate)} (w{GetQualityWeight(headQuality)}), " +
                $"Leg: {GetDisplayName(legCandidate)} (w{GetQualityWeight(legQuality)}). " +
                $"Final: {GetDisplayName(finalTrait)}.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogAssembly(OrcQuality quality, CoreTrait trait)
        {
            Debug.Log(
                $"[AssemblyStation '{gameObject.name}'] Assembly complete. " +
                $"Head + {_torsoLabel} + {_armsLabel} + Leg. " +
                $"Quality: {quality}, Trait: {GetDisplayName(trait)}.", this);
        }
    }
}
