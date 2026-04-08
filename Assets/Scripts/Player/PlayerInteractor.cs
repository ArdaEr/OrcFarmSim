using OrcFarm.Interaction;
using UnityEngine;
using VContainer;

namespace OrcFarm.Player
{
    /// <summary>
    /// Bridges the interact input binding to <see cref="IInteractionService"/>.
    ///
    /// Both <see cref="IPlayerInputProvider"/> and <see cref="IInteractionService"/> are
    /// received via VContainer injection (§1.3) — no SerializeField concrete references needed.
    /// </summary>
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private IPlayerInputProvider _input;
        private IInteractionService  _service;

        // ── VContainer injection ───────────────────────────────────────────────

        /// <summary>Receives dependencies from VContainer (§1.3).</summary>
        [Inject]
        private void Construct(IPlayerInputProvider input, IInteractionService service)
        {
            _input   = input;
            _service = service;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Update()
        {
            if (_input.InteractPressed)
                _service.TryInteract();
        }
    }
}
