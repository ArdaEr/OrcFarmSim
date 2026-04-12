namespace OrcFarm.Workers
{
    /// <summary>
    /// Secondary (Q-key) interaction contract. Implemented alongside
    /// <see cref="OrcFarm.Interaction.IInteractable"/> on objects that offer a second
    /// action to the player — e.g. an assembled orc that can be Kept (E) or Stored (Q).
    ///
    /// <see cref="OrcFarm.UI.InteractHUD"/> reads this interface to show a second
    /// prompt line and to route Q-key input.
    /// </summary>
    public interface ISecondaryInteractable
    {
        /// <summary>True when the secondary action is available right now.</summary>
        bool CanSecondaryInteract { get; }

        /// <summary>Execute the secondary action.</summary>
        void OnSecondaryInteract();
    }
}
