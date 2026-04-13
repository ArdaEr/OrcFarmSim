namespace OrcFarm.Farming
{
    /// <summary>
    /// Pond is stocked and settling. Waits for <see cref="LegPondConfig.StockedDelay"/>
    /// before growth begins. No player interaction is available.
    /// </summary>
    internal sealed class LegPondStockedState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondStockedState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => false;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void OnInteract(){ }

        public void Update()
        {
            _ctx.IncrementStockedTimer(UnityEngine.Time.deltaTime);

            if (_ctx.StockedTimer >= _ctx.Config.StockedDelay)
                _ctx.TransitionTo(LegPondState.Growing);
        }
    }
}
