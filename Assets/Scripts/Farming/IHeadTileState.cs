namespace OrcFarm.Farming
{
    /// <summary>
    /// Contract for a single state in the <see cref="HeadTileStateMachine"/>.
    /// All implementations must be <c>internal sealed</c> (§7.4).
    /// </summary>
    internal interface IHeadTileState
    {
        /// <summary>True if the player can currently interact with the tile in this state.</summary>
        bool CanInteract { get; }

        /// <summary>Prompt string shown by the HUD while this state is active and interactable.</summary>
        string InteractPrompt { get; }

        /// <summary>Called by the machine immediately after this state becomes active.</summary>
        void OnEnter();

        /// <summary>Called by the machine immediately before this state is replaced.</summary>
        void OnExit();

        /// <summary>Called every frame while this state is active.</summary>
        void Update();

        /// <summary>Called when the player triggers an interaction while this state is active.</summary>
        void OnInteract();
    }
}
