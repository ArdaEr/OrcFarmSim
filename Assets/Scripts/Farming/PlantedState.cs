namespace OrcFarm.Farming
{
    /// <summary>
    /// Seed just placed. Resets growth tracking on entry and displays a brief
    /// confirmation ("Plot: Starting...") for <see cref="HeadSeedConfig.PlantedConfirmationDuration"/>
    /// seconds before automatically transitioning to <see cref="PlotState.Growing"/>.
    /// No player interaction required.
    /// </summary>
    internal sealed class PlantedState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;
        private float _elapsed;

        internal PlantedState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => false;
        public void OnExit()    { }
        public void OnInteract(){ }

        public void OnEnter()
        {
            _elapsed = 0f;
            _ctx.ResetGrowthTracking();
        }

        public void Update()
        {
            _elapsed += UnityEngine.Time.deltaTime;
            if (_elapsed >= _ctx.Config.PlantedConfirmationDuration)
                _ctx.TransitionTo(PlotState.Growing);
        }
    }
}
