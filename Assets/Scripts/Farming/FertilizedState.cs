using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>Fertilizer applied. A Plant interaction places a seed (consumes one from inventory).</summary>
    internal sealed class FertilizedState : IFarmPlotState
    {
        private readonly IFarmPlotStateContext _ctx;

        internal FertilizedState(IFarmPlotStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { }

        public void OnInteract()
        {
            if (_ctx.TryConsumeItem(ItemType.HeadSeed))
                _ctx.TransitionTo(PlotState.Planted);
        }
    }
}
