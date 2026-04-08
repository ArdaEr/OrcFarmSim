namespace OrcFarm.Interaction
{
    /// <summary>
    /// Contract for any world object the player can interact with.
    /// Implemented by Farming plots, storage containers, carry pickups, etc.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Whether this object is currently available for interaction.</summary>
        bool CanInteract { get; }

        /// <summary>
        /// Called by the interaction system when the player confirms an interaction.
        /// Implementations must not call this themselves.
        /// </summary>
        void OnInteract();
    }
}
