namespace OrcFarm.Farming
{
    /// <summary>
    /// Pond is empty. The player must be carrying a <see cref="LegFryItem"/> to stock it.
    /// On interaction: reads the item's tier, populates fish data, consumes the item,
    /// and transitions to <see cref="LegPondState.Stocked"/>.
    /// </summary>
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
            LegFryItem carried = _ctx.CarriedLegFry;
            if (carried != null)
            {
                LegFryTier tier  = carried.Tier;
                int        count = _ctx.GetCappedFishCount(tier);
                _ctx.ConsumeCarriedLegFry();
                _ctx.InitializeFish(tier, count);
                _ctx.TransitionTo(LegPondState.Stocked);
                return;
            }

            if (_ctx.TryStockFromHotbar())
            {
                _ctx.TransitionTo(LegPondState.Stocked);
                return;
            }

            LogNotCarrying();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogNotCarrying()
        {
            UnityEngine.Debug.Log(
                "[LegPondEmptyState] Cannot stock — no LegFry carried or in selected hotbar slot.");
        }
    }
}
