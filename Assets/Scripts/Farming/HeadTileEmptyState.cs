namespace OrcFarm.Farming
{
    /// <summary>Soil is bare. A Till interaction advances to Tilled.</summary>
    internal sealed class HeadTileEmptyState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileEmptyState(IHeadTileStateContext ctx) => _ctx = ctx;

        public bool   CanInteract    => true;
        public string InteractPrompt => "Till soil";
        public void   OnEnter()      { }
        public void   OnExit()       { }
        public void   Update()       { }
        public void   OnInteract()   => _ctx.TransitionTo(HeadTileState.Tilled);
    }
}
