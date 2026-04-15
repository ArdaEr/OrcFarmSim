using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Soil is tilled. A Plant interaction consumes one HeadSeed and advances to Seeded.
    /// The prompt changes based on whether the player currently holds a seed (§req 4).
    /// </summary>
    internal sealed class HeadTileTilledState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileTilledState(IHeadTileStateContext ctx) => _ctx = ctx;

        public bool   CanInteract => true;

        /// <summary>
        /// Returns "Plant seed" when a HeadSeed is available, otherwise a clarifying message.
        /// Evaluated fresh each time the HUD queries — no caching needed as inventory changes
        /// are infrequent and this property is not called in a hot inner loop.
        /// </summary>
        public string InteractPrompt =>
            _ctx.HasItem(ItemType.HeadSeed) ? "Plant seed" : "Need a HeadSeed to plant";

        public void OnEnter()    { }
        public void OnExit()     { }
        public void Update()     { }

        public void OnInteract()
        {
            if (_ctx.TryConsumeItem(ItemType.HeadSeed))
                _ctx.TransitionTo(HeadTileState.Seeded);
            // No transition if inventory check fails — prompt already warns the player.
        }
    }
}
