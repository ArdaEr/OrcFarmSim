namespace OrcFarm.Farming
{
    /// <summary>
    /// Seed just placed. Immediately resets growth tracking and transitions to
    /// <see cref="PlotState.Growing"/> on the first Update tick — no player interaction needed.
    /// </summary>
    internal sealed class PlantedState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal PlantedState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => false;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void OnInteract(){ }

        public void Update()
        {
            _ctx.ResetGrowthTracking();
            _ctx.TransitionTo(PlotState.Growing);
        }
    }
}
