using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.Player
{
    /// <summary>
    /// Thin wrapper around <see cref="InputAction"/> instances for the three core
    /// player bindings: Move, Look, and Interact.
    ///
    /// Owns the action objects and their lifecycle (Enable / Disable / Dispose).
    /// Does not contain any movement or gameplay logic — that lives in the Player
    /// controller that consumes <see cref="IPlayerInputProvider"/>.
    ///
    /// Bindings are defined in code here so the wrapper compiles and runs without
    /// requiring a .inputactions asset reference. When the project adopts a shared
    /// InputActionAsset, this class should be replaced by a generated wrapper.
    /// </summary>
    public sealed class PlayerInputWrapper : IPlayerInputProvider, IDisposable
    {
        private readonly InputAction _moveAction;
        private readonly InputAction _lookAction;
        private readonly InputAction _interactAction;

        /// <inheritdoc/>
        public Vector2 MoveInput => _moveAction.ReadValue<Vector2>();

        /// <inheritdoc/>
        public Vector2 LookInput => _lookAction.ReadValue<Vector2>();

        /// <inheritdoc/>
        public bool InteractPressed => _interactAction.WasPressedThisFrame();

        public PlayerInputWrapper()
        {
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _lookAction = new InputAction("Look", InputActionType.Value);
            _lookAction.AddBinding("<Mouse>/delta");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            _interactAction = new InputAction("Interact", InputActionType.Button);
            _interactAction.AddBinding("<Keyboard>/e");
            _interactAction.AddBinding("<Gamepad>/buttonSouth");

            _moveAction.Enable();
            _lookAction.Enable();
            _interactAction.Enable();
        }

        /// <summary>Disables and disposes all managed <see cref="InputAction"/> instances.</summary>
        public void Dispose()
        {
            _moveAction.Disable();
            _lookAction.Disable();
            _interactAction.Disable();

            _moveAction.Dispose();
            _lookAction.Dispose();
            _interactAction.Dispose();
        }
    }
}
