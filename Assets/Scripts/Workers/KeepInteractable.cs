using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Workers
{
    /// <summary>
    /// Placed on the assembled orc world object alongside its visual representation.
    /// Implements <see cref="IInteractable"/> so the player sees a Keep prompt after
    /// assembly completes and the orc's GameObject is activated.
    ///
    /// On interaction: activates the assigned <see cref="HaulerWorker"/> and disables
    /// itself so the prompt never appears again for this orc.
    ///
    /// Scene setup: add this component to the same GameObject as the AssembledOrc
    /// placeholder. Assign the pre-placed (inactive) HaulerWorker in the Inspector.
    /// The orc's GameObject must have a Collider so InteractionDetector can detect it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KeepInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("The HaulerWorker to activate when the player chooses Keep. " +
                 "The worker GameObject should start inactive in the scene.")]
        [SerializeField] private HaulerWorker _hauler;

        // ── IInteractable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// False after Keep has been chosen (<see cref="HaulerWorker.IsKept"/> is true)
        /// or if the component is disabled. Prevents the prompt from re-appearing if
        /// AssemblyStation re-activates this GameObject for a second assembly.
        /// </remarks>
        public bool CanInteract => enabled && _hauler != null && !_hauler.IsKept;

        /// <inheritdoc/>
        public void OnInteract()
        {
            if (!CanInteract)
                return;

            _hauler.Keep();
            enabled = false;  // suppress prompt permanently for this orc instance
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_hauler == null)
                throw new System.InvalidOperationException(
                    $"[KeepInteractable '{gameObject.name}'] HaulerWorker not assigned.");
        }
    }
}
