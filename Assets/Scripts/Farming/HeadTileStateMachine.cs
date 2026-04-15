namespace OrcFarm.Farming
{
    /// <summary>
    /// Manages the active <see cref="IHeadTileState"/> for a <see cref="HeadFarmTile"/>.
    /// Mirrors the pattern of <see cref="FarmPlotStateMachine"/> (§7.5).
    /// </summary>
    internal sealed class HeadTileStateMachine
    {
        private IHeadTileState _current;

        /// <summary>True if the current state allows player interaction.</summary>
        internal bool CanInteract => _current != null && _current.CanInteract;

        /// <summary>Prompt string from the current state, or empty if no state is set.</summary>
        internal string InteractPrompt => _current != null ? _current.InteractPrompt : string.Empty;

        /// <summary>
        /// Transitions to <paramref name="next"/>, calling <see cref="IHeadTileState.OnExit"/>
        /// on the outgoing state and <see cref="IHeadTileState.OnEnter"/> on the incoming one.
        /// </summary>
        internal void ChangeState(IHeadTileState next)
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
