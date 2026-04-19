using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrcFarm.Player
{
    /// <summary>
    /// Thin wrapper around <see cref="InputAction"/> instances for the three core
    /// player bindings: Move, Look, Interact, Jump, and Run.
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
        private readonly InputAction _jumpAction;
        private readonly InputAction _runAction;

        /// <inheritdoc/>
        public Vector2 MoveInput => _moveAction.ReadValue<Vector2>();

        /// <inheritdoc/>
        public Vector2 LookInput => _lookAction.ReadValue<Vector2>();

        /// <inheritdoc/>
        public bool InteractPressed => _interactAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public bool JumpPressed => _jumpAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public bool RunHeld => _runAction.IsPressed();

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
            _interactAction.AddBinding("<Gamepad>/buttonNorth");

            _jumpAction = new InputAction("Jump", InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");

            _runAction = new InputAction("Run", InputActionType.Button);
            _runAction.AddBinding("<Keyboard>/leftShift");
            _runAction.AddBinding("<Keyboard>/rightShift");
            _runAction.AddBinding("<Gamepad>/leftStickPress");

            _moveAction.Enable();
            _lookAction.Enable();
            _interactAction.Enable();
            _jumpAction.Enable();
            _runAction.Enable();
        }

        /// <summary>Disables and disposes all managed <see cref="InputAction"/> instances.</summary>
        public void Dispose()
        {
            _moveAction.Disable();
            _lookAction.Disable();
            _interactAction.Disable();
            _jumpAction.Disable();
            _runAction.Disable();

            _moveAction.Dispose();
            _lookAction.Dispose();
            _interactAction.Dispose();
            _jumpAction.Dispose();
            _runAction.Dispose();
        }
    }
}
