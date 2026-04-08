using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>Soil is tilled. A Fertilize interaction applies fertilizer (consumes one from inventory).</summary>
    internal sealed class PreparedState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal PreparedState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { }

        public void OnInteract()
        {
            if (_ctx.TryConsumeItem(ItemType.Fertilizer))
                _ctx.TransitionTo(PlotState.Fertilized);
        }
    }
}
