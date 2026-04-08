namespace OrcFarm.Farming
{
    /// <summary>Soil is bare. A Prepare interaction tills the soil.</summary>
    internal sealed class EmptyState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal EmptyState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()    { }
        public void OnExit()     { }
        public void Update()     { }
        public void OnInteract() => _ctx.TransitionTo(PlotState.Prepared);
    }
}
