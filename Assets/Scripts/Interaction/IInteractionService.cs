namespace OrcFarm.Interaction
{
    /// <summary>
    /// Detects the nearest valid <see cref="IInteractable"/> and executes interactions
    /// on behalf of the player. Owned by the Interaction assembly; consumed by Player.
    /// </summary>
    public interface IInteractionService
    {
        /// <summary>The <see cref="IInteractable"/> currently in range, or null.</summary>
        IInteractable CurrentTarget { get; }

        /// <summary>
        /// Attempts to call <see cref="IInteractable.OnInteract"/> on the current target.
        /// No-op if <see cref="CurrentTarget"/> is null or <c>CanInteract</c> is false.
        /// </summary>
        void TryInteract();
    }
}
