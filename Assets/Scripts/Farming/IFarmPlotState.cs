namespace OrcFarm.Farming
{
    /// <summary>
    /// Contract for a single state in the <see cref="FarmPlotStateMachine"/>.
    /// All implementations must be <c>internal sealed</c> (§7.4).
    /// </summary>
    internal interface IFarmPlotState
    {
        /// <summary>True if the player can currently interact with the plot in this state.</summary>
        bool CanInteract { get; }

        /// <summary>Called by the state machine immediately after this state becomes active.</summary>
        void OnEnter();

        /// <summary>Called by the state machine immediately before this state is replaced.</summary>
        void OnExit();

        /// <summary>Called every frame while this state is active.</summary>
        void Update();

        /// <summary>Called when the player triggers an interaction while this state is active.</summary>
        void OnInteract();
    }
}
