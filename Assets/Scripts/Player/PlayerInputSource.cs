using UnityEngine;

namespace OrcFarm.Player
{
    /// <summary>
    /// MonoBehaviour adapter that owns the <see cref="PlayerInputWrapper"/> lifecycle and
    /// exposes <see cref="IPlayerInputProvider"/> for sibling components.
    ///
    /// MonoBehaviour justification: InputActions must be enabled and disposed alongside
    /// the GameObject. Without VContainer in place, a MonoBehaviour is the correct
    /// lifetime anchor. Replace with a VContainer binding once DI is introduced.
    /// </summary>
    public sealed class PlayerInputSource : MonoBehaviour, IPlayerInputProvider
    {
        private PlayerInputWrapper _wrapper;

        /// <inheritdoc/>
        public Vector2 MoveInput    => _wrapper.MoveInput;

        /// <inheritdoc/>
        public Vector2 LookInput    => _wrapper.LookInput;

        /// <inheritdoc/>
        public bool InteractPressed => _wrapper.InteractPressed;

        /// <inheritdoc/>
        public bool JumpPressed => _wrapper.JumpPressed;

        /// <inheritdoc/>
        public bool RunHeld => _wrapper.RunHeld;

        private void Awake()     => _wrapper = new PlayerInputWrapper();
        private void OnDestroy() => _wrapper.Dispose();
    }
}
