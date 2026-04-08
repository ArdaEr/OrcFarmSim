namespace OrcFarm.Farming
{
    /// <summary>
    /// Care checkpoint reached. The player must interact before
    /// <see cref="HeadSeedConfig.CareWindowDuration"/> expires or the crop fails.
    /// </summary>
    internal sealed class NeedsCareState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal NeedsCareState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }

        public void Update()
        {
            _ctx.IncrementCareWindowTimer(UnityEngine.Time.deltaTime);

            if (_ctx.CareWindowTimer >= _ctx.Config.CareWindowDuration)
                _ctx.TransitionTo(PlotState.FailedCrop);
        }

        public void OnInteract()
        {
            _ctx.SetCareGiven();
            _ctx.TransitionTo(PlotState.Growing);
        }
    }
}
