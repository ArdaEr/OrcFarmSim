namespace OrcFarm.Farming
{
    /// <summary>
    /// Growth timer is running. Opens the care window at the configured checkpoint;
    /// completes growth when the full duration is reached.
    /// The timer is paused while in <see cref="PlotState.NeedsCare"/>.
    /// </summary>
    internal sealed class GrowingState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal GrowingState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => false;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void OnInteract(){ }

        public void Update()
        {
            _ctx.IncrementGrowthTimer(UnityEngine.Time.deltaTime);

            if (!_ctx.CareGiven && _ctx.GrowthTimer >= _ctx.Config.CareCheckpointTime)
            {
                _ctx.TransitionTo(PlotState.NeedsCare);
                return; // don't check ReadyToHarvest in the same tick
            }

            if (_ctx.GrowthTimer >= _ctx.Config.GrowthDuration)
                _ctx.TransitionTo(PlotState.ReadyToHarvest);
        }
    }
}
