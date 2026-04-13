namespace OrcFarm.Farming
{
    /// <summary>
    /// Contract for a single <see cref="LegPond"/> lifecycle state.
    /// Mirrors the pattern used by <see cref="IFarmPlotState"/> (§7.1–§7.4).
    /// </summary>
    internal interface ILegPondState
    {
        /// <summary>True when this state allows a player interaction.</summary>
        bool CanInteract { get; }

        /// <summary>Called by the state machine when this state becomes active.</summary>
        void OnEnter();

        /// <summary>Called by the state machine when this state is leaving.</summary>
        void OnExit();

        /// <summary>Called each frame while this state is active.</summary>
        void Update();

        /// <summary>Called when the player confirms an interaction.</summary>
        void OnInteract();
    }
}
