using UnityEngine;

namespace OrcFarm.Player
{
    /// <summary>
    /// Read-only view of the player's current input frame.
    /// Consumed by controllers and systems that need input state without
    /// depending on the concrete MonoBehaviour or Input System types.
    /// </summary>
    public interface IPlayerInputProvider
    {
        /// <summary>Normalised XZ movement intent in local space. Range [-1, 1] per axis.</summary>
        Vector2 MoveInput { get; }

        /// <summary>Mouse/stick delta for camera look this frame.</summary>
        Vector2 LookInput { get; }

        /// <summary>True on the frame the interact binding was pressed.</summary>
        bool InteractPressed { get; }
    }
}
