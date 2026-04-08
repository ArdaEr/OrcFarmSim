using UnityEngine;

namespace OrcFarm.Player
{
    /// <summary>
    /// First-person look controller.
    ///
    /// Yaw  — rotates the player's root Transform around the Y axis.
    /// Pitch — rotates a child camera-pivot Transform around its local X axis,
    ///         clamped to prevent over-rotation.
    ///
    /// Reads from <see cref="IPlayerInputProvider"/>; contains no input bindings.
    ///
    /// Note: <c>LookInput</c> returns mouse pixel-delta or gamepad stick values
    /// from the same channel. Mouse feels correct at default sensitivity; gamepad
    /// may need a separate multiplier — a future improvement once device-split
    /// input is added.
    /// </summary>
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private float _sensitivityX = 0.15f;
        [SerializeField] private float _sensitivityY = 0.15f;
        [SerializeField] private float _pitchMin     = -80f;
        [SerializeField] private float _pitchMax     =  80f;

        private IPlayerInputProvider _input;
        private float _pitch;

        private void Awake()
        {
            _input = GetComponent<PlayerInputSource>();

            if (_cameraPivot == null)
            {
                Debug.LogError($"[PlayerLook] _cameraPivot is not assigned on '{gameObject.name}'. Look disabled.", this);
                enabled = false;
            }
        }

        // Zero allocations: Vector2/Vector3/Quaternion are value types;
        // Mathf.Clamp and Quaternion.Euler return value types (§3.1).
        private void Update()
        {
            Vector2 look = _input.LookInput;

            // Yaw: spin the player body left / right
            transform.Rotate(Vector3.up, look.x * _sensitivityX, Space.Self);

            // Pitch: tilt the camera pivot up / down
            _pitch = Mathf.Clamp(_pitch - look.y * _sensitivityY, _pitchMin, _pitchMax);
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
