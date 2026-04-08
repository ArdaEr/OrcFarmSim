namespace OrcFarm.Farming
{
    /// <summary>
    /// Manages the active <see cref="IFarmPlotState"/> for a <see cref="FarmPlot"/>.
    /// The controller calls <see cref="Update"/> and <see cref="OnInteract"/> each frame /
    /// interaction event; all branching lives in the state classes (§7.5).
    /// </summary>
    internal sealed class FarmPlotStateMachine
    {
        private IFarmPlotState _current;

        /// <summary>True if the current state allows player interaction.</summary>
        internal bool CanInteract => _current?.CanInteract ?? false;

        /// <summary>
        /// Transitions to <paramref name="next"/>, calling <see cref="IFarmPlotState.OnExit"/>
        /// on the outgoing state and <see cref="IFarmPlotState.OnEnter"/> on the incoming one.
        /// </summary>
        internal void ChangeState(IFarmPlotState next)
        {
            _current?.OnExit();
            _current = next;
            _current.OnEnter();
        }

        /// <summary>Forwards the frame tick to the active state.</summary>
        internal void Update() => _current?.Update();

        /// <summary>Forwards a player interaction event to the active state.</summary>
        internal void OnInteract() => _current?.OnInteract();
    }
}
