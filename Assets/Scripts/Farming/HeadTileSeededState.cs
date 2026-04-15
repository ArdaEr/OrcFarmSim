namespace OrcFarm.Farming
{
    /// <summary>Seed is placed. A Cover interaction advances to Covered.</summary>
    internal sealed class HeadTileSeededState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileSeededState(IHeadTileStateContext ctx) => _ctx = ctx;

        public bool   CanInteract    => true;
        public string InteractPrompt => "Cover soil";
        public void   OnEnter()      { }
        public void   OnExit()       { }
        public void   Update()       { }
        public void   OnInteract()   => _ctx.TransitionTo(HeadTileState.Covered);
    }
}
