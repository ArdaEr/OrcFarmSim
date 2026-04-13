using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>Pond is empty. Stocking consumes one LegFry and moves to Stocked.</summary>
    internal sealed class LegPondEmptyState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondEmptyState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { }

        public void OnInteract()
        {
            if (!_ctx.TryConsumeItem(ItemType.LegFry))
                return;

            _ctx.TransitionTo(LegPondState.Stocked);
        }
    }
}
