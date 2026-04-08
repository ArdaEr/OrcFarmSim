using OrcFarm.Interaction;
using UnityEngine;

namespace OrcFarm.Player
{
    /// <summary>
    /// Bridges the interact input binding to <see cref="IInteractionService"/>.
    ///
    /// The detector is serialized as the concrete <see cref="InteractionDetector"/> type
    /// because Unity cannot serialize interfaces. It is immediately stored as
    /// <see cref="IInteractionService"/> so all runtime usage goes through the interface.
    ///
    /// When VContainer is introduced, replace the [SerializeField] field with
    /// [Inject] IInteractionService and remove the concrete reference.
    /// </summary>
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InteractionDetector _detector;

        private IPlayerInputProvider _input;
        private IInteractionService  _service;

        private void Awake()
        {
            _input   = GetComponent<PlayerInputSource>();
            _service = _detector;

            if (_detector == null)
            {
                Debug.LogError($"[PlayerInteractor] _detector is not assigned on '{gameObject.name}'. Interaction disabled.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_input.InteractPressed)
                _service.TryInteract();
        }
    }
}
