using UnityEngine;

namespace OrcFarm.Player
{
    /// <summary>
    /// Translates <see cref="IPlayerInputProvider.MoveInput"/> into
    /// <see cref="CharacterController"/> displacement each frame.
    ///
    /// Movement is flattened to the XZ plane: the forward vector has its Y
    /// component zeroed and re-normalized before use, so the player does not
    /// gain vertical momentum when looking up or down.
    ///
    /// Gravity is applied; no jumping or ability logic is included here.
    /// </summary>
    [RequireComponent(typeof(PlayerInputSource))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        [SerializeField] private float _speed   = 4f;
        [SerializeField] private float _gravity = -9.81f;

        private IPlayerInputProvider _input;
        private CharacterController  _cc;
        private float                _verticalVelocity;

        private void Awake()
        {
            _input = GetComponent<PlayerInputSource>();
            _cc    = GetComponent<CharacterController>();
        }

        // Zero allocations: all Vector3 operations are value-type arithmetic (§3.1).
        // Uses sqrMagnitude implicitly avoided — forward.Normalize() handles the
        // near-zero case (sets to zero if magnitude < 1e-5) so no extra branch needed.
        private void Update()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f; // small constant keeps the controller grounded

            _verticalVelocity += _gravity * Time.deltaTime;

            // Flatten forward so the player doesn't fly when the camera tilts
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize(); // becomes Vector3.zero when looking straight up/down

            Vector2 move   = _input.MoveInput;
            Vector3 motion = transform.right * move.x + forward * move.y;
            motion        *= _speed;
            motion.y       = _verticalVelocity;

            _cc.Move(motion * Time.deltaTime);
        }
    }
}
