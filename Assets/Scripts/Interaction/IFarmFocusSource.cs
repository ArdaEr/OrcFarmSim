using OrcFarm.Core;

namespace OrcFarm.Interaction
{
    /// <summary>
    /// Exposes which <see cref="IFarmActionTarget"/> is currently under the player's
    /// crosshair. Implemented by <c>FarmFocusDetector</c> in OrcFarm.Player; consumed
    /// by <c>LegPond</c> and <c>HeadFarmTile</c> in OrcFarm.Farming to gate
    /// <see cref="IInteractable.CanInteract"/> on line-of-sight.
    ///
    /// Serialized as <c>MonoBehaviour</c> in the Inspector; cast to this interface in
    /// Awake. This avoids a direct Farming → Player assembly dependency (§1.7).
    /// </summary>
    public interface IFarmFocusSource
    {
        /// <summary>
        /// The farm action target currently in the player's crosshair, or null when
        /// no farm tile is focused.
        /// </summary>
        IFarmActionTarget CurrentTarget { get; }
    }
}
