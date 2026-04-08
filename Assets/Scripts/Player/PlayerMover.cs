using UnityEngine;
using VContainer;

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

        private const float GroundedSnapVelocity = -2f;

        private IPlayerInputProvider _input;
        private CharacterController  _cc;
        private float                _verticalVelocity;

        // ── VContainer injection ───────────────────────────────────────────────

        /// <summary>Receives <see cref="IPlayerInputProvider"/> from VContainer (§1.3).</summary>
        [Inject]
        private void Construct(IPlayerInputProvider input) => _input = input;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        // Zero allocations: all Vector3 operations are value-type arithmetic (§3.1).
        private void Update()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = GroundedSnapVelocity; // small constant keeps the controller grounded

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
