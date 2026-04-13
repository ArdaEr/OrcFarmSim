namespace OrcFarm.Farming
{
    /// <summary>
    /// Failure state — the legs were not fed in time and have eaten each other.
    /// Interacting clears the pond and returns it to Empty with no refund.
    /// </summary>
    internal sealed class LegPondStarvedState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondStarvedState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { }

        public void OnInteract() => _ctx.TransitionTo(LegPondState.Empty);
    }
}
