namespace OrcFarm.Farming
{
    /// <summary>
    /// Crop died. A Clear interaction resets the tile to Empty with no refund.
    /// Currently reachable only via the <c>_debugForceDead</c> inspector hook on
    /// <see cref="HeadFarmTile"/>. Condition-tracking starvation will reach this
    /// state automatically in a future task.
    /// </summary>
    internal sealed class HeadTileDeadState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileDeadState(IHeadTileStateContext ctx) => _ctx = ctx;

        public bool   CanInteract    => true;
        public string InteractPrompt => "Clear dead crop";
        public void   OnEnter()      { }
        public void   OnExit()       { }
        public void   Update()       { }

        public void OnInteract()
        {
            _ctx.ResetTimer();
            _ctx.TransitionTo(HeadTileState.Empty);
        }
    }
}
