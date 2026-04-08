namespace OrcFarm.Farming
{
    /// <summary>
    /// Care window was missed. The player must clear the failed crop before replanting.
    /// Terminal: does not autonomously transition to any other state (§7.2).
    /// Only exits when the player explicitly clears via <see cref="OnInteract"/>.
    /// </summary>
    internal sealed class FailedCropState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal FailedCropState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { } // terminal: no autonomous transitions
        public void OnInteract() => _ctx.TransitionTo(PlotState.Empty);
    }
}
