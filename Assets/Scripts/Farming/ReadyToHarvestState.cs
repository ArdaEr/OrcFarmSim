namespace OrcFarm.Farming
{
    /// <summary>Growth completed successfully. A Harvest interaction spawns the head.</summary>
    internal sealed class ReadyToHarvestState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal ReadyToHarvestState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { }

        public void OnInteract()
        {
            _ctx.SpawnHarvestedHead();
            _ctx.TransitionTo(PlotState.Empty);
        }
    }
}
