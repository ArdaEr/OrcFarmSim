namespace OrcFarm.Farming
{
    /// <summary>
    /// Manages the active <see cref="ILegPondState"/> for a <see cref="LegPond"/>.
    /// Mirrors the pattern of <see cref="FarmPlotStateMachine"/> (§7.5).
    /// </summary>
    internal sealed class LegPondStateMachine
    {
        private ILegPondState _current;

        /// <summary>True if the current state allows player interaction.</summary>
        internal bool CanInteract => _current?.CanInteract ?? false;

        /// <summary>
        /// Transitions to <paramref name="next"/>, calling OnExit on the outgoing state
        /// and OnEnter on the incoming one.
        /// </summary>
        internal void ChangeState(ILegPondState next)
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
